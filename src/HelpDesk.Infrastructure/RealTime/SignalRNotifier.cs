using HelpDesk.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace HelpDesk.Infrastructure.RealTime;

public class SignalRNotifier : INotifier
{
    private readonly IHubContext<Hub> _hub;
    private readonly ILogger<SignalRNotifier> _logger;

    public SignalRNotifier(IHubContext<Hub> hub, ILogger<SignalRNotifier> logger)
    {
        _hub = hub; _logger = logger;
    }

    public Task TicketUpdatedAsync(int ticketId, string action, object payload) =>
        SafeSendAsync($"ticket:{ticketId}", "TicketUpdated", new { action, payload });

    public Task MessageAddedAsync(int ticketId, object messagePayload) =>
        SafeSendAsync($"ticket:{ticketId}", "ReceiveMessage", messagePayload);

    public async Task NotificationAsync(int userId, string title, string body, object? data = null)
    {
        try
        {
            await _hub.Clients.Group($"user:{userId}").SendAsync("Notification", new
            {
                Title = title,
                Body = body,
                Data = data,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR notification to user {UserId} failed.", userId); }
    }

    public async Task NotifyStaffAsync(string title, string body, object? data = null)
    {
        try
        {
            await _hub.Clients.Group("staff").SendAsync("Notification", new
            {
                Title = title,
                Body = body,
                Data = data,
                CreatedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR staff notification failed."); }
    }

    public async Task AiStatusChangedAsync(int ticketId, bool aiModeEnabled, bool waitingForAgent)
    {
        try
        {
            await _hub.Clients.Group($"ticket:{ticketId}").SendAsync("AiStatusChanged", new
            {
                ticketId,
                aiModeEnabled,
                waitingForAgent
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR AiStatusChanged broadcast failed."); }
    }

    public async Task HumanAgentJoinedAsync(int ticketId, string agentName)
    {
        try
        {
            await _hub.Clients.Group($"ticket:{ticketId}").SendAsync("HumanAgentJoined", new
            {
                ticketId,
                agentName
            });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR HumanAgentJoined broadcast failed."); }
    }

    private async Task SafeSendAsync(string group, string method, object payload)
    {
        try { await _hub.Clients.Group(group).SendAsync(method, JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(payload))); }
        catch (Exception ex) { _logger.LogWarning(ex, "SignalR broadcast to {Group} failed.", group); }
    }
}
