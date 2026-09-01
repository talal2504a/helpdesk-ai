using HelpDesk.Application.DTOs.Messages;
using HelpDesk.Application.DTOs.Tickets;

namespace HelpDesk.Application.Interfaces;

public interface ITicketHub;

public interface ICurrentUser
{
    int? UserId { get; }
    string? Role { get; }
    bool IsAdmin { get; }
    bool IsAgent { get; }
    bool IsCustomer => !IsAdmin && !IsAgent;
}

public interface ITicketService
{
    Task<TicketDetailDto> GetForViewerAsync(int ticketId, ICurrentUser viewer);
    Task<PagedResult<TicketDto>> SearchAsync(TicketQueryParameters query, ICurrentUser viewer);
    Task<TicketDto> CreateAsync(CreateTicketRequest request, int customerId);
    Task<TicketDto> AssignAsync(int ticketId, AssignTicketRequest request, ICurrentUser actor);
    Task<TicketDto> ChangeStatusAsync(int ticketId, UpdateTicketStatusRequest request, ICurrentUser actor);
    Task<TicketDto> ToggleAiModeAsync(int ticketId, bool enable, ICurrentUser actor);
    Task<TicketDto> TakeOverAsync(int ticketId, ICurrentUser agent);
    Task DeleteAsync(int ticketId, ICurrentUser actor);
}

public interface IMessageService
{
    Task<MessageDto> SendAsync(int ticketId, SendMessageRequest request, ICurrentUser sender);
}

public interface INotifier
{
    Task TicketUpdatedAsync(int ticketId, string action, object payload);
    Task MessageAddedAsync(int ticketId, object messagePayload);
    Task NotificationAsync(int userId, string title, string body, object? data = null);
    Task NotifyStaffAsync(string title, string body, object? data = null);
    Task AiStatusChangedAsync(int ticketId, bool aiModeEnabled, bool waitingForAgent);
    Task HumanAgentJoinedAsync(int ticketId, string agentName);
}
