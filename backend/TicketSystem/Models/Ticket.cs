namespace TicketSystem.Models
{
    public class Ticket
    {
        // Column sizes in the database (AppDbContext uses these), enforced here so an oversized
        // value is rejected as invalid input instead of failing on save.
        public const int TitleMaxLength = 255;
        public const int DescriptionMaxLength = 2000;

        public Guid Id { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public TicketPriority Priority { get; private set; }
        public TicketStatus Status { get; private set; }
        public Guid? AssignedToUserId { get; private set; }
        public Guid CreatedByUserId { get; private set; }
        public DateTimeOffset CreatedAt { get; private set; }
        public DateTimeOffset? ResolvedAt { get; private set; }
        public DateTimeOffset? DeletedAt { get; private set; }  
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

            title = title.Trim();
            description = description?.Trim() ?? string.Empty;

            if (title.Length > TitleMaxLength)
                throw new ArgumentException($"Title cannot be longer than {TitleMaxLength} characters");

            if (description.Length > DescriptionMaxLength)
                throw new ArgumentException($"Description cannot be longer than {DescriptionMaxLength} characters");

            return new Ticket
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
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


         // Business rule: work can only start on an assigned, open ticket

        public void StartWork()
{
    if (Status != TicketStatus.Open)
        throw new InvalidOperationException(
            $"Cannot start work on a {Status} ticket. Only Open tickets can move to In Progress.");

    if (AssignedToUserId is null)
        throw new InvalidOperationException(
            "Cannot start work on an unassigned ticket. Assign a technician first.");

    Status = TicketStatus.InProgress;
}

        // Business rules: only assigned, unresolved tickets can be resolved,
        // and a resolution summary is required for the audit record.
        // Resolving twice would overwrite the original summary and ResolvedAt.
public void MarkResolved(string resolutionSummary)
{
    if (Status is TicketStatus.Resolved or TicketStatus.Closed)
        throw new InvalidOperationException($"Cannot resolve a ticket that is already {Status}.");

    if (AssignedToUserId is null)
        throw new InvalidOperationException(
            "Cannot resolve an unassigned ticket. Assign a technician before resolving.");

    if (string.IsNullOrWhiteSpace(resolutionSummary))
        throw new ArgumentException("A resolution summary is required to resolve a ticket.");

    Status = TicketStatus.Resolved;
    ResolutionSummary = resolutionSummary.Trim();
    ResolvedAt = DateTimeOffset.UtcNow;
}

        // Business rule: Can't delete a ticket that is already deleted
public void MarkDeleted()
{
    if (DeletedAt is not null)
        throw new InvalidOperationException("Ticket is already deleted.");

    DeletedAt = DateTimeOffset.UtcNow;
}

        // Business rule: only resolved tickets can be closed
public void Close()
        {
    if (Status != TicketStatus.Resolved)
        throw new InvalidOperationException(
            $"Cannot close a {Status} ticket. Tickets must be resolved before closing.");

    Status = TicketStatus.Closed;
        }

    }
}