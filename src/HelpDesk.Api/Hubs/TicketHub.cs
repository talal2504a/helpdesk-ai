using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HelpDesk.Api.Hubs;

[Authorize]
public class TicketHub : Hub, ITicketHub
{
    private readonly ITicketService _tickets;
    private readonly ILogger<TicketHub> _logger;

    public TicketHub(ITicketService tickets, ILogger<TicketHub> logger)
    {
        _tickets = tickets; _logger = logger;
    }

    public async Task JoinTicket(int ticketId)
    {
        await EnsureAccessAsync(ticketId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");
    }

    public async Task LeaveTicket(int ticketId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    public async Task ToggleAiMode(int ticketId, bool enable)
    {
        await EnsureAccessAsync(ticketId);
        var result = await _tickets.ToggleAiModeAsync(ticketId, enable, new HubCurrentUser(Context.User));
        await Clients.Group($"ticket:{ticketId}").SendAsync("AiStatusChanged", new
        {
            ticketId = result.Id,
            aiModeEnabled = result.AiModeEnabled,
            waitingForAgent = result.WaitingForAgent
        });
    }

    public async Task TakeOver(int ticketId)
    {
        await EnsureAccessAsync(ticketId);
        var result = await _tickets.TakeOverAsync(ticketId, new HubCurrentUser(Context.User));
        await Clients.Group($"ticket:{ticketId}").SendAsync("HumanAgentJoined", new
        {
            ticketId = result.Id,
            agentName = result.AssignedAgentName
        });
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (int.TryParse(userId, out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{id}");

        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (role is "Agent" or "Admin")
            await Groups.AddToGroupAsync(Context.ConnectionId, "staff");

        await base.OnConnectedAsync();
    }

    private async Task EnsureAccessAsync(int ticketId)
    {
        try
        {
            await _tickets.GetForViewerAsync(ticketId, new HubCurrentUser(Context.User));
        }
        catch (ForbiddenException)
        {
            throw new HubException("You do not have access to this ticket.");
        }
        catch (NotFoundException)
        {
            throw new HubException("Ticket not found.");
        }
    }

    private sealed class HubCurrentUser : ICurrentUser
    {
        public HubCurrentUser(ClaimsPrincipal? user) => User = user;
        public ClaimsPrincipal? User { get; }

        public int? UserId =>
            int.TryParse(User?.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

        public string? Role => User?.FindFirstValue(ClaimTypes.Role);
        public bool IsAdmin => Role == "Admin";
        public bool IsAgent => Role == "Agent";
    }
}
