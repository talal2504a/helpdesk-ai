using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Application.DTOs.Admin;
using HelpDesk.Application.DTOs.Attachments;
using HelpDesk.Application.DTOs.Messages;

namespace HelpDesk.Application.DTOs.Tickets;

public record TicketHistoryDto(int Id, int UserId, string UserName, string Action,
    string? OldValue, string? NewValue, DateTime CreatedAt);

public record TicketDto(
    int Id, string TicketNumber, string Title, string Description,
    int UserId, string CustomerName,
    int? AssignedAgentId, string? AssignedAgentName,
    int? DepartmentId, string? DepartmentName,
    CategoryLookupDto Category, PriorityLookupDto Priority, StatusLookupDto Status,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ResolvedAt, DateTime? ClosedAt,
    int MessageCount, int AttachmentCount,
    bool AiModeEnabled, bool WaitingForAgent, string? EscalationReason);

public record TicketDetailDto(TicketDto Ticket, IReadOnlyList<MessageDto> Messages,
    IReadOnlyList<AttachmentDto> Attachments, IReadOnlyList<TicketHistoryDto> History, AiAnalysisDto? LatestAi);

public record CreateTicketRequest(string Title, string Description, int? CategoryId, int? PriorityId, int? DepartmentId);
public record UpdateTicketStatusRequest(int StatusId);
public record AssignTicketRequest(int AgentId);
public record ToggleAiModeRequest(bool Enable);
public record TakeOverRequest;

public record TicketQueryParameters
{
    private const int MaxPageSize = 100;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public int? StatusId { get; set; }
    public int? PriorityId { get; set; }
    public int? CategoryId { get; set; }
    public int? DepartmentId { get; set; }
    public int? AssignedAgentId { get; set; }
    public bool UnassignedOnly { get; set; }
    public string SortBy { get; set; } = "createdAt";
    public string SortDir { get; set; } = "desc";

    public int EffectivePage => Math.Max(1, Page);
    public int EffectivePageSize => Math.Clamp(PageSize, 1, MaxPageSize);
}

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
