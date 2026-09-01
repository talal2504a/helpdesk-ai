using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Domain.Entities;

namespace HelpDesk.Application.Interfaces;

public interface IAiClient
{
    bool IsConfigured { get; }

    Task<AIAnalysis> AnalyzeAsync(string title, string description, CancellationToken ct = default);

    Task<string> GenerateReplyAsync(Ticket ticket, int customerId, string customerMessage, IReadOnlyList<MessageDto> conversationHistory, CancellationToken ct = default);
    Task<string> GenerateAutoReplyAsync(string ticketTitle, string customerMessage, CancellationToken ct = default);
    Task<AiEscalationResult> ShouldEscalateAsync(string customerMessage, IReadOnlyList<MessageDto> conversationHistory, Ticket ticket, CancellationToken ct = default);
}
