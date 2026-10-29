using TicketSystem.Models;

namespace TicketSystem.Services
{
    // Who may do what to a ticket. The entity decides whether a step is allowed in the ticket's
    // current state; these decide whether this person may take it at all. TicketService checks
    // them before every step, and the frontend mirrors them (api.ts, availableSteps) to hide
    // buttons that would only fail.
    //
    // A user who may not see a ticket gets a 404 for it, not a 403, so ticket ids don't leak.
    public static class TicketPermissions
    {
        // Staff see every ticket; a plain user sees the tickets they reported.
        public static bool CanView(User user, Ticket ticket) =>
            user.IsStaff || ticket.CreatedByUserId == user.Id;

        public static bool CanComment(User user, Ticket ticket) => CanView(user, ticket);

        // Any technician or admin can hand a ticket to any technician, themselves included.
        public static bool CanAssign(User user, Ticket ticket) => user.IsStaff;

        // Start and resolve: the assigned technician, or an admin.
        public static bool CanWork(User user, Ticket ticket) =>
            user.Role == UserRole.Admin
            || (user.Role == UserRole.Technician && ticket.AssignedToUserId == user.Id);

        // Close: the reporter confirming the fix, the assigned technician, or an admin.
        public static bool CanClose(User user, Ticket ticket) =>
            user.Role == UserRole.Admin
            || ticket.CreatedByUserId == user.Id
            || ticket.AssignedToUserId == user.Id;

        public static bool CanDelete(User user, Ticket ticket) => user.Role == UserRole.Admin;
    }

    // The caller is signed in and may see the ticket, but this step isn't theirs to take (403).
    public class NotAllowedException(string message) : Exception(message);
}
