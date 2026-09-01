using HelpDesk.Application.DTOs.Admin;
using HelpDesk.Application.Exceptions;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

/// <summary>Admin CRUD for departments, categories, priorities, statuses and agents.</summary>
public class AdminService : IAdminService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AdminService(Data.HelpDeskDbContext db, IPasswordHasher hasher)
    {
        _db = db; _hasher = hasher;
    }

    // ---------------- Departments ----------------
    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync() =>
        await _db.Departments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Name, d.Description, d.IsActive, d.Users.Count, d.Tickets.Count))
            .ToListAsync();

    public async Task<LookupItemDto> CreateDepartmentAsync(LookupUpsertRequest r)
    {
        var name = TrimName(r);
        if (await _db.Departments.AnyAsync(d => d.Name == name)) throw new ConflictException($"Department '{name}' already exists.");
        var e = new Domain.Entities.Department { Name = name, Description = r.Description?.Trim(), IsActive = r.IsActive };
        _db.Departments.Add(e);
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, e.Description, e.IsActive);
    }

    public async Task<LookupItemDto> UpdateDepartmentAsync(int id, LookupUpsertRequest r)
    {
        var e = await _db.Departments.FindAsync(id) ?? throw new NotFoundException("Department");
        var name = TrimName(r);
        if (await _db.Departments.AnyAsync(d => d.Name == name && d.Id != id)) throw new ConflictException($"Department '{name}' already exists.");
        e.Name = name; e.Description = r.Description?.Trim(); e.IsActive = r.IsActive;
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, e.Description, e.IsActive);
    }

    public async Task DeleteDepartmentAsync(int id)
    {
        var hasTickets = await _db.Tickets.AnyAsync(t => t.DepartmentId == id);
        if (hasTickets) throw new ConflictException("Cannot delete a department that has tickets. Deactivate it instead.");
        _db.Departments.Remove(await _db.Departments.FindOrThrowAsync(id, "Department"));
        await _db.SaveChangesAsync();
    }

    // ---------------- Categories ----------------
    public async Task<IReadOnlyList<CategoryLookupDto>> GetCategoriesAsync() =>
        await _db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new CategoryLookupDto(c.Id, c.Name)).ToListAsync();

    public async Task<LookupItemDto> CreateCategoryAsync(LookupUpsertRequest r)
    {
        var name = TrimName(r);
        if (await _db.Categories.AnyAsync(c => c.Name == name)) throw new ConflictException($"Category '{name}' already exists.");
        var e = new Domain.Entities.Category { Name = name, Description = r.Description?.Trim(), IsActive = r.IsActive };
        _db.Categories.Add(e);
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, e.Description, e.IsActive);
    }

    public async Task<LookupItemDto> UpdateCategoryAsync(int id, LookupUpsertRequest r)
    {
        var e = await _db.Categories.FindAsync(id) ?? throw new NotFoundException("Category");
        var name = TrimName(r);
        if (await _db.Categories.AnyAsync(c => c.Name == name && c.Id != id)) throw new ConflictException($"Category '{name}' already exists.");
        e.Name = name; e.Description = r.Description?.Trim(); e.IsActive = r.IsActive;
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, e.Description, e.IsActive);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        if (await _db.Tickets.AnyAsync(t => t.CategoryId == id))
            throw new ConflictException("Cannot delete a category that has tickets. Deactivate it instead.");
        _db.Categories.Remove(await _db.Categories.FindOrThrowAsync(id, "Category"));
        await _db.SaveChangesAsync();
    }

    // ---------------- Priorities ----------------
    public async Task<IReadOnlyList<PriorityLookupDto>> GetPrioritiesAsync() =>
        await _db.Priorities.AsNoTracking().OrderBy(p => p.Level)
            .Select(p => new PriorityLookupDto(p.Id, p.Name, p.Level)).ToListAsync();

    public async Task<LookupItemDto> CreatePriorityAsync(PriorityLookupUpsertRequest r)
    {
        var name = TrimName(new(r.Name, r.Description, r.IsActive));
        if (r.Level is < 1 or > 10) throw new ValidationException("Level", "Priority level must be between 1 and 10.");
        if (await _db.Priorities.AnyAsync(p => p.Name == name)) throw new ConflictException($"Priority '{name}' already exists.");
        var e = new Domain.Entities.Priority { Name = name, Level = r.Level, IsActive = r.IsActive };
        _db.Priorities.Add(e);
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, r.Description?.Trim(), e.IsActive);
    }

    public async Task<LookupItemDto> UpdatePriorityAsync(int id, PriorityLookupUpsertRequest r)
    {
        var e = await _db.Priorities.FindAsync(id) ?? throw new NotFoundException("Priority");
        var name = TrimName(new(r.Name, r.Description, r.IsActive));
        if (r.Level is < 1 or > 10) throw new ValidationException("Level", "Priority level must be between 1 and 10.");
        if (await _db.Priorities.AnyAsync(p => p.Name == name && p.Id != id)) throw new ConflictException($"Priority '{name}' already exists.");
        e.Name = name; e.Level = r.Level; e.IsActive = r.IsActive;
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, null, e.IsActive);
    }

    public async Task DeletePriorityAsync(int id)
    {
        if (await _db.Tickets.AnyAsync(t => t.PriorityId == id))
            throw new ConflictException("Cannot delete a priority that has tickets. Deactivate it instead.");
        _db.Priorities.Remove(await _db.Priorities.FindOrThrowAsync(id, "Priority"));
        await _db.SaveChangesAsync();
    }

    // ---------------- Statuses ----------------
    public async Task<IReadOnlyList<StatusLookupDto>> GetStatusesAsync() =>
        await _db.Statuses.AsNoTracking().OrderBy(s => s.Id)
            .Select(s => new StatusLookupDto(s.Id, s.Name)).ToListAsync();

    public async Task<LookupItemDto> CreateStatusAsync(LookupUpsertRequest r)
    {
        var name = TrimName(r);
        if (await _db.Statuses.AnyAsync(s => s.Name == name)) throw new ConflictException($"Status '{name}' already exists.");
        var e = new Domain.Entities.Status { Name = name, IsActive = r.IsActive };
        _db.Statuses.Add(e);
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, null, e.IsActive);
    }

    public async Task<LookupItemDto> UpdateStatusAsync(int id, LookupUpsertRequest r)
    {
        var e = await _db.Statuses.FindAsync(id) ?? throw new NotFoundException("Status");
        var name = TrimName(r);
        if (await _db.Statuses.AnyAsync(s => s.Name == name && s.Id != id)) throw new ConflictException($"Status '{name}' already exists.");
        e.Name = name; e.IsActive = r.IsActive;
        await _db.SaveChangesAsync();
        return new LookupItemDto(e.Id, e.Name, null, e.IsActive);
    }

    public async Task DeleteStatusAsync(int id)
    {
        if (await _db.Tickets.AnyAsync(t => t.StatusId == id))
            throw new ConflictException("Cannot delete a status that has tickets. Deactivate it instead.");
        _db.Statuses.Remove(await _db.Statuses.FindOrThrowAsync(id, "Status"));
        await _db.SaveChangesAsync();
    }

    private static string TrimName(LookupUpsertRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length < 2 || r.Name.Length > 80)
            throw new ValidationException("Name", "Name must be between 2 and 80 characters.");
        return r.Name.Trim();
    }

    // ---------------- Agents ----------------
    public async Task<IReadOnlyList<AgentDto>> GetAgentsAsync() =>
        await _db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Agent.ToRoleName() || u.Role == UserRole.Admin.ToRoleName())
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new AgentDto(
                u.Id, u.Name, u.Email, u.Role, u.Department.Name == null ? null : u.Department.Name, u.IsActive,
                u.TicketsAssigned.Count, u.CreatedAt))
            .ToListAsync();

    public async Task<AgentCreatedResponse> CreateAgentAsync(CreateAgentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 2)
            throw new ValidationException("Name", "Name must be at least 2 characters.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains("@"))
            throw new ValidationException("Email", "Valid email is required.");
        if (await _db.Users.AnyAsync(u => u.Email == request.Email.Trim()))
            throw new ConflictException("Email already exists.");

        var generatedPassword = GenerateSecurePassword();
        var department = request.DepartmentId.HasValue
            ? await _db.Departments.FindAsync(request.DepartmentId.Value)
            : await _db.Departments.OrderBy(d => d.Id).FirstOrDefaultAsync();

        var agent = new User
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _hasher.Hash(generatedPassword),
            Role = UserRole.Agent.ToRoleName(),
            DepartmentId = department?.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(agent);
        await _db.SaveChangesAsync();

        return new AgentCreatedResponse(
            agent.Id,
            agent.Name,
            agent.Email,
            agent.Role,
            department?.Name,
            generatedPassword,
            "/login"
        );
    }

    public async Task DeleteAgentAsync(int id)
    {
        var agent = await _db.Users.FindAsync(id) 
                    ?? throw new NotFoundException("Agent");
        
        if (agent.Role == UserRole.Admin.ToRoleName())
            throw new ConflictException("Cannot delete an admin account.");
        
        if (await _db.Tickets.AnyAsync(t => t.AssignedAgentId == id))
            throw new ConflictException("Cannot delete an agent with assigned tickets. Reassign tickets first.");
        
        _db.Users.Remove(agent);
        await _db.SaveChangesAsync();
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 12)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}