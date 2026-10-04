using Microsoft.AspNetCore.Mvc;
using TicketSystem.DTOs;
using TicketSystem.Models;
using TicketSystem.Services;

namespace TicketSystem.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserService _userService;

        public UsersController(UserService userService)
        {
            _userService = userService;
        }

        [HttpPost]
        public async Task<ActionResult<User>> CreateUser(CreateUserRequest request)
        {
            User user;
            try
            {
                user = await _userService.CreateUser(request.Name, request.Email, request.Role);
            }
            // User.Create and the duplicate-email check reject bad input; that's a 400, not a 500
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid user",
                    Detail = ex.Message
                });
            }

            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUserById(Guid id)
        {
            var user = await _userService.GetUserById(id);
            if (user is null)
                return NotFound();

            return Ok(user);
        }

        // GET /api/users?role=Technician lists who can be assigned tickets.
        [HttpGet]
        public async Task<ActionResult<List<User>>> GetUsers(UserRole? role)
        {
            return Ok(await _userService.GetUsers(role));
        }
    }
}
