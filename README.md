# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **SQL Server**, with a **React / TypeScript** frontend. The system lets IT staff create, track, assign, and resolve support tickets.

## What it does

Every endpoint needs a signed-in user (see [Sign-in and roles](#sign-in-and-roles)); the person behind each request comes from the session, never from the request body.

- Create a support ticket with a title, description, and priority (`POST /api/tickets`), returning 201 with a Location header for the new ticket. The reporter is whoever is signed in
- Retrieve a ticket by id (`GET /api/tickets/{id}`), with a 404 for unknown or deleted ids, and for tickets the user may not see
- List tickets (`GET /api/tickets`): every ticket for technicians and admins, a user's own tickets for a plain user. 200 with an empty array when there are none — an empty list is an answer, not an error. Deleted tickets are not listed
- Soft-delete a ticket (`DELETE /api/tickets/{id}`, Admin only), returning 204 — deletion stamps a `DeletedAt` timestamp rather than removing the row, so the audit history survives. Deleting it again returns 404, like any ticket that doesn't exist
- List the deleted tickets (`GET /api/tickets/deleted`, Admin only), most recently deleted first, as they stood when deleted: the audit history, read-only (there is no undelete, and `GET /api/tickets/{id}` stays a 404 for them)
- Move a ticket through its workflow, each returning 200 with the updated ticket:
  - `POST /api/tickets/{id}/assign` with `{"technicianId": "…"}` (Open tickets only; an empty id is a 400)
  - `POST /api/tickets/{id}/start` (Open → In Progress; needs an assigned technician)
  - `POST /api/tickets/{id}/resolve` with `{"resolutionSummary": "…"}` (needs a technician and a summary; once only)
  - `POST /api/tickets/{id}/close` (Resolved → Closed)

  The entity decides whether a step is allowed; the controller only translates its answer. A step the ticket's current state doesn't allow (closing an Open ticket, starting unassigned work, resolving twice) is a **409 Conflict** with the reason, a step this user may not take is a **403**, bad input is a **400**, and an unknown, deleted or invisible ticket is a **404**. A rejected step saves nothing.

- A ticket's history (`GET /api/tickets/{id}/activity`), oldest first: one entry per workflow step (created, assigned, started, resolved, closed, deleted) and per comment, with who did it (`actorUserId`; null on entries from before sign-in), who it names, any note (the comment, or the resolution summary) and the status change. Add a comment with `POST /api/tickets/{id}/comments` and `{"text": "…"}` (the author is the signed-in user), which returns 201 with the entry; empty or over-2,000-character text is a 400. An unknown, deleted or invisible ticket is a 404 on both.

- Users (`/api/users`): list them (`GET /api/users`, optionally `?role=1`) and look one up (`GET /api/users/{id}`); a plain user sees themselves and the staff. Admins add users (`POST /api/users` with `{"name", "email", "role", "password"}`; role 0 = User, 1 = Technician, 2 = Admin), change a role (`PUT /api/users/{id}/role`) and set a password (`PUT /api/users/{id}/password`). A duplicate email (compared case-insensitively), a bad name/email or a password under 12 characters is a 400; demoting the last admin who can sign in is a 409. Seeded users: **Adrian Rodriguez** (Technician) and **John Doe** (User), with placeholder `@example.com` emails and no password until an admin sets one.

## Sign-in and roles

Sign-in is a cookie session (`tickets.session`: HttpOnly, SameSite=Strict, Secure outside development, 8 hours sliding):

- `GET /api/auth/csrf` returns an antiforgery token. Every POST, PUT and DELETE (sign-in included) must send it in an `X-XSRF-TOKEN` header, or it's a 400. The token is tied to who is signed in, so the client fetches a new one after signing in or out.
- `POST /api/auth/login` with `{"email", "password"}` returns the user and sets the cookie; a wrong email or password is a 401 with the same message either way. Five attempts per minute per IP, then 429 (`Auth:LoginAttemptsPerMinute`).
- `POST /api/auth/logout` ends the session; `GET /api/auth/me` returns the signed-in user, or 401.

Passwords are hashed with ASP.NET Core's `PasswordHasher` (PBKDF2) and never leave the API. Each user has a security stamp that the cookie carries; changing their password or role changes the stamp, and every request checks it, so a reset or demotion ends their existing sessions at once.

| | User | Technician | Admin |
|---|---|---|---|
| See tickets | their own | all | all |
| Report, comment | ✓ (on their own) | ✓ | ✓ |
| Assign | | ✓ (to any technician or admin) | ✓ |
| Start, resolve | | the assigned technician | ✓ |
| Close a resolved ticket | the reporter | the assigned technician | ✓ |
| Delete, see deleted | | | ✓ |
| Add users, change roles, set passwords | | | ✓ |

The rules live in `TicketPermissions` (who may), next to the state rules on `Ticket` (when). A ticket a user may not see is a 404, not a 403, so ids don't leak.

**The first admin** comes from configuration, so no default password ships with the code. Locally:

```bash
cd backend/TicketSystem
dotnet user-secrets set "Auth:BootstrapAdmin:Email" "you@example.com"
dotnet user-secrets set "Auth:BootstrapAdmin:Password" "<at least 12 characters>"
dotnet run --launch-profile http   # logs "Bootstrap admin: …" once
dotnet user-secrets remove "Auth:BootstrapAdmin:Password"
```

It creates that user (or promotes an existing one) only while no admin can sign in, so later restarts never reset a changed password.

## Frontend

`frontend/` is a React + TypeScript app (Vite) over the same API:

- **Sign in**: shown until someone is signed in. The header then shows who it is, their role and a Sign out button; if the session ends (it expired, or an admin reset the password), the app goes back here.
- **Tickets** (**My tickets** for a plain user): the tickets that aren't closed, most urgent first, with counts per status, search, and status/priority filters. Names replace the user ids the API returns. A form creates a ticket (title, description, priority) as the signed-in user.
- **Ticket page**: the details, plus the workflow steps this user may take in the ticket's current state: assign a technician, start work, resolve with a summary, close, and (admins) delete. When the API refuses a step, its 400/403/409 reason is shown as is and the ticket stays as it was. Below the details, the **Activity** timeline shows the ticket's history (who did each step) and comments, with a box to add one.
- **Deleted** (admins): the deleted tickets, newest deletion first, with their status, people, resolution and when they were deleted. Read-only.
- **Users** (technicians and admins): the list; admins also add users with a password, change roles and set passwords.

The buttons mirror the API's rules (`availableSteps` in `api.ts`) only so the page doesn't offer steps that can only fail; the API still decides.

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
- Every ticket's creator and technician are real users: the database enforces it with foreign keys, and the API turns an unknown technician into a 400. Only a **Technician** or **Admin** can be assigned a ticket (`Ticket.AssignTo(User, actor)` checks the role).
- A ticket cannot be resolved unless it has an assigned technician and a written resolution summary, and it can only be resolved once — resolving again would overwrite the original summary and `ResolvedAt`.
- A ticket cannot be soft-deleted twice — the original `DeletedAt` timestamp is preserved because overwriting it would falsify the audit trail.
- The ticket writes its own history: every step above adds a `TicketActivity` entry in the same method that changes the state, and a rejected step adds nothing, so the history can't disagree with the ticket. Comments go through `Ticket.AddComment(User, text)` too (any status, but not on a deleted ticket). Each entry records the actor who took the step. Entries are only ever added, never edited.

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

`backend/TicketSystem.Tests` is an xUnit project with three layers:

- **Entity tests** (`TicketTests`) cover every business rule on `Ticket`: creation, assignment, starting work, resolving, closing and soft delete, including the transitions that must be rejected.
- **Service and controller tests** run against EF Core's in-memory provider, so they need no SQL Server. Each test gets its own database and reads back through a fresh context to check what was actually saved. `TicketPermissionTests` checks who may do what, role by role.
- **Pipeline tests** (`SignInPipelineTests`) run the whole app in memory with `WebApplicationFactory`: 401 without a session, 403 for admin-only endpoints, the CSRF check (missing or stale token), the login rate limit, and a password reset ending existing sessions.

```bash
cd backend/TicketSystem.Tests
dotnet test
```

## Security

[`docs/THREAT_MODEL.md`](docs/THREAT_MODEL.md) is a STRIDE threat model of the design. Sign-in and roles closed its high-severity items (no authentication, identity taken from the request body, anyone creating technicians, no actor on activity, CSRF). What's left before a deploy is in its checklist: paging, Production settings, TLS and a least-privilege database login.
