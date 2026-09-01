using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Data;

public class HelpDeskDbContext : DbContext
{
    public HelpDeskDbContext(DbContextOptions<HelpDeskDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Priority> Priorities => Set<Priority>();
    public DbSet<Status> Statuses => Set<Status>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();
    public DbSet<AIAnalysis> AiAnalyses => Set<AIAnalysis>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Department)
             .WithMany(d => d.Users)
             .HasForeignKey(x => x.DepartmentId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Department>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Message>(e =>
        {
            e.ToTable("Messages");
            e.Property(x => x.Body).HasMaxLength(5000).IsRequired();
            e.Property(x => x.IsAiGenerated).HasDefaultValue(false);
            e.HasIndex(x => x.TicketId);
            e.HasOne(x => x.Ticket).WithMany(t => t.Messages)
             .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany(u => u.Messages)
             .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Attachment>(e =>
        {
            e.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            e.Property(x => x.FileUrl).HasMaxLength(1024).IsRequired();
            e.Property(x => x.FileType).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.TicketId);
            e.HasOne(x => x.Ticket).WithMany(t => t.Attachments)
             .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Message).WithMany(m => m.Attachments)
             .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.Uploader).WithMany(u => u.Attachments)
             .HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Ticket>(e =>
        {
            e.Property(x => x.TicketNumber).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.TicketNumber).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(8000).IsRequired();
            e.Property(x => x.AiModeEnabled).HasDefaultValue(true);
            e.Property(x => x.WaitingForAgent).HasDefaultValue(false);
            e.Property(x => x.IsAiReply).HasDefaultValue(false);
            e.Property(x => x.EscalationReason).HasMaxLength(500);
            e.HasIndex(x => x.StatusId);
            e.HasIndex(x => x.AssignedAgentId);
            e.HasIndex(x => x.PriorityId);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => new { x.CreatedAt, x.Id });

            e.HasOne(x => x.User).WithMany(u => u.TicketsCreated)
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedAgent).WithMany(u => u.TicketsAssigned)
             .HasForeignKey(x => x.AssignedAgentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Department).WithMany(d => d.Tickets)
             .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Category).WithMany(c => c.Tickets)
             .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Priority).WithMany(p => p.Tickets)
             .HasForeignKey(x => x.PriorityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Status).WithMany(s => s.Tickets)
             .HasForeignKey(x => x.StatusId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Priority>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<Status>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<TicketHistory>(e =>
        {
            e.ToTable("TicketHistory");
            e.Property(x => x.Action).HasMaxLength(60).IsRequired();
            e.Property(x => x.OldValue).HasMaxLength(8000);
            e.Property(x => x.NewValue).HasMaxLength(8000);
            e.HasIndex(x => x.TicketId);
            e.HasOne(x => x.Ticket).WithMany(t => t.History)
             .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.HistoryEntries)
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AIAnalysis>(e =>
        {
            e.ToTable("AiAnalyses");
            e.Property(x => x.Category).HasMaxLength(80);
            e.Property(x => x.Priority).HasMaxLength(40);
            e.Property(x => x.Summary).HasMaxLength(4000);
            e.Property(x => x.Sentiment).HasMaxLength(20);
            e.Property(x => x.SuggestedReply).HasMaxLength(4000);
            e.Property(x => x.Provider).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.TicketId);
            e.HasOne(x => x.Ticket).WithMany(t => t.AiAnalyses)
             .HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
