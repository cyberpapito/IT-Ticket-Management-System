# Project Structure

The folder layout for the IT Ticket Management System and the commands used to create it.

---

## Repository Layout

```
IT-Ticket-Management-System/
├── README.md
├── ARCHITECTURE.md
├── PROJECT_STRUCTURE.md
├── .gitignore
├── LICENSE
├── docker-compose.yml
│
├── backend/
│   ├── TicketSystem.sln
│   │
│   ├── TicketSystem.API/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   └── Controllers/
│   │       ├── TicketsController.cs
│   │       ├── UsersController.cs
│   │       └── AuthController.cs
│   │
│   ├── TicketSystem.Application/
│   │   ├── Commands/
│   │   │   ├── CreateTicketCommand.cs
│   │   │   ├── UpdateTicketCommand.cs
│   │   │   └── DeleteTicketCommand.cs
│   │   ├── Queries/
│   │   │   ├── GetTicketsQuery.cs
│   │   │   └── GetTicketByIdQuery.cs
│   │   ├── Handlers/
│   │   │   ├── CreateTicketCommandHandler.cs
│   │   │   └── GetTicketsQueryHandler.cs
│   │   ├── DTOs/
│   │   │   ├── CreateTicketRequest.cs
│   │   │   ├── TicketDto.cs
│   │   │   └── UserDto.cs
│   │   ├── Validators/
│   │   │   ├── CreateTicketValidator.cs
│   │   │   └── UpdateTicketValidator.cs
│   │   └── Mappings/
│   │       └── TicketMappingProfile.cs
│   │
│   ├── TicketSystem.Domain/
│   │   ├── Entities/
│   │   │   ├── Ticket.cs
│   │   │   ├── User.cs
│   │   │   └── TicketNote.cs
│   │   ├── Enums/
│   │   │   ├── TicketPriority.cs
│   │   │   └── TicketStatus.cs
│   │   ├── Interfaces/
│   │   │   ├── ITicketRepository.cs
│   │   │   ├── IUserRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   └── Exceptions/
│   │       └── TicketNotFoundException.cs
│   │
│   ├── TicketSystem.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── TicketDbContext.cs
│   │   │   ├── Migrations/
│   │   │   └── SeedData.cs
│   │   ├── Repositories/
│   │   │   ├── TicketRepository.cs
│   │   │   ├── UserRepository.cs
│   │   │   └── UnitOfWork.cs
│   │   └── Services/
│   │       ├── JwtTokenService.cs
│   │       └── PasswordHasher.cs
│   │
│   └── TicketSystem.Tests/
│       ├── Handlers/
│       │   ├── CreateTicketCommandHandlerTests.cs
│       │   └── GetTicketsQueryHandlerTests.cs
│       ├── Validators/
│       │   └── CreateTicketValidatorTests.cs
│       └── Repositories/
│           └── TicketRepositoryTests.cs
│
├── frontend/
│   ├── package.json
│   ├── tsconfig.json
│   ├── vite.config.ts
│   ├── .env.example
│   ├── Dockerfile
│   │
│   ├── public/
│   │   └── index.html
│   │
│   └── src/
│       ├── main.tsx
│       ├── App.tsx
│       │
│       ├── pages/
│       │   ├── DashboardPage.tsx
│       │   ├── TicketListPage.tsx
│       │   ├── TicketDetailPage.tsx
│       │   ├── CreateTicketPage.tsx
│       │   ├── UserListPage.tsx
│       │   └── LoginPage.tsx
│       │
│       ├── components/
│       │   ├── Layout/
│       │   │   ├── Header.tsx
│       │   │   ├── Sidebar.tsx
│       │   │   └── Layout.tsx
│       │   ├── Ticket/
│       │   │   ├── TicketTable.tsx
│       │   │   ├── TicketForm.tsx
│       │   │   └── TicketCard.tsx
│       │   ├── Common/
│       │   │   ├── Button.tsx
│       │   │   ├── Modal.tsx
│       │   │   ├── Spinner.tsx
│       │   │   └── ErrorBoundary.tsx
│       │   └── Dashboard/
│       │       ├── KpiCard.tsx
│       │       ├── TicketChart.tsx
│       │       └── Dashboard.tsx
│       │
│       ├── hooks/
│       │   ├── useTickets.ts
│       │   ├── useUsers.ts
│       │   └── useAuth.ts
│       │
│       ├── services/
│       │   ├── api.ts
│       │   ├── ticketService.ts
│       │   ├── userService.ts
│       │   └── authService.ts
│       │
│       ├── types/
│       │   ├── ticket.ts
│       │   ├── user.ts
│       │   └── api.ts
│       │
│       ├── context/
│       │   ├── AuthContext.tsx
│       │   └── ToastContext.tsx
│       │
│       └── utils/
│           ├── formatters.ts
│           ├── validators.ts
│           └── constants.ts
│
└── .github/
    └── workflows/
        └── ci.yml
```

---

## Folder Purposes

| Folder | Purpose |
|---|---|
| backend/ | The ASP.NET Core API and all its projects |
| backend/TicketSystem.API | HTTP entry point: controllers and startup |
| backend/TicketSystem.Application | Business logic: commands, queries, handlers |
| backend/TicketSystem.Domain | Core entities, enums, and interfaces |
| backend/TicketSystem.Infrastructure | Database access, repositories, migrations |
| backend/TicketSystem.Tests | Unit tests |
| frontend/ | The React single-page application |
| frontend/src/pages | Full page components |
| frontend/src/components | Reusable UI components |
| frontend/src/services | API client code |
| frontend/src/hooks | Custom React hooks |
| frontend/src/types | TypeScript interface definitions |
| .github/workflows | GitHub Actions CI/CD configuration |

---

## Creating the Backend Structure

Run these from the project root.

### Create the backend folder

```bash
mkdir backend
cd backend
```

### Create the solution

```bash
dotnet new sln -n TicketSystem
```

### Create the five projects

```bash
dotnet new classlib -n TicketSystem.Domain -f net8.0
dotnet new classlib -n TicketSystem.Application -f net8.0
dotnet new classlib -n TicketSystem.Infrastructure -f net8.0
dotnet new webapi -n TicketSystem.API -f net8.0
dotnet new xunit -n TicketSystem.Tests -f net8.0
```

### Add the projects to the solution

```bash
dotnet sln add TicketSystem.Domain
dotnet sln add TicketSystem.Application
dotnet sln add TicketSystem.Infrastructure
dotnet sln add TicketSystem.API
dotnet sln add TicketSystem.Tests
```

### Wire up the project references

```bash
dotnet add TicketSystem.Application reference TicketSystem.Domain
dotnet add TicketSystem.Infrastructure reference TicketSystem.Domain
dotnet add TicketSystem.Infrastructure reference TicketSystem.Application
dotnet add TicketSystem.API reference TicketSystem.Application
dotnet add TicketSystem.API reference TicketSystem.Infrastructure
dotnet add TicketSystem.Tests reference TicketSystem.Application
```

### Confirm it builds

```bash
dotnet build
```

A successful run reports "Build succeeded" and produces a .dll for each project under bin/Debug/net8.0/.

---

## Creating the Frontend Structure

Run these from the project root.

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install axios @tanstack/react-query zod react-hook-form
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
```

---

## Implementation Order

### Backend (Week 1)

- Create the Ticket, User, and TicketNote entities in the Domain layer.
- Add the TicketPriority and TicketStatus enums.
- Create the DbContext and write the first migration.
- Implement the repositories.
- Build the MediatR handlers for create, read, update, and delete.
- Add validators.
- Seed sample data.
- Test the endpoints.

### Testing (Week 1-2)

- Write handler tests.
- Write validator tests.
- Add a few integration tests.

### Frontend (Week 2-3)

- Create the pages: list, detail, create.
- Build reusable components.
- Implement hooks for API calls.
- Add the authentication flow.
- Build the dashboard with charts.
- Style with Tailwind.

### Deployment (Week 3-4)

- Write the Dockerfiles.
- Create docker-compose.yml.
- Set up the GitHub Actions workflow.
- Deploy to a free cloud tier.
- Finish the documentation.
