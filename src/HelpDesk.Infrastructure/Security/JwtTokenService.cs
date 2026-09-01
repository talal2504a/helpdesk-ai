using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace HelpDesk.Infrastructure.Security;

/// <summary>JWT issuer. All settings from configuration (user-secrets in dev / env vars in prod): Jwt__Secret, Jwt__Issuer, Jwt__Audience, Jwt__ExpiryMinutes.</summary>
public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(Domain.Entities.User user);
}

public class JwtTokenService : ITokenService
{
    private readonly ILogger<JwtTokenService> _logger;
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _logger = logger;
        _secret = configuration["Jwt:Secret"]
            ?? configuration["Jwt__Secret"]
            ?? Environment.GetEnvironmentVariable("Jwt__Secret")
            ?? "default-32-characters-long-secret-change-me-1234567890";
        _issuer = configuration["Jwt:Issuer"] ?? configuration["Jwt__Issuer"] ?? "HelpDesk.Api";
        _audience = configuration["Jwt:Audience"] ?? configuration["Jwt__Audience"] ?? "HelpDesk.Client";
        _expiryMinutes = configuration.GetValue<int?>("Jwt:ExpiryMinutes")
                         ?? configuration.GetValue<int?>("Jwt__ExpiryMinutes") ?? 720;

        if (_secret.Length < 32)
            throw new InvalidOperationException(
                "Configuration key 'Jwt__Secret' is missing or shorter than 32 characters. " +
                "Set it via user-secrets (dev) or environment variables (prod): e.g. dotnet user-secrets set \"Jwt__Secret\" \"...at-least-32-chars-long...\"");
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(Domain.Entities.User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_expiryMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.Name),
            new(ClaimTypes.Role, user.Role)
        };

        var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_issuer, _audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}