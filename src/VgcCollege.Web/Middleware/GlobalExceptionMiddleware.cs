using System.Net;

namespace VgcCollege.Web.Middleware;

/// Catches any unhandled exception in the pipeline, logs the full details
/// server-side, and returns a user-friendly error response.
///
/// This prevents raw exception messages and stack traces from leaking to
/// the browser
///
/// Placed first in the pipeline so it wraps everything else.
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log the full exception with stack trace server-side
            _logger.LogError(ex,
                "Unhandled exception for {Method} {Path} by {User}",
                context.Request.Method,
                context.Request.Path,
                context.User?.Identity?.Name ?? "anonymous");

            // Don't leak details to the browser in production
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // If response already started, we can't change it
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("Response already started, cannot modify headers");
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        // For AJAX/API requests return JSON, for normal requests redirect
        if (context.Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            context.Response.ContentType = "application/json";
            var message = _env.IsDevelopment()
                ? $"{{\"error\": \"{ex.Message}\"}}"
                : "{\"error\": \"An unexpected error occurred.\"}";
            await context.Response.WriteAsync(message);
        }
        else
        {
            context.Response.Redirect("/Home/Error");
        }
    }
}
