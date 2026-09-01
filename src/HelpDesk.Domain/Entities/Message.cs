namespace HelpDesk.Domain.Entities;

public class Message : BaseEntity
{
    public int TicketId { get; set; }
    public int SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; }
    public bool IsAiGenerated { get; set; } = false;

    public Ticket Ticket { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
}
