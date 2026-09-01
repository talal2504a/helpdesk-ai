using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using HelpDesk.Infrastructure.Ai;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly INotifier _notifier;
    private readonly IAiClient _ai;
    private readonly ILogger<MessageService> _logger;

    public MessageService(Data.HelpDeskDbContext db, INotifier notifier, IAiClient ai, ILogger<MessageService> logger)
    {
        _db = db; _notifier = notifier; _ai = ai; _logger = logger;
    }

    public async Task<MessageDto> SendAsync(int ticketId, SendMessageRequest request, ICurrentUser sender)
    {
        TicketValidators.Validate(request);

        var ticket = await _db.Tickets
            .Include(t => t.Status)
            .Include(t => t.AssignedAgent)
            .FirstOrDefaultAsync(t => t.Id == ticketId)
            ?? throw new NotFoundException("Ticket");

        if (!sender.IsStaffView() && ticket.UserId != sender.UserId)
            throw new ForbiddenException("You can only reply on your own tickets.");
        if (sender.IsStaffView() && !sender.IsAgentOrAdmin())
            throw new ForbiddenException();
        if (ticket.Status.Name == "Closed" && !sender.IsAdmin)
            throw new AppException("This ticket is closed. Reopen it before replying.", 409, "TICKET_CLOSED");

        if (request.IsInternal && !sender.IsAgentOrAdmin())
            throw new ForbiddenException("Only agents and admins can post internal notes.");

        var message = new Message
        {
            TicketId = ticketId,
            SenderId = sender.UserId!.Value,
            Body = request.Body.Trim(),
            IsInternal = request.IsInternal,
            CreatedAt = DateTime.UtcNow
        };
        _db.Messages.Add(message);

        ticket.UpdatedAt = DateTime.UtcNow;
        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticketId, UserId = sender.UserId.Value,
            Action = Domain.Entities.TicketActions.Commented,
            NewValue = request.IsInternal ? "(internal note)" : TruncatePreview(request.Body),
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var dto = await LoadDtoAsync(message.Id);
        await _notifier.MessageAddedAsync(ticketId, dto);

        var notifyUserId = sender.UserId == ticket.UserId ? ticket.AssignedAgentId : ticket.UserId;
        if (notifyUserId is int uid && !(request.IsInternal && uid == ticket.UserId))
            await _notifier.NotificationAsync(uid, $"New message on {ticket.TicketNumber}",
                $"{(sender.Role ?? "User")}: {TruncatePreview(request.Body)}", new { ticketId });

        if (sender.IsCustomer && ticket.AssignedAgentId is null && !request.IsInternal)
            await _notifier.NotifyStaffAsync($"New message on {ticket.TicketNumber}",
                $"{(sender.Role ?? "User")}: {TruncatePreview(request.Body)}", new { ticketId });

        if (!request.IsInternal && sender.IsCustomer)
        {
            try { await TryAiResponseAsync(ticket, message.SenderId); }
            catch (Exception ex) { _logger.LogWarning(ex, "AI response failed for ticket {TicketId}", ticketId); }
        }

        return dto;
    }

    private async Task TryAiResponseAsync(Ticket ticket, int customerId)
    {
        var conversation = await _db.Messages
            .Where(m => m.TicketId == ticket.Id && !m.IsInternal)
            .Include(m => m.Sender)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync();

        var messageDtos = conversation.Select(m => new MessageDto(
            m.Id, m.TicketId, m.SenderId, m.Sender.Name, m.Sender.Role,
            m.Body, m.CreatedAt, m.IsInternal, m.IsAiGenerated, new List<AttachmentDto>())).ToList();

        var customerMessages = messageDtos.Where(m => m.SenderRole == "Customer").ToList();
        if (!customerMessages.Any()) return;

        var lastCustomerMessage = customerMessages.Last().Body;

        // AI answers every customer message while AI mode is ON — even after escalation —
        // so the customer is never left without a reply. Only an agent turning AI mode off
        // (or a Closed ticket) silences it.
        if (!ticket.AiModeEnabled || ticket.Status.Name == "Closed")
            return;

        if (!ticket.WaitingForAgent)
        {
            var escalation = await _ai.ShouldEscalateAsync(lastCustomerMessage, messageDtos, ticket);
            if (escalation.ShouldEscalate)
            {
                ticket.WaitingForAgent = true;
                ticket.EscalationReason = escalation.Reason;
                ticket.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                if (!string.IsNullOrEmpty(escalation.SuggestedResponse))
                {
                    await SaveAiMessageAsync(ticket, await ResolveAiSenderIdAsync(ticket), escalation.SuggestedResponse, "AI escalation");
                }

                await _notifier.AiStatusChangedAsync(ticket.Id, false, true);

                if (ticket.AssignedAgentId is int aid)
                {
                    await _notifier.NotificationAsync(aid, $"Customer waiting for agent on {ticket.TicketNumber}",
                        $"Customer requested human assistance. Reason: {escalation.Reason}", new { ticketId = ticket.Id });
                }
                else
                {
                    await _notifier.NotifyStaffAsync($"Customer waiting for agent on {ticket.TicketNumber}",
                        $"Ticket {ticket.TicketNumber}: Customer requested human assistance.", new { ticketId = ticket.Id });
                }
                return;
            }
        }

        var aiReply = await _ai.GenerateReplyAsync(ticket, customerId, lastCustomerMessage, messageDtos);
        if (!string.IsNullOrWhiteSpace(aiReply))
        {
            await SaveAiMessageAsync(ticket, await ResolveAiSenderIdAsync(ticket), aiReply, "AI reply");
        }
    }

    /// <summary>AI messages are attributed to the dedicated AI Support user (ai@helpdesk.local).</summary>
    private async Task<int> ResolveAiSenderIdAsync(Ticket ticket)
    {
        var aiId = await _db.Users.Where(u => u.Email == "ai@helpdesk.local")
            .Select(u => (int?)u.Id).FirstOrDefaultAsync();
        return aiId ?? ticket.AssignedAgentId ?? ticket.UserId;
    }

    private async Task SaveAiMessageAsync(Ticket ticket, int senderId, string body, string historyAction)
    {
        var aiMessage = new Message
        {
            TicketId = ticket.Id,
            SenderId = senderId,
            Body = body.Trim(),
            IsInternal = false,
            IsAiGenerated = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Messages.Add(aiMessage);
        _db.TicketHistory.Add(new TicketHistory
        {
            TicketId = ticket.Id, UserId = senderId,
            Action = historyAction,
            NewValue = "(AI generated)",
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var aiDto = await LoadDtoAsync(aiMessage.Id);
        await _notifier.MessageAddedAsync(ticket.Id, aiDto);
        await _notifier.NotificationAsync(ticket.UserId, $"AI replied on {ticket.TicketNumber}",
            TruncatePreview(body), new { ticketId = ticket.Id });
    }

    private async Task<MessageDto> LoadDtoAsync(int messageId) =>
        await _db.Messages.AsNoTracking()
            .Where(m => m.Id == messageId)
            .Include(m => m.Sender)
            .Include(m => m.Attachments)
            .Select(m => new MessageDto(m.Id, m.TicketId, m.SenderId, m.Sender.Name, m.Sender.Role,
                m.Body, m.CreatedAt, m.IsInternal, m.IsAiGenerated,
                m.Attachments.Select(a => new AttachmentDto(a.Id, a.TicketId, a.MessageId,
                    a.FileName, a.FileUrl, a.FileType, a.FileSize, a.UploadedBy, a.Uploader.Name, a.UploadedAt)).ToList()))
            .FirstAsync();

    private static string TruncatePreview(string body) => body.Length <= 80 ? body.Trim() : body.Trim()[..77] + "...";
}
