namespace HelpDesk.Domain.Entities;

public class Attachment : BaseEntity
{
    public int TicketId { get; set; }
    public int? MessageId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Ticket Ticket { get; set; } = null!;
    public Message? Message { get; set; }
    public User Uploader { get; set; } = null!;
}