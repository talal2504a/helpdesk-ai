using HelpDesk.Application.DTOs.Auth;
using HelpDesk.Application.Exceptions;
using HelpDesk.Application.Interfaces;
using HelpDesk.Domain.Entities;
using HelpDesk.Domain.Enums;
using HelpDesk.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly Data.HelpDeskDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ILogger<AuthService> _logger;

    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public AuthService(Data.HelpDeskDbContext db, IPasswordHasher hasher, ITokenService tokens, ILogger<AuthService> logger)
    {
        _db = db; _hasher = hasher; _tokens = tokens; _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        AuthValidators.Validate(request);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = UserRole.Customer.ToRoleName() // public self-registration is always Customer
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var (token, exp) = _tokens.CreateToken(user);
        return new AuthResponse(user.Id, user.Name, user.Email, user.Role, token, exp);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        AuthValidators.Validate(request);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email)
                ?? throw new UnauthorizedException("Invalid credentials or inactive account.");

        if (!user.IsActive)
            throw new UnauthorizedException("Invalid credentials or inactive account.");

        if (user.LockedUntilUtc.HasValue && user.LockedUntilUtc.Value > DateTime.UtcNow)
            throw new AppException($"Account locked. Try again after {user.LockedUntilUtc.Value:HH:mm} UTC.", 429, "ACCOUNT_LOCKED");

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockedUntilUtc = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                _logger.LogWarning("Account {Email} locked for {Minutes} minutes after repeated failed logins.", email, LockoutDuration.TotalMinutes);
            }
            await _db.SaveChangesAsync();
            throw new UnauthorizedException();
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;
        await _db.SaveChangesAsync();

        var (token, exp) = _tokens.CreateToken(user);
        return new AuthResponse(user.Id, user.Name, user.Email, user.Role, token, exp);
    }
}