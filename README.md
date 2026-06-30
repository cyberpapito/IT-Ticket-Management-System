# IT Ticket Management System

A production-ready, full-stack IT ticket management system demonstrating enterprise architecture patterns, modern web development practices, and complete DevOps workflow. Built with **C# / ASP.NET Core** backend and **React / TypeScript** frontend.

**Portfolio Project** for Miami-Dade County IT Software Developer Role

---

## Project Overview

This system manages IT support tickets across departments, enabling technicians to:
- Create, update, and resolve support tickets
- Assign tickets to team members
- Track ticket status and priority in real-time
- View analytics dashboards with KPIs and charts
- Collaborate via ticket notes and comments

**Live Demo:** [Coming Soon]  
**Architecture:** [ARCHITECTURE.md](./ARCHITECTURE.md)

---

## Tech Stack

### Backend
- **Language:** C# 12
- **Framework:** ASP.NET Core 8 (LTS)
- **Database:** SQL Server (LocalDB / Docker)
- **ORM:** Entity Framework Core 8
- **Architecture:** Clean Architecture + CQRS-lite (MediatR)
- **API Spec:** OpenAPI 3.0 (Swagger UI)
- **Testing:** xUnit + Moq

### Frontend
- **Framework:** React 18
- **Language:** TypeScript 5
- **Styling:** Tailwind CSS 4
- **State Management:** React Query (TanStack Query)
- **Forms:** React Hook Form + Zod validation
- **Charts:** Recharts
- **Build Tool:** Vite
- **Testing:** Vitest + React Testing Library

### DevOps
- **Containerization:** Docker + docker-compose
- **CI/CD:** GitHub Actions
- **Deployment:** Render.com (free tier)
- **Version Control:** Git (GitHub)

---

## Quick Start

### Prerequisites
- Docker & Docker Compose (https://www.docker.com/products/docker-desktop)
- Git
- Node.js 18+ (for local frontend development)
- .NET 8 SDK (for local backend development)

### Local Development (Docker)
```bash
# Clone repository
git clone https://github.com/yourusername/IT-Ticket-Management-System.git
cd IT-Ticket-Management-System

# Start entire stack (API + DB + Frontend)
docker-compose up

# Access applications
# Frontend: http://localhost:3000
# API: http://localhost:5000
# Swagger Docs: http://localhost:5000/swagger
# SQL Server: localhost:1433 (user: sa, password: YourSecurePassword123!)
```

### Local Development (Native)
**Backend:**
```bash
cd backend
dotnet restore
dotnet build
dotnet run --project TicketSystem.API
# API running at: http://localhost:5000
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
# UI running at: http://localhost:5173
```

---

## Project Structure

```
IT-Ticket-Management-System/
├── backend/                          # ASP.NET Core API
│   ├── TicketSystem.API/             # Controllers, middleware
│   ├── TicketSystem.Application/     # Commands, queries, handlers
│   ├── TicketSystem.Domain/          # Entities, value objects
│   ├── TicketSystem.Infrastructure/  # DbContext, repositories
│   ├── TicketSystem.Tests/           # xUnit tests
│   └── TicketSystem.sln
├── frontend/                         # React SPA
│   ├── src/
│   │   ├── pages/                    # Page components
│   │   ├── components/               # Reusable UI components
│   │   ├── hooks/                    # Custom React hooks
│   │   ├── services/                 # API client, auth
│   │   └── types/                    # TypeScript interfaces
│   ├── package.json
│   └── vite.config.ts
├── docker-compose.yml                # Multi-container setup
├── .github/
│   └── workflows/
│       └── ci.yml                    # GitHub Actions CI/CD
├── ARCHITECTURE.md                   # Design decisions & patterns
├── .gitignore
└── LICENSE (MIT)
```

---

## API Documentation

Interactive API docs available at **`/swagger`** when API is running.

### Core Endpoints

#### Tickets
```
GET    /api/tickets                   # List all tickets (paginated, filterable)
POST   /api/tickets                   # Create new ticket
GET    /api/tickets/{id}              # Get ticket details
PUT    /api/tickets/{id}              # Update ticket
DELETE /api/tickets/{id}              # Delete ticket (soft delete)

GET    /api/tickets/{id}/notes        # Get ticket notes
POST   /api/tickets/{id}/notes        # Add note to ticket
```

#### Users
```
GET    /api/users                     # List all users
POST   /api/users                     # Create user
GET    /api/users/{id}                # Get user details
PUT    /api/users/{id}                # Update user
```

#### Authentication
```
POST   /api/auth/login                # Login (returns JWT token)
POST   /api/auth/refresh              # Refresh expired token
```

#### Health
```
GET    /api/health                    # Health check
```

---

## Architecture Highlights

### Clean Architecture Layers
```
TicketSystem.API
  ↓ (HTTP Requests)
TicketSystem.Application (MediatR Handlers)
  ↓ (Business Logic)
TicketSystem.Domain (Entities, Interfaces)
  ↓ (Data Access)
TicketSystem.Infrastructure (EF Core, Repositories)
  ↓ (Database)
SQL Server
```

**Benefits:**
- Each layer has single responsibility
- Easy to test (mock dependencies)
- Easy to extend (add features without touching existing code)

### CQRS-Lite (MediatR Pattern)
```
Commands (Write Operations)        Queries (Read Operations)
├── CreateTicketCommand            ├── GetTicketsQuery
├── UpdateTicketCommand            ├── GetTicketByIdQuery
└── DeleteTicketCommand            └── GetUsersQuery

↓
Handlers (MediatR)
↓
Domain Logic → Database
```

---

## Testing

### Backend Tests
```bash
cd backend
dotnet test
```

Test coverage includes:
- MediatR handler tests (CreateTicket, UpdateTicket, GetTickets)
- Validator tests (required fields, valid enums)
- Entity tests

### Frontend Tests
```bash
cd frontend
npm test
```

Test coverage includes:
- Component tests (TicketTable, TicketForm, Dashboard)
- Hook tests (useTickets, useAuth)

---

## Authentication & Authorization

### JWT Bearer Tokens
- Login endpoint returns JWT token
- Token stored in browser localStorage
- Token injected in Authorization header: `Authorization: Bearer <token>`
- Token expires after 24 hours (configurable)

### Role-Based Access Control
```
Admin       → Full access (create, read, update, delete all)
Technician  → Can manage assigned tickets + add notes
EndUser     → Can create tickets, view own tickets
```

---

## Deployment

### Docker Compose (Local)
```bash
docker-compose up
# Spins up: SQL Server + API + Frontend
```

### Render.com (Cloud)
1. Push to GitHub
2. Connect GitHub repo to Render.com
3. Set environment variables (connection string, JWT secret)
4. Deploy

See [DEPLOYMENT.md](./DEPLOYMENT.md) for detailed steps.

---

## Key Features

- **Full CRUD Operations** — Create, read, update, delete tickets
- **Real-time Status Updates** — See changes immediately
- **Advanced Filtering** — Filter by priority, status, assignee
- **Pagination & Sorting** — Handle large datasets efficiently
- **Analytics Dashboard** — KPI cards and charts
- **Responsive Design** — Mobile, tablet, desktop
- **JWT Authentication** — Secure stateless auth
- **Role-Based Permissions** — Admin, Technician, EndUser
- **Global Error Handling** — Graceful error messages
- **Soft Deletes** — Data recovery without hard delete
- **Audit Timestamps** — Track CreatedAt, UpdatedAt, ResolvedAt


## CI/CD Pipeline

GitHub Actions automatically:
1. Builds backend (.NET solution)
2. Builds frontend (Node.js)
3. Runs backend tests (xUnit)
4. Runs frontend tests (Vitest)
5. Builds Docker images
6. Reports coverage

See [`.github/workflows/ci.yml`](./.github/workflows/ci.yml)

---

## Learning Outcomes

Building this system demonstrates:

1. **Enterprise Architecture** — Clean Architecture + CQRS patterns
2. **Full-Stack Development** — Backend API + Frontend UI
3. **Database Design** — SQL Server schema with EF Core migrations
4. **Type Safety** — C# and TypeScript for compile-time safety
5. **Testing & Quality** — Unit tests, validation, error handling
6. **DevOps & Deployment** — Docker, GitHub Actions, cloud deployment
7. **Security** — JWT auth, role-based authorization, SQL injection prevention
8. **Best Practices** — Git workflow, meaningful commits, documentation


## Resources

### C# / ASP.NET Core
- [Microsoft Learn: ASP.NET Core](https://learn.microsoft.com/aspnet/core)
- [Entity Framework Core Docs](https://learn.microsoft.com/ef/core)
- [Clean Architecture Guide](https://github.com/ardalis/CleanArchitecture)

### React / TypeScript
- [React Documentation](https://react.dev)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/)
- [React Query Docs](https://tanstack.com/query/latest)

### DevOps / Docker
- [Docker Docs](https://docs.docker.com/)
- [GitHub Actions Docs](https://docs.github.com/en/actions)

---

## License

MIT License — See [LICENSE](./LICENSE) file

---

## Author

Adrian Rodriguez

---

**Last Updated:** June 2026  
**Status:** Active Development
