using TicketSystem.Data;      // adjust to wherever your AppDbContext lives
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
            // 1. Create the ticket using your factory method

            var ticket = Ticket.Create(title, description, priority, createdByUserId);

            // 2. Stage it in the DbContext

            _dbContext.Tickets.Add(ticket);
        
            // 3. Save changes to the database (remember: await)
    
            await _dbContext.SaveChangesAsync();

            // 4. Return the created ticket
            return ticket;

        }   

    }
}