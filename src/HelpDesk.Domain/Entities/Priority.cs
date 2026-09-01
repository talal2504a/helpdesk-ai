namespace HelpDesk.Domain.Entities;

public class Priority : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}