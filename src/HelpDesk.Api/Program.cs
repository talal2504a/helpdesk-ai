using System.Text;
using HelpDesk.Api.Common;
using HelpDesk.Api.Hubs;
using HelpDesk.Api.Middleware;
using HelpDesk.Application.Interfaces;
using HelpDesk.Infrastructure.Ai;
using HelpDesk.Infrastructure.Data;
using HelpDesk.Infrastructure.RealTime;
using HelpDesk.Infrastructure.Repositories;
using HelpDesk.Infrastructure.Security;
using HelpDesk.Infrastructure.Services;
using HelpDesk.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (connection string from env var / user-secrets only) ----------
var connectionString = builder.Configuration.GetConnectionString("HelpDeskDb")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__HelpDeskDb")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=HelpDeskDb;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<HelpDeskDbContext>(opt => opt.UseSqlServer(connectionString,
    // Shared hosting DB connections can be flaky - retry transient failures automatically.
    sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

// ---------- Application services ----------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAiAnalysisService, AiAnalysisService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<IAiClient>(sp =>
{
    // Ai__ApiKey set ho (OpenRouter / OpenAI-compatible) → woh client, warna Cohere, warna rule-based fallback.
    if (!string.IsNullOrWhiteSpace(builder.Configuration.GetSection("Ai")["ApiKey"]))
        return sp.GetRequiredService<OpenAiCompatibleAiClient>();
    if (!string.IsNullOrWhiteSpace(builder.Configuration.GetSection("Cohere")["ApiKey"]))
        return sp.GetRequiredService<CohereAiClient>();
    return sp.GetRequiredService<RuleBasedAiFallback>();
});
builder.Services.AddScoped<OpenAiCompatibleAiClient>();
builder.Services.AddScoped<CohereAiClient>();
builder.Services.AddScoped<RuleBasedAiFallback>();
builder.Services.AddHttpClient("Ai");

// ---------- SignalR + current user ----------
builder.Services.AddSignalR();
// Non-generic alias so Infrastructure can broadcast without referencing the Api hub type.
builder.Services.AddSingleton<IHubContext<Hub>>(sp => sp.GetRequiredService<IHubContext<TicketHub>>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<INotifier, SignalRNotifier>();
builder.Services.AddScoped<DbSeeder>();

// ---------- Authentication (JWT) ----------
        var jwtSecret = builder.Configuration["Jwt:Secret"]
            ?? builder.Configuration["Jwt__Secret"]
            ?? Environment.GetEnvironmentVariable("Jwt__Secret")
            ?? "default-32-characters-long-secret-change-me-1234567890";
        var jwtIssuer = builder.Configuration["Jwt__Issuer"] ?? "HelpDesk.Api";
        var jwtAudience = builder.Configuration["Jwt__Audience"] ?? "HelpDesk.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Allow SignalR JS client to send the access token via query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/tickets"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---------- Controllers + Swagger ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Help Desk API", Version = "v1", Description = "Customer Support Ticketing System API" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste your JWT token here (from /api/auth/login)."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});

// CORS for the React dev server / frontend container.
var corsOrigins = Environment.GetEnvironmentVariable("Cors__Origins")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(o => o.AddPolicy("Frontend", p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Allow appsettings.Production.json to supply seed settings (shared hosting has no env vars).
foreach (var key in new[] { "Seed:AdminPassword", "Seed:AgentPassword", "Seed:CustomerPassword", "Seed:ResetPasswords" })
{
    var cfgValue = builder.Configuration[key];
    if (!string.IsNullOrWhiteSpace(cfgValue) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key.Replace(":", "__"))))
        Environment.SetEnvironmentVariable(key.Replace(":", "__"), cfgValue);
}

// ---------- Pipeline ----------
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TicketHub>("/hubs/tickets");

// Serve uploaded attachments (local storage impl) at /uploads.
var uploadsRoot = LocalFileStorageEnvRoot();
if (Directory.Exists(uploadsRoot))
{
    app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });
}
static string LocalFileStorageEnvRoot() => Path.GetFullPath(Environment.GetEnvironmentVariable("Storage__AttachmentsPath") ?? "uploads");
app.UseStaticFiles(); // wwwroot (React SPA build) if present

// SPA fallback: React Router deep links -> index.html, but /api and /hubs keep real 404s.
app.MapFallback(async ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api") || ctx.Request.Path.StartsWithSegments("/hubs"))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    var indexPath = Path.Combine(webRoot, "index.html");
    if (File.Exists(indexPath))
    {
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.SendFileAsync(indexPath);
    }
    else
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
    }
});

// Health endpoint for Azure/Docker probes.
// Health endpoint for Azure/Docker probes.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", utc = DateTime.UtcNow }));

// ---------- Migrate + seed on startup ----------
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

app.Run();

// Makes the implicit Program class visible to integration tests.
public partial class Program { }
