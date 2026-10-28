using Microsoft.EntityFrameworkCore;
using TicketSystem.Data;
using TicketSystem.Models;


namespace TicketSystem.Services
{
    // Every method takes the signed-in user's id (actorId) from the session, never from the
    // request body. Tickets the actor may not see are treated as missing (null → 404); a step the
    // actor may see but not take throws NotAllowedException (403). See TicketPermissions.
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
            Guid actorId)
        {
            var actor = await Actor(actorId);
            var ticket = Ticket.Create(title, description, priority, actor.Id);

            _dbContext.Tickets.Add(ticket);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        public async Task<Ticket?> GetTicketById(Guid ticketId, Guid actorId)
        {
            var actor = await Actor(actorId);
            return await Visible(ticketId, actor);
        }

        // Staff get every ticket; a plain user gets the ones they reported.
        public async Task<List<Ticket>> GetAllTickets(Guid actorId)
        {
            var actor = await Actor(actorId);
            var tickets = _dbContext.Tickets.AsQueryable();
            if (!actor.IsStaff)
                tickets = tickets.Where(t => t.CreatedByUserId == actor.Id);
            return await tickets.ToListAsync();
        }

        // The audit history: the only query that steps past the soft-delete filter. Most recently
        // deleted first. Read-only; there is no undelete. Admin only (the controller's [Authorize]).
        public async Task<List<Ticket>> GetDeletedTickets()
        {
            return await _dbContext.Tickets
                .IgnoreQueryFilters()
                .Where(t => t.DeletedAt != null)
                .OrderByDescending(t => t.DeletedAt)
                .ToListAsync();
        }

        // Workflow steps. Each returns null for an unknown, deleted or invisible ticket; the entity
        // throws InvalidOperationException when the step isn't allowed in the ticket's current
        // state and ArgumentException for bad input, and nothing is saved in either case.
        // An unknown ticket is null (404); an unknown or non-technician user is bad input (400).
        public async Task<Ticket?> AssignTicket(Guid ticketId, Guid technicianId, Guid actorId)
        {
            var actor = await Actor(actorId);
            var ticket = await Visible(ticketId, actor);
            if (ticket is null)
                return null;

            Require(TicketPermissions.CanAssign(actor, ticket), "Only technicians and admins can assign tickets.");

            var technician = await _dbContext.Users.FindAsync(technicianId)
                ?? throw new ArgumentException($"No user with id {technicianId}.");

            ticket.AssignTo(technician, actor.Id);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        public Task<Ticket?> StartWork(Guid ticketId, Guid actorId) =>
            ApplyToTicket(ticketId, actorId, TicketPermissions.CanWork,
                "Only the assigned technician or an admin can start work.",
                (ticket, actor) => ticket.StartWork(actor.Id));

        public Task<Ticket?> ResolveTicket(Guid ticketId, string resolutionSummary, Guid actorId) =>
            ApplyToTicket(ticketId, actorId, TicketPermissions.CanWork,
                "Only the assigned technician or an admin can resolve a ticket.",
                (ticket, actor) => ticket.MarkResolved(resolutionSummary, actor.Id));

        public Task<Ticket?> CloseTicket(Guid ticketId, Guid actorId) =>
            ApplyToTicket(ticketId, actorId, TicketPermissions.CanClose,
                "Only the reporter, the assigned technician or an admin can close a ticket.",
                (ticket, actor) => ticket.Close(actor.Id));

        // Returns null for unknown and already-deleted tickets alike: the query filter
        // hides deleted ones, so a second DELETE is a 404 rather than a domain error.
        public Task<Ticket?> SoftDeleteTicket(Guid ticketId, Guid actorId) =>
            ApplyToTicket(ticketId, actorId, TicketPermissions.CanDelete,
                "Only an admin can delete a ticket.",
                (ticket, actor) => ticket.MarkDeleted(actor.Id));

        private async Task<Ticket?> ApplyToTicket(
            Guid ticketId,
            Guid actorId,
            Func<User, Ticket, bool> allowed,
            string notAllowed,
            Action<Ticket, User> step)
        {
            var actor = await Actor(actorId);
            var ticket = await Visible(ticketId, actor);
            if (ticket is null)
                return null;

            Require(allowed(actor, ticket), notAllowed);
            step(ticket, actor);
            await _dbContext.SaveChangesAsync();
            return ticket;
        }

        // The ticket's history, oldest first. Null for an unknown, deleted or invisible ticket.
        public async Task<List<TicketActivity>?> GetActivity(Guid ticketId, Guid actorId)
        {
            var actor = await Actor(actorId);
            if (await Visible(ticketId, actor) is null)
                return null;

            return await _dbContext.TicketActivities
                .Where(a => a.TicketId == ticketId)
                .OrderBy(a => a.At)
                .ToListAsync();
        }

        // Null for an unknown, deleted or invisible ticket; the entity rejects an empty or
        // oversized comment. The author is the signed-in user.
        public async Task<TicketActivity?> AddComment(Guid ticketId, Guid actorId, string text)
        {
            var actor = await Actor(actorId);
            var ticket = await Visible(ticketId, actor);
            if (ticket is null)
                return null;

            Require(TicketPermissions.CanComment(actor, ticket), "You can't comment on this ticket.");

            var comment = ticket.AddComment(actor, text);
            await _dbContext.SaveChangesAsync();
            return comment;
        }

        // The session names a user that no longer exists only if the database was reset under it.
        private async Task<User> Actor(Guid actorId) =>
            await _dbContext.Users.FindAsync(actorId)
            ?? throw new NotAllowedException("Your account no longer exists. Sign in again.");

        private async Task<Ticket?> Visible(Guid ticketId, User actor)
        {
            var ticket = await _dbContext.Tickets.FindAsync(ticketId);
            return ticket is not null && TicketPermissions.CanView(actor, ticket) ? ticket : null;
        }

        private static void Require(bool allowed, string message)
        {
            if (!allowed)
                throw new NotAllowedException(message);
        }
    }
}
