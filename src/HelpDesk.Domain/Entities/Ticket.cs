namespace HelpDesk.Domain.Entities;

public class Ticket : BaseEntity
{
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? AssignedAgentId { get; set; }
    public int? DepartmentId { get; set; }
    public int CategoryId { get; set; }
    public int PriorityId { get; set; }
    public int StatusId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool AiModeEnabled { get; set; } = true;
    public bool WaitingForAgent { get; set; } = false;
    public bool IsAiReply { get; set; } = false;
    public string? EscalationReason { get; set; }

    public User User { get; set; } = null!;
    public User? AssignedAgent { get; set; }
    public Department? Department { get; set; }
    public Category Category { get; set; } = null!;
    public Priority Priority { get; set; } = null!;
    public Status Status { get; set; } = null!;
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<TicketHistory> History { get; set; } = new List<TicketHistory>();
    public ICollection<AIAnalysis> AiAnalyses { get; set; } = new List<AIAnalysis>();
}
