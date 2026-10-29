using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using TicketSystem.Data;
using TicketSystem.Models;

namespace TicketSystem.Auth
{
    // The sign-in cookie: who the user is, their role, and the security stamp it was issued with.
    public static class Session
    {
        public const string SecurityStampClaim = "ticketsystem:stamp";

        public static ClaimsPrincipal For(User user) =>
            new(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Name),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim(SecurityStampClaim, user.SecurityStamp),
                ],
                CookieAuthenticationDefaults.AuthenticationScheme));

        // Only called behind [Authorize] (the fallback policy), so the claim is always there.
        public static Guid UserId(ClaimsPrincipal principal) =>
            Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // Runs on every request that carries the cookie. A cookie for a user who has since been
        // removed, lost their password, or had their password or role changed (new stamp) is
        // rejected, so the role claim [Authorize(Roles)] reads is never stale.
        public static async Task Validate(CookieValidatePrincipalContext context)
        {
            var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var stamp = context.Principal?.FindFirstValue(SecurityStampClaim);
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = Guid.TryParse(id, out var userId) ? await db.Users.FindAsync(userId) : null;

            if (user?.PasswordHash is null || user.SecurityStamp != stamp)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        // An API answers 401 / 403; the cookie handler's default is a redirect to a login page.
        public static Task Unauthorized(RedirectContext<CookieAuthenticationOptions> context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        public static Task Forbidden(RedirectContext<CookieAuthenticationOptions> context)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
