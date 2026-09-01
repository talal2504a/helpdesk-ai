using HelpDesk.Application.DTOs.Auth;
using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Register a new customer account.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        => Ok(await _auth.RegisterAsync(request));

    /// <summary>Login with email + password, returns JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        => Ok(await _auth.LoginAsync(request));
}

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly ICurrentUser _me;

    public UsersController(IUserService users, ICurrentUser me) { _users = users; _me = me; }

    /// <summary>List users (Admin only). Filter by role/department.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll([FromQuery] string? role, [FromQuery] int? departmentId)
        => Ok(await _users.GetAllAsync(role, departmentId));

    /// <summary>List agents — used by agent/admin to assign tickets.</summary>
    [HttpGet("agents")]
    [Authorize(Roles = "Agent, Admin")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAgents() =>
        Ok(await _users.GetAllAsync("Agent"));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserDto>> Get(int id)
    {
        if (!_me.IsAgentOrAdmin() && _me.UserId != id) return Forbid();
        return Ok(await _users.GetByIdAsync(id));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public Task<UserDto> Create([FromBody] CreateUserRequest request) => _users.CreateAsync(request);

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task<UserDto> Update(int id, [FromBody] UpdateUserRequest request) => _users.UpdateAsync(id, request);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public Task Deactivate(int id) => _users.DeactivateAsync(id);
}