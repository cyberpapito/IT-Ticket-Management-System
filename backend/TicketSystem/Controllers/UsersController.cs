using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Auth;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Controllers
{
    // Reading users needs any sign-in (a plain user sees themselves and the staff); creating them,
    // changing roles and resetting passwords is Admin only.
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPost]
        public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
        {
            User user;
            try
            {
                user = await _userService.CreateUser(request.Name, request.Email, request.Role, request.Password);
            }
            // User.Create, the password rule and the duplicate-email check reject bad input; that's a 400, not a 500
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }

            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(Guid id)
        {
            var user = await _userService.GetUserById(id, Session.UserId(User));
            if (user is null)
                return NotFound();

            return Ok(user);
        }

        // GET /api/users?role=1 lists who can be assigned tickets.
        [HttpGet]
        public async Task<ActionResult<List<User>>> GetUsers(UserRole? role)
        {
            return Ok(await _userService.GetUsers(role, Session.UserId(User)));
        }

        // 409 when it would leave no admin who can sign in.
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("{id}/role")]
        public async Task<ActionResult<User>> ChangeRole(Guid id, ChangeRoleRequest request)
        {
            try
            {
                var user = await _userService.ChangeRole(id, request.Role);
                return user is null ? NotFound() : Ok(user);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Role not changed",
                    Detail = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        }

        // An admin's password reset; the user's existing sessions end.
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpPut("{id}/password")]
        public async Task<IActionResult> SetPassword(Guid id, SetPasswordRequest request)
        {
            try
            {
                var user = await _userService.SetPassword(id, request.Password);
                return user is null ? NotFound() : NoContent();
            }
            catch (ArgumentException ex)
            {
                return Invalid(ex);
            }
        }

        private BadRequestObjectResult Invalid(ArgumentException ex) =>
            BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid user",
                Detail = ex.Message
            });
    }
}
