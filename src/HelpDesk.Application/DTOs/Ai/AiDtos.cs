namespace HelpDesk.Application.DTOs.Ai;

public record AiAnalysisDto(int Id, int TicketId, string? Category, string? Priority, string? Summary,
    string? Sentiment, string? SuggestedReply, double ConfidenceScore, string Provider, DateTime CreatedAt);

public record AnalyzeTicketRequest(string Title, string Description);   