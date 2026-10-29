using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TicketSystem.Auth;
using TicketSystem.Data;
using TicketSystem.Models;
using TicketSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Every endpoint needs a signed-in user unless it says [AllowAnonymous] (only csrf and login
// do). POST/PUT/DELETE also need the antiforgery token: the session is a cookie, which the
// browser would send along with a forged cross-site request.
builder.Services.AddControllers(options => options.Filters.Add<CsrfFilter>());
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "tickets.csrf";
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "tickets.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        // Plain http only in development (dotnet run --launch-profile http); HTTPS everywhere else.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = Session.Validate;
        options.Events.OnRedirectToLogin = Session.Unauthorized;
        options.Events.OnRedirectToAccessDenied = Session.Forbidden;
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(options =>
    options.AddLoginLimit(builder.Configuration.GetValue("Auth:LoginAttemptsPerMinute", 5)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<TicketService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
// Register AppDbContext with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// The first admin (Auth:BootstrapAdmin:Email / :Name / :Password, from user secrets or the
// environment). Skipped when not configured, and a no-op once any admin can sign in.
var bootstrap = app.Configuration.GetSection("Auth:BootstrapAdmin");
if (!string.IsNullOrWhiteSpace(bootstrap["Email"]) && !string.IsNullOrEmpty(bootstrap["Password"]))
{
    using var scope = app.Services.CreateScope();
    var done = await scope.ServiceProvider.GetRequiredService<UserService>()
        .EnsureBootstrapAdmin(bootstrap["Email"]!, bootstrap["Name"] ?? "Administrator", bootstrap["Password"]!);
    if (done is not null)
        app.Logger.LogInformation("Bootstrap admin: {Result}", done);
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

// For WebApplicationFactory<Program> in the tests.
public partial class Program;
