# IT Ticket Management System

A full-stack IT support ticket system built with **C# / ASP.NET Core** and **SQL Server**,
with a **React / TypeScript** frontend planned. The system lets IT staff create, track,
assign, and resolve support tickets.

---

## What it does

Currently working:

- Create a support ticket with a title, description, and priority (`POST /api/tickets`)
- Retrieve a ticket by id (`GET /api/tickets/{id}`), with a 404 for unknown ids
- Business rules enforced on the domain entity itself: tickets cannot be assigned
  unless Open, cannot be resolved without an assigned technician and a written
  resolution summary, and cannot be closed until resolved

---

## Tech stack

**Backend (current)**
- C# / ASP.NET Core 9 (Web API)
- Entity Framework Core 9
- SQL Server (LocalDB for development)
- Swagger / OpenAPI for interactive API docs


The backend is organized as a single ASP.NET Core project using a
**Controller → Service → DbContext** flow: controllers handle HTTP,
services orchestrate, and business rules live on the entities themselves.
This is a deliberate choice for an app of this size — it keeps the
code clear and easy to follow rather than over-engineering it.

---

## Getting started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (included with Visual Studio, or installable standalone)

### Run the backend
```bash
cd backend/TicketSystem
dotnet restore
dotnet ef database update     # create the database from migrations
dotnet run
# API runs at http://localhost:5137
# Swagger UI at http://localhost:5137/swagger
```

---

## API endpoints

Interactive documentation is available at `/swagger` when the API is running.

| Method | Endpoint | Status |
|--------|----------|--------|
| POST   | `/api/tickets`        | Working — creates a ticket, returns 201 with location header |
| GET    | `/api/tickets/{id}`   | Working — returns 200, or 404 if not found |
| GET    | `/api/tickets`        | Planned — list with status/priority filtering |
| PUT    | `/api/tickets/{id}`   | Planned |
| DELETE | `/api/tickets/{id}`   | Planned — soft delete |
| GET    | `/api/tickets/{id}/notes` | Planned |
| POST   | `/api/tickets/{id}/notes` | Planned |

---

## Project structure

IT-Ticket-Management-System/
├── backend/TicketSystem/
│   ├── Controllers/        
│   ├── Services/          
│   ├── Models/            
│   ├── DTOs/              
│   ├── Data/              
│   ├── Migrations/         
│   └── Program.cs          
│
└── README.md
