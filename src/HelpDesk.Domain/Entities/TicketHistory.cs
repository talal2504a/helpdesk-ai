namespace HelpDesk.Domain.Entities;

public class TicketHistory : BaseEntity
{
    public int TicketId { get; set; }
    public int UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public User User { get; set; } = null!;
}

public static class TicketActions
{
    public const string Created = "Created";
    public const string Assigned = "Assigned";
    public const string Unassigned = "Unassigned";
    public const string StatusChanged = "StatusChanged";
    public const string PriorityChanged = "PriorityChanged";
    public const string DepartmentChanged = "DepartmentChanged";
    public const string CategoryChanged = "CategoryChanged";
    public const string Commented = "Commented";
    public const string AttachmentAdded = "AttachmentAdded";
    public const string AiAnalyzed = "AiAnalyzed";
}