# Architecture

This document explains how the IT Ticket Management System is structured and
why it's built this way. It's written to match the actual code as it grows, so
it stays short and concrete rather than describing patterns the project doesn't use.

---

## Overview

The system has three parts:

```
React frontend  →  ASP.NET Core API  →  SQL Server
  (browser)          (the server)        (the database)
```

- The **frontend** (React + TypeScript) runs in the browser. It shows tickets
  and sends requests when the user creates or updates one.
- The **backend** (ASP.NET Core, C#) receives those requests, applies the
  business rules, and reads or writes the database.
- The **database** (SQL Server) stores tickets and notes.

The frontend and backend talk over HTTP using JSON. They're separate programs
and could be developed independently.

---

## Backend structure

The backend is a **single ASP.NET Core project**, organized by responsibility:

```
backend/
├── Controllers/   HTTP endpoints. Receive requests, return responses.
├── Services/      Business logic. The actual work happens here.
├── Models/        Entities (Ticket, TicketNote) and enums.
├── DTOs/          Request and response shapes for the API.
├── Data/          AppDbContext, EF Core migrations, seed data.
└── Program.cs     Startup: registers services, configures the pipeline.
```

### The request flow: Controller → Service → DbContext

Every request follows the same path:

```
1. Controller receives the HTTP request
       ↓
2. Service applies business rules
       ↓
3. DbContext (EF Core) reads/writes SQL Server
       ↓
4. Result flows back up and returns as JSON
```

A concrete example — creating a ticket:

```csharp
// 1. Controller: handles HTTP, delegates to the service
[ApiController]
[Route("api/tickets")]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public TicketsController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTicketDto dto)
    {
        var created = await _ticketService.CreateTicketAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}

// 2. Service: holds the business logic
public class TicketService : ITicketService
{
    private readonly AppDbContext _context;

    public TicketService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<TicketResponseDto> CreateTicketAsync(CreateTicketDto dto)
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            Priority = dto.Priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tickets.Add(ticket);   // 3. DbContext writes to SQL Server
        await _context.SaveChangesAsync();

        return ToResponseDto(ticket);
    }
}
```

### Why this structure

**Controllers stay thin.** They only handle HTTP concerns — reading the
request, returning the right status code. They contain no business logic, so
they're easy to read and the logic lives in one predictable place.

**Services hold the logic.** When a rule changes (e.g. "resolved tickets need a
resolution note"), there's one obvious place to change it. Services also make
testing straightforward — they can be tested without spinning up a web server.

**Separation of concerns without over-engineering.** This is deliberately *not*
a multi-project Clean Architecture / CQRS setup. For an app this size, that
adds layers and indirection without real benefit. The Controller → Service →
DbContext split already keeps HTTP, logic, and data access cleanly separated.
If the app grew much larger, splitting into more projects would make sense —
but matching the architecture to the problem is itself a deliberate choice.

---

## Data model

Two main entities:

```csharp
public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }   // Low, Medium, High, Critical
    public TicketStatus Status { get; set; }       // Open, InProgress, Resolved, Closed
    public Guid? AssignedToUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DeletedAt { get; set; }        // soft delete
}

public class TicketNote
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Soft deletes

Deleting a ticket sets `DeletedAt` to the current time rather than removing the
row. Normal queries filter out rows where `DeletedAt` is not null. This keeps
history and allows recovery.

```csharp
// Only return tickets that haven't been soft-deleted
var tickets = await _context.Tickets
    .Where(t => t.DeletedAt == null)
    .ToListAsync();
```

### Audit timestamps

Each ticket tracks `CreatedAt`, `UpdatedAt`, and `ResolvedAt` so changes can be
traced over time.

---

## Entity Framework Core

EF Core is the ORM that maps C# objects to SQL Server rows. It's used instead of
raw SQL for two main reasons:

**Safety.** EF Core parameterizes queries automatically, which prevents SQL
injection.

```csharp
// Raw SQL built from user input would be injectable. EF Core is not:
var tickets = await _context.Tickets
    .Where(t => t.Priority == priority)
    .ToListAsync();
```

**Migrations.** Schema changes are tracked in version control as migration
files, so the database structure evolves alongside the code.

```bash
dotnet ef migrations add InitialCreate   # create a migration
dotnet ef database update                # apply it
```

---

## DTOs (Data Transfer Objects)

The API doesn't expose EF entities directly. Instead, requests and responses use
separate DTO classes:

```csharp
public class CreateTicketDto
{
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
}

public class TicketResponseDto
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

This keeps the database shape and the API contract independent — the entity can
change without breaking the API, and the API doesn't leak internal fields.

---

## Frontend structure

The React frontend is a single-page application:

```
frontend/src/
├── pages/        Full pages (ticket list, create, detail)
├── components/   Reusable pieces (table, form, buttons)
├── hooks/        Custom hooks that call the API
└── types/        TypeScript interfaces mirroring the API's DTOs
```

The frontend talks to the backend through a small set of API functions. The
TypeScript types mirror the backend DTOs, so the data shape is consistent on
both sides.

---

## Validation and error handling

- **Validation** uses ASP.NET Core's built-in data annotations
  (`[Required]`, `[StringLength]`) on the DTOs. The framework returns a 400
  response automatically when validation fails.
- **Error handling** returns appropriate HTTP status codes: 200/201 for
  success, 400 for bad input, 404 for not found.

---

## Decisions summary

| Decision | Why | Trade-off |
|----------|-----|-----------|
| Single ASP.NET Core project | Right size for the app; easy to follow | Would need restructuring if it grew large |
| Controller → Service → DbContext | Clear separation of concerns | None significant at this scale |
| Entity Framework Core | Type-safe queries, prevents SQL injection, migrations | Small overhead vs raw SQL |
| DTOs separate from entities | API contract independent of DB shape | A little extra mapping code |
| Soft deletes | Recoverable, preserves history | Queries must filter deleted rows |

---

## Possible future work

If the project continued past its current scope:

- Simple JWT authentication for login
- A dashboard with ticket counts and charts
- Live deployment to a cloud free tier
- Docker for reproducible local setup
- A CI pipeline to run tests on every push
