using HelpDesk.Application.DTOs.Auth;

namespace HelpDesk.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}

public interface IUserService
{
    System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<UserDto>> GetAllAsync(string? role = null, int? departmentId = null);
    System.Threading.Tasks.Task<UserDto> GetByIdAsync(int id);
    System.Threading.Tasks.Task<UserDto> CreateAsync(CreateUserRequest request);
    System.Threading.Tasks.Task<UserDto> UpdateAsync(int id, UpdateUserRequest request);
    System.Threading.Tasks.Task DeactivateAsync(int id);
}