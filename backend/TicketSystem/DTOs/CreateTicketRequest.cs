using TicketSystem.Models;

namespace TicketSystem.DTOs
{
    public class CreateTicketRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public TicketPriority Priority { get; set; }

        public Guid CreatedByUserId { get; set; }
    }
}