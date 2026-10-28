using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketSystem.Auth;
using TicketSystem.Models;
using TicketSystem.Services;
using TicketSystem.DTOs;

namespace TicketSystem.Controllers
{
    // Every endpoint needs a signed-in user (the fallback policy in Program.cs); who they are comes
    // from the session. TicketService applies TicketPermissions: tickets a user may not see are
    // 404s, steps they may not take are 403s.
    [ApiController]
    [Route("api/tickets")]
    public class TicketsController : ControllerBase
    {
        private readonly TicketService _ticketService;

        public TicketsController(TicketService ticketService)
        {
            _ticketService = ticketService;
        }

        [HttpPost]
        public async Task<ActionResult<Ticket>> CreateTicket(CreateTicketRequest request)
        {
            Ticket ticket;
            try
            {
                ticket = await _ticketService.CreateTicket(
                    request.Title,
                    request.Description,
                    request.Priority,
                    Session.UserId(User));
            }
            // Ticket.Create rejects invalid input (e.g. a blank title); that's the caller's
            // mistake, so it's a 400, not an unhandled 500
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid ticket",
                    Detail = ex.Message
                });
            }

            return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, ticket);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Ticket>> GetTicketById(Guid id)
        {
            var ticket = await _ticketService.GetTicketById(id, Session.UserId(User));

            if (ticket is null)
                return NotFound();

            return Ok(ticket);
        }
        [HttpGet]
        public async Task<ActionResult<List<Ticket>>> GetAllTickets()
        {
            var tickets = await _ticketService.GetAllTickets(Session.UserId(User));
            return Ok(tickets);
        }

        // Soft-deleted tickets, newest deletion first. The literal segment outranks "{id}" in
        // routing, so this never reaches GetTicketById. Admin only.
        [Authorize(Roles = nameof(UserRole.Admin))]
        [HttpGet("deleted")]
        public async Task<ActionResult<List<Ticket>>> GetDeletedTickets()
        {
            return Ok(await _ticketService.GetDeletedTickets());
        }

        // The ticket's history, oldest first: each workflow step and comment.
        [HttpGet("{id}/activity")]
        public async Task<ActionResult<List<TicketActivity>>> GetActivity(Guid id)
        {
            var activity = await _ticketService.GetActivity(id, Session.UserId(User));
            return activity is null ? NotFound() : Ok(activity);
        }

        [HttpPost("{id}/comments")]
        public async Task<ActionResult<TicketActivity>> AddComment(Guid id, AddCommentRequest request)
        {
            TicketActivity? comment;
            try
            {
                comment = await _ticketService.AddComment(id, Session.UserId(User), request.Text);
            }
            catch (NotAllowedException ex)
            {
                return NotAllowed(ex);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid comment",
                    Detail = ex.Message
                });
            }

            if (comment is null)
                return NotFound();

            return CreatedAtAction(nameof(GetActivity), new { id }, comment);
        }

        [HttpPost("{id}/assign")]
        public Task<ActionResult<Ticket>> AssignTicket(Guid id, AssignTicketRequest request) =>
            ApplyWorkflowStep(() => _ticketService.AssignTicket(id, request.TechnicianId, Session.UserId(User)));

        [HttpPost("{id}/start")]
        public Task<ActionResult<Ticket>> StartWork(Guid id) =>
            ApplyWorkflowStep(() => _ticketService.StartWork(id, Session.UserId(User)));

        [HttpPost("{id}/resolve")]
        public Task<ActionResult<Ticket>> ResolveTicket(Guid id, ResolveTicketRequest request) =>
            ApplyWorkflowStep(() => _ticketService.ResolveTicket(id, request.ResolutionSummary, Session.UserId(User)));

        [HttpPost("{id}/close")]
        public Task<ActionResult<Ticket>> CloseTicket(Guid id) =>
            ApplyWorkflowStep(() => _ticketService.CloseTicket(id, Session.UserId(User)));

        // The entity decides whether a step is allowed; this only translates its answer to HTTP:
        // 404 for an unknown (or invisible) ticket, 403 for a step this user may not take, 409 for
        // a step its current state doesn't allow (e.g. closing an Open ticket), 400 for bad input
        // (e.g. a blank resolution summary).
        private async Task<ActionResult<Ticket>> ApplyWorkflowStep(Func<Task<Ticket?>> step)
        {
            try
            {
                var ticket = await step();
                return ticket is null ? NotFound() : Ok(ticket);
            }
            catch (NotAllowedException ex)
            {
                return NotAllowed(ex);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Not allowed in the ticket's current state",
                    Detail = ex.Message
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid request",
                    Detail = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTicket(Guid id)
        {
            Ticket? deletedTicket;
            try
            {
                deletedTicket = await _ticketService.SoftDeleteTicket(id, Session.UserId(User));
            }
            catch (NotAllowedException ex)
            {
                return NotAllowed(ex);
            }

            if (deletedTicket is null)
                return NotFound();

            return NoContent();
        }

        private ObjectResult NotAllowed(NotAllowedException ex) =>
            StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Not allowed",
                Detail = ex.Message
            });
    }
}