# Project Structure

The folder layout for the IT Ticket Management System and the commands to
create it from scratch. This matches the simplified single-project design.

---

## Repository layout

```
IT-Ticket-Management-System/
├── README.md
├── ARCHITECTURE.md
├── .gitignore
├── LICENSE
│
├── backend/                         # ASP.NET Core Web API (single project)
│   ├── TicketSystem.API.csproj
│   ├── Program.cs                   # Startup, DI, middleware
│   ├── appsettings.json             # Connection string, configuration
│   │
│   ├── Controllers/
│   │   └── TicketsController.cs      # HTTP endpoints for tickets and notes
│   │
│   ├── Services/
│   │   ├── ITicketService.cs         # Service interface
│   │   └── TicketService.cs          # Business logic
│   │
│   ├── Models/
│   │   ├── Ticket.cs                 # Ticket entity
│   │   ├── TicketNote.cs             # Note entity
│   │   └── Enums.cs                  # TicketPriority, TicketStatus
│   │
│   ├── DTOs/
│   │   ├── CreateTicketDto.cs        # Shape of a create request
│   │   ├── UpdateTicketDto.cs        # Shape of an update request
│   │   └── TicketResponseDto.cs      # Shape of a response
│   │
│   └── Data/
│       ├── AppDbContext.cs           # EF Core DbContext
│       ├── Migrations/               # EF Core migrations
│       └── SeedData.cs               # Sample tickets for development
│
├── backend.Tests/                   # xUnit test project
│   └── TicketServiceTests.cs        # Tests for the service layer
│
└── frontend/                        # React + TypeScript SPA
    ├── package.json
    ├── tsconfig.json
    ├── vite.config.ts
    │
    └── src/
        ├── main.tsx                  # App entry point
        ├── App.tsx                   # Root component / routing
        │
        ├── pages/
        │   ├── TicketListPage.tsx
        │   ├── CreateTicketPage.tsx
        │   └── TicketDetailPage.tsx
        │
        ├── components/
        │   ├── TicketTable.tsx
        │   ├── TicketForm.tsx
        │   └── common/
        │       ├── Button.tsx
        │       └── Spinner.tsx
        │
        ├── hooks/
        │   └── useTickets.ts          # Fetch/create/update tickets
        │
        ├── services/
        │   └── api.ts                 # Axios/fetch client
        │
        └── types/
            └── ticket.ts              # TypeScript interfaces
```

---

## Folder purposes

### Backend

| Folder | Purpose |
|--------|---------|
| `Controllers/` | HTTP endpoints. Thin — they delegate to services. |
| `Services/` | Business logic. The real work happens here. |
| `Models/` | Entities and enums — the core data shapes. |
| `DTOs/` | Request and response shapes, separate from entities. |
| `Data/` | EF Core DbContext, migrations, and seed data. |

### Frontend

| Folder | Purpose |
|--------|---------|
| `pages/` | Full-page components, one per route. |
| `components/` | Reusable UI pieces (table, form, buttons). |
| `hooks/` | Custom hooks that call the API. |
| `services/` | The API client code. |
| `types/` | TypeScript interfaces mirroring the backend DTOs. |

---

## Creating the backend

From the repository root:

```bash
# Create the API project
mkdir backend
cd backend
dotnet new webapi -n TicketSystem.API -f net8.0
# (or: dotnet new webapi without -n if you want it in the current folder)

# Add the packages we need
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design

# Confirm it builds and runs
dotnet build
dotnet run
# API starts at http://localhost:5000, Swagger at /swagger
```

### Create the test project

```bash
cd ..
dotnet new xunit -n backend.Tests -f net8.0
dotnet add backend.Tests reference backend/TicketSystem.API.csproj
dotnet add backend.Tests package Microsoft.EntityFrameworkCore.InMemory
```

The in-memory EF Core provider lets the service layer be tested without a real
SQL Server instance.

---

## Creating the frontend

From the repository root:

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install axios
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

# Run it
npm run dev
# http://localhost:5173
```

---

## Build order

A realistic order to build things, so the app works end to end as early as
possible.

### Week 1 — backend core
1. Create the `Ticket` and `TicketNote` entities and the enums.
2. Create `AppDbContext` and the first migration.
3. Apply the migration to create the database (`dotnet ef database update`).
4. Write `TicketService` with create and get-all methods.
5. Write `TicketsController` exposing those methods.
6. Test in Swagger: create a ticket, list tickets.
7. Add update and soft-delete.
8. Add seed data.

### Week 2 — tests and notes
1. Write xUnit tests for `TicketService` (create, get, update).
2. Add filtering (by status, priority) and pagination to the list.
3. Add the ticket notes endpoints.

### Week 2–3 — frontend
1. Set up the React project and the API client.
2. Build the ticket list page.
3. Build the create-ticket form.
4. Build the ticket detail / edit page.
5. Add status and priority filters.

### Later — if time permits
- Simple JWT login.
- A small dashboard with ticket counts.
- Deploy to a cloud free tier.
- Docker and a CI pipeline.

---

## What's committed vs generated

**Committed to Git:**
- Source code (`.cs`, `.tsx`, `.ts`)
- `appsettings.json` (without secrets), `vite.config.ts`
- EF Core migrations
- Documentation

**Not committed (in `.gitignore`):**
- `bin/`, `obj/` (build output)
- `node_modules/`
- `.env` and any file with secrets
- `dist/` / build output
