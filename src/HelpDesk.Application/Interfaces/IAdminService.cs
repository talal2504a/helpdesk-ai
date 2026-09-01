using HelpDesk.Application.DTOs.Admin;
using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Application.DTOs.Dashboard;
using HelpDesk.Application.DTOs.Messages;

namespace HelpDesk.Application.Interfaces;

public interface IAdminService
{
    // Departments
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync();
    Task<LookupItemDto> CreateDepartmentAsync(LookupUpsertRequest request);
    Task<LookupItemDto> UpdateDepartmentAsync(int id, LookupUpsertRequest request);
    Task DeleteDepartmentAsync(int id);

    // Categories
    Task<IReadOnlyList<CategoryLookupDto>> GetCategoriesAsync();
    Task<LookupItemDto> CreateCategoryAsync(LookupUpsertRequest request);
    Task<LookupItemDto> UpdateCategoryAsync(int id, LookupUpsertRequest request);
    Task DeleteCategoryAsync(int id);

    // Priorities
    Task<IReadOnlyList<PriorityLookupDto>> GetPrioritiesAsync();
        Task<LookupItemDto> CreatePriorityAsync(PriorityLookupUpsertRequest request);
        Task<LookupItemDto> UpdatePriorityAsync(int id, PriorityLookupUpsertRequest request);
    Task DeletePriorityAsync(int id);

    // Statuses
    Task<IReadOnlyList<StatusLookupDto>> GetStatusesAsync();
    Task<LookupItemDto> CreateStatusAsync(LookupUpsertRequest request);
    Task<LookupItemDto> UpdateStatusAsync(int id, LookupUpsertRequest request);
    Task DeleteStatusAsync(int id);

    // Agents
    Task<IReadOnlyList<AgentDto>> GetAgentsAsync();
    Task<AgentCreatedResponse> CreateAgentAsync(CreateAgentRequest request);
    Task DeleteAgentAsync(int id);
}

public interface IDashboardService
{
    Task<DashboardStatsDto> GetStatsAsync(ICurrentUser viewer);
}

public interface IAiAnalysisService
{
    Task<AiAnalysisDto> AnalyzeExistingTicketAsync(int ticketId, ICurrentUser actor, bool persist = true);
    Task<string> GenerateReplyAsync(int ticketId, ICurrentUser actor);
    Task<string> GenerateAutoReplyAsync(int ticketId, int customerId);
}