# Project Structure: IT Ticket Management System

This document explains the folder layout, naming conventions, and implementation strategy for the IT Ticket Management System.

---

## Repository Layout

```
IT-Ticket-Management-System/
├── README.md                          # Project overview, setup instructions
├── ARCHITECTURE.md                    # Design decisions and patterns
├── LICENSE                            # MIT License
├── .gitignore                         # Ignore node_modules, bin, obj, .env
├── docker-compose.yml                 # Local development stack (SQL, API, frontend)
│
├── backend/                           # ASP.NET Core 8 solution
│   ├── TicketSystem.sln               # Solution file (lists all projects)
│   │
│   ├── TicketSystem.API/              # HTTP entry point
│   │   ├── Program.cs                 # Startup, dependency injection, middleware
│   │   ├── appsettings.json           # Configuration (database, JWT, logging)
│   │   ├── Dockerfile                 # Container image for API
│   │   ├── TicketSystem.API.csproj    # Project file (NuGet dependencies)
│   │   │
│   │   └── Controllers/
│   │       ├── TicketsController.cs
│   │       │   └── HTTP handlers: GET /api/tickets, POST /api/tickets, etc.
│   │       │
│   │       ├── UsersController.cs
│   │       │   └── HTTP handlers: GET /api/users, POST /api/users, etc.
│   │       │
│   │       └── AuthController.cs
│   │           └── HTTP handler: POST /api/auth/login
│   │
│   ├── TicketSystem.Application/      # Business logic (commands, queries, handlers)
│   │   ├── TicketSystem.Application.csproj
│   │   │
│   │   ├── Commands/
│   │   │   ├── CreateTicketCommand.cs
│   │   │   ├── UpdateTicketCommand.cs
│   │   │   ├── AssignTicketCommand.cs
│   │   │   ├── ResolveTicketCommand.cs
│   │   │   └── DeleteTicketCommand.cs
│   │   │
│   │   ├── Queries/
│   │   │   ├── GetTicketByIdQuery.cs
│   │   │   ├── GetTicketsForTechnicianQuery.cs
│   │   │   ├── GetAllTicketsQuery.cs
│   │   │   └── GetUsersQuery.cs
│   │   │
│   │   ├── Handlers/
│   │   │   ├── Commands/
│   │   │   │   ├── CreateTicketCommandHandler.cs
│   │   │   │   ├── UpdateTicketCommandHandler.cs
│   │   │   │   ├── AssignTicketCommandHandler.cs
│   │   │   │   ├── ResolveTicketCommandHandler.cs
│   │   │   │   └── DeleteTicketCommandHandler.cs
│   │   │   │
│   │   │   └── Queries/
│   │   │       ├── GetTicketByIdQueryHandler.cs
│   │   │       ├── GetTicketsForTechnicianQueryHandler.cs
│   │   │       ├── GetAllTicketsQueryHandler.cs
│   │   │       └── GetUsersQueryHandler.cs
│   │   │
│   │   ├── DTOs/
│   │   │   ├── Requests/
│   │   │   │   ├── CreateTicketRequest.cs
│   │   │   │   ├── UpdateTicketRequest.cs
│   │   │   │   └── AssignTicketRequest.cs
│   │   │   │
│   │   │   └── Responses/
│   │   │       ├── TicketResponse.cs
│   │   │       ├── UserResponse.cs
│   │   │       └── TicketNoteResponse.cs
│   │   │
│   │   ├── Validators/
│   │   │   ├── CreateTicketValidator.cs
│   │   │   ├── UpdateTicketValidator.cs
│   │   │   └── AssignTicketValidator.cs
│   │   │
│   │   └── Mappings/
│   │       ├── TicketMappingProfile.cs
│   │       └── UserMappingProfile.cs
│   │
│   ├── TicketSystem.Domain/           # Core entities and business rules
│   │   ├── TicketSystem.Domain.csproj
│   │   │
│   │   ├── Entities/
│   │   │   ├── Ticket.cs
│   │   │   │   └── Contains: Id, Title, Description, Priority, Status, AssignedTo, etc.
│   │   │   │   └── Methods: AssignTo(), MarkResolved(), MarkInProgress()
│   │   │   │
│   │   │   ├── User.cs
│   │   │   │   └── Contains: Id, Email, FirstName, LastName, Role, IsActive, etc.
│   │   │   │
│   │   │   └── TicketNote.cs
│   │   │       └── Contains: Id, TicketId, AuthorId, Content, CreatedAt (audit trail)
│   │   │
│   │   ├── Enums/
│   │   │   ├── TicketPriority.cs
│   │   │   │   └── Values: Low, Medium, High, Critical
│   │   │   │
│   │   │   ├── TicketStatus.cs
│   │   │   │   └── Values: Open, InProgress, Resolved, Closed
│   │   │   │
│   │   │   ├── UserRole.cs
│   │   │   │   └── Values: Admin, Technician, EndUser
│   │   │   │
│   │   │   └── Department.cs
│   │   │       └── Values: ITNetworking, ITHelp, ITSecurity, ITInfrastructure
│   │   │
│   │   ├── Interfaces/
│   │   │   ├── ITicketRepository.cs
│   │   │   │   └── Defines: GetById(), GetAllActive(), GetAssignedTo(), Create(), Update(), Delete()
│   │   │   │
│   │   │   ├── IUserRepository.cs
│   │   │   │   └── Defines: GetById(), GetByEmail(), GetAllActive(), Create(), Update()
│   │   │   │
│   │   │   └── IUnitOfWork.cs
│   │   │       └── Coordinates transactions across multiple repositories
│   │   │
│   │   └── Exceptions/
│   │       ├── TicketNotFoundException.cs
│   │       ├── UserNotActiveException.cs
│   │       ├── InvalidTicketStatusException.cs
│   │       └── UnauthorizedTicketAccessException.cs
│   │
│   ├── TicketSystem.Infrastructure/    # Database and external services
│   │   ├── TicketSystem.Infrastructure.csproj
│   │   │
│   │   ├── Persistence/
│   │   │   ├── TicketDbContext.cs
│   │   │   │   └── Entity Framework DbContext configuration
│   │   │   │   └── DbSet<Ticket>, DbSet<User>, DbSet<TicketNote>
│   │   │   │
│   │   │   ├── Migrations/
│   │   │   │   ├── 20240115_InitialCreate.cs
│   │   │   │   ├── 20240120_AddSoftDelete.cs
│   │   │   │   └── ...
│   │   │   │
│   │   │   ├── SeedData.cs
│   │   │   │   └── Creates sample users, departments, and tickets for development
│   │   │   │
│   │   │   └── Configuration/
│   │   │       ├── TicketConfiguration.cs  (EF Core fluent configuration)
│   │   │       ├── UserConfiguration.cs
│   │   │       └── TicketNoteConfiguration.cs
│   │   │
│   │   ├── Repositories/
│   │   │   ├── TicketRepository.cs
│   │   │   │   └── Implements ITicketRepository; queries TicketDbContext
│   │   │   │
│   │   │   ├── UserRepository.cs
│   │   │   │   └── Implements IUserRepository; queries TicketDbContext
│   │   │   │
│   │   │   └── UnitOfWork.cs
│   │   │       └── Coordinates multiple repositories, ensures transaction consistency
│   │   │
│   │   └── Services/
│   │       ├── JwtTokenService.cs
│   │       │   └── Generates and validates JWT tokens
│   │       │
│   │       ├── PasswordHasher.cs
│   │       │   └── Hash passwords using PBKDF2; validate during login
│   │       │
│   │       └── NotificationService.cs
│   │           └── Send email notifications (future: SMS, Slack)
│   │
│   └── TicketSystem.Tests/             # Unit and integration tests
│       ├── TicketSystem.Tests.csproj
│       │
│       ├── Unit/
│       │   ├── Handlers/
│       │   │   ├── CreateTicketCommandHandlerTests.cs
│       │   │   ├── AssignTicketCommandHandlerTests.cs
│       │   │   ├── ResolveTicketCommandHandlerTests.cs
│       │   │   └── GetTicketsForTechnicianQueryHandlerTests.cs
│       │   │
│       │   ├── Validators/
│       │   │   ├── CreateTicketValidatorTests.cs
│       │   │   └── AssignTicketValidatorTests.cs
│       │   │
│       │   └── Entities/
│       │       ├── TicketTests.cs
│       │       └── UserTests.cs
│       │
│       └── Integration/
│           ├── API/
│           │   ├── TicketsEndpointTests.cs
│           │   ├── UsersEndpointTests.cs
│           │   └── AuthEndpointTests.cs
│           │
│           └── Repositories/
│               ├── TicketRepositoryTests.cs
│               └── UserRepositoryTests.cs
│
├── frontend/                          # React 18 + TypeScript single-page application
│   ├── package.json                   # Dependencies: react, axios, react-query, tailwind
│   ├── package-lock.json              # Locked dependency versions
│   ├── tsconfig.json                  # TypeScript configuration
│   ├── vite.config.ts                 # Vite bundler configuration
│   ├── .env.example                   # Template for environment variables
│   ├── Dockerfile                     # Container image for frontend
│   │
│   ├── public/
│   │   └── index.html                 # Main HTML file (single entry point)
│   │
│   └── src/
│       ├── main.tsx                   # React app entry point
│       ├── App.tsx                    # Root component (router)
│       │
│       ├── pages/
│       │   ├── DashboardPage.tsx
│       │   │   └── KPI cards (Open, InProgress, Resolved, Closed counts)
│       │   │   └── Charts (Tickets by Priority, by Status)
│       │   │   └── Recently updated tickets
│       │   │
│       │   ├── TicketListPage.tsx
│       │   │   └── Searchable, filterable, paginated table of all tickets
│       │   │   └── Filter by status, priority, assigned technician
│       │   │   └── Link to create new ticket
│       │   │
│       │   ├── TicketDetailPage.tsx
│       │   │   └── Full ticket details
│       │   │   └── Form to edit ticket (title, description, priority)
│       │   │   └── Assign ticket to technician
│       │   │   └── Notes/comments section (audit trail)
│       │   │
│       │   ├── CreateTicketPage.tsx
│       │   │   └── Form to create new ticket
│       │   │   └── Validate required fields on submit
│       │   │
│       │   ├── UserListPage.tsx
│       │   │   └── List all technicians and end-users
│       │   │   └── Show role, department, active status
│       │   │
│       │   └── LoginPage.tsx
│       │       └── Email + password form
│       │       └── Submit to /api/auth/login
│       │
│       ├── components/
│       │   ├── Layout/
│       │   │   ├── Header.tsx (navigation, user dropdown, logout)
│       │   │   ├── Sidebar.tsx (main menu)
│       │   │   └── Layout.tsx (header + sidebar + content area)
│       │   │
│       │   ├── Ticket/
│       │   │   ├── TicketTable.tsx (sortable, filterable columns)
│       │   │   ├── TicketForm.tsx (controlled form with validation)
│       │   │   ├── TicketCard.tsx (summary card view)
│       │   │   └── AssignmentModal.tsx (assign ticket to technician)
│       │   │
│       │   ├── Common/
│       │   │   ├── Button.tsx (styled button)
│       │   │   ├── Modal.tsx (modal dialog)
│       │   │   ├── Spinner.tsx (loading indicator)
│       │   │   ├── ErrorBoundary.tsx (catches React errors)
│       │   │   └── Toast.tsx (success/error messages)
│       │   │
│       │   └── Dashboard/
│       │       ├── KpiCard.tsx (display single metric)
│       │       ├── TicketChart.tsx (bar or pie chart)
│       │       └── Dashboard.tsx (orchestrates dashboard)
│       │
│       ├── hooks/
│       │   ├── useTickets.ts
│       │   │   └── Fetch tickets, filter, paginate (uses React Query)
│       │   │
│       │   ├── useUsers.ts
│       │   │   └── Fetch users by role, department (uses React Query)
│       │   │
│       │   ├── useAuth.ts
│       │   │   └── Login, logout, store/retrieve token
│       │   │
│       │   └── useForm.ts
│       │       └── Reusable form state management
│       │
│       ├── services/
│       │   ├── api.ts
│       │   │   └── Axios instance with JWT interceptor
│       │   │   └── Automatically attaches token to every request
│       │   │
│       │   ├── ticketService.ts
│       │   │   └── API calls: createTicket(), updateTicket(), assignTicket()
│       │   │
│       │   ├── userService.ts
│       │   │   └── API calls: getUsers(), getUserById()
│       │   │
│       │   └── authService.ts
│       │       └── API calls: login(), logout()
│       │
│       ├── types/
│       │   ├── ticket.ts
│       │   │   └── Interface Ticket (mirrors backend domain model)
│       │   │   └── Types TicketPriority, TicketStatus
│       │   │
│       │   ├── user.ts
│       │   │   └── Interface User
│       │   │   └── Type UserRole
│       │   │
│       │   └── api.ts
│       │       └── Generic API response types
│       │
│       ├── context/
│       │   ├── AuthContext.tsx
│       │   │   └── Provides current user and login state to all components
│       │   │
│       │   └── ToastContext.tsx
│       │       └── Provides toast notification system
│       │
│       ├── utils/
│       │   ├── formatters.ts
│       │   │   └── Format dates, ticket status text, priority labels
│       │   │
│       │   ├── validators.ts
│       │   │   └── Form validation rules (email, required fields, etc.)
│       │   │
│       │   └── constants.ts
│       │       └── API endpoints, role permissions, color mappings
│       │
│       └── styles/
│           └── globals.css (Tailwind CSS, global resets)
│
└── .github/
    └── workflows/
        └── ci.yml                     # GitHub Actions CI/CD pipeline
            ├── Trigger: on push, pull_request
            ├── Jobs: build backend, build frontend, run tests, build docker
            └── Deploy on success
```

---

## Folder Purposes Explained

### Backend Structure: Why It's Organized This Way

**Controllers** are HTTP handlers only. They receive a request, delegate to MediatR, and return a response. No business logic lives here.

**Commands & Queries** represent operations. A command modifies state (CreateTicketCommand). A query retrieves data (GetTicketsForTechnicianQuery). This separation makes intent clear.

**Handlers** contain the business logic for each command/query. One handler per operation. Each handler is small, testable, and focused.

**DTOs** (Data Transfer Objects) are the API contracts. A TicketCreateRequest defines what the client must send. A TicketResponse defines what the API returns. DTOs decouple the API from domain entities.

**Validators** enforce domain constraints. CreateTicketValidator checks that title is not empty, priority is valid, etc. Validation happens before the handler runs.

**Domain** entities model the core business. A Ticket knows how to assign itself to a technician and validate that it's open. A User knows its role and whether it's active. This layer has no dependencies on HTTP, databases, or frameworks.

**Repositories** implement the ITicketRepository and IUserRepository interfaces defined in Domain. They talk to the database using Entity Framework Core. The domain layer doesn't know they exist.

**Services** (JWT, PasswordHasher, NotificationService) encapsulate cross-cutting concerns. JwtTokenService generates authentication tokens. PasswordHasher hashes user passwords. NotificationService sends emails.

**Tests** follow the same structure as the production code. Tests for CreateTicketCommandHandler live in Handlers/CreateTicketCommandHandlerTests.cs. This makes tests easy to find.

### Frontend Structure: Why It's Organized This Way

**Pages** are full-screen components (LoginPage, TicketListPage, DashboardPage). Each page is a route.

**Components** are reusable UI pieces. TicketTable displays a list of tickets. TicketForm is a controlled form for creating or editing. Common components (Button, Modal, Spinner) are used throughout.

**Hooks** encapsulate data fetching and state management. useTickets() fetches tickets from the API using React Query. useAuth() manages login state. Custom hooks make components simpler and more testable.

**Services** are API clients. ticketService.createTicket() calls POST /api/tickets. These are thin wrappers around axios.

**Types** are TypeScript interfaces. They mirror the backend domain models (Ticket, User, etc.). The frontend and backend both use TicketPriority: 'Low' | 'Medium' | 'High' | 'Critical'.

**Context** provides global state. AuthContext provides the current user to all components. ToastContext provides a notification system.

**Utils** are helper functions. formatDate() formats a timestamp for display. validateEmail() validates an email address. These are pure functions with no side effects.

---

## Implementation Order (Weeks 1-4)

### Why This Order?

The order is chosen to maximize business value and enable parallel work:

1. Start with **domain entities** so the team agrees on the data model
2. Build **repository interfaces** so frontend can start with mocked data
3. Implement **handlers** so the API works end-to-end (even if frontend isn't ready)
4. Write **tests** to ensure reliability as complexity grows
5. Build **frontend** once the API is stable
6. Add **polish** (error handling, logging, documentation)
7. Deploy and iterate

### Week 1: Core Backend and Data Model

**Goal:** Working API with real database. Front-end team can start with mock data.

**Backend Tasks:**

1. Create the five projects (Domain, Application, Infrastructure, API, Tests)
2. Define domain entities:
   - `Ticket` with properties (Id, Title, Description, Priority, Status, AssignedToUserId, CreatedAt, UpdatedAt, ResolvedAt, DeletedAt)
   - `User` with properties (Id, Email, FirstName, LastName, Role, Department, IsActive, CreatedAt, UpdatedAt)
   - `TicketNote` with properties (Id, TicketId, AuthorId, Content, CreatedAt)
   - Enums: TicketPriority, TicketStatus, UserRole, Department

3. Create DbContext (TicketDbContext) in Infrastructure
4. Create first migration: `dotnet ef migrations add InitialCreate`
5. Implement repositories:
   - TicketRepository: GetById(), GetAll(), GetAssignedTo(), Create(), Update()
   - UserRepository: GetById(), GetByEmail(), GetAll(), Create()

6. Implement MediatR handlers for basic CRUD:
   - CreateTicketCommandHandler
   - GetTicketByIdQueryHandler
   - GetTicketsQueryHandler (with filtering, sorting, pagination)
   - UpdateTicketCommandHandler
   - AssignTicketCommandHandler
   - ResolveTicketCommandHandler

7. Build controllers to expose handlers:
   - TicketsController: GET /api/tickets, GET /api/tickets/{id}, POST /api/tickets, PUT /api/tickets/{id}
   - UsersController: GET /api/users

8. Seed sample data (10 tickets, 5 users) in SeedData.cs
9. Test endpoints manually with Postman

**Success Criteria:**
- API runs: `dotnet run` from backend/TicketSystem.API
- Endpoints work: GET /api/tickets returns list, POST /api/tickets creates one
- Database contains seeded data
- No null reference exceptions

### Week 1-2: Backend Polish and Testing

**Goal:** Production-quality API with validation, auth, and tests.

**Backend Tasks:**

1. Add authentication:
   - JwtTokenService: Generate tokens on login
   - AuthController: POST /api/auth/login with email/password
   - Middleware to validate JWT on protected endpoints

2. Add validation:
   - CreateTicketValidator: title required, description optional, priority valid
   - AssignTicketValidator: technician must exist and be active
   - Apply validators in handlers

3. Add error handling:
   - Global exception middleware (catch unhandled exceptions, log, return 500)
   - Domain exceptions (TicketNotFoundException, InvalidTicketStatusException)
   - Return appropriate HTTP status codes (200, 201, 400, 404, 500)

4. Write unit tests (target 70%+ coverage):
   - CreateTicketCommandHandlerTests: success path, validation failure, duplicate title
   - AssignTicketCommandHandlerTests: success, ticket not open, technician not active
   - GetTicketsQueryHandlerTests: filtering, pagination
   - CreateTicketValidatorTests: valid input, empty title, invalid priority

5. Add logging:
   - Log ticket creation, assignment, resolution at INFO level
   - Log errors at ERROR level
   - Include context (ticket ID, user ID, timestamp)

6. Document API with Swagger:
   - Controllers already generate Swagger docs
   - Test at http://localhost:5000/swagger

7. Add CORS configuration for localhost:3000

**Success Criteria:**
- All tests pass: `dotnet test`
- Coverage report shows >70% coverage on handlers
- Swagger UI shows all endpoints
- Login endpoint returns JWT token
- Protected endpoints reject requests without token

### Week 2-3: React Frontend

**Goal:** Fully functional UI consuming the API.

**Frontend Tasks:**

1. Set up React + Vite + TypeScript:
   - `npm create vite@latest frontend -- --template react-ts`
   - Install dependencies: axios, @tanstack/react-query, react-hook-form, zod, tailwindcss

2. Create types (mirror backend):
   ```typescript
   interface Ticket {
     id: string;
     title: string;
     priority: 'Low' | 'Medium' | 'High' | 'Critical';
     status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
     assignedToTechnicianId?: string;
     createdAt: string;
   }
   ```

3. Create API client (services/api.ts):
   - Axios instance with JWT interceptor
   - Error handling

4. Create custom hooks:
   - useAuth(): login, logout, get current user
   - useTickets(): fetch, filter, paginate tickets
   - useUsers(): fetch users by role

5. Create pages:
   - LoginPage: form for email/password
   - DashboardPage: KPI cards, charts
   - TicketListPage: table with filtering
   - TicketDetailPage: full details, edit form, notes
   - CreateTicketPage: form to create ticket

6. Create reusable components:
   - Header: navigation, user dropdown
   - Sidebar: main menu
   - TicketTable: sortable, filterable table
   - TicketForm: controlled form with validation
   - Modal: generic modal dialog
   - Spinner: loading indicator

7. Style with Tailwind CSS:
   - Make responsive (mobile 375px, tablet 768px, desktop 1920px)
   - Dark mode optional (can add later)

8. Test critical components:
   - TicketTable: renders rows, sorting works, filtering works
   - TicketForm: validation works, submit sends data
   - Authentication: login redirects to dashboard

**Success Criteria:**
- All pages load without errors
- Forms validate input (red border on empty required fields)
- Charts render with sample data
- Responsive on mobile and desktop
- Can create, edit, view, delete tickets
- Can assign tickets

### Week 3-4: Deployment and Polish

**Goal:** Live deployment, documentation, CI/CD pipeline.

**Deployment Tasks:**

1. Docker:
   - Write Dockerfile for backend (ASP.NET Core)
   - Write Dockerfile for frontend (Node build → Nginx serve)
   - Create docker-compose.yml with SQL Server + API + frontend

2. GitHub Actions:
   - Create .github/workflows/ci.yml
   - Build backend: restore, build, test
   - Build frontend: install, build, test
   - Build Docker images
   - Push to registry on main branch

3. Cloud deployment (choose one):
   - **Render.com:** Create web service, connect GitHub repo, deploy
   - **Railway.app:** Create service, connect GitHub repo, deploy
   - **Azure:** Create App Service, deploy container

4. Documentation:
   - README.md: project overview, tech stack, getting started, architecture diagram
   - ARCHITECTURE.md: design decisions, patterns, trade-offs
   - Contributing guidelines: how to add features, branch naming, commit conventions
   - API documentation: link to Swagger UI

5. Polish:
   - Add error toasts (success/error messages)
   - Add loading states (spinners during API calls)
   - Add success messages (ticket created, assigned, etc.)
   - Clean up console warnings
   - Add GitHub Projects board (track issues, features, bugs)

6. Security audit:
   - No hardcoded secrets (use environment variables)
   - JWT secret is strong and unique per environment
   - CORS allows only frontend domain
   - SQL Server password is changed from default
   - No sensitive data in logs

**Success Criteria:**
- `docker-compose up` starts entire stack
- Frontend accessible at http://localhost:3000
- API accessible at http://localhost:5000
- GitHub Actions passes on every commit
- Live deployment URL works
- README is clear and professional

---

## Creating the Backend from Scratch

These commands create the five projects and wire them together.

```bash
mkdir backend
cd backend

# Create solution
dotnet new sln -n TicketSystem

# Create five projects
dotnet new classlib -n TicketSystem.Domain -f net8.0
dotnet new classlib -n TicketSystem.Application -f net8.0
dotnet new classlib -n TicketSystem.Infrastructure -f net8.0
dotnet new webapi -n TicketSystem.API -f net8.0
dotnet new xunit -n TicketSystem.Tests -f net8.0

# Add to solution
dotnet sln add TicketSystem.Domain
dotnet sln add TicketSystem.Application
dotnet sln add TicketSystem.Infrastructure
dotnet sln add TicketSystem.API
dotnet sln add TicketSystem.Tests

# Wire project references (dependencies point inward)
dotnet add TicketSystem.Application reference TicketSystem.Domain
dotnet add TicketSystem.Infrastructure reference TicketSystem.Domain
dotnet add TicketSystem.Infrastructure reference TicketSystem.Application
dotnet add TicketSystem.API reference TicketSystem.Application
dotnet add TicketSystem.API reference TicketSystem.Infrastructure
dotnet add TicketSystem.Tests reference TicketSystem.Application
dotnet add TicketSystem.Tests reference TicketSystem.Infrastructure

# Install common NuGet packages
cd TicketSystem.API
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package MediatR
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection
dotnet add package FluentValidation.DependencyInjectionExtensions
cd ../TicketSystem.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
cd ../TicketSystem.Tests
dotnet add package Moq
dotnet add package FluentAssertions

# Build to check for errors
cd ..
dotnet build
```

If the build succeeds, you have a working foundation.

---

## Creating the Frontend from Scratch

```bash
# Create React + TypeScript project with Vite
npm create vite@latest frontend -- --template react-ts

cd frontend
npm install

# Install libraries
npm install axios @tanstack/react-query react-hook-form zod
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

# Build and test
npm run build
npm test

# Start dev server
npm run dev
# http://localhost:5173
```

---

## File Naming Conventions

### Backend

- **Entities:** PascalCase, singular (Ticket.cs, User.cs, TicketNote.cs)
- **Enums:** PascalCase, singular (TicketPriority.cs, TicketStatus.cs)
- **Commands:** PascalCase, ends with "Command" (CreateTicketCommand.cs)
- **Queries:** PascalCase, ends with "Query" (GetTicketsQuery.cs)
- **Handlers:** PascalCase, ends with "Handler" (CreateTicketCommandHandler.cs)
- **Validators:** PascalCase, ends with "Validator" (CreateTicketValidator.cs)
- **Repositories:** PascalCase, ends with "Repository" (TicketRepository.cs)
- **DTOs:** PascalCase, descriptive (CreateTicketRequest.cs, TicketResponse.cs)

### Frontend

- **Pages:** PascalCase, ends with "Page" (TicketListPage.tsx)
- **Components:** PascalCase (TicketTable.tsx, Header.tsx, Button.tsx)
- **Hooks:** camelCase, starts with "use" (useTickets.ts, useAuth.ts)
- **Services:** camelCase (ticketService.ts, authService.ts)
- **Types:** camelCase, descriptive (ticket.ts, user.ts, api.ts)
- **Utilities:** camelCase (formatters.ts, validators.ts, constants.ts)

---

## Key Dependencies

### Backend NuGet Packages

| Package | Purpose |
|---|---|
| EntityFrameworkCore 8.0 | ORM for SQL Server |
| EntityFrameworkCore.SqlServer 8.0 | SQL Server provider |
| MediatR | Command/Query pattern |
| AutoMapper | Entity → DTO mapping |
| FluentValidation | Input validation |
| Microsoft.IdentityModel.Tokens | JWT validation |

### Frontend npm Packages

| Package | Purpose |
|---|---|
| axios | HTTP client |
| @tanstack/react-query | Server state management |
| react-hook-form | Form state and validation |
| zod | Schema validation |
| tailwindcss | CSS framework |
| react-router-dom | Client-side routing |

---

## Git Workflow

**Branch naming:**
```
main                 # Production-ready code
develop              # Integration branch
feature/auth         # New feature (checkout from develop)
feature/dashboard    # New feature
bugfix/soft-delete   # Bug fix (if urgent)
```

**Commit message format:**
```
feat(api): implement JWT authentication
  Add JwtTokenService for token generation and validation.
  Protect API endpoints with [Authorize] attribute.
  Tests: JwtTokenServiceTests with valid/expired tokens.

fix(frontend): resolve TicketTable sort order bug
  Tickets now sort by priority descending, then by creation date.
  Added test: TicketTable sorts correctly on header click.

test(handlers): add unit tests for GetTicketsQuery
  Tests cover filtering by status, priority, pagination.
  Coverage: 100% of query handler logic.
```

**Pull request template:**
```
## Description
Brief explanation of what this PR does.

## Type of Change
- [ ] New feature
- [ ] Bug fix
- [ ] Refactoring
- [ ] Documentation

## Testing
- [ ] Unit tests written
- [ ] Manual testing completed

## Screenshots (if UI change)
[Paste screenshots here]

## Checklist
- [ ] Code follows style guide
- [ ] Tests pass (`dotnet test`, `npm test`)
- [ ] No hardcoded secrets
```

---

## What's Checked Into Git vs. Generated

**Checked in:**
- Source code (.cs, .tsx, .ts, .json)
- Configuration (appsettings.json, vite.config.ts)
- Documentation (README.md, ARCHITECTURE.md)
- Tests
- Migrations (Migrations/ folder)

**Not checked in (add to .gitignore):**
- node_modules/ (too large)
- bin/, obj/ (generated during build)
- .env (contains secrets)
- dist/, build/ (generated during build)
- .vs/ (Visual Studio cache)

