using HelpDesk.Application.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace HelpDesk.Api.Middleware;

/// <summary>Converts domain exceptions into a uniform JSON error envelope.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            if (ex is ValidationException v)
                await context.Response.WriteAsJsonAsync(new { error = new { code = ex.ErrorCode, message = ex.Message, errors = v.Errors } }, JsonOpts);
            else
                await context.Response.WriteAsJsonAsync(new { error = new { code = ex.ErrorCode, message = ex.Message } }, JsonOpts);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499; // client closed request
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            // Debug:VerboseErrors=true returns the real exception message (shared-hosting stdout logs are unreliable).
            var verbose = context.RequestServices.GetRequiredService<IConfiguration>()["Debug:VerboseErrors"] == "true";
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = verbose ? ex.ToString() : "An unexpected error occurred. Please try again later."
                }
            }, JsonOpts);
        }
    }
}

public record ApiError(string Code, string Message, object? Errors = null);