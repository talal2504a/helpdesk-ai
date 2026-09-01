using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Controllers;

/// <summary>
/// Lightweight HTTP polling endpoints — reliable realtime alternative to SignalR
/// for hosts where WebSockets are unavailable (e.g. shared hosting).
/// Frontend polls /api/poll/messages every few seconds with sinceId cursor.
/// </summary>
[ApiController]
[Route("api/poll")]
[Authorize]
public class PollingController : ControllerBase
{
    private readonly HelpDeskDbContext _db;
    private readonly ICurrentUser _me;

    public PollingController(HelpDeskDbContext db, ICurrentUser me)
    {
        _db = db;
        _me = me;
    }

    /// <summary>Returns messages with Id &gt; sinceId that the current user may see. Ordered by Id ascending.</summary>
    [HttpGet("messages")]
    public async Task<IActionResult> Messages([FromQuery] int sinceId = 0, [FromQuery] int? ticketId = null)
    {
        var query = _db.Messages.AsNoTracking().AsQueryable();

        if (ticketId.HasValue)
        {
            var ticket = await _db.Tickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == ticketId.Value);
            if (ticket == null) return NotFound();

            // Customers can only poll their own tickets
            if (_me.IsCustomer && ticket.UserId != _me.UserId) return Forbid();

            query = query.Where(m => m.TicketId == ticketId.Value);
        }
        else if (_me.IsCustomer)
        {
            query = query.Where(m => m.Ticket.UserId == _me.UserId);
        }

        // Internal notes are never visible to customers
        if (_me.IsCustomer) query = query.Where(m => !m.IsInternal);

        var messages = await query
            .Where(m => m.Id > sinceId)
            .OrderBy(m => m.Id)
            .Take(100)
            .Select(m => new
            {
                m.Id,
                m.TicketId,
                m.Body,
                m.CreatedAt,
                m.IsInternal,
                m.IsAiGenerated,
                m.SenderId,
                SenderName = m.Sender.Name,
                SenderRole = m.Sender.Role
            })
            .ToListAsync();

        return Ok(messages);
    }

    /// <summary>Current max message Id visible to the user — used as polling baseline on login.</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> Latest()
    {
        var query = _db.Messages.AsNoTracking().AsQueryable();
        if (_me.IsCustomer)
        {
            query = query.Where(m => m.Ticket.UserId == _me.UserId && !m.IsInternal);
        }

        var latestId = await query.MaxAsync(m => (int?)m.Id) ?? 0;
        return Ok(new { latestId });
    }
}
