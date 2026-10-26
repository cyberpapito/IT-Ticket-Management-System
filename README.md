# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **SQL Server**, with a **React / TypeScript** frontend. The system lets IT staff create, track, assign, and resolve support tickets.

## What it does

Currently working, verified end-to-end via Swagger:

- Create a support ticket with a title, description, and priority (`POST /api/tickets`), returning 201 with a Location header for the new ticket
- Retrieve a ticket by id (`GET /api/tickets/{id}`), with a 404 for unknown or deleted ids
- List all tickets (`GET /api/tickets`), returning 200 with an empty array when no tickets exist — an empty list is an answer, not an error. Deleted tickets are not listed
- Soft-delete a ticket (`DELETE /api/tickets/{id}`), returning 204 — deletion stamps a `DeletedAt` timestamp rather than removing the row, so the audit history survives. Deleting it again returns 404, like any ticket that doesn't exist
- List the deleted tickets (`GET /api/tickets/deleted`), most recently deleted first, as they stood when deleted: the audit history, read-only (there is no undelete, and `GET /api/tickets/{id}` stays a 404 for them)
- Move a ticket through its workflow, each returning 200 with the updated ticket:
  - `POST /api/tickets/{id}/assign` with `{"technicianId": "…"}` (Open tickets only; an empty id is a 400)
  - `POST /api/tickets/{id}/start` (Open → In Progress; needs an assigned technician)
  - `POST /api/tickets/{id}/resolve` with `{"resolutionSummary": "…"}` (needs a technician and a summary; once only)
  - `POST /api/tickets/{id}/close` (Resolved → Closed)

  The entity decides whether a step is allowed; the controller only translates its answer. A step the ticket's current state doesn't allow (closing an Open ticket, starting unassigned work, resolving twice) is a **409 Conflict** with the reason, bad input is a **400**, and an unknown or deleted ticket is a **404**. A rejected step saves nothing.

- A ticket's history (`GET /api/tickets/{id}/activity`), oldest first: one entry per workflow step (created, assigned, started, resolved, closed, deleted) and per comment, with who it names, any note (the comment, or the resolution summary) and the status change. Add a comment with `POST /api/tickets/{id}/comments` and `{"authorUserId": "…", "text": "…"}`, which returns 201 with the entry; empty or over-2,000-character text, or an unknown author, is a 400. An unknown or deleted ticket is a 404 on both.

- Users (`/api/users`): list them (`GET /api/users`, optionally `?role=Technician`), look one up (`GET /api/users/{id}`), or add one (`POST /api/users` with `{"name", "email", "role"}`; role 0 = User, 1 = Technician). A duplicate email (compared case-insensitively) or a bad name/email is a 400. Seeded users: **Adrian Rodriguez** (Technician) and **John Doe** (User), with placeholder `@example.com` emails.

## Frontend

`frontend/` is a React + TypeScript app (Vite) over the same API:

- **Tickets**: every ticket that isn't closed, most urgent first, with counts per status, search, and status/priority filters. Names replace the user ids the API returns. A form creates a ticket (title, description, priority, who reported it).
- **Ticket page**: the details, plus the workflow steps the ticket's state allows: assign a technician (only Technicians are offered), start work, resolve with a summary, close, or delete. When the API refuses a step, its 400/409 reason is shown as is and the ticket stays as it was. Below the details, the **Activity** timeline shows the ticket's history and comments, with a box to add one (pick who's commenting).
- **Deleted**: the deleted tickets, newest deletion first, with their status, people, resolution and when they were deleted. Read-only.
- **Users**: the list, and a form to add a user or technician.

The buttons mirror the entity's rules only so the page doesn't offer steps that can only fail; the API still decides. There's no sign-in yet, so anyone using the page can act as anyone.

```bash
# terminal 1: the API on http://localhost:5137
cd backend/TicketSystem && dotnet run --launch-profile http

# terminal 2: the UI on http://localhost:5173
cd frontend && npm install && npm run dev
```

The Vite dev server forwards `/api` to the API, so the API needs no CORS setup. `npm test` runs the Vitest suite (no API needed).

## Business rules

Rules are enforced on the `Ticket` entity itself, not in controllers or services, so they cannot be bypassed regardless of the caller:

- Tickets can only be constructed through the `Ticket.Create()` factory, which guarantees a valid initial state. An invalid ticket is unrepresentable, not merely validated.
- A title must be non-blank and at most 255 characters, and a description at most 2,000, measured after trimming. The limits are constants on `Ticket` that `AppDbContext` also uses for the column sizes, so they can't drift apart; an over-long or blank title is a 400 from `POST /api/tickets`, never a database error.
- Every ticket's creator and technician are real users: the database enforces it with foreign keys, and the API turns an unknown user into a 400. Only a **Technician** can be assigned a ticket (`Ticket.AssignTo(User)` checks the role).
- A ticket cannot be resolved unless it has an assigned technician and a written resolution summary, and it can only be resolved once — resolving again would overwrite the original summary and `ResolvedAt`.
- A ticket cannot be soft-deleted twice — the original `DeletedAt` timestamp is preserved because overwriting it would falsify the audit trail.
- The ticket writes its own history: every step above adds a `TicketActivity` entry in the same method that changes the state, and a rejected step adds nothing, so the history can't disagree with the ticket. Comments go through `Ticket.AddComment(User, text)` too (any user, any status, but not on a deleted ticket). Entries are only ever added, never edited.

Soft-deleted tickets are hidden by an EF Core global query filter (`DeletedAt == null`) on `AppDbContext`, so no query can forget to exclude them; `IgnoreQueryFilters()` reaches them when the audit history is needed, and `TicketService.GetDeletedTickets()` (behind `GET /api/tickets/deleted`) is the one query that does.

`DeletedAt` is a nullable `DateTimeOffset` rather than a boolean flag or a status value: when a ticket was deleted is an independent fact from where it was in its workflow, and the timestamp records both that it happened and when.

## Tech stack

**Frontend**

- React 19 + TypeScript, built with Vite
- React Router
- Vitest + Testing Library

**Backend**

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

## Security

[`docs/THREAT_MODEL.md`](docs/THREAT_MODEL.md) is a STRIDE threat model of the current design. The app has no sign-in yet and only runs on localhost, so the model's high-severity items are requirements for the next roadmap step (sign-in and roles), which comes before any deploy.
