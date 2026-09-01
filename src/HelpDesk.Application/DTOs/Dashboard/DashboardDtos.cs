namespace HelpDesk.Application.DTOs.Dashboard;

public record DashboardStatsDto(
    int TotalTickets, int OpenTickets, int InProgressTickets, int PendingTickets,
    int ResolvedTickets, int ClosedTickets, int UnassignedTickets,
    IReadOnlyList<GroupedStatDto> TicketsByPriority,
    IReadOnlyList<GroupedStatDto> TicketsByDepartment,
    IReadOnlyList<TrendPointDto> Last7DaysTrend);

public record GroupedStatDto(string Key, int Count);
public record TrendPointDto(DateTime Date, int Created, int Resolved);
public record MyQueueStatsDto(int AssignedTotal, int OpenOnMe, int ResolvedByMe, int NewMessagesForMe);