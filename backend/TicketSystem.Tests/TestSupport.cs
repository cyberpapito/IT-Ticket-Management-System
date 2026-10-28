using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Tests
{
    public static class TestSupport
    {
        public const string Password = "correct horse battery";

        // A controller called directly, as if this user's session cookie came with the request.
        // Only the id matters: the services look the user up and apply their role.
        public static T SignedInAs<T>(this T controller, Guid userId) where T : ControllerBase
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "Test"))
                }
            };
            return controller;
        }

        public static UserService UserService(AppDbContext context) => new(context, new PasswordHasher<User>());

        // The seeded users are a technician (Adrian Rodriguez) and plain users; deleting and the
        // deleted list need an admin.
        public static async Task<Guid> AddUser(
            DbContextOptions<AppDbContext> options, UserRole role, string name = "Ada Admin", string? email = null)
        {
            using var context = new AppDbContext(options);
            var user = await UserService(context).CreateUser(
                name, email ?? $"{name.Replace(' ', '.').ToLowerInvariant()}@example.com", role, Password);
            return user.Id;
        }
    }
}
