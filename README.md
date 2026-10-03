# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **SQL Server**, with a **React / TypeScript** frontend planned. The system lets IT staff create, track, assign, and resolve support tickets.

## What it does

Currently working, verified end-to-end via Swagger:

- Create a support ticket with a title, description, and priority (`POST /api/tickets`), returning 201 with a Location header for the new ticket
- Retrieve a ticket by id (`GET /api/tickets/{id}`), with a 404 for unknown or deleted ids
- List all tickets (`GET /api/tickets`), returning 200 with an empty array when no tickets exist — an empty list is an answer, not an error. Deleted tickets are not listed
- Soft-delete a ticket (`DELETE /api/tickets/{id}`), returning 204 — deletion stamps a `DeletedAt` timestamp rather than removing the row, so the audit history survives. Deleting it again returns 404, like any ticket that doesn't exist
- Move a ticket through its workflow, each returning 200 with the updated ticket:
  - `POST /api/tickets/{id}/assign` with `{"technicianId": "…"}` (Open tickets only; an empty id is a 400)
  - `POST /api/tickets/{id}/start` (Open → In Progress; needs an assigned technician)
  - `POST /api/tickets/{id}/resolve` with `{"resolutionSummary": "…"}` (needs a technician and a summary; once only)
  - `POST /api/tickets/{id}/close` (Resolved → Closed)

  The entity decides whether a step is allowed; the controller only translates its answer. A step the ticket's current state doesn't allow (closing an Open ticket, starting unassigned work, resolving twice) is a **409 Conflict** with the reason, bad input is a **400**, and an unknown or deleted ticket is a **404**. A rejected step saves nothing.

## Business rules

Rules are enforced on the `Ticket` entity itself, not in controllers or services, so they cannot be bypassed regardless of the caller:

- Tickets can only be constructed through the `Ticket.Create()` factory, which guarantees a valid initial state. An invalid ticket is unrepresentable, not merely validated.
- A title must be non-blank and at most 255 characters, and a description at most 2,000, measured after trimming. The limits are constants on `Ticket` that `AppDbContext` also uses for the column sizes, so they can't drift apart; an over-long or blank title is a 400 from `POST /api/tickets`, never a database error.
- A ticket cannot be resolved unless it has an assigned technician and a written resolution summary, and it can only be resolved once — resolving again would overwrite the original summary and `ResolvedAt`.
- A ticket cannot be soft-deleted twice — the original `DeletedAt` timestamp is preserved because overwriting it would falsify the audit trail.

Soft-deleted tickets are hidden by an EF Core global query filter (`DeletedAt == null`) on `AppDbContext`, so no query can forget to exclude them; `IgnoreQueryFilters()` reaches them when the audit history is needed.

`DeletedAt` is a nullable `DateTimeOffset` rather than a boolean flag or a status value: when a ticket was deleted is an independent fact from where it was in its workflow, and the timestamp records both that it happened and when.

## Tech stack

**Backend (current)**

- C# / ASP.NET Core 9 (Web API)
- Entity Framework Core 9
- SQL Server (LocalDB for development)
- Swagger / OpenAPI for interactive API docs

## Architecture

The backend is a single ASP.NET Core project with a strict **Controller → Service → Entity → DbContext** flow: controllers translate HTTP in and out and nothing more, the service orchestrates operations, business rules live on the entity, and EF Core handles persistence. 


## Testing

`backend/TicketSystem.Tests` is an xUnit project with two layers:

- **Entity tests** (`TicketTests`) cover every business rule on `Ticket`: creation, assignment, starting work, resolving, closing and soft delete, including the transitions that must be rejected.
- **Service tests** (`TicketServiceTests`) run `TicketService` against EF Core's in-memory provider, so they need no SQL Server. Each test gets its own database and reads back through a fresh context to check what was actually saved.

```bash
cd backend/TicketSystem.Tests
dotnet test
```
