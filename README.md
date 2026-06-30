# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **React / TypeScript**, backed by **SQL Server**. The system lets IT staff create, track, assign, and resolve support tickets.

> **Status:** Early development. This README describes the current state of the project, not a finished product. See [Roadmap](#roadmap) for what's done and what's planned.

---

## What it does

IT technicians and end users can:
- Create support tickets with a title, description, and priority
- View and filter tickets by status and priority
- Update a ticket's details and status
- Assign tickets to technicians
- Add notes to a ticket for follow-up

---

## Tech stack

**Backend**
- C# 12 / ASP.NET Core 8 (Web API)
- Entity Framework Core 8
- SQL Server (LocalDB for development)
- Swagger / OpenAPI for interactive API docs
- xUnit for unit tests

**Frontend**
- React 18 + TypeScript 5
- Vite (build tool)
- Tailwind CSS

The backend is organized as a single ASP.NET Core project using a
**Controller → Service → DbContext** flow: controllers handle HTTP,
services hold the business logic, and Entity Framework Core handles data
access. This is a deliberate choice for an app of this size — it keeps the
code clear and easy to follow rather than over-engineering it.

---

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) (for the frontend)
- SQL Server LocalDB (included with Visual Studio, or installable standalone)

### Run the backend
```bash
cd backend
dotnet restore
dotnet ef database update     # create the database from migrations
dotnet run
# API runs at http://localhost:5000
# Swagger UI at http://localhost:5000/swagger
```

### Run the frontend
```bash
cd frontend
npm install
npm run dev
# UI runs at http://localhost:5173
```

---

## API endpoints

Interactive documentation is available at `/swagger` when the API is running.

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET    | `/api/tickets`        | List tickets (filter by status, priority) |
| POST   | `/api/tickets`        | Create a ticket |
| GET    | `/api/tickets/{id}`   | Get a single ticket |
| PUT    | `/api/tickets/{id}`   | Update a ticket |
| DELETE | `/api/tickets/{id}`   | Soft-delete a ticket |
| GET    | `/api/tickets/{id}/notes` | List a ticket's notes |
| POST   | `/api/tickets/{id}/notes` | Add a note to a ticket |

---

## Project structure

```
IT-Ticket-Management-System/
├── backend/
│   ├── Controllers/        # HTTP endpoints (thin, no business logic)
│   ├── Services/           # Business logic
│   ├── Models/             # Entities (Ticket, TicketNote) and enums
│   ├── DTOs/               # Request/response shapes (kept separate from entities)
│   ├── Data/               # AppDbContext, migrations, seed data
│   └── Program.cs          # Startup and configuration
│
├── backend.Tests/          # xUnit tests for the service layer
│
├── frontend/
│   └── src/
│       ├── pages/          # Page components
│       ├── components/     # Reusable UI components
│       ├── hooks/          # Custom React hooks (API calls)
│       └── types/          # TypeScript interfaces
│
└── README.md
```

---

## Roadmap

This is the build order. Items are checked off as they're actually working.

**Backend**
- [ ] `Ticket` and `TicketNote` entities with EF Core
- [ ] SQL Server database via EF migrations
- [ ] Ticket CRUD endpoints (create, read, update, soft-delete)
- [ ] Filtering and pagination on the ticket list
- [ ] Ticket notes endpoints
- [ ] Service-layer unit tests (xUnit)
- [ ] Seed data for development

**Frontend**
- [ ] Ticket list page
- [ ] Create ticket form
- [ ] Ticket detail / edit page
- [ ] Status and priority filtering

**Later / if time permits**
- [ ] Simple JWT login (no refresh tokens)
- [ ] Dashboard with basic charts
- [ ] Live deployment (Azure free tier)
- [ ] Docker setup
- [ ] CI pipeline (GitHub Actions)

---

## Notes on design decisions

A few choices worth explaining, since they're deliberate:

- **Single project, not multi-layer Clean Architecture.** For an app this
  size, a clean single project is easier to follow and maintain than four
  separate layers. The Controller → Service → DbContext split already gives
  good separation of concerns.
- **DTOs separate from entities.** API requests and responses use their own
  classes rather than exposing EF entities directly. This keeps the database
  shape and the API contract independent.
- **Soft deletes.** Tickets are marked deleted with a timestamp rather than
  removed, so records can be recovered and history is preserved.

---

## License

MIT License — see [LICENSE](./LICENSE).

---

## Author

Adrian Rodriguez
