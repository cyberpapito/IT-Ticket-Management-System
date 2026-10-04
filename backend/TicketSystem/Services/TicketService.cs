using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;


namespace TicketSystem.Services
{
    public class TicketService
    {
        private readonly AppDbContext _dbContext;

        public TicketService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Ticket> CreateTicket(

    string title,
    string description,
    TicketPriority priority,
    Guid createdByUserId)

        {



            var ticket = Ticket.Create(title, description, priority, createdByUserId);

            // Checked after the ticket's own rules, so a blank title is reported before an unknown user.
            if (!await _dbContext.Users.AnyAsync(u => u.Id == createdByUserId))
                throw new ArgumentException($"No user with id {createdByUserId}.");

            _dbContext.Tickets.Add(ticket);

            await _dbContext.SaveChangesAsync();

            return ticket;
        }
        public async Task<Ticket?> GetTicketById(Guid ticketId)
        {
            return await _dbContext.Tickets.FindAsync(ticketId);
        }

        public async Task<List<Ticket>> GetAllTickets()
        {
            return await _dbContext.Tickets.ToListAsync();
        }

        // Workflow steps. Each returns null for an unknown (or deleted) ticket; the entity throws
        // InvalidOperationException when the step isn't allowed in the ticket's current state and
        // ArgumentException for bad input, and nothing is saved in either case.
        // An unknown ticket is null (404); an unknown or non-technician user is bad input (400).
        public async Task<Ticket?> AssignTicket(Guid ticketId, Guid technicianId)
        {
            var ticket = await _dbContext.Tickets.FindAsync(ticketId);
            if (ticket is null)
                return null;

            var technician = await _dbContext.Users.FindAsync(technicianId)
                ?? throw new ArgumentException($"No user with id {technicianId}.");

            ticket.AssignTo(technician);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        public Task<Ticket?> StartWork(Guid ticketId) =>
            ApplyToTicket(ticketId, ticket => ticket.StartWork());

        public Task<Ticket?> ResolveTicket(Guid ticketId, string resolutionSummary) =>
            ApplyToTicket(ticketId, ticket => ticket.MarkResolved(resolutionSummary));

        public Task<Ticket?> CloseTicket(Guid ticketId) =>
            ApplyToTicket(ticketId, ticket => ticket.Close());

        private async Task<Ticket?> ApplyToTicket(Guid ticketId, Action<Ticket> step)
        {
            var ticket = await _dbContext.Tickets.FindAsync(ticketId);
            if (ticket is null)
                return null;

            step(ticket);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        // Returns null for unknown and already-deleted tickets alike: the query filter
        // hides deleted ones, so a second DELETE is a 404 rather than a domain error.
        public async Task<Ticket?> SoftDeleteTicket(Guid ticketId)
        {
            var ticket = await _dbContext.Tickets.FindAsync(ticketId);
            if (ticket is null)
             return null;

            ticket.MarkDeleted();
            await _dbContext.SaveChangesAsync();
            return ticket;
        }


    }
}