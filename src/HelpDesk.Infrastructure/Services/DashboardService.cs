using HelpDesk.Application.DTOs.Dashboard;
using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly Data.HelpDeskDbContext _db;
    public DashboardService(Data.HelpDeskDbContext db) => _db = db;

    public async Task<DashboardStatsDto> GetStatsAsync(ICurrentUser viewer)
    {
        var tickets = _db.Tickets
            .Include(t => t.Priority).Include(t => t.Status).Include(t => t.Department)
            .AsNoTracking();

        if (!viewer.IsStaffView()) tickets = tickets.Where(t => t.UserId == viewer.UserId);

        var list = await tickets.ToListAsync();
        var weekAgo = DateTime.UtcNow.Date.AddDays(-6);

        return new DashboardStatsDto(
            TotalTickets: list.Count,
            OpenTickets: list.Count(t => t.Status.Name == "Open"),
            InProgressTickets: list.Count(t => t.Status.Name == "InProgress"),
            PendingTickets: list.Count(t => t.Status.Name == "Pending"),
            ResolvedTickets: list.Count(t => t.Status.Name == "Resolved"),
            ClosedTickets: list.Count(t => t.Status.Name == "Closed"),
            UnassignedTickets: list.Count(t => t.AssignedAgentId == null),
            TicketsByPriority: list.GroupBy(t => t.Priority.Name)
                .Select(g => new GroupedStatDto(g.Key, g.Count())).OrderByDescending(g => g.Count).ToList(),
            TicketsByDepartment: list.GroupBy(t => t.Department?.Name ?? "(Unassigned)")
                .Select(g => new GroupedStatDto(g.Key, g.Count())).OrderByDescending(g => g.Count).ToList(),
            Last7DaysTrend: Enumerable.Range(0, 7).Select(i =>
            {
                var day = weekAgo.AddDays(i);
                return new TrendPointDto(day,
                    list.Count(t => t.CreatedAt.Date == day),
                    list.Count(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value.Date == day));
            }).ToList());
    }
}

/// <summary>Runs AI analysis on an existing ticket and stores the result + history entry.</summary>
public class AiAnalysisService : IAiAnalysisService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IAiClient _ai;
    private readonly INotifier _notifier;
    public AiAnalysisService(Data.HelpDeskDbContext db, IAiClient ai, INotifier notifier)
    {
        _db = db; _ai = ai; _notifier = notifier;
    }

    public async Task<AiAnalysisDto> AnalyzeExistingTicketAsync(int ticketId, ICurrentUser actor, bool persist = true)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new Application.Exceptions.NotFoundException("Ticket");

        var result = await _ai.AnalyzeAsync(ticket.Title, ticket.Description);
        var analysis = new Domain.Entities.AIAnalysis
        {
            TicketId = ticket.Id,
            Category = result.Category,
            Priority = result.Priority,
            Summary = result.Summary,
            Sentiment = result.Sentiment,
            SuggestedReply = result.SuggestedReply,
            ConfidenceScore = result.ConfidenceScore,
            Provider = result.Provider,
            CreatedAt = DateTime.UtcNow
        };

        if (persist)
        {
            _db.AiAnalyses.Add(analysis);
            _db.TicketHistory.Add(new Domain.Entities.TicketHistory
            {
                TicketId = ticket.Id, UserId = actor.UserId!.Value, Action = Domain.Entities.TicketActions.AiAnalyzed,
                NewValue = $"{result.Category}/{result.Priority}/{result.Sentiment} ({result.Provider})",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            await _notifier.TicketUpdatedAsync(ticket.Id, "aiAnalyzed", new { provider = result.Provider });
        }

        return new AiAnalysisDto(analysis.Id, analysis.TicketId, analysis.Category, analysis.Priority,
            analysis.Summary, analysis.Sentiment, analysis.SuggestedReply, analysis.ConfidenceScore, analysis.Provider, analysis.CreatedAt);
    }

    public async Task<string> GenerateReplyAsync(int ticketId, ICurrentUser actor)
    {
        var ticket = await _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.AssignedAgent)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new Application.Exceptions.NotFoundException("Ticket");

        var messages = await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.TicketId == ticketId && !m.IsInternal)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync();

        var customerMessage = messages.LastOrDefault()?.Body ?? ticket.Description;

        var history = messages.Select(m => new MessageDto(
            m.Id, m.TicketId, m.SenderId, m.Sender.Name, m.Sender.Role,
            m.Body, m.CreatedAt, m.IsInternal, m.IsAiGenerated, Array.Empty<AttachmentDto>())).ToList();

        return await _ai.GenerateReplyAsync(ticket, ticket.UserId, customerMessage, history);
    }

    public async Task<string> GenerateAutoReplyAsync(int ticketId, int customerId)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId)
                     ?? throw new Application.Exceptions.NotFoundException("Ticket");

        var customerMessage = ticket.Description;
        var lastCustomerMessage = await _db.Messages
            .Where(m => m.TicketId == ticketId && m.SenderId == customerId && !m.IsInternal)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        if (lastCustomerMessage is not null)
            customerMessage = lastCustomerMessage.Body;

        return await _ai.GenerateAutoReplyAsync(ticket.Title, customerMessage);
    }
}