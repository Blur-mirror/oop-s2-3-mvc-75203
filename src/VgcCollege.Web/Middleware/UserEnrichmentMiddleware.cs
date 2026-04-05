namespace VgcCollege.Web.Middleware;

/// <summary>
/// Enriches every request with the current user's identity for structured logging.
/// Placed after UseAuthentication() so User.Identity is already populated.
/// Also stores the user's role in HttpContext.Items so views/controllers
/// can access it without an extra DB call.
/// </summary>
public class UserEnrichmentMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserEnrichmentMiddleware> _logger;

    public UserEnrichmentMiddleware(RequestDelegate next,
        ILogger<UserEnrichmentMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userName = context.User?.Identity?.Name ?? "anonymous";
        var isAuth = context.User?.Identity?.IsAuthenticated ?? false;

        // Determine role for logging (reads the claims already on the principal
        // — no DB hit required)
        var role = "none";
        if (isAuth)
        {
            if (context.User!.IsInRole("Administrator")) role = "Administrator";
            else if (context.User.IsInRole("Faculty")) role = "Faculty";
            else if (context.User.IsInRole("Student")) role = "Student";
        }

        // Store on Items so any controller/view can read it without DI
        context.Items["UserRole"] = role;

        // Structured log: every request records who made it and their role
        _logger.LogInformation(
            "Request {Method} {Path} by {UserName} [{Role}]",
            context.Request.Method,
            context.Request.Path,
            userName,
            role);

        await _next(context);

        // Log the response status too — useful for spotting 403s (auth failures)
        _logger.LogInformation(
            "Response {StatusCode} for {Method} {Path} by {UserName}",
            context.Response.StatusCode,
            context.Request.Method,
            context.Request.Path,
            userName);
    }
}
