namespace VgcCollege.Web.Middleware;


/// Measures the time taken to process each HTTP request and logs a warning
/// for slow requests. Demonstrates the bidirectional nature of middleware —
/// we start timing on the way in, and log on the way out.
public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    // Requests slower than this threshold get a warning log
    private const int SlowRequestThresholdMs = 500;

    public RequestTimingMiddleware(RequestDelegate next,
        ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            var path = context.Request.Path;
            var method = context.Request.Method;
            var status = context.Response.StatusCode;

            if (elapsed > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW REQUEST: {Method} {Path} took {ElapsedMs}ms (status {StatusCode})",
                    method, path, elapsed, status);
            }
            else
            {
                _logger.LogDebug(
                    "Request {Method} {Path} completed in {ElapsedMs}ms (status {StatusCode})",
                    method, path, elapsed, status);
            }
        }
    }
}
