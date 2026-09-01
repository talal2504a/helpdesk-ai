using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HelpDesk.Infrastructure.Data;

/// <summary>Seeds lookup data + demo accounts on first run. Admin password comes from env/user-secrets only.</summary>
public class DbSeeder
{
    private readonly HelpDeskDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(HelpDeskDbContext db, IPasswordHasher hasher, ILogger<DbSeeder> logger)
    {
        _db = db; _hasher = hasher; _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        try
        {
            await MigrateIfPossibleAsync(ct);
            await EnsureAiSupportUserAsync(ct);

            if (await _db.Statuses.AnyAsync(ct))
            {
                _logger.LogInformation("Database already seeded.");
                await ResetDemoPasswordsIfRequestedAsync(ct);
                return;
            }
            if (await _db.Statuses.AnyAsync(ct)) { _logger.LogInformation("Database already seeded."); return; }

            var departments = new[]
            {
                new Department { Name = "Technical Support", Description = "Technical issues and bug reports" },
                new Department { Name = "Billing", Description = "Payments, invoices and refunds" },
                new Department { Name = "Sales & Accounts", Description = "Account management and pre-sales" },
                new Department { Name = "General", Description = "Everything else" },
            };
            _db.Departments.AddRange(departments);

            var categories = new[]
            {
                new Category { Name = "Bug Report", Description = "Software defects" },
                new Category { Name = "Feature Request", Description = "New functionality ideas" },
                new Category { Name = "Billing Issue", Description = "Payment or invoice problems" },
                new Category { Name = "Account Access", Description = "Login / access problems" },
                new Category { Name = "How To Question", Description = "Usage guidance" },
                new Category { Name = "Other", Description = "Anything else" },
            };
            _db.Categories.AddRange(categories);

            var priorities = new[]
            {
                new Priority { Name = "Low", Level = 1 },
                new Priority { Name = "Medium", Level = 2 },
                new Priority { Name = "High", Level = 3 },
                new Priority { Name = "Critical", Level = 4 },
            };
            _db.Priorities.AddRange(priorities);

            var statuses = new[]
            {
                new Status { Name = "Open" },
                new Status { Name = "InProgress" },
                new Status { Name = "Pending" },
                new Status { Name = "Resolved" },
                new Status { Name = "Closed" },
            };
            _db.Statuses.AddRange(statuses);

            var adminPassword = Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? "HelpDesk@123";
            var agentPassword = Environment.GetEnvironmentVariable("Seed__AgentPassword") ?? adminPassword;
            var customerPassword = Environment.GetEnvironmentVariable("Seed__CustomerPassword") ?? adminPassword;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Seed__AdminPassword")))
            {
                _logger.LogWarning("Seed__AdminPassword not set - using default password 'HelpDesk@123' for demo users.");
            }
            await _db.SaveChangesAsync(ct);

            _db.Users.AddRange(new[]
            {
                new User { Name = "System Admin", Email = "admin@helpdesk.local",
                    PasswordHash = _hasher.Hash(adminPassword), Role = UserRole.Admin.ToRoleName(), IsActive = true },
                new User { Name = "Amit Agent", Email = "agent@helpdesk.local",
                    PasswordHash = _hasher.Hash(agentPassword), Role = UserRole.Agent.ToRoleName(),
                    DepartmentId = departments[0].Id, IsActive = true },
                new User { Name = "Priya Customer", Email = "customer@helpdesk.local",
                    PasswordHash = _hasher.Hash(customerPassword), Role = UserRole.Customer.ToRoleName(), IsActive = true },
            });

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Seed data created: {Deps} departments, {Cats} categories, {Prios} priorities, {Stats} statuses.",
                departments.Length, categories.Length, priorities.Length, statuses.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Seeding failed at startup. The app will continue without initial data.");
        }
    }

    /// <summary>
    /// Makes sure the dedicated AI Support user exists on every startup — AI-generated
    /// messages are attributed to this account. Runs before the normal seed early-return.
    /// </summary>
    private async Task EnsureAiSupportUserAsync(CancellationToken ct)
    {
        try
        {
            if (await _db.Users.AnyAsync(u => u.Email == "ai@helpdesk.local", ct)) return;

            var techDeptId = await _db.Departments.Where(d => d.Name == "Technical Support")
                .Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
            var password = Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? "HelpDesk@123";

            _db.Users.Add(new User
            {
                Name = "AI Support",
                Email = "ai@helpdesk.local",
                PasswordHash = _hasher.Hash(password),
                Role = UserRole.Agent.ToRoleName(),
                DepartmentId = techDeptId,
                IsActive = true
            });
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("AI Support user (ai@helpdesk.local) created.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure AI Support user at startup.");
        }
    }

    /// <summary>
    /// Existing (already-seeded) databases never re-run the seed, so demo account
    /// passwords can drift from configuration. When Seed__ResetPasswords=true,
    /// update the three demo accounts' password hashes from per-account env vars
    /// (Seed__AdminPassword / Seed__AgentPassword / Seed__CustomerPassword).
    /// Also clears any leftover lockouts so demo logins always work.
    /// </summary>
    private async Task ResetDemoPasswordsIfRequestedAsync(CancellationToken ct)
    {
        var reset = Environment.GetEnvironmentVariable("Seed__ResetPasswords");
        if (!string.Equals(reset, "true", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(reset, "1", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var adminPassword = Environment.GetEnvironmentVariable("Seed__AdminPassword") ?? "HelpDesk@123";
            var agentPassword = Environment.GetEnvironmentVariable("Seed__AgentPassword") ?? adminPassword;
            var customerPassword = Environment.GetEnvironmentVariable("Seed__CustomerPassword") ?? adminPassword;

            var demoEmails = new[] { "admin@helpdesk.local", "agent@helpdesk.local", "customer@helpdesk.local" };
            var demoUsers = await _db.Users.Where(u => demoEmails.Contains(u.Email)).ToListAsync(ct);
            foreach (var user in demoUsers)
            {
                var pwd = user.Email switch
                {
                    "agent@helpdesk.local" => agentPassword,
                    "customer@helpdesk.local" => customerPassword,
                    _ => adminPassword
                };
                user.PasswordHash = _hasher.Hash(pwd);
                user.FailedLoginCount = 0;
                user.LockedUntilUtc = null;
                user.IsActive = true;
                _logger.LogInformation("Reset password/lockout for demo account {Email}.", user.Email);
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resetting demo passwords failed at startup.");
        }
    }

    private async Task MigrateIfPossibleAsync(CancellationToken ct)
    {
        try { await _db.Database.MigrateAsync(ct); }
        catch (Exception ex) { _logger.LogError(ex, "Migration failed at startup."); }
    }
}