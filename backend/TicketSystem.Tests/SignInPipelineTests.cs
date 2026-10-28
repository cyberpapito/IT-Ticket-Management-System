using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using TicketSystem.Data;
using TicketSystem.DTOs;
using TicketSystem.Models;

namespace TicketSystem.Tests
{
    // The whole app in memory (WebApplicationFactory), with the in-memory database in place of
    // SQL Server: the cookie, the fallback policy, [Authorize(Roles)], antiforgery and the login
    // rate limit, none of which run when a controller is called directly.
    public class SignInPipelineTests : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly string _database = Guid.NewGuid().ToString();

        public SignInPipelineTests() : this(loginAttemptsPerMinute: 1000) { }

        private SignInPipelineTests(int loginAttemptsPerMinute)
        {
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Auth:LoginAttemptsPerMinute", loginAttemptsPerMinute.ToString());
                builder.ConfigureServices(services =>
                {
                    // EF Core 9 keeps the SQL Server setup in IDbContextOptionsConfiguration too.
                    foreach (var d in services.Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                                 || d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>)).ToList())
                        services.Remove(d);
                    services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_database));
                });
            });

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            var users = TestSupport.UserService(db);
            users.CreateUser("Ada Admin", "ada@example.com", UserRole.Admin, TestSupport.Password).GetAwaiter().GetResult();
            users.SetPassword(SeedUsers.JohnDoeId, TestSupport.Password).GetAwaiter().GetResult();
        }

        public void Dispose() => _factory.Dispose();

        private HttpClient NewBrowser() =>
            _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

        // What the frontend does before each POST/PUT/DELETE: fetch the antiforgery token (it also
        // sets the antiforgery cookie) and send it back in the header.
        private static async Task UseFreshCsrfToken(HttpClient browser)
        {
            var token = await browser.GetFromJsonAsync<CsrfToken>("/api/auth/csrf");
            browser.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            browser.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token!.Token);
        }

        private static async Task<HttpResponseMessage> Login(HttpClient browser, string email, string password = TestSupport.Password)
        {
            await UseFreshCsrfToken(browser);
            var response = await browser.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
            await UseFreshCsrfToken(browser);  // the token is tied to who is signed in
            return response;
        }

        private async Task<HttpClient> SignedIn(string email)
        {
            var browser = NewBrowser();
            Assert.Equal(HttpStatusCode.OK, (await Login(browser, email)).StatusCode);
            return browser;
        }

        [Fact]
        public async Task EverythingButCsrfAndLoginNeedsASession()
        {
            var browser = NewBrowser();

            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/tickets")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/users")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/api/auth/csrf")).StatusCode);
        }

        [Fact]
        public async Task SignInThenCreateATicketAsYourself()
        {
            var browser = await SignedIn("John.Doe@example.com");

            var me = await browser.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.Equal(SeedUsers.JohnDoeId, me.GetProperty("id").GetGuid());
            Assert.False(me.TryGetProperty("passwordHash", out _));

            // A createdByUserId in the body is ignored: the creator is whoever is signed in.
            var response = await browser.PostAsJsonAsync("/api/tickets", new
            {
                title = "Laptop fan loud", description = "", priority = 1, createdByUserId = SeedUsers.AdrianRodriguezId
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            // Read from the raw JSON: Ticket's private setters keep the serializer from filling it in.
            var ticket = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(SeedUsers.JohnDoeId, ticket.GetProperty("createdByUserId").GetGuid());
        }

        [Fact]
        public async Task WrongPasswordIs401WithAReason()
        {
            var response = await Login(NewBrowser(), "john.doe@example.com", "not the password");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("Wrong email or password.", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ASeededUserWithoutAPasswordCantSignIn()
        {
            var response = await Login(NewBrowser(), "adrian.rodriguez@example.com", "");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Without the header (a forged cross-site form or fetch can't read the token), even a
        // signed-in browser's POST is refused; so is a token issued before signing in.
        [Fact]
        public async Task PostsWithoutAValidCsrfTokenAreRefused()
        {
            var noToken = NewBrowser();
            var login = await noToken.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = "john.doe@example.com", Password = TestSupport.Password });
            Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);

            var browser = NewBrowser();
            await UseFreshCsrfToken(browser);
            Assert.Equal(HttpStatusCode.OK, (await browser.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = "john.doe@example.com", Password = TestSupport.Password })).StatusCode);
            // Still the anonymous token from before the login.
            var stale = await browser.PostAsJsonAsync("/api/tickets", new { title = "Stale token", description = "", priority = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);

            browser.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            var none = await browser.PostAsJsonAsync("/api/tickets", new { title = "No token", description = "", priority = 0 });
            Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        }

        [Fact]
        public async Task AdminOnlyEndpointsAre403ForEveryoneElse()
        {
            var john = await SignedIn("john.doe@example.com");
            Assert.Equal(HttpStatusCode.Forbidden, (await john.GetAsync("/api/tickets/deleted")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await john.PostAsJsonAsync("/api/users", new CreateUserRequest
            {
                Name = "Mallory", Email = "mallory@example.com", Role = UserRole.Admin, Password = TestSupport.Password
            })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await john.PutAsJsonAsync($"/api/users/{SeedUsers.JohnDoeId}/role",
                new ChangeRoleRequest { Role = UserRole.Admin })).StatusCode);

            var ada = await SignedIn("ada@example.com");
            Assert.Equal(HttpStatusCode.OK, (await ada.GetAsync("/api/tickets/deleted")).StatusCode);
        }

        // The cookie carries the security stamp it was issued with; a reset changes the stamp.
        [Fact]
        public async Task APasswordResetEndsExistingSessions()
        {
            var john = await SignedIn("john.doe@example.com");
            Assert.Equal(HttpStatusCode.OK, (await john.GetAsync("/api/auth/me")).StatusCode);

            var ada = await SignedIn("ada@example.com");
            Assert.Equal(HttpStatusCode.NoContent, (await ada.PutAsJsonAsync($"/api/users/{SeedUsers.JohnDoeId}/password",
                new SetPasswordRequest { Password = "a brand new passphrase" })).StatusCode);

            Assert.Equal(HttpStatusCode.Unauthorized, (await john.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Login(NewBrowser(), "john.doe@example.com", "a brand new passphrase")).StatusCode);
        }

        [Fact]
        public async Task SigningOutEndsTheSession()
        {
            var john = await SignedIn("john.doe@example.com");

            Assert.Equal(HttpStatusCode.NoContent, (await john.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await john.GetAsync("/api/auth/me")).StatusCode);
        }

        [Fact]
        public async Task TooManyLoginAttemptsAre429()
        {
            using var limited = new SignInPipelineTests(loginAttemptsPerMinute: 3);
            var browser = limited.NewBrowser();

            for (var i = 0; i < 3; i++)
                Assert.Equal(HttpStatusCode.Unauthorized, (await Login(browser, "john.doe@example.com", "guess " + i)).StatusCode);

            Assert.Equal(HttpStatusCode.TooManyRequests, (await Login(browser, "john.doe@example.com")).StatusCode);
        }
    }
}
