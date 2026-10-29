using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TicketSystem.Auth
{
    public static class RateLimits
    {
        public const string Login = "login";

        // Sign-in attempts per client IP per minute (Auth:LoginAttemptsPerMinute, default 5). A
        // refusal is a 429; the window resets on its own.
        public static void AddLoginLimit(this RateLimiterOptions options, int permitsPerMinute)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Login, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        }
    }
}
