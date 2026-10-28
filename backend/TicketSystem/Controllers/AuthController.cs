using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TicketSystem.Auth;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Controllers
{
    // Sign-in with an HttpOnly cookie. Every POST, PUT and DELETE in the API (login included)
    // needs the antiforgery token from GET /api/auth/csrf in an X-XSRF-TOKEN header. The token is
    // tied to who is signed in, so the client fetches a new one after logging in or out.
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserService _userService;
        private readonly IAntiforgery _antiforgery;

        public AuthController(UserService userService, IAntiforgery antiforgery)
        {
            _userService = userService;
            _antiforgery = antiforgery;
        }

        [AllowAnonymous]
        [HttpGet("csrf")]
        public ActionResult<CsrfToken> GetCsrfToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return new CsrfToken(tokens.RequestToken!);
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [EnableRateLimiting(RateLimits.Login)]
        public async Task<ActionResult<User>> Login(LoginRequest request)
        {
            var user = await _userService.Authenticate(request.Email, request.Password);
            if (user is null)
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Sign-in failed",
                    Detail = "Wrong email or password."
                });

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Session.For(user));
            return Ok(user);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        // Who is signed in; 401 when nobody is (the frontend shows the sign-in page).
        [HttpGet("me")]
        public async Task<ActionResult<User>> Me()
        {
            var id = Session.UserId(User);
            var me = await _userService.GetUserById(id, id);
            return me is null ? Unauthorized() : Ok(me);
        }
    }
}
