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