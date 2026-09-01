using HelpDesk.Application.DTOs.Attachments;
using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.DTOs.Tickets;
using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;
    private readonly IMessageService _messages;
    private readonly IAttachmentService _attachments;
    private readonly ICurrentUser _me;

    public TicketsController(ITicketService tickets, IMessageService messages,
        IAttachmentService attachments, ICurrentUser me)
    {
        _tickets = tickets; _messages = messages; _attachments = attachments; _me = me;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketDto>>> Search([FromQuery] TicketQueryParameters query) =>
        Ok(await _tickets.SearchAsync(query, _me));

    [HttpGet("my-queue")]
    [Authorize(Roles = "Agent, Admin")]
    public Task<PagedResult<TicketDto>> MyQueue([FromQuery] TicketQueryParameters query)
    {
        query.AssignedAgentId = _me.UserId;
        return _tickets.SearchAsync(query, _me);
    }

    [HttpGet("unassigned")]
    [Authorize(Roles = "Agent, Admin")]
    public Task<PagedResult<TicketDto>> Unassigned([FromQuery] TicketQueryParameters query)
    {
        query.UnassignedOnly = true;
        return _tickets.SearchAsync(query, _me);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TicketDetailDto>> Get(int id) => Ok(await _tickets.GetForViewerAsync(id, _me));

    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create([FromBody] CreateTicketRequest request)
    {
        var created = await _tickets.CreateAsync(request, _me.UserId!.Value);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:int}/assign")]
    [Authorize(Roles = "Agent, Admin")]
    public async Task<ActionResult<TicketDto>> Assign(int id, [FromBody] AssignTicketRequest request) =>
        Ok(await _tickets.AssignAsync(id, request, _me));

    [HttpPost("{id:int}/status")]
    [Authorize(Roles = "Agent, Admin")]
    public async Task<ActionResult<TicketDto>> ChangeStatus(int id, [FromBody] UpdateTicketStatusRequest request) =>
        Ok(await _tickets.ChangeStatusAsync(id, request, _me));

    [HttpPost("{id:int}/ai-mode")]
    [Authorize(Roles = "Agent, Admin")]
    public async Task<ActionResult<TicketDto>> ToggleAiMode(int id, [FromBody] ToggleAiModeRequest request) =>
        Ok(await _tickets.ToggleAiModeAsync(id, request.Enable, _me));

    [HttpPost("{id:int}/take-over")]
    [Authorize(Roles = "Agent, Admin")]
    public async Task<ActionResult<TicketDto>> TakeOver(int id) =>
        Ok(await _tickets.TakeOverAsync(id, _me));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _tickets.DeleteAsync(id, _me);
        return NoContent();
    }

    [HttpPost("{id:int}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(int id, [FromBody] SendMessageRequest request) =>
        Ok(await _messages.SendAsync(id, request, _me));

    [HttpPost("{id:int}/attachments")]
    [RequestSizeLimit(55_000_000)]
    public async Task<ActionResult<UploadAttachmentsResponse>> Upload(int id, [FromForm] IFormFileCollection files)
    {
        var uploads = files.Select(f => new FileUploadDto(f.FileName, f.ContentType, f.OpenReadStream())).ToList();
        var result = await _attachments.UploadForTicketAsync(id, uploads, _me);
        return Ok(result);
    }
}
