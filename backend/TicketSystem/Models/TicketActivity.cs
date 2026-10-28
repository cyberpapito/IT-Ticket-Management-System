namespace TicketSystem.Models
{
    // One entry in a ticket's history. Only Ticket creates these (each workflow step and each
    // comment adds one), so the history can't drift from what actually happened to the ticket.
    public class TicketActivity
    {
        public const int NoteMaxLength = 2000;

        public Guid Id { get; private set; }
        public Guid TicketId { get; private set; }
        public TicketActivityType Type { get; private set; }
        public DateTimeOffset At { get; private set; }

        // Who did it: the signed-in user behind the step. Null only on entries from before sign-in.
        public Guid? ActorUserId { get; private set; }

        // The person the entry names: the reporter on Created, the technician on Assigned, the
        // author on Comment; null on the other steps (ActorUserId says who did those).
        public Guid? UserId { get; private set; }

        // The comment text, or the resolution summary on Resolved.
        public string? Note { get; private set; }

        // Set when the entry changed the ticket's status.
        public TicketStatus? FromStatus { get; private set; }
        public TicketStatus? ToStatus { get; private set; }

        internal static TicketActivity For(
            Ticket ticket,
            TicketActivityType type,
            Guid actorId,
            Guid? userId = null,
            string? note = null,
            TicketStatus? fromStatus = null,
            TicketStatus? toStatus = null) =>
            new()
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                Type = type,
                At = DateTimeOffset.UtcNow,
                ActorUserId = actorId,
                UserId = userId,
                Note = note,
                FromStatus = fromStatus,
                ToStatus = toStatus
            };
    }
}
