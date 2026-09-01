using HelpDesk.Application.DTOs.Admin;
using HelpDesk.Application.DTOs.Ai;
using HelpDesk.Application.DTOs.Dashboard;
using HelpDesk.Application.Interfaces;
using HelpDesk.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

/// <summary>Lookup endpoints (dropdown data) — any authenticated user.</summary>
[ApiController]
[Route("api")]
[Authorize]
public class LookupController : ControllerBase
{
    private readonly IAdminService _admin;
    public LookupController(IAdminService admin) => _admin = admin;

    [HttpGet("departments")] public Task<IReadOnlyList<DepartmentDto>> Departments() => _admin.GetDepartmentsAsync();
    [HttpGet("categories")] public Task<IReadOnlyList<CategoryLookupDto>> Categories() => _admin.GetCategoriesAsync();
    [HttpGet("priorities")] public Task<IReadOnlyList<PriorityLookupDto>> Priorities() => _admin.GetPrioritiesAsync();
    [HttpGet("statuses")] public Task<IReadOnlyList<StatusLookupDto>> Statuses() => _admin.GetStatusesAsync();
}

/// <summary>Dashboard statistics (role-scoped).</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ICurrentUser _me;
    public DashboardController(IDashboardService dashboard, ICurrentUser me) { _dashboard = dashboard; _me = me; }

    [HttpGet("stats")]
    public async Task<ActionResult<DashboardStatsDto>> Stats() => Ok(await _dashboard.GetStatsAsync(_me));
}

/// <summary>AI: re-run classification on an existing ticket (Agent/Admin).</summary>
[ApiController]
[Route("api/ai")]
[Authorize(Roles = "Agent, Admin")]
public class AiController : ControllerBase
{
    private readonly IAiAnalysisService _ai;
    private readonly IAiClient _aiClient;
    private readonly ICurrentUser _me;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiAnalysisService ai, IAiClient aiClient, ICurrentUser me, ILogger<AiController> logger)
    {
        _ai = ai; _aiClient = aiClient; _me = _me; _logger = logger;
    }

    /// <summary>Analyze (category/priority/summary/sentiment/suggested reply) and persist an AIAnalysis row.</summary>
    [HttpPost("analyze/{ticketId:int}")]
    public async Task<ActionResult<AiAnalysisDto>> Analyze(int ticketId)
    {
        try { return Ok(await _ai.AnalyzeExistingTicketAsync(ticketId, _me)); }
        catch (Exception ex) { _logger.LogWarning(ex, "AI analyze failed for ticket {TicketId}", ticketId); throw; }
    }

    /// <summary>Dry-run analysis of arbitrary text (no persistence).</summary>
    [HttpPost("analyze-preview")]
    public async Task<ActionResult<AiAnalysisDto>> AnalyzePreview([FromBody] AnalyzeTicketRequest request)
    {
        try
        {
            var analysis = await _aiClient.AnalyzeAsync(request.Title, request.Description);
            return Ok(new AiAnalysisDto(0, 0, analysis.Category, analysis.Priority, analysis.Summary,
                analysis.Sentiment, analysis.SuggestedReply, analysis.ConfidenceScore, analysis.Provider, DateTime.UtcNow));
        }
        catch (Exception ex) { _logger.LogWarning(ex, "AI preview failed"); throw; }
    }

    /// <summary>Generate an AI reply for the ticket based on conversation history.</summary>
    [HttpPost("reply/{ticketId:int}")]
    public async Task<ActionResult<string>> GenerateReply(int ticketId)
    {
        try
        {
            var reply = await _ai.GenerateReplyAsync(ticketId, _me);
            return Ok(new { reply });
        }
        catch (Exception ex) { _logger.LogWarning(ex, "AI reply generation failed for ticket {TicketId}", ticketId); throw; }
    }
}