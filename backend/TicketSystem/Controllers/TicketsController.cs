using Microsoft.AspNetCore.Mvc;
using TicketSystem.Models;
using TicketSystem.Services;
using TicketSystem.DTOs;

namespace TicketSystem.Controllers
{
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
                    request.CreatedByUserId);
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
            var ticket = await _ticketService.GetTicketById(id);

            if (ticket is null)
                return NotFound();

            return Ok(ticket);
        }
        [HttpGet]
        public async Task<ActionResult<List<Ticket>>> GetAllTickets()
        {
            var tickets = await _ticketService.GetAllTickets();
            return Ok(tickets);
        }

        [HttpPost("{id}/assign")]
        public Task<ActionResult<Ticket>> AssignTicket(Guid id, AssignTicketRequest request) =>
            ApplyWorkflowStep(() => _ticketService.AssignTicket(id, request.TechnicianId));

        [HttpPost("{id}/start")]
        public Task<ActionResult<Ticket>> StartWork(Guid id) =>
            ApplyWorkflowStep(() => _ticketService.StartWork(id));

        [HttpPost("{id}/resolve")]
        public Task<ActionResult<Ticket>> ResolveTicket(Guid id, ResolveTicketRequest request) =>
            ApplyWorkflowStep(() => _ticketService.ResolveTicket(id, request.ResolutionSummary));

        [HttpPost("{id}/close")]
        public Task<ActionResult<Ticket>> CloseTicket(Guid id) =>
            ApplyWorkflowStep(() => _ticketService.CloseTicket(id));

        // The entity decides whether a step is allowed; this only translates its answer to HTTP:
        // 404 for an unknown ticket, 409 for a step its current state doesn't allow (e.g. closing
        // an Open ticket), 400 for bad input (e.g. a blank resolution summary).
        private async Task<ActionResult<Ticket>> ApplyWorkflowStep(Func<Task<Ticket?>> step)
        {
            try
            {
                var ticket = await step();
                return ticket is null ? NotFound() : Ok(ticket);
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
            var deletedTicket = await _ticketService.SoftDeleteTicket(id);
            if (deletedTicket is null)
                return NotFound();

            return NoContent();
        }
    }
}