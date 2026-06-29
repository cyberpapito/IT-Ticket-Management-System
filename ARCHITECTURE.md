# Architecture Decision Record

This document explains the architectural choices, patterns, and trade-offs in the IT Ticket Management System.

---

## 1. Clean Architecture

### Decision
Organize codebase into **4 independent layers**: API, Application, Domain, and Infrastructure.

### Structure
```
┌─────────────────────────────────────────┐
│  TicketSystem.API (Controllers)         │ ← HTTP entry point
├─────────────────────────────────────────┤
│  TicketSystem.Application (Handlers)    │ ← Business logic orchestration
├─────────────────────────────────────────┤
│  TicketSystem.Domain (Entities)         │ ← Core business rules
├─────────────────────────────────────────┤
│  TicketSystem.Infrastructure (EF Core)  │ ← Data access
└─────────────────────────────────────────┘
        ↓ (only downward dependencies)
```

### Benefits
- **Testability** — Mock Infrastructure layer in tests
- **Flexibility** — Swap SQL Server for PostgreSQL without touching business logic
- **Maintainability** — Each layer has single responsibility
- **Enterprise-Ready** — Used by Microsoft, major tech companies

### Trade-offs
- **More Files** — 4 projects instead of 1 monolith
- **Setup Overhead** — More boilerplate initially
- **Learning Curve** — Team needs to understand layer contracts

### Example
```
// Feature: Create Ticket
// ════════════════════════════════════════════════════════════════

// 1️⃣ API Layer (Controllers)
[ApiController]
public class TicketsController
{
    [HttpPost]
    public async Task<IActionResult> CreateTicket(CreateTicketRequest request)
    {
        var command = new CreateTicketCommand(request.Title, request.Description);
        var result = await _mediator.Send(command);
        return Ok(result);
    }
}

// 2️⃣ Application Layer (MediatR Handler)
public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, TicketDto>
{
    private readonly ITicketRepository _repository;
    
    public async Task<TicketDto> Handle(CreateTicketCommand command, CancellationToken ct)
    {
        // ✓ Validate input
        // ✓ Check business rules (can user create ticket?)
        // ✓ Create entity
        // ✓ Save to repository
        var ticket = new Ticket(command.Title, command.Description);
        await _repository.AddAsync(ticket);
        return _mapper.Map<TicketDto>(ticket);
    }
}

// 3️⃣ Domain Layer (Entities)
public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    
    // Business logic encapsulated here
    public void Resolve(string resolution)
    {
        if (Status != TicketStatus.InProgress)
            throw new InvalidOperationException("Only in-progress tickets can be resolved");
        Status = TicketStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
}

// 4️⃣ Infrastructure Layer (Entity Framework)
public class TicketRepository : ITicketRepository
{
    private readonly TicketDbContext _context;
    
    public async Task AddAsync(Ticket ticket)
    {
        _context.Tickets.Add(ticket);
        await _context.SaveChangesAsync();
    }
}
```

---

## 2. CQRS-Lite (MediatR Pattern)

### Decision
Separate read operations (Queries) from write operations (Commands) using MediatR handlers.

### Why CQRS?
```
CQRS = Command Query Responsibility Segregation

Traditional Approach:
  POST /api/tickets → CreateTicket method
  GET  /api/tickets → GetTickets method
  PUT  /api/tickets/{id} → UpdateTicket method
  ❌ All logic mixed in one controller

CQRS Approach:
  POST /api/tickets → CreateTicketCommand (MediatR handler)
  GET  /api/tickets → GetTicketsQuery (MediatR handler)
  PUT  /api/tickets/{id} → UpdateTicketCommand (MediatR handler)
  ✅ Clear separation of concerns
```

### Command vs Query

**Commands** (Write Operations) — Return results, change state
```csharp
public class CreateTicketCommand : IRequest<TicketDto>
{
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
}

public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, TicketDto>
{
    public async Task<TicketDto> Handle(CreateTicketCommand request, CancellationToken ct)
    {
        // 1. Validate
        // 2. Create entity
        // 3. Save to DB
        // 4. Return DTO
    }
}
```

**Queries** (Read Operations) — No side effects, just return data
```csharp
public class GetTicketsQuery : IRequest<List<TicketDto>>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public TicketStatus? FilterStatus { get; set; }
}

public class GetTicketsQueryHandler : IRequestHandler<GetTicketsQuery, List<TicketDto>>
{
    public async Task<List<TicketDto>> Handle(GetTicketsQuery request, CancellationToken ct)
    {
        // 1. Query database
        // 2. Filter, sort, paginate
        // 3. Return DTOs (never modify DB)
    }
}
```

### Benefits
- **Single Responsibility** — Handlers do one thing (read or write)
- **Scalability** — Can optimize queries separately from commands
- **Testability** — Easy to test: given input → expect output
- **Event Sourcing Ready** — Foundation for audit logging

### Trade-offs
- **More Files** — 1 handler per command/query (10+ total)
- **Learning Curve** — Team needs to understand MediatR
- **Slight Overhead** — MediatR reflection adds milliseconds

### Interview Talking Point
> "I used CQRS to scale independently. Commands go through validation pipeline, queries can be cached. In enterprise, you might use separate read/write databases—MediatR foundation supports that."

---

## 3. Entity Framework Core (ORM)

### Decision
Use Entity Framework Core 8 instead of raw SQL or other ORMs.

### Why EF Core?

```csharp
// ❌ Raw SQL (vulnerable to SQL injection)
var tickets = dbConnection.ExecuteQuery(
    "SELECT * FROM Tickets WHERE Priority = '" + userInput + "'"
);

// ✅ Entity Framework (parameterized, safe)
var tickets = await _dbContext.Tickets
    .Where(t => t.Priority == priority)
    .ToListAsync();
```

### Benefits
- **Safety** — Parameterized queries prevent SQL injection
- **Type Safety** — LINQ compiler checks column names at compile time
- **Productivity** — Auto-generate migrations
- **Portable** — Easy to swap SQL Server ↔ PostgreSQL ↔ MySQL
- **Job Requirement** — Explicitly listed in posting

### Entity Model Example
```csharp
public class Ticket
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(255)]
    public string Title { get; set; }

    public string Description { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.Open;

    // Foreign key
    public Guid? AssignedToUserId { get; set; }
    public User AssignedToUser { get; set; }

    // Soft delete
    public DateTime? DeletedAt { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Migrations (Database Version Control)
```bash
# Create migration
dotnet ef migrations add InitialCreate

# Apply to database
dotnet ef database update

# Rollback
dotnet ef migrations remove
```

### Trade-offs
- **Overhead** — Reflection adds ~1-5ms per query
- **Learning Curve** — LINQ and navigation properties
- **Magic** — Change tracking can be confusing

---

## 4. JWT Authentication (Stateless)

### Decision
Use JWT Bearer tokens instead of session cookies.

### How It Works

```
┌──────────────┐                    ┌──────────────────┐
│    Client    │                    │   API Server     │
└──────────────┘                    └──────────────────┘
       │                                     │
       │ 1️⃣ POST /api/auth/login             │
       │        (email, password)            │
       ├────────────────────────────────────>│
       │                                     │ Verify credentials
       │                   2️⃣ Return JWT Token│
       │                  (eyJhbGc...)       │
       │<────────────────────────────────────┤
       │                                     │
       │ Store token in localStorage         │
       │                                     │
       │ 3️⃣ GET /api/tickets                 │
       │    Authorization: Bearer <token>   │
       ├────────────────────────────────────>│
       │                                     │ Verify signature
       │                                     │ Check expiration
       │                  4️⃣ Return data     │
       │<────────────────────────────────────┤
```

### JWT Token Structure
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.
eyJzdWIiOiJ1c2VyLTEyMyIsImVtYWlsIjoiam9obi5kbEBleGFtcGxlLmNvbSIsImlhdCI6MTcxMzcyNzk5NH0.
7EJB7N2SL8cZqLXVQ9oH5hXz6pQ...

↓ (Base64 decode)

Header:     {"alg":"HS256","typ":"JWT"}
Payload:    {"sub":"user-123","email":"john.dl@example.com","iat":1713727994}
Signature:  <HMAC-SHA256 with server secret>
```

### Benefits
- **Stateless** — No server-side session storage needed
- **Scalable** — Works with multiple API instances (no affinity needed)
- **Microservices** — Token verified by any service knowing the secret
- **Mobile-Friendly** — Works for web + mobile + SPA

### Implementation
```csharp
// 1️⃣ Generate token on login
var tokenHandler = new JwtSecurityTokenHandler();
var key = Encoding.ASCII.GetBytes(jwtSecret);
var tokenDescriptor = new SecurityTokenDescriptor
{
    Subject = new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    }),
    Expires = DateTime.UtcNow.AddHours(24),
    SigningCredentials = new SigningCredentials(
        new SymmetricSecurityKey(key), 
        SecurityAlgorithms.HmacSha256Signature
    )
};
var token = tokenHandler.CreateToken(tokenDescriptor);
return tokenHandler.WriteToken(token);

// 2️⃣ Validate token in middleware
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(jwtSecret)
            ),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });
```

### Trade-offs
- **Token Size** — Tokens are larger than session IDs
- **No Revocation** — Can't revoke token immediately (expires in 24h)
- **Secret Management** — Server secret must be kept secure

---

## 5. React + TypeScript (Frontend)

### Decision
Use React 18 + TypeScript 5 instead of vanilla JavaScript or other frameworks.

### Why React?
```
✅ Component reusability
✅ Virtual DOM (efficient rendering)
✅ Large ecosystem (libraries, tools)
✅ Industry standard (Meta, Netflix, Airbnb)
✅ Job posting explicitly mentions React
```

### Why TypeScript?
```
❌ JavaScript (runtime errors)
const user = getUserById(123);
console.log(user.fullName); // Oops, property doesn't exist!
// Error only caught at runtime → user sees broken UI

✅ TypeScript (compile-time errors)
interface User {
  id: number;
  firstName: string;
  lastName: string;
}
const user: User = getUserById(123);
console.log(user.fullName); // ❌ TypeScript error before running!
// Error caught before deploying
```

### Component Architecture Example
```
App
├── Layout
│   ├── Header (navigation, user dropdown)
│   └── Sidebar (menu)
├── pages/
│   ├── TicketListPage
│   ├── TicketDetailPage
│   └── DashboardPage
└── components/
    ├── TicketTable (reusable table)
    ├── TicketForm (reusable form)
    ├── Modal (generic modal)
    └── Loading Spinner (generic loading)
```

### State Management (React Query)
```typescript
// Hook that manages ticket data fetching + caching
function useTickets(status?: TicketStatus) {
  return useQuery({
    queryKey: ['tickets', status],
    queryFn: async () => {
      const response = await axios.get('/api/tickets', {
        params: { status }
      });
      return response.data;
    },
    staleTime: 5 * 60 * 1000 // Cache for 5 minutes
  });
}

// Component using the hook
export function TicketList() {
  const { data: tickets, isLoading, error } = useTickets();
  
  if (isLoading) return <Spinner />;
  if (error) return <Error message={error.message} />;
  
  return <TicketTable tickets={tickets} />;
}
```

### Type Safety Example
```typescript
// Types enforced at compile time
interface Ticket {
  id: string;
  title: string;
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
}

// ✅ Correct
const ticket: Ticket = {
  id: '123',
  title: 'Printer broken',
  priority: 'High',
  status: 'Open'
};

// ❌ Compiler error
const badTicket: Ticket = {
  id: '456',
  title: 'Network issue',
  priority: 'URGENT', // ❌ Not in union type
  status: 'pending'   // ❌ Not in union type
};
```

### Trade-offs
- **JavaScript Overhead** — TypeScript compiles to JavaScript (adds build step)
- **Verbose** — Types require more code than vanilla JS
- **Learning Curve** — Developers must know both React and TypeScript

---

## 6. Docker & Containerization

### Decision
Use Docker to standardize development and deployment environments.

### Problem Solved
```
❌ "Works on my machine"
   Dev: Windows
   QA: macOS
   Prod: Linux
   → Different results, mystery bugs

✅ Docker
   Dev: Container with Linux + .NET 8 + SQL Server
   QA: Same container
   Prod: Same container
   → Guaranteed consistency
```

### Docker Compose (Multi-Container)
```yaml
version: '3.8'
services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:latest
    environment:
      SA_PASSWORD: "YourSecurePassword123!"
    ports:
      - "1433:1433"
    volumes:
      - sql-data:/var/opt/mssql
  
  api:
    build: ./backend                # Build from Dockerfile
    depends_on:
      - sql-server
    environment:
      ConnectionStrings__DefaultConnection: 
        "Server=sql-server;Database=TicketDb;..."
    ports:
      - "5000:8080"
  
  frontend:
    build: ./frontend
    depends_on:
      - api
    ports:
      - "3000:80"
    environment:
      REACT_APP_API_URL: "http://localhost:5000"

volumes:
  sql-data:
```

### One-Command Setup
```bash
# Entire stack: SQL Server + API + Frontend
docker-compose up

# Everything running and connected!
```

### Benefits
- **Reproducibility** — Runs identically locally, on Render, on Azure, on AWS
- **Isolation** — Bugs in one service don't affect others
- **Onboarding** — New developer: 1 command instead of 20-step setup guide

### Trade-offs
- **Disk Space** — Docker images are large (~1GB+)
- **Performance** — Slight overhead vs. native execution
- **Complexity** — Network between containers, volume management

---

## 7. GitHub Actions (CI/CD)

### Decision
Use GitHub Actions for automated testing and deployment on every commit.

### CI/CD Pipeline
```
Developer pushes code to GitHub
         ↓
GitHub Actions triggered automatically
         ↓
┌─────────────────────────────┐
│ 1️⃣ Build Backend (.NET)     │
│ 2️⃣ Build Frontend (Node)    │
│ 3️⃣ Run Backend Tests (xUnit)│
│ 4️⃣ Run Frontend Tests       │
│ 5️⃣ Build Docker images      │
│ 6️⃣ Deploy to production     │
└─────────────────────────────┘
         ↓
✅ All tests passed → deploy
❌ Tests failed → block merge
```

### Workflow File (`.github/workflows/ci.yml`)
```yaml
name: CI/CD Pipeline

on: [push, pull_request]

jobs:
  build-test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore backend
        run: dotnet restore backend/
      
      - name: Build backend
        run: dotnet build backend/
      
      - name: Test backend
        run: dotnet test backend/TicketSystem.Tests/
      
      - name: Setup Node 18
        uses: actions/setup-node@v3
        with:
          node-version: '18'
      
      - name: Install frontend deps
        run: cd frontend && npm ci
      
      - name: Build frontend
        run: cd frontend && npm run build
      
      - name: Test frontend
        run: cd frontend && npm test
      
      - name: Build Docker images
        run: docker-compose build
      
      - name: Deploy to Render
        run: |
          curl -X POST https://api.render.com/deploy/...
```

### Benefits
- **Automation** — No manual testing before merge
- **Safety** — Broken code can't reach production
- **Feedback Loop** — Developer knows if code is good in <5 minutes
- **Audit Trail** — Every test run is logged

### Trade-offs
- **Setup Time** — Workflow configuration is complex
- **CI Minutes** — GitHub provides 2000 free minutes/month
- **Latency** — Tests take 5-10 minutes (not instant feedback)

---

## 8. Database Design (Soft Deletes & Audit)

### Soft Delete Pattern
```sql
-- ❌ Hard delete
DELETE FROM Tickets WHERE Id = 'abc';
-- Data is gone forever, can't recover

-- ✅ Soft delete
UPDATE Tickets 
SET DeletedAt = GETUTCDATE() 
WHERE Id = 'abc';
-- Data still exists, marked as deleted

-- Query only active records
SELECT * FROM Tickets WHERE DeletedAt IS NULL;
```

### Audit Timestamps
```csharp
public class Ticket
{
    // When was this record created?
    public DateTime CreatedAt { get; set; }
    
    // When was this record last modified?
    public DateTime UpdatedAt { get; set; }
    
    // When was this ticket resolved?
    public DateTime? ResolvedAt { get; set; }
    
    // When was this record deleted?
    public DateTime? DeletedAt { get; set; }
}
```

### Entity Framework Auto-Update
```csharp
public class TicketDbContext : DbContext
{
    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Auto-update timestamps before saving
        var entries = ChangeTracker
            .Entries<Ticket>()
            .Where(e => e.State == EntityState.Modified);
        
        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        
        return await base.SaveChangesAsync(ct);
    }
}
```

### Benefits
- **Data Recovery** — Accidentally deleted ticket? Restore it
- **Audit Trail** — See when changes happened
- **Compliance** — GDPR allows "right to be forgotten" without losing history
- **Business Logic** — Query only active records with WHERE clause

---

## Decision Summary

| Pattern | Why | Trade-off |
|---------|-----|-----------|
| **Clean Architecture** | Testable, maintainable, scalable | More files, setup overhead |
| **CQRS (MediatR)** | Clear separation, single responsibility | More handlers, learning curve |
| **Entity Framework** | Type-safe, SQL injection protection, portable | Slight performance overhead |
| **JWT Auth** | Stateless, scalable, mobile-friendly | No token revocation, larger tokens |
| **React + TypeScript** | Type safety, productivity, industry standard | Compilation step, verbose |
| **Docker** | Reproducibility, consistency across environments | Disk space, performance overhead |
| **GitHub Actions** | Automated testing, safe deployments | Setup complexity, CI minutes |
| **Soft Deletes** | Data recovery, audit trail, compliance | Query complexity (always filter DeletedAt) |

---

## Next Steps (Scalability)

### Phase 2 Considerations
- **Event Sourcing** — Store all changes as events (audit trail)
- **CQRS Full** — Separate read/write databases
- **Microservices** — Split into independent services
- **Message Queue** — Async processing (Hangfire, RabbitMQ)
- **Caching** — Redis for high-traffic queries
- **Notifications** — Email/SMS when ticket status changes

### Enterprise Patterns
- **Domain-Driven Design (DDD)** — Model around business domains
- **Event-Driven Architecture** — Services communicate via events
- **Saga Pattern** — Distributed transactions across services

---

**Last Updated:** June 2026
