namespace TicketSystem.Models
{
    public class Ticket
    {
        public Guid Id { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public TicketPriority Priority { get; private set; }
        public TicketStatus Status { get; private set; }
        public Guid? AssignedToUserId { get; private set; }
        public Guid CreatedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? ResolvedAt { get; private set; }
        public string? ResolutionSummary { get; private set; }

        // Factory method - only way to create a new ticket
        public static Ticket Create(
            string title,
            string description,
            TicketPriority priority,
            Guid createdByUserId)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty");

            return new Ticket
            {
                Id = Guid.NewGuid(),
                Title = title.Trim(),
                Description = description?.Trim() ?? string.Empty,
                Priority = priority,
                Status = TicketStatus.Open,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
        }

        // Business rule: can only assign open tickets
        public void AssignTo(Guid technicianId)
        {
            if (Status != TicketStatus.Open)
                throw new InvalidOperationException(
                    $"Cannot assign a {Status} ticket. Only Open tickets can be assigned.");

            AssignedToUserId = technicianId;
        }

        // Business rule: can only resolve non-closed tickets
        public void MarkResolved()
        {
            if (Status == TicketStatus.Closed)
                throw new InvalidOperationException(
                    "Cannot resolve a ticket that is already closed.");

            Status = TicketStatus.Resolved;
            ResolvedAt = DateTimeOffset.UtcNow;
        }
// Business rule: only resolved tickets can be closed
public void Close()
        {
    if (Status != TicketStatus.Resolved)
        throw new InvalidOperationException(
            $"Cannot close a {Status} ticket. Tickets must be resolved before closing.");

    Status = TicketStatus.Closed;
        }



    // Business rules: only assigned, non-closed tickets can be resolved,
// and a resolution summary is required for the audit record
public void MarkResolved(string resolutionSummary)
{
    if (Status == TicketStatus.Closed)
        throw new InvalidOperationException("Cannot resolve a ticket that is already closed.");

    if (AssignedToUserId is null)
        throw new InvalidOperationException(
            "Cannot resolve an unassigned ticket. Assign a technician before resolving.");

    if (string.IsNullOrWhiteSpace(resolutionSummary))
        throw new ArgumentException("A resolution summary is required to resolve a ticket.");

    Status = TicketStatus.Resolved;
    ResolutionSummary = resolutionSummary.Trim();
    ResolvedAt = DateTimeOffset.UtcNow;
}


    }
}