using TicketSystem.Models;

namespace TicketSystem.DTOs
{
    public class CreateTicketRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; }

        // TODO: derive from auth token once JWT is implemented —
        // clients should not claim identities
        public Guid CreatedByUserId { get; set; }
    }
}