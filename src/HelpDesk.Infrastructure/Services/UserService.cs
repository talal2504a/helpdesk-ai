using HelpDesk.Application.DTOs.Auth;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IPasswordHasher _hasher;

    public UserService(Data.HelpDeskDbContext db, IPasswordHasher hasher)
    {
        _db = db; _hasher = hasher;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllAsync(string? role = null, int? departmentId = null)
    {
        var q = _db.Users.Include(u => u.Department).AsNoTracking().OrderBy(u => u.Name).AsQueryable();
        if (!string.IsNullOrWhiteSpace(role)) q = q.Where(u => u.Role == role);
        if (departmentId.HasValue) q = q.Where(u => u.DepartmentId == departmentId);
        var users = await q.ToListAsync();
        return users.Select(u => new UserDto(u.Id, u.Name, u.Email, u.Role, u.DepartmentId, u.Department?.Name, u.IsActive, u.CreatedAt)).ToList();
    }

    public async Task<UserDto> GetByIdAsync(int id)
    {
        var u = await _db.Users.Include(x => x.Department).AsNoTracking().FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NotFoundException("User");
        return new UserDto(u.Id, u.Name, u.Email, u.Role, u.DepartmentId, u.Department?.Name, u.IsActive, u.CreatedAt);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new ValidationException(nameof(r.Name), "Name is required.");
        if (r.Password is null || r.Password.Length < 8) throw new ValidationException(nameof(r.Password), "Password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(r.Email) || !AuthValidators.IsValidEmail(r.Email))
            throw new ValidationException(nameof(r.Email), "A valid email address is required.");

        var email = r.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new ConflictException("An account with this email already exists.");
        if (r.DepartmentId.HasValue && !await _db.Departments.AnyAsync(d => d.Id == r.DepartmentId))
            throw new ValidationException(nameof(r.DepartmentId), "Department does not exist.");
        var role = ParseRole(r.Role);

        var user = new User { Name = r.Name.Trim(), Email = email, PasswordHash = _hasher.Hash(r.Password), Role = role, DepartmentId = r.DepartmentId };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return new UserDto(user.Id, user.Name, user.Email, user.Role, user.DepartmentId, null, true, user.CreatedAt);
    }

    public async Task<UserDto> UpdateAsync(int id, UpdateUserRequest r)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new NotFoundException("User");

        if (r.Name is not null)
        {
            if (r.Name.Trim().Length < 2 || r.Name.Length > 100) throw new ValidationException("Name", "Name must be between 2 and 100 characters.");
            user.Name = r.Name.Trim();
        }
        if (r.Email is not null)
        {
            if (!AuthValidators.IsValidEmail(r.Email)) throw new ValidationException("Email", "Invalid email address.");
            var email = r.Email.Trim().ToLowerInvariant();
            if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id)) throw new ConflictException("Email already in use.");
            user.Email = email;
        }
        if (r.Password is not null)
        {
            if (r.Password.Length < 8) throw new ValidationException("Password", "Password must be at least 8 characters.");
            user.PasswordHash = _hasher.Hash(r.Password);
        }
        if (r.Role is not null) user.Role = ParseRole(r.Role);
        if (r.DepartmentId.HasValue)
        {
            if (!await _db.Departments.AnyAsync(d => d.Id == r.DepartmentId.Value)) throw new ValidationException("DepartmentId", "Department does not exist.");
            user.DepartmentId = r.DepartmentId;
        }
        else user.DepartmentId = null;
        if (r.IsActive.HasValue) user.IsActive = r.IsActive.Value;

        await _db.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task DeactivateAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id) ?? throw new NotFoundException("User");
        user.IsActive = false;
        await _db.SaveChangesAsync();
    }

    private static string ParseRole(string? role) =>
        Enum.TryParse<UserRole>(role, true, out var parsed)
            ? parsed.ToRoleName()
            : throw new ValidationException("Role", "Role must be Customer, Agent or Admin.");
}