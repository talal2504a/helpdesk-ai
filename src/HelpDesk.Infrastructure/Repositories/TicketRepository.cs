using System.Linq.Expressions;
using HelpDesk.Application.DTOs.Tickets;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using NotSupportedException = System.NotSupportedException;

namespace HelpDesk.Infrastructure.Repositories;

public class TicketRepository : GenericRepository<Ticket>, ITicketRepository
{
    public TicketRepository(HelpDeskDbContext db) : base(db) { }

    public IQueryable<Ticket> VisibleTo(bool isStaff, int userId, int? agentId) =>
        isStaff ? Db.Tickets.AsQueryable()
                : Db.Tickets.Where(t => t.UserId == userId);

    private static readonly Dictionary<string, Expression<Func<Ticket, object>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["createdAt"] = t => t.CreatedAt,
            ["updatedAt"] = t => t.UpdatedAt,
            ["priority"] = t => t.Priority.Level,
            ["status"] = t => t.Status.Name,
            ["ticketNumber"] = t => t.TicketNumber,
            ["title"] = t => t.Title
        };

    /// <summary>Builds the filtered/sorted/paged ticket query used by the search endpoint.</summary>
    public IQueryable<Ticket> BuildSearchQuery(TicketQueryParameters p, bool isStaff, int userId)
    {
        IQueryable<Domain.Entities.Ticket> q = VisibleTo(isStaff, userId, null)
            .Include(t => t.User).Include(t => t.AssignedAgent).Include(t => t.Department)
            .Include(t => t.Category).Include(t => t.Priority).Include(t => t.Status);

        if (!isStaff) q = q.Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(p.Search))
        {
            var term = p.Search.Trim().ToLower();
            q = q.Where(t => EF.Functions.Like(t.Title.ToLower(), $"%{term}%")
                          || EF.Functions.Like(t.Description.ToLower(), $"%{term}%")
                          || EF.Functions.Like(t.TicketNumber.ToLower(), $"%{term}%"));
        }
        if (p.StatusId.HasValue) q = q.Where(t => t.StatusId == p.StatusId);
        if (p.PriorityId.HasValue) q = q.Where(t => t.PriorityId == p.PriorityId);
        if (p.CategoryId.HasValue) q = q.Where(t => t.CategoryId == p.CategoryId);
        if (p.DepartmentId.HasValue) q = q.Where(t => t.DepartmentId == p.DepartmentId);
        if (p.UnassignedOnly) q = q.Where(t => t.AssignedAgentId == null);
        else if (p.AssignedAgentId.HasValue) q = q.Where(t => t.AssignedAgentId == p.AssignedAgentId);

        var sortKey = p.SortBy?.Trim() ?? "createdAt";
        if (!SortMap.TryGetValue(sortKey, out var sortExpr)) throw new NotSupportedException($"Cannot sort by '{sortKey}'.");
        return string.Equals(p.SortDir, "asc", StringComparison.OrdinalIgnoreCase)
            ? q.OrderBy(sortExpr).ThenBy(t => t.Id)
            : q.OrderByDescending(sortExpr).ThenByDescending(t => t.Id);
    }

    // paging projection lives in service; repo exposes raw query
    internal static TicketDto ToDto(Ticket t) => new(
        t.Id, t.TicketNumber, t.Title, t.Description, t.UserId, t.User.Name,
        t.AssignedAgentId, t.AssignedAgent?.Name, t.DepartmentId, t.Department?.Name,
        new CategoryLookupDto(t.CategoryId, t.Category.Name),
        new PriorityLookupDto(t.PriorityId, t.Priority.Name, t.Priority.Level),
        new StatusLookupDto(t.StatusId, t.Status.Name),
        t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.ClosedAt,
        t.Messages.Count, t.Attachments.Count,
        t.AiModeEnabled, t.WaitingForAgent, t.EscalationReason);
}