namespace HelpDesk.Application.DTOs.Admin;

public record LookupItemDto(int Id, string Name, string? Description, bool IsActive);
public record LookupUpsertRequest(string Name, string? Description, bool IsActive = true);
public record PriorityLookupUpsertRequest(string Name, int Level, string? Description, bool IsActive = true);
public record DepartmentDto(int Id, string Name, string? Description, bool IsActive, int UserCount, int TicketCount);
public record CreateAgentRequest(string Name, string Email, int? DepartmentId);
public record AgentCreatedResponse(int Id, string Name, string Email, string Role, string? Department, string GeneratedPassword, string LoginUrl);
public record AgentDto(int Id, string Name, string Email, string Role, string? DepartmentName, bool IsActive, int AssignedTickets, DateTime CreatedAt);