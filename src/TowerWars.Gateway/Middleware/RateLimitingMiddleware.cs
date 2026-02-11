using System.Collections.Concurrent;

namespace TowerWars.Gateway.Middleware;

public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly ConcurrentDictionary<string, RateLimitInfo> _clients = new();
    private readonly int _requestsPerMinute;
    private readonly TimeSpan _window = TimeSpan.FromMinutes(1);

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _requestsPerMinute = configuration.GetValue<int>("RateLimiting:RequestsPerMinute", 100);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = GetClientIdentifier(context);
        var now = DateTime.UtcNow;

        var info = _clients.AddOrUpdate(
            clientId,
            _ => new RateLimitInfo { Count = 1, WindowStart = now },
            (_, existing) =>
            {
                if (now - existing.WindowStart > _window)
                {
                    return new RateLimitInfo { Count = 1, WindowStart = now };
                }
                existing.Count++;
                return existing;
            });

        if (info.Count > _requestsPerMinute)
        {
            _logger.LogWarning("Rate limit exceeded for client {ClientId}", clientId);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = "60";
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        context.Response.Headers["X-RateLimit-Limit"] = _requestsPerMinute.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, _requestsPerMinute - info.Count).ToString();

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        return $"ip:{context.Connection.RemoteIpAddress}";
    }

    private sealed class RateLimitInfo
    {
        public int Count { get; set; }
        public DateTime WindowStart { get; set; }
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
