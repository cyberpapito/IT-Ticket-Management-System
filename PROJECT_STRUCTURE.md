# Project Structure Setup Guide

Initial directory layout for IT Ticket Management System.

---

## 📁 Complete Repository Structure

```
IT-Ticket-Management-System/
│
├── 📄 README.md                          ← Project overview (START HERE)
├── 📄 ARCHITECTURE.md                    ← Design decisions
├── 📄 TECHNICAL_TERMS_CHEAT_SHEET.md    ← Study guide
├── 📄 PROJECT_STRUCTURE.md              ← This file
├── 📄 .gitignore                         ← Git ignore rules
├── 📄 LICENSE                            ← MIT License
│
├── 🐳 docker-compose.yml                 ← Local dev setup (all containers)
│
├── 📂 backend/                           ← ASP.NET Core API
│   │
│   ├── 📄 TicketSystem.sln              ← .NET solution file (all projects)
│   │
│   ├── 📂 TicketSystem.API/             ← HTTP layer
│   │   ├── Program.cs                   ← Startup, middleware config
│   │   ├── appsettings.json             ← Config (DB connection, JWT)
│   │   ├── Dockerfile                   ← Container image for API
│   │   └── 📂 Controllers/
│   │       ├── TicketsController.cs
│   │       ├── UsersController.cs
│   │       └── AuthController.cs
│   │
│   ├── 📂 TicketSystem.Application/     ← Business logic layer
│   │   ├── 📂 Commands/
│   │   │   ├── CreateTicketCommand.cs
│   │   │   ├── UpdateTicketCommand.cs
│   │   │   └── DeleteTicketCommand.cs
│   │   ├── 📂 Queries/
│   │   │   ├── GetTicketsQuery.cs
│   │   │   └── GetTicketByIdQuery.cs
│   │   ├── 📂 Handlers/
│   │   │   ├── CreateTicketCommandHandler.cs
│   │   │   └── GetTicketsQueryHandler.cs
│   │   ├── 📂 DTOs/
│   │   │   ├── CreateTicketRequest.cs
│   │   │   ├── TicketDto.cs
│   │   │   └── UserDto.cs
│   │   ├── 📂 Validators/
│   │   │   ├── CreateTicketValidator.cs
│   │   │   └── UpdateTicketValidator.cs
│   │   ├── 📂 Mappings/
│   │   │   └── TicketMappingProfile.cs  ← AutoMapper config
│   │   └── ServiceCollectionExtensions.cs
│   │
│   ├── 📂 TicketSystem.Domain/          ← Core business entities
│   │   ├── 📂 Entities/
│   │   │   ├── Ticket.cs
│   │   │   ├── User.cs
│   │   │   └── TicketNote.cs
│   │   ├── 📂 Enums/
│   │   │   ├── TicketPriority.cs
│   │   │   └── TicketStatus.cs
│   │   ├── 📂 Interfaces/
│   │   │   ├── ITicketRepository.cs
│   │   │   ├── IUserRepository.cs
│   │   │   └── IUnitOfWork.cs
│   │   └── 📂 Exceptions/
│   │       └── TicketNotFoundException.cs
│   │
│   ├── 📂 TicketSystem.Infrastructure/  ← Data access layer
│   │   ├── 📂 Persistence/
│   │   │   ├── TicketDbContext.cs       ← Entity Framework DbContext
│   │   │   ├── 📂 Migrations/          ← Database version control
│   │   │   │   ├── 20240101_InitialCreate.cs
│   │   │   │   └── TicketDbContextModelSnapshot.cs
│   │   │   └── SeedData.cs              ← Sample data
│   │   ├── 📂 Repositories/
│   │   │   ├── TicketRepository.cs
│   │   │   ├── UserRepository.cs
│   │   │   └── UnitOfWork.cs
│   │   ├── 📂 Services/
│   │   │   ├── JwtTokenService.cs       ← Generate JWT tokens
│   │   │   └── PasswordHasher.cs
│   │   └── ServiceCollectionExtensions.cs
│   │
│   ├── 📂 TicketSystem.Tests/           ← Unit tests (xUnit)
│   │   ├── 📂 Handlers/
│   │   │   ├── CreateTicketCommandHandlerTests.cs
│   │   │   └── GetTicketsQueryHandlerTests.cs
│   │   ├── 📂 Validators/
│   │   │   └── CreateTicketValidatorTests.cs
│   │   ├── 📂 Repositories/
│   │   │   └── TicketRepositoryTests.cs
│   │   ├── Fixtures/
│   │   │   └── TestDataFixture.cs       ← Mock data for tests
│   │   └── TicketSystem.Tests.csproj
│   │
│   └── 📄 .dockerignore                 ← Exclude files from Docker image
│
├── 📂 frontend/                          ← React TypeScript SPA
│   │
│   ├── 📄 package.json                   ← Dependencies (React, Axios, etc.)
│   ├── 📄 package-lock.json             ← Lock file (reproducible installs)
│   ├── 📄 tsconfig.json                 ← TypeScript configuration
│   ├── 📄 vite.config.ts                ← Vite build configuration
│   ├── 📄 .env.example                  ← Example env variables
│   ├── 📄 Dockerfile                    ← Container image for frontend
│   ├── 📄 .dockerignore
│   │
│   ├── 📂 public/                        ← Static assets (favicon, etc.)
│   │   └── index.html
│   │
│   ├── 📂 src/
│   │   │
│   │   ├── 📄 main.tsx                   ← Entry point
│   │   ├── 📄 App.tsx                    ← Root component
│   │   │
│   │   ├── 📂 pages/                     ← Full pages
│   │   │   ├── DashboardPage.tsx
│   │   │   ├── TicketListPage.tsx
│   │   │   ├── TicketDetailPage.tsx
│   │   │   ├── CreateTicketPage.tsx
│   │   │   ├── UserListPage.tsx
│   │   │   └── LoginPage.tsx
│   │   │
│   │   ├── 📂 components/                ← Reusable components
│   │   │   ├── 📂 Layout/
│   │   │   │   ├── Header.tsx
│   │   │   │   ├── Sidebar.tsx
│   │   │   │   └── Layout.tsx
│   │   │   ├── 📂 Ticket/
│   │   │   │   ├── TicketTable.tsx
│   │   │   │   ├── TicketForm.tsx
│   │   │   │   └── TicketCard.tsx
│   │   │   ├── 📂 Common/
│   │   │   │   ├── Button.tsx
│   │   │   │   ├── Modal.tsx
│   │   │   │   ├── Spinner.tsx
│   │   │   │   ├── ErrorBoundary.tsx
│   │   │   │   └── Toast.tsx
│   │   │   └── 📂 Dashboard/
│   │   │       ├── KpiCard.tsx
│   │   │       ├── TicketChart.tsx
│   │   │       └── Dashboard.tsx
│   │   │
│   │   ├── 📂 hooks/                    ← Custom React hooks
│   │   │   ├── useTickets.ts            ← Fetch tickets
│   │   │   ├── useUsers.ts              ← Fetch users
│   │   │   ├── useAuth.ts               ← Auth logic
│   │   │   └── useApi.ts                ← Generic API calls
│   │   │
│   │   ├── 📂 services/                 ← API clients
│   │   │   ├── api.ts                   ← Axios instance + interceptors
│   │   │   ├── ticketService.ts         ← Ticket API calls
│   │   │   ├── userService.ts           ← User API calls
│   │   │   ├── authService.ts           ← Auth API calls
│   │   │   └── tokenService.ts          ← JWT token management
│   │   │
│   │   ├── 📂 types/                    ← TypeScript interfaces
│   │   │   ├── index.ts                 ← Export all types
│   │   │   ├── ticket.ts
│   │   │   ├── user.ts
│   │   │   └── api.ts
│   │   │
│   │   ├── 📂 context/                  ← React Context
│   │   │   ├── AuthContext.tsx
│   │   │   └── ToastContext.tsx
│   │   │
│   │   ├── 📂 utils/                    ← Helper functions
│   │   │   ├── formatters.ts
│   │   │   ├── validators.ts
│   │   │   └── constants.ts
│   │   │
│   │   ├── 📂 styles/
│   │   │   └── globals.css              ← Global Tailwind styles
│   │   │
│   │   └── 📂 __tests__/                ← Component tests (Vitest)
│   │       ├── components/
│   │       │   └── TicketTable.test.tsx
│   │       ├── hooks/
│   │       │   └── useTickets.test.ts
│   │       └── services/
│   │           └── ticketService.test.ts
│   │
│   └── 📄 nginx.conf                    ← Nginx config (production)
│
├── 📂 .github/
│   └── 📂 workflows/
│       ├── ci.yml                       ← Build + test on every push
│       ├── deploy.yml                   ← Deploy to Render
│       └── codeql-analysis.yml          ← Security scanning
│
└── 📂 docs/                              ← Documentation (optional)
    ├── API.md
    ├── DEPLOYMENT.md
    ├── CONTRIBUTING.md
    └── TROUBLESHOOTING.md
```

---

## 🚀 Quick Start: Creating Folders

### 1. Create Repository on GitHub

```bash
# On GitHub.com:
# 1. Click "New repository"
# 2. Name: IT-Ticket-Management-System
# 3. Description: Full-stack IT ticket management system
# 4. Public (portfolio)
# 5. Initialize with README, .gitignore, License
# 6. Copy HTTPS clone URL
```

### 2. Clone Locally

```bash
git clone https://github.com/yourusername/IT-Ticket-Management-System.git
cd IT-Ticket-Management-System
```

### 3. Create Backend Structure

```bash
# In project root
mkdir -p backend
cd backend

# Create .NET solution
dotnet new sln -n TicketSystem

# Create projects
dotnet new classlib -n TicketSystem.Domain
dotnet new classlib -n TicketSystem.Application
dotnet new classlib -n TicketSystem.Infrastructure
dotnet new webapi -n TicketSystem.API
dotnet new xunit -n TicketSystem.Tests

# Add projects to solution
dotnet sln TicketSystem.sln add TicketSystem.Domain/TicketSystem.Domain.csproj
dotnet sln TicketSystem.sln add TicketSystem.Application/TicketSystem.Application.csproj
dotnet sln TicketSystem.sln add TicketSystem.Infrastructure/TicketSystem.Infrastructure.csproj
dotnet sln TicketSystem.sln add TicketSystem.API/TicketSystem.API.csproj
dotnet sln TicketSystem.sln add TicketSystem.Tests/TicketSystem.Tests.csproj

# Add project references
cd TicketSystem.Application
dotnet add reference ../TicketSystem.Domain/TicketSystem.Domain.csproj
cd ../TicketSystem.Infrastructure
dotnet add reference ../TicketSystem.Domain/TicketSystem.Domain.csproj
dotnet add reference ../TicketSystem.Application/TicketSystem.Application.csproj
cd ../TicketSystem.API
dotnet add reference ../TicketSystem.Application/TicketSystem.Application.csproj
cd ../TicketSystem.Tests
dotnet add reference ../TicketSystem.Application/TicketSystem.Application.csproj
cd ..
```

### 4. Create Frontend Structure

```bash
# In project root
cd ..
npm create vite@latest frontend -- --template react-ts
cd frontend

# Install dependencies
npm install

# Add common libraries
npm install axios react-query zod react-hook-form
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p

cd ..
```

### 5. Copy README, ARCHITECTURE, etc.

```bash
# These files already created:
# - README.md
# - ARCHITECTURE.md
# - TECHNICAL_TERMS_CHEAT_SHEET.md
# - .gitignore
# - LICENSE (create MIT)
# - docker-compose.yml
# - docker-compose files in backend/TicketSystem.API/
# - docker-compose files in frontend/
```

---

## 📝 Initial Commit

```bash
# Check status
git status

# Add all files
git add .

# Create initial commit
git commit -m "chore: initialize project structure with README, ARCHITECTURE, and documentation"

# Push to GitHub
git push origin main
```

---

## 📊 Folder Purposes

| Folder | Purpose | Files |
|--------|---------|-------|
| **backend/** | ASP.NET Core API | Controllers, Models, DbContext |
| **backend/TicketSystem.API/** | HTTP entry point | Program.cs, controllers |
| **backend/TicketSystem.Application/** | Business logic | Commands, Queries, Handlers |
| **backend/TicketSystem.Domain/** | Core entities | Entities, Value Objects, Interfaces |
| **backend/TicketSystem.Infrastructure/** | Data access | DbContext, Repositories, Migrations |
| **backend/TicketSystem.Tests/** | Unit tests | xUnit test classes |
| **frontend/** | React SPA | Components, hooks, pages |
| **frontend/src/pages/** | Full pages | Login, Dashboard, Tickets |
| **frontend/src/components/** | Reusable UI | Button, Table, Form, Modal |
| **frontend/src/services/** | API clients | axios instances, call functions |
| **frontend/src/hooks/** | Custom logic | useTickets, useAuth, useApi |
| **frontend/src/types/** | TypeScript | Interface definitions |
| **.github/workflows/** | CI/CD | GitHub Actions automation |

---

## 🎯 Next Steps

1. **Backend (Week 1):**
   - [ ] Create database schema (Ticket, User entities)
   - [ ] Write EF Core migrations
   - [ ] Implement repositories
   - [ ] Build MediatR handlers for CRUD
   - [ ] Add validators
   - [ ] Seed sample data
   - [ ] Test endpoints in Postman

2. **Testing (Week 1-2):**
   - [ ] Write handler tests
   - [ ] Write validator tests
   - [ ] Achieve 70%+ coverage
   - [ ] Add integration tests

3. **Frontend (Week 2-3):**
   - [ ] Create pages (List, Detail, Create)
   - [ ] Build reusable components
   - [ ] Implement hooks for API calls
   - [ ] Add authentication flow
   - [ ] Create dashboard with charts
   - [ ] Style with Tailwind

4. **DevOps (Week 3-4):**
   - [ ] Write Dockerfiles
   - [ ] Create docker-compose.yml
   - [ ] Set up GitHub Actions CI/CD
   - [ ] Deploy to Render.com
   - [ ] Document everything

---

**Status:** ✅ Ready to code  
**Last Updated:** June 2026
