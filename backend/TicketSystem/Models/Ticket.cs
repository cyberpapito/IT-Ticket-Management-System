using System.Text.Json.Serialization;

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

        // The ticket's history, oldest first. Every step below adds to it. Left out of the ticket's
        // JSON (it isn't loaded with the ticket); GET /api/tickets/{id}/activity serves it.
        private readonly List<TicketActivity> _activities = [];

        [JsonIgnore]
        public IReadOnlyList<TicketActivity> Activities => _activities;

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

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = description,
                Priority = priority,
                Status = TicketStatus.Open,
                CreatedByUserId = createdByUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            ticket.Record(TicketActivityType.Created, userId: createdByUserId, toStatus: TicketStatus.Open);
            return ticket;
        }

        // Business rules: only technicians can be assigned, and only to open tickets
        public void AssignTo(User technician)
        {
            if (technician.Role != UserRole.Technician)
                throw new ArgumentException($"{technician.Name} is not a technician. Only technicians can be assigned.");

            if (Status != TicketStatus.Open)
                throw new InvalidOperationException(
                    $"Cannot assign a ticket that is {Status}. Only Open tickets can be assigned.");

            AssignedToUserId = technician.Id;
            Record(TicketActivityType.Assigned, userId: technician.Id);
        }


         // Business rule: work can only start on an assigned, open ticket

        public void StartWork()
{
    if (Status != TicketStatus.Open)
        throw new InvalidOperationException(
            $"Cannot start work on a ticket that is {Status}. Only Open tickets can move to In Progress.");

    if (AssignedToUserId is null)
        throw new InvalidOperationException(
            "Cannot start work on an unassigned ticket. Assign a technician first.");

    Status = TicketStatus.InProgress;
    Record(TicketActivityType.Started, fromStatus: TicketStatus.Open, toStatus: TicketStatus.InProgress);
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

    var fromStatus = Status;
    Status = TicketStatus.Resolved;
    ResolutionSummary = resolutionSummary.Trim();
    ResolvedAt = DateTimeOffset.UtcNow;
    Record(TicketActivityType.Resolved, note: ResolutionSummary, fromStatus: fromStatus, toStatus: TicketStatus.Resolved);
}

        // Business rule: Can't delete a ticket that is already deleted
public void MarkDeleted()
{
    if (DeletedAt is not null)
        throw new InvalidOperationException("Ticket is already deleted.");

    DeletedAt = DateTimeOffset.UtcNow;
    Record(TicketActivityType.Deleted);
}

        // Business rule: only resolved tickets can be closed
public void Close()
        {
    if (Status != TicketStatus.Resolved)
        throw new InvalidOperationException(
            $"Cannot close a ticket that is {Status}. Tickets must be resolved before closing.");

    Status = TicketStatus.Closed;
    Record(TicketActivityType.Closed, fromStatus: TicketStatus.Resolved, toStatus: TicketStatus.Closed);
        }

        // Business rules: a comment needs an author and some text, and a deleted ticket takes no
        // more comments. Any user can comment, in any status, so a requester can still reply
        // after their ticket is closed.
        public TicketActivity AddComment(User author, string text)
        {
            if (DeletedAt is not null)
                throw new InvalidOperationException("Cannot comment on a deleted ticket.");

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("A comment cannot be empty.");

            text = text.Trim();
            if (text.Length > TicketActivity.NoteMaxLength)
                throw new ArgumentException($"A comment cannot be longer than {TicketActivity.NoteMaxLength} characters.");

            return Record(TicketActivityType.Comment, userId: author.Id, note: text);
        }

        private TicketActivity Record(
            TicketActivityType type,
            Guid? userId = null,
            string? note = null,
            TicketStatus? fromStatus = null,
            TicketStatus? toStatus = null)
        {
            var activity = TicketActivity.For(this, type, userId, note, fromStatus, toStatus);
            _activities.Add(activity);
            return activity;
        }

    }
}