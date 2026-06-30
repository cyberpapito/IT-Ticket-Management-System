# Architecture

This document explains the architectural choices, patterns, and trade-offs in the IT Ticket Management System.

---

## 1. Clean Architecture

The codebase is organized into four projects, each with a single responsibility. Dependencies only point inward, toward the Domain.

```
API              depends on Application, Infrastructure
Application      depends on Domain
Infrastructure   depends on Domain, Application
Domain           depends on nothing
```

- API: HTTP entry point. Controllers receive requests and pass them to the Application layer.
- Application: Business logic. Orchestrates operations, validation, and mapping.
- Domain: Core entities and business rules. Knows nothing about the database or HTTP.
- Infrastructure: Data access. Implements database operations using Entity Framework Core.

### Benefits

- Testability: the Infrastructure layer can be mocked in tests.
- Flexibility: the database can be swapped without changing business logic.
- Maintainability: each layer has one clear purpose.

### Trade-offs

- More projects to manage than a single-project application.
- More setup and boilerplate at the start.
- Requires understanding of how the layers depend on each other.

### Example: creating a ticket

```csharp
// API layer (Controller)
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

// Application layer (Handler)
public class CreateTicketCommandHandler : IRequestHandler<CreateTicketCommand, TicketDto>
{
    private readonly ITicketRepository _repository;

    public async Task<TicketDto> Handle(CreateTicketCommand command, CancellationToken ct)
    {
        var ticket = new Ticket(command.Title, command.Description);
        await _repository.AddAsync(ticket);
        return _mapper.Map<TicketDto>(ticket);
    }
}

// Domain layer (Entity)
public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public TicketPriority Priority { get; set; }
    public TicketStatus Status { get; set; }

    public void Resolve()
    {
        if (Status != TicketStatus.InProgress)
            throw new InvalidOperationException("Only in-progress tickets can be resolved");
        Status = TicketStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
}

// Infrastructure layer (Repository)
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

## 2. CQRS-Lite (MediatR)

CQRS stands for Command Query Responsibility Segregation. It separates write operations (Commands) from read operations (Queries).

Commands change state and return a result. Queries return data and cause no side effects.

```
Commands (write)            Queries (read)
CreateTicketCommand         GetTicketsQuery
UpdateTicketCommand         GetTicketByIdQuery
DeleteTicketCommand         GetUsersQuery
```

### Command example

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
        // Validate, create entity, save, return DTO
    }
}
```

### Query example

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
        // Query database, filter, sort, paginate, return DTOs
    }
}
```

MediatR is the library that implements this pattern. Install with `dotnet add package MediatR`.

### Benefits

- Each handler does one thing, either a read or a write.
- Easy to test: given an input, expect an output.
- Queries can be optimized or cached separately from commands.

### Trade-offs

- One handler per command or query, which means more files.
- Requires learning the MediatR library.
- Adds a small amount of overhead per request.

---

## 3. Entity Framework Core

Entity Framework Core is the ORM (Object-Relational Mapper) used to talk to the database. It converts database rows into C# objects and back.

### Raw SQL vs Entity Framework

```csharp
// Raw SQL: vulnerable to SQL injection
var tickets = dbConnection.ExecuteQuery(
    "SELECT * FROM Tickets WHERE Priority = '" + userInput + "'"
);

// Entity Framework: parameterized and safe
var tickets = await _dbContext.Tickets
    .Where(t => t.Priority == priority)
    .ToListAsync();
```

### Benefits

- Parameterized queries prevent SQL injection.
- LINQ queries are checked at compile time.
- Migrations track database schema changes in version control.
- The database provider can be changed with minimal code changes.

### Entity example

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

    public Guid? AssignedToUserId { get; set; }
    public User AssignedToUser { get; set; }

    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

### Migrations

```bash
# Create a migration
dotnet ef migrations add InitialCreate

# Apply it to the database
dotnet ef database update

# Remove the last migration
dotnet ef migrations remove
```

### Trade-offs

- Adds a small amount of overhead per query.
- LINQ and navigation properties take time to learn.
- Change tracking can behave in unexpected ways if not understood.

---

## 4. JWT Authentication

The API uses JWT (JSON Web Token) bearer tokens instead of server-side session cookies. This keeps the API stateless.

### Flow

```
1. Client sends POST /api/auth/login with email and password.
2. Server verifies credentials and returns a JWT token.
3. Client stores the token and includes it on future requests
   in the header: Authorization: Bearer <token>
4. Server verifies the token signature and expiration on each request.
```

### Token structure

A JWT has three parts separated by dots: a header, a payload, and a signature.

```
Header:     {"alg":"HS256","typ":"JWT"}
Payload:    {"sub":"user-123","email":"user@example.com","iat":1713727994}
Signature:  HMAC-SHA256 hash signed with the server secret
```

### Benefits

- Stateless: no session storage needed on the server.
- Scalable: works across multiple API instances without shared session state.
- Works for web, mobile, and single-page apps.

### Implementation

```csharp
// Generate a token on login
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
```

### Trade-offs

- Tokens are larger than session IDs.
- A token cannot be revoked before it expires.
- The signing secret must be kept secure.

---

## 5. React and TypeScript

The frontend uses React 18 with TypeScript 5.

### Why TypeScript over plain JavaScript

```typescript
// JavaScript: error only appears at runtime
const user = getUserById(123);
console.log(user.fullName); // undefined, no warning

// TypeScript: error caught at compile time
interface User {
  id: number;
  firstName: string;
  lastName: string;
}
const user: User = getUserById(123);
console.log(user.fullName); // compiler error before running
```

### State management with React Query

```typescript
function useTickets(status?: TicketStatus) {
  return useQuery({
    queryKey: ['tickets', status],
    queryFn: async () => {
      const response = await axios.get('/api/tickets', { params: { status } });
      return response.data;
    },
    staleTime: 5 * 60 * 1000 // cache for 5 minutes
  });
}
```

### Type safety example

```typescript
interface Ticket {
  id: string;
  title: string;
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  status: 'Open' | 'InProgress' | 'Resolved' | 'Closed';
}

// Valid
const ticket: Ticket = {
  id: '123',
  title: 'Printer broken',
  priority: 'High',
  status: 'Open'
};

// Compiler error: 'URGENT' is not a valid priority
const badTicket: Ticket = {
  id: '456',
  title: 'Network issue',
  priority: 'URGENT',
  status: 'pending'
};
```

### Trade-offs

- TypeScript adds a compile step.
- Types require more code than plain JavaScript.
- Developers need to know both React and TypeScript.

---

## 6. Docker

Docker packages the application along with its dependencies so it runs the same way in every environment.

### Problem it solves

Without Docker, an app might behave differently on a developer's Windows machine, a tester's Mac, and a Linux production server. With Docker, all three run the same container.

### docker-compose

```yaml
version: '3.8'
services:
  sql-server:
    image: mcr.microsoft.com/mssql/server:latest
    environment:
      SA_PASSWORD: "YourSecurePassword123!"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"

  api:
    build: ./backend
    depends_on:
      - sql-server
    environment:
      ConnectionStrings__DefaultConnection: "Server=sql-server;Database=TicketDb;..."
    ports:
      - "5000:8080"

  frontend:
    build: ./frontend
    depends_on:
      - api
    ports:
      - "3000:80"
```

Running `docker-compose up` starts the database, API, and frontend together.

### Benefits

- The same container runs locally and in the cloud.
- Services are isolated from each other.
- New developers can start the whole stack with one command.

### Trade-offs

- Docker images take up disk space.
- There is a small performance cost compared to running natively.
- Networking and volumes add some complexity.

---

## 7. GitHub Actions (CI/CD)

GitHub Actions runs automated builds and tests on every push and pull request.

### Pipeline

```
1. Build the backend (.NET)
2. Build the frontend (Node.js)
3. Run backend tests (xUnit)
4. Run frontend tests (Vitest)
5. Build Docker images
6. Deploy if all steps pass
```

### Workflow file

```yaml
name: CI Pipeline

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
```

### Benefits

- Tests run automatically before code is merged.
- Broken code is caught before it reaches production.
- Every run is logged.

### Trade-offs

- Workflow configuration takes time to set up.
- GitHub provides a limited number of free CI minutes per month.
- A full run can take several minutes.

---

## 8. Database Design: Soft Deletes and Audit Fields

### Soft delete

Instead of permanently removing a record, a soft delete marks it as deleted with a timestamp. The record stays in the database but is filtered out of normal queries.

```sql
-- Hard delete: data is gone permanently
DELETE FROM Tickets WHERE Id = 'abc';

-- Soft delete: data remains, marked as deleted
UPDATE Tickets SET DeletedAt = GETUTCDATE() WHERE Id = 'abc';

-- Query only active records
SELECT * FROM Tickets WHERE DeletedAt IS NULL;
```

### Audit fields

```csharp
public class Ticket
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
```

### Auto-updating timestamps in EF Core

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var entries = ChangeTracker
        .Entries<Ticket>()
        .Where(e => e.State == EntityState.Modified);

    foreach (var entry in entries)
    {
        entry.Entity.UpdatedAt = DateTime.UtcNow;
    }

    return await base.SaveChangesAsync(ct);
}
```

### Benefits

- Deleted records can be recovered.
- Changes can be traced through timestamps.
- Supports compliance requirements without losing history.

---

## Decision Summary

| Pattern | Reason | Trade-off |
|---|---|---|
| Clean Architecture | Testable, maintainable, scalable | More files and setup |
| CQRS (MediatR) | Clear separation, single responsibility | More handlers, learning curve |
| Entity Framework | Type-safe, SQL injection protection | Small performance overhead |
| JWT Auth | Stateless, scalable | No token revocation, larger tokens |
| React and TypeScript | Type safety, industry standard | Compile step, more verbose |
| Docker | Consistent across environments | Disk space, performance overhead |
| GitHub Actions | Automated testing and deployment | Setup complexity, CI minutes |
| Soft Deletes | Data recovery, audit trail | Queries must filter deleted rows |

---

## Possible Future Work

- Event sourcing for a full audit trail of changes.
- Separate read and write databases (full CQRS).
- Background job processing for notifications.
- Caching with Redis for high-traffic queries.
- Email or SMS notifications on ticket status changes.
