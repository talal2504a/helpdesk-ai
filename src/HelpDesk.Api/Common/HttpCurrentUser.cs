using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Services;

namespace HelpDesk.Api.Common;

/// <summary>Reads the authenticated user from the HTTP context claims (JWT).</summary>
public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public int? UserId
    {
        get
        {
            var ctx = _accessor.HttpContext;
            var sub = ctx?.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                      ?? ctx?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Role => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

    public bool IsAdmin => Role == Domain.Enums.UserRoleExtensions.AdminRole;
    public bool IsAgent => Role == Domain.Enums.UserRoleExtensions.AgentRole;
}
