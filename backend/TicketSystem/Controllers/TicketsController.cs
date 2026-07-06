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
            var ticket = await _ticketService.CreateTicket(
                request.Title,
                request.Description,
                request.Priority,
                request.CreatedByUserId);

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

    }
}