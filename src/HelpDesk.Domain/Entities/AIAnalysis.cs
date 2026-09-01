namespace HelpDesk.Domain.Entities;

public class AIAnalysis : BaseEntity
{
    public int TicketId { get; set; }
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public string? Summary { get; set; }
    public string? Sentiment { get; set; }
    public string? SuggestedReply { get; set; }
    public double ConfidenceScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Provider { get; set; } = "RuleBased";

    public Ticket Ticket { get; set; } = null!;
}