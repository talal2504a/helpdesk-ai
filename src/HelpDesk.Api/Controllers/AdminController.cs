using HelpDesk.Application.DTOs.Admin;
using HelpDesk.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>Admin management of departments, categories, priorities, statuses, agents and tickets.</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;
    private readonly ITicketService _tickets;
    public AdminController(IAdminService admin, ITicketService tickets) { _admin = admin; _tickets = tickets; }

    // -------- Departments --------
    [HttpPost("departments")] public Task<LookupItemDto> CreateDepartment([FromBody] LookupUpsertRequest r) => _admin.CreateDepartmentAsync(r);
    [HttpPut("departments/{id:int}")] public Task<LookupItemDto> UpdateDepartment(int id, [FromBody] LookupUpsertRequest r) => _admin.UpdateDepartmentAsync(id, r);
    [HttpDelete("departments/{id:int}")] public Task DeleteDepartment(int id) => _admin.DeleteDepartmentAsync(id);

    // -------- Categories --------
    [HttpPost("categories")] public Task<LookupItemDto> CreateCategory([FromBody] LookupUpsertRequest r) => _admin.CreateCategoryAsync(r);
    [HttpPut("categories/{id:int}")] public Task<LookupItemDto> UpdateCategory(int id, [FromBody] LookupUpsertRequest r) => _admin.UpdateCategoryAsync(id, r);
    [HttpDelete("categories/{id:int}")] public Task DeleteCategory(int id) => _admin.DeleteCategoryAsync(id);

    // -------- Priorities --------
    [HttpPost("priorities")] public Task<LookupItemDto> CreatePriority([FromBody] PriorityLookupUpsertRequest r) => _admin.CreatePriorityAsync(r);
    [HttpPut("priorities/{id:int}")] public Task<LookupItemDto> UpdatePriority(int id, [FromBody] PriorityLookupUpsertRequest r) => _admin.UpdatePriorityAsync(id, r);
    [HttpDelete("priorities/{id:int}")] public Task DeletePriority(int id) => _admin.DeletePriorityAsync(id);

    // -------- Statuses --------
    [HttpPost("statuses")] public Task<LookupItemDto> CreateStatus([FromBody] LookupUpsertRequest r) => _admin.CreateStatusAsync(r);
    [HttpPut("statuses/{id:int}")] public Task<LookupItemDto> UpdateStatus(int id, [FromBody] LookupUpsertRequest r) => _admin.UpdateStatusAsync(id, r);
    [HttpDelete("statuses/{id:int}")] public Task DeleteStatus(int id) => _admin.DeleteStatusAsync(id);

    // -------- Agents --------
    [HttpGet("agents")] public Task<IReadOnlyList<AgentDto>> GetAgents() => _admin.GetAgentsAsync();
    [HttpPost("agents")] public Task<AgentCreatedResponse> CreateAgent([FromBody] CreateAgentRequest r) => _admin.CreateAgentAsync(r);
    [HttpDelete("agents/{id:int}")] public Task DeleteAgent(int id) => _admin.DeleteAgentAsync(id);

    // -------- Tickets --------
    [HttpDelete("tickets/{id:int}")] public Task DeleteTicket(int id) => _tickets.DeleteAsync(id, new AdminCurrentUser());
}

public class AdminCurrentUser : ICurrentUser
{
    public int? UserId => 1;
    public string? Role => "Admin";
    public bool IsAdmin => true;
    public bool IsAgent => true;
}