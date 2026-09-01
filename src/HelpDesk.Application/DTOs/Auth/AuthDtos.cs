namespace HelpDesk.Application.DTOs.Auth;

public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(int Id, string Name, string Email, string Role, string Token, DateTime ExpiresAtUtc);
public record UserDto(int Id, string Name, string Email, string Role, int? DepartmentId, string? DepartmentName, bool IsActive, DateTime CreatedAt);
public record CreateUserRequest(string Name, string Email, string Password, string Role, int? DepartmentId);
public record UpdateUserRequest(string? Name, string? Email, string? Password, string? Role, int? DepartmentId, bool? IsActive);