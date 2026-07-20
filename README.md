# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **SQL Server**, with a **React / TypeScript** frontend planned. The system lets IT staff create, track, assign, and resolve support tickets.

## What it does

Currently working, verified end-to-end via Swagger:

- Create a support ticket with a title, description, and priority (`POST /api/tickets`), returning 201 with a Location header for the new ticket
- Retrieve a ticket by id (`GET /api/tickets/{id}`), with a 404 for unknown ids
- List all tickets (`GET /api/tickets`), returning 200 with an empty array when no tickets exist — an empty list is an answer, not an error
- Soft-delete a ticket (`DELETE /api/tickets/{id}`), returning 204 — deletion stamps a `DeletedAt` timestamp rather than removing the row, so the audit history survives

## Business rules

Rules are enforced on the `Ticket` entity itself, not in controllers or services, so they cannot be bypassed regardless of the caller:

- Tickets can only be constructed through the `Ticket.Create()` factory, which guarantees a valid initial state. An invalid ticket is unrepresentable, not merely validated.
- A ticket cannot be resolved unless it has an assigned technician and a written resolution summary.
- A ticket cannot be soft-deleted twice — the original `DeletedAt` timestamp is preserved because overwriting it would falsify the audit trail.

`DeletedAt` is a nullable `DateTimeOffset` rather than a boolean flag or a status value: when a ticket was deleted is an independent fact from where it was in its workflow, and the timestamp records both that it happened and when.

## Tech stack

**Backend (current)**

- C# / ASP.NET Core 9 (Web API)
- Entity Framework Core 9
- SQL Server (LocalDB for development)
- Swagger / OpenAPI for interactive API docs

## Architecture

The backend is a single ASP.NET Core project with a strict **Controller → Service → Entity → DbContext** flow: controllers translate HTTP in and out and nothing more, the service orchestrates operations, business rules live on the entity, and EF Core handles persistence. 

