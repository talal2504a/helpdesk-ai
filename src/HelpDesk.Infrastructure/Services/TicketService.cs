using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Application.DTOs.Tickets;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class TicketService : ITicketService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IAiClient _ai;
    private readonly INotifier _notifier;
    private readonly ILogger<TicketService> _logger;

    public static readonly Dictionary<string, string[]> AllowedTransitions = new()
    {
        ["Open"] = new[] { "InProgress", "Pending", "Closed" },
        ["InProgress"] = new[] { "Pending", "Resolved", "Closed" },
        ["Pending"] = new[] { "InProgress", "Resolved", "Closed" },
        ["Resolved"] = new[] { "Closed", "InProgress" },
        ["Closed"] = new[] { "Open" }
    };

    public TicketService(Data.HelpDeskDbContext db, IAiClient ai, INotifier notifier, ILogger<TicketService> logger)
    {
        _db = db; _ai = ai; _notifier = notifier; _logger = logger;
    }

    public async Task<TicketDetailDto> GetForViewerAsync(int ticketId, ICurrentUser viewer)
    {
        var t = await LoadFullTicket(ticketId);
        EnsureCanView(t, viewer);

        var messages = await _db.Messages.AsNoTracking()
            .Where(m => m.TicketId == ticketId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync();

        if (!viewer.IsStaffView()) messages = messages.Where(m => !m.IsInternal).ToList();

        return new TicketDetailDto(ToDto(t),
            messages.Select(ToMessageDto).ToList(),
            t.Attachments.Select(a => ToAttachmentDto(a)).ToList(),
            viewer.IsStaffView()
                ? t.History.OrderByDescending(h => h.CreatedAt).Select(h =>
                    new TicketHistoryDto(h.Id, h.UserId, h.User.Name, h.Action, h.OldValue, h.NewValue, h.CreatedAt)).ToList()
                : Array.Empty<TicketHistoryDto>(),
            t.AiAnalyses.OrderByDescending(a => a.CreatedAt).Select(a =>
                new AiAnalysisDto(a.Id, a.TicketId, a.Category, a.Priority, a.Summary, a.Sentiment,
                    a.SuggestedReply, a.ConfidenceScore, a.Provider, a.CreatedAt)).FirstOrDefault());
    }

    public async Task<PagedResult<TicketDto>> SearchAsync(TicketQueryParameters p, ICurrentUser viewer)
    {
        var repo = new Repositories.TicketRepository(_db);
        var q = repo.BuildSearchQuery(p, viewer.IsStaffView(), viewer.UserId!.Value);

        var total = await q.CountAsync();
        var items = await q
            .Skip((p.EffectivePage - 1) * p.EffectivePageSize)
            .Take(p.EffectivePageSize)
            .ToListAsync();

        return new PagedResult<TicketDto>(items.Select(ToDto).ToList(), p.EffectivePage, p.EffectivePageSize, total);
    }

    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, int customerId)
    {
        TicketValidators.Validate(request);

        var customer = await _db.Users.FirstOrDefaultAsync(u => u.Id == customerId && u.IsActive)
                       ?? throw new NotFoundException("Customer account");

        var statusOpen = await _db.Statuses.FirstAsync(s => s.Name == "Open");
        var analysis = await _ai.AnalyzeAsync(request.Title, request.Description);

        var category = request.CategoryId is int cid
            ? await _db.Categories.FirstOrDefaultAsync(c => c.Id == cid) ?? throw new ValidationException("CategoryId", "Category does not exist.")
            : await _db.Categories.FirstOrDefaultAsync(c => c.Name == analysis.Category)
              ?? await _db.Categories.FirstAsync(c => c.Name == "Other");

        var priority = request.PriorityId is int pid
            ? await _db.Priorities.FirstOrDefaultAsync(p => p.Id == pid) ?? throw new ValidationException("PriorityId", "Priority does not exist.")
            : await _db.Priorities.FirstOrDefaultAsync(p => p.Name == analysis.Priority)
              ?? await _db.Priorities.OrderBy(p => p.Level).FirstAsync();

        Department? department = null;
        if (request.DepartmentId is int did)
            department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == did && d.IsActive)
                         ?? throw new ValidationException("DepartmentId", "Department does not exist or is inactive.");

        var ticket = new Ticket
        {
            TicketNumber = await NextTicketNumberAsync(),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            UserId = customerId,
            DepartmentId = department?.Id,
            CategoryId = category.Id,
            PriorityId = priority.Id,
            StatusId = statusOpen.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AiModeEnabled = true,
            WaitingForAgent = false
        };
        _db.Tickets.Add(ticket);
        await _db.SaveChangesAsync();

        ticket.AiAnalyses.Add(new AIAnalysis
        {
            TicketId = ticket.Id, Category = analysis.Category, Priority = analysis.Priority,
            Summary = analysis.Summary, Sentiment = analysis.Sentiment,
            SuggestedReply = analysis.SuggestedReply, ConfidenceScore = analysis.ConfidenceScore,
            Provider = analysis.Provider, CreatedAt = DateTime.UtcNow
        });
        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticket.Id, UserId = customerId,
            Action = TicketActions.Created,
            NewValue = $"AI: {analysis.Category} / {analysis.Priority} / {analysis.Sentiment}",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogInformation("Ticket {Number} created by user {User}; AI provider {Provider}.",
            ticket.TicketNumber, customerId, analysis.Provider);

        var dto = ToDto(await LoadFullTicket(ticket.Id));
        await _notifier.NotificationAsync(customerId, $"Ticket {ticket.TicketNumber} created",
            $"Your ticket \"{ticket.Title}\" has been received.", new { ticketId = ticket.Id });

        await _notifier.NotifyStaffAsync($"New ticket {ticket.TicketNumber}",
            $"{ticket.Title} (priority: {analysis.Priority})", new { ticketId = ticket.Id });

        try
        {
            var autoReply = await _ai.GenerateAutoReplyAsync(ticket.Title, ticket.Description);
            if (!string.IsNullOrWhiteSpace(autoReply))
            {
                // Attribute the AI auto-reply to the dedicated AI Support user so it renders as an AI bubble.
                var aiUserId = await _db.Users.Where(u => u.Email == "ai@helpdesk.local")
                    .Select(u => (int?)u.Id).FirstOrDefaultAsync() ?? customerId;
                var aiMsg = new Message
                {
                    TicketId = ticket.Id,
                    SenderId = aiUserId,
                    Body = autoReply.Trim(),
                    IsInternal = false,
                    IsAiGenerated = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Messages.Add(aiMsg);
                _db.TicketHistory.Add(new TicketHistory
                {
                    TicketId = ticket.Id, UserId = aiUserId,
                    Action = "AI Auto-Reply",
                    NewValue = "(AI generated)",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                var aiDto = new MessageDto(aiMsg.Id, aiMsg.TicketId, aiMsg.SenderId,
                    "AI Support", "Agent", aiMsg.Body, aiMsg.CreatedAt, aiMsg.IsInternal,
                    aiMsg.IsAiGenerated, new List<AttachmentDto>());
                await _notifier.MessageAddedAsync(ticket.Id, aiDto);
                await _notifier.NotificationAsync(customerId, $"AI replied on {ticket.TicketNumber}",
                    autoReply.Length <= 80 ? autoReply.Trim() : autoReply.Trim()[..77] + "...", new { ticketId = ticket.Id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI auto-reply failed for new ticket {TicketId}", ticket.Id);
        }

        return dto;
    }

    public async Task<TicketDto> AssignAsync(int ticketId, AssignTicketRequest request, ICurrentUser actor)
    {
        if (!actor.IsAgentOrAdmin()) throw new ForbiddenException("Only agents and admins can assign tickets.");

        var t = await LoadFullTicket(ticketId);
        var agent = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.AgentId && u.IsActive)
                    ?? throw new NotFoundException("Agent");

        if (agent.Role != "Agent" && agent.Role != "Admin")
            throw new ValidationException("AgentId", "Selected user is not an agent.");
        if (t.AssignedAgentId == agent.Id) return ToDto(t);

        var oldValue = t.AssignedAgent?.Name;
        t.AssignedAgentId = agent.Id;
        t.UpdatedAt = DateTime.UtcNow;

        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = t.Id, UserId = actor.UserId!.Value,
            Action = oldValue is null ? TicketActions.Assigned : "Reassigned",
            OldValue = oldValue, NewValue = agent.Name, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _notifier.NotificationAsync(agent.Id, "New ticket assigned",
            $"{t.TicketNumber}: {t.Title}", new { ticketId = t.Id });
        await _notifier.TicketUpdatedAsync(t.Id, "assigned", new { assignedTo = agent.Name });
        return ToDto(await LoadFullTicket(t.Id));
    }

    public async Task<TicketDto> ChangeStatusAsync(int ticketId, UpdateTicketStatusRequest request, ICurrentUser actor)
    {
        if (!actor.IsAgentOrAdmin()) throw new ForbiddenException("Only agents and admins can change ticket status.");

        var t = await LoadFullTicket(ticketId);
        var newStatus = await _db.Statuses.FirstOrDefaultAsync(s => s.Id == request.StatusId)
                        ?? throw new NotFoundException("Status");

        if (t.StatusId == newStatus.Id) return ToDto(t);

        var oldName = t.Status.Name;
        if (!AllowedTransitions.TryGetValue(oldName, out var allowed) || !allowed.Contains(newStatus.Name))
            throw new AppException(
                $"Cannot move ticket from '{oldName}' to '{newStatus.Name}'. Allowed: {string.Join(", ", allowed)}.",
                409, "INVALID_TRANSITION");

        t.StatusId = newStatus.Id;
        t.UpdatedAt = DateTime.UtcNow;
        if (newStatus.Name == "Resolved") t.ResolvedAt = DateTime.UtcNow;
        if (newStatus.Name == "Closed") { t.ClosedAt = DateTime.UtcNow; t.ResolvedAt ??= DateTime.UtcNow; }
        if (newStatus.Name == "Open" && oldName == "Closed") { t.ResolvedAt = null; t.ClosedAt = null; }

        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = t.Id, UserId = actor.UserId!.Value,
            Action = TicketActions.StatusChanged,
            OldValue = oldName, NewValue = newStatus.Name, CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _notifier.NotificationAsync(t.UserId, $"Ticket {t.TicketNumber} {newStatus.Name.ToLowerInvariant()}",
            $"Status changed from {oldName} to {newStatus.Name}.", new { ticketId = t.Id });
        await _notifier.TicketUpdatedAsync(t.Id, "statusChanged", new { from = oldName, to = newStatus.Name });
        return ToDto(await LoadFullTicket(t.Id));
    }

    public async Task<TicketDto> ToggleAiModeAsync(int ticketId, bool enable, ICurrentUser actor)
    {
        if (!actor.IsAgentOrAdmin()) throw new ForbiddenException("Only agents and admins can toggle AI mode.");

        var t = await LoadFullTicket(ticketId);
        var oldMode = t.AiModeEnabled;

        if (oldMode == enable) return ToDto(t);

        t.AiModeEnabled = enable;
        if (enable) t.WaitingForAgent = false;
        t.UpdatedAt = DateTime.UtcNow;

        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = t.Id, UserId = actor.UserId!.Value,
            Action = "AiModeChanged",
            OldValue = oldMode.ToString(),
            NewValue = enable.ToString(),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _notifier.AiStatusChangedAsync(t.Id, t.AiModeEnabled, t.WaitingForAgent);
        return ToDto(await LoadFullTicket(t.Id));
    }

    public async Task<TicketDto> TakeOverAsync(int ticketId, ICurrentUser agent)
    {
        if (!agent.IsAgentOrAdmin()) throw new ForbiddenException("Only agents and admins can take over tickets.");

        var t = await LoadFullTicket(ticketId);
        var agentUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == agent.UserId && u.IsActive)
                    ?? throw new NotFoundException("Agent");

        if (agentUser.Role != "Agent" && agentUser.Role != "Admin")
            throw new ValidationException("AgentId", "Selected user is not an agent.");

        var oldAgentId = t.AssignedAgentId;
        t.AssignedAgentId = agent.UserId;
        t.AiModeEnabled = false;
        t.WaitingForAgent = false;
        t.UpdatedAt = DateTime.UtcNow;

        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = t.Id, UserId = agent.UserId!.Value,
            Action = "AgentTookOver",
            OldValue = oldAgentId?.ToString(),
            NewValue = agentUser.Name,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        await _notifier.HumanAgentJoinedAsync(t.Id, agentUser.Name);
        await _notifier.AiStatusChangedAsync(t.Id, false, false);
        return ToDto(await LoadFullTicket(t.Id));
    }

    public async Task DeleteAsync(int ticketId, ICurrentUser actor)
    {
        if (!actor.IsAdmin) throw new ForbiddenException("Only admins can delete tickets.");

        var t = await LoadFullTicket(ticketId);
        
        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = t.Id, UserId = actor.UserId!.Value,
            Action = "Deleted",
            OldValue = t.TicketNumber,
            NewValue = $"Ticket {t.TicketNumber} deleted by {actor.UserId}",
            CreatedAt = DateTime.UtcNow
        });
        
        _db.Tickets.Remove(t);
        await _db.SaveChangesAsync();
        
        await _notifier.TicketUpdatedAsync(ticketId, "deleted", new { ticketId, ticketNumber = t.TicketNumber });
    }

    private async Task<Ticket> LoadFullTicket(int id) =>
        await _db.Tickets
            .Include(t => t.User).Include(t => t.AssignedAgent).Include(t => t.Department)
            .Include(t => t.Category).Include(t => t.Priority).Include(t => t.Status)
            .Include(t => t.Attachments).ThenInclude(a => a.Uploader)
            .Include(t => t.History).ThenInclude(h => h.User)
            .Include(t => t.AiAnalyses)
            .Include(t => t.Messages)
            .FirstOrDefaultAsync(t => t.Id == id)
        ?? throw new NotFoundException("Ticket");

    internal static void EnsureCanView(Ticket t, ICurrentUser viewer)
    {
        if (viewer.IsStaffView()) return;
        if (viewer.UserId != t.UserId) throw new ForbiddenException("You can only view your own tickets.");
    }

    private async Task<string> NextTicketNumberAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"TKT-{today}-";
        var lastToday = await _db.Tickets
            .Where(t => t.TicketNumber.StartsWith(prefix))
            .OrderByDescending(t => t.TicketNumber)
            .Select(t => t.TicketNumber)
            .FirstOrDefaultAsync();
        var seq = lastToday is null ? 0 : int.Parse(lastToday[^4..]);
        return $"{prefix}{(seq + 1):D4}";
    }

    public static TicketDto ToDto(Ticket t) => new(
        t.Id, t.TicketNumber, t.Title, t.Description, t.UserId, t.User.Name,
        t.AssignedAgentId, t.AssignedAgent?.Name, t.DepartmentId, t.Department?.Name,
        new CategoryLookupDto(t.CategoryId, t.Category.Name),
        new PriorityLookupDto(t.PriorityId, t.Priority.Name, t.Priority.Level),
        new StatusLookupDto(t.StatusId, t.Status.Name),
        t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt,
        t.Messages.Count, t.Attachments.Count,
        t.AiModeEnabled, t.WaitingForAgent, t.EscalationReason);

    public static MessageDto ToMessageDto(Message m) => new(
        m.Id, m.TicketId, m.SenderId, m.Sender.Name, m.Sender.Role, m.Body, m.CreatedAt, m.IsInternal,
        m.IsAiGenerated, m.Attachments.Select(ToAttachmentDto).ToList());

    public static AttachmentDto ToAttachmentDto(Attachment a) => new(
        a.Id, a.TicketId, a.MessageId, a.FileName, a.FileUrl, a.FileType, a.FileSize,
        a.UploadedBy, a.Uploader.Name, a.UploadedAt);
}

public static class CurrentUserExtensions
{
    public static bool IsStaffView(this ICurrentUser u) => u.IsAdmin || u.IsAgent;
    public static bool IsAgentOrAdmin(this ICurrentUser u) => u.IsAdmin || u.IsAgent;
}
