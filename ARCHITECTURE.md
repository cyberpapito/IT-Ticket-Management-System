# Architecture: IT Ticket Management System

This document explains the architectural decisions, patterns, and trade-offs in the IT Ticket Management System built for Miami-Dade County's CITD division.

---

## 1. Clean Architecture: Layers and Dependency Flow

The codebase is organized into four projects. Each layer has a single responsibility, and dependencies only point inward toward the Domain layer. This structure supports the system's core requirement: **enabling IT technicians and administrators to manage tickets reliably while maintaining a clear audit trail and enforcing business rules.**

```
TicketSystem.API
  └─ depends on: Application, Infrastructure

TicketSystem.Application
  └─ depends on: Domain

TicketSystem.Infrastructure
  └─ depends on: Domain, Application

TicketSystem.Domain
  └─ depends on: (nothing)
```

### Layer Responsibilities

**Domain (TicketSystem.Domain)**  
Contains the core entities and business rules that represent IT ticketing workflows. This layer knows nothing about HTTP, databases, or web frameworks. It models:
- `Ticket`: The central entity representing a user's IT problem or request
- `User`: IT personnel with roles (Admin, Technician, EndUser)
- `TicketNote`: Audit trail of technician actions and communications
- `TicketPriority`, `TicketStatus`: Enums that enforce valid ticket states

**Application (TicketSystem.Application)**  
Orchestrates domain operations and translates between domain entities and API contracts. It contains:
- **Commands**: Write operations (CreateTicketCommand, AssignTicketCommand, ResolveTicketCommand)
- **Queries**: Read operations (GetTicketsForAssignedTechnicianQuery, GetTicketHistoryQuery)
- **Handlers**: MediatR handlers that implement each command/query
- **DTOs**: Data Transfer Objects (TicketCreateRequest, TicketResponseDto) that represent API contracts
- **Validators**: FluentValidation rules that enforce domain constraints (title required, priority valid enum, etc.)
- **Mappings**: AutoMapper profiles that translate between domain entities and DTOs

**API (TicketSystem.API)**  
The HTTP entry point. Controllers receive requests, delegate to MediatR handlers, and return HTTP responses. Controllers do not contain business logic—they are stateless HTTP handlers.

**Infrastructure (TicketSystem.Infrastructure)**  
Implements data persistence and external services. It contains:
- **DbContext**: Entity Framework Core configuration for SQL Server
- **Repositories**: Concrete implementations of domain interfaces (ITicketRepository, IUserRepository)
- **Migrations**: EF Core migrations that track schema changes
- **Services**: JWT token generation, password hashing, notification services

### Why This Structure Matters for Miami-Dade County

The domain layer captures the business rules that *must always hold true*, regardless of whether we later add a mobile app, a GraphQL API, or Salesforce integration. For example:
- A ticket cannot be assigned to an inactive technician (domain rule, enforced in Ticket entity)
- Resolving a ticket requires documenting the resolution (domain rule, enforced in TicketNote handler)
- Only admins can delete tickets (authorization, enforced in application handler)

These rules live in one place, are tested in isolation, and can be understood without knowing SQL or HTTP.

### Trade-offs

**Benefit:** Testability. We can test the domain and application layers without touching a database or HTTP client. We mock repositories and services.

```csharp
// Test domain logic in isolation
[Fact]
public void AssignTicketToInactiveTechnician_ThrowsInvalidOperationException()
{
    var ticket = Ticket.CreateForDepartment("Network issue", "...", TicketPriority.High, Department.ITNetworking, User.Admin);
    var inactiveTechnician = new User { Id = Guid.NewGuid(), IsActive = false };
    
    var ex = Assert.Throws<InvalidOperationException>(() => ticket.AssignTo(inactiveTechnician));
    Assert.Contains("active", ex.Message);
}
```

**Cost:** More files and projects to manage. More boilerplate. Requires understanding how layers depend on each other. A junior developer may initially find it overwhelming. However, this investment pays off as the system grows and multiple teams need to understand the rules.

---

## 2. CQRS-Lite: Separating Commands from Queries

CQRS stands for Command Query Responsibility Segregation. Commands are write operations that change state. Queries are read operations that return data without side effects. We use MediatR to implement this pattern—each command and query has its own handler.

### Why Separate Commands and Queries?

**For IT ticketing, this separation clarifies intent:**

A **command** like `AssignTicketCommand` explicitly models a technician assigning a ticket to themselves or being assigned by an admin. It validates that the ticket is open, the technician is active, and generates audit events.

A **query** like `GetTicketsForAssignedTechnicianQuery` retrieves tickets without changing state. It can be optimized (cached, indexed) separately from write operations.

```csharp
// Command: Write operation
public class AssignTicketCommand : IRequest<AssignmentResult>
{
    public Guid TicketId { get; set; }
    public Guid AssignedToTechnicianId { get; set; }
}

public class AssignTicketCommandHandler : IRequestHandler<AssignTicketCommand, AssignmentResult>
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AssignTicketCommandHandler> _logger;
    
    public async Task<AssignmentResult> Handle(AssignTicketCommand request, CancellationToken ct)
    {
        var ticket = await _ticketRepository.GetTicketById(request.TicketId);
        if (ticket == null)
            return AssignmentResult.Failure($"Ticket {request.TicketId} not found");
        
        var technician = await _userRepository.GetUserById(request.AssignedToTechnicianId);
        if (technician == null || !technician.IsActive)
            return AssignmentResult.Failure("Assigned technician is not active");
        
        try
        {
            ticket.AssignTo(technician);
            await _ticketRepository.Update(ticket);
        }
        catch (InvalidOperationException ex) when (ticket.Status != TicketStatus.Open)
        {
            _logger.LogWarning("Attempted to assign {Status} ticket {TicketId}", ticket.Status, ticket.Id);
            return AssignmentResult.Failure($"Cannot assign {ticket.Status} ticket");
        }
        
        // Notify technician (best-effort; don't fail if notification is unavailable)
        try
        {
            await _notificationService.NotifyTicketAssignment(ticket, technician);
        }
        catch (NotificationServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Failed to notify technician {TechnicianId} of assignment", technician.Id);
            // Assignment succeeded; notification failed. Client should know.
            return AssignmentResult.SuccessWithWarning(ticket, "Assignment successful, but technician notification failed.");
        }
        
        return AssignmentResult.Success(ticket);
    }
}

// Query: Read operation
public class GetTicketsForAssignedTechnicianQuery : IRequest<PaginatedTicketList>
{
    public Guid TechnicianId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public TicketStatus? StatusFilter { get; set; }
}

public class GetTicketsForAssignedTechnicianQueryHandler : IRequestHandler<GetTicketsForAssignedTechnicianQuery, PaginatedTicketList>
{
    private readonly ITicketRepository _ticketRepository;
    
    public async Task<PaginatedTicketList> Handle(GetTicketsForAssignedTechnicianQuery request, CancellationToken ct)
    {
        var tickets = await _ticketRepository.GetTicketsAssignedTo(
            request.TechnicianId,
            statusFilter: request.StatusFilter);
        
        // Order by business priority: Critical tickets first, then by creation date
        var ordered = tickets
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt);
        
        var paginated = ordered.Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();
        
        return new PaginatedTicketList(paginated, ordered.Count(), request.PageNumber, request.PageSize);
    }
}
```

### Benefits of This Separation

1. **Clarity**: When someone reads `AssignTicketCommand`, they immediately understand this changes state.
2. **Optimization**: Read queries can be cached or replicated to a read database without affecting write logic.
3. **Testing**: Each handler is small and testable. Mock the dependencies, test the logic.
4. **Auditability**: Commands naturally generate events; queries do not. Audit trails follow commands.

### Trade-offs

**Cost:** One handler per command or query. For a system with 20 ticket-related operations, that's 20 handler files. This feels verbose at first.

**Benefit:** Each handler is focused and self-contained. No "fat service classes" that do both reads and writes. Future developers understand the intent faster.

---

## 3. Entity Framework Core: Type-Safe Database Access

Entity Framework Core is the ORM (Object-Relational Mapper). It converts SQL Server rows into C# objects and back, using LINQ queries.

### Why EF Core Over Raw SQL?

**Raw SQL is vulnerable to injection attacks:**
```csharp
// DANGEROUS: Never do this
var priorityFromUser = userInput; // "High' OR '1'='1"
var sql = "SELECT * FROM Tickets WHERE Priority = '" + priorityFromUser + "'";
var tickets = dbConnection.ExecuteQuery(sql);
// Attacker has now selected all tickets
```

**Entity Framework Core parameterizes queries automatically:**
```csharp
// SAFE: Priority value is parameterized
var tickets = await _dbContext.Tickets
    .Where(t => t.Priority == priority)
    .ToListAsync();
// EF Core generates: SELECT * FROM Tickets WHERE Priority = @p0
```

### Domain Entities in EF Core

```csharp
public class Ticket
{
    [Key]
    public Guid Id { get; private set; }
    
    [Required]
    [StringLength(255)]
    public string Title { get; private set; }
    
    public string Description { get; private set; } = string.Empty;
    
    [Required]
    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;
    
    [Required]
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    
    public Guid? AssignedToTechnicianId { get; private set; }
    public User AssignedToTechnician { get; private set; }
    
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; }
    
    [Required]
    public Department OriginatingDepartment { get; private set; }
    
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; } // Soft delete
    
    // Business logic: encapsulated in the entity
    public void AssignTo(User technician)
    {
        if (Status != TicketStatus.Open)
            throw new InvalidOperationException($"Cannot assign {Status} ticket. Only open tickets can be assigned.");
        
        AssignedToTechnician = technician;
        AssignedToTechnicianId = technician.Id;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
    
    public void MarkResolved(string resolutionSummary)
    {
        if (Status == TicketStatus.Closed)
            throw new InvalidOperationException("Ticket is already closed.");
        
        Status = TicketStatus.Resolved;
        ResolvedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

### Migrations Track Schema Changes

Migrations are C# files that track database schema evolution in version control. When you add a field or table, you generate a migration; when you deploy, migrations apply the changes.

```bash
# Create a migration after adding a property
dotnet ef migrations add AddTicketResolutionSummary

# See what changed
dotnet ef migrations list

# Apply to database
dotnet ef database update
```

Migration files are checked into Git, so the entire team sees schema changes and can review them before deployment.

### Trade-offs

**Benefit:** 
- Parameterized queries prevent SQL injection
- LINQ is type-checked at compile time
- Migrations keep schema in version control
- Easy to swap databases (from SQL Server to PostgreSQL) with minimal code changes

**Cost:** 
- ORM overhead: EF Core is slightly slower than raw SQL (usually negligible for business apps)
- LINQ has a learning curve
- Change tracking can surprise developers if not understood

---

## 4. JWT Authentication: Stateless Security

The API uses JWT (JSON Web Token) bearer tokens for authentication instead of server-side session cookies. This keeps the API stateless, enabling horizontal scaling.

### Authentication Flow for IT Staff

```
1. Admin/Technician logs in with email and password
   POST /api/auth/login
   { "email": "alice@miamidade.gov", "password": "..." }

2. Server verifies credentials against password hash in database
   
3. Server creates a JWT token containing:
   - User ID (Guid)
   - Email (alice@miamidade.gov)
   - Role (Technician, Admin, EndUser)
   - Expiration (24 hours from now)

4. Server returns token to client
   { "token": "eyJhbGc..." }

5. Client stores token in localStorage (frontend) or in memory (mobile)
   
6. Client includes token on subsequent requests
   Authorization: Bearer eyJhbGc...

7. Server verifies token signature and expiration on each request
   (No database lookup needed; signature proves it wasn't tampered with)
```

### Token Structure

A JWT has three parts, separated by dots:

```
Header: {"alg":"HS256","typ":"JWT"}
Payload: {"sub":"user-123","email":"alice@miamidade.gov","role":"Technician","exp":1713727994}
Signature: HMAC-SHA256(header.payload, secret)
```

The server signs the token using a secret key. If the client modifies the payload, the signature no longer matches, and the token is rejected.

### Implementation

```csharp
public class JwtTokenService
{
    private readonly string _jwtSecret;
    private readonly int _expirationHours;
    
    public JwtTokenService(IConfiguration config)
    {
        _jwtSecret = config["Jwt:Secret"] ?? throw new InvalidOperationException("JWT:Secret not configured");
        _expirationHours = int.Parse(config["Jwt:ExpirationHours"] ?? "24");
    }
    
    public string GenerateToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtSecret);
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("Department", user.Department)
        };
        
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(_expirationHours),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    
    public ClaimsPrincipal ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSecret);
            
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);
            
            return principal;
        }
        catch (SecurityTokenException ex)
        {
            throw new UnauthorizedAccessException("Token validation failed", ex);
        }
    }
}
```

### Benefits

1. **Stateless:** No need for a distributed session store. Token verification requires only the secret key.
2. **Scalable:** Multiple API instances can all verify the same token without coordinating.
3. **Mobile-Friendly:** Works for web apps, mobile apps, and third-party integrations.

### Trade-offs

**Cost:**
- Tokens cannot be revoked before expiration. If a technician's credentials are compromised, their valid tokens are still valid until they expire. Mitigation: short expiration (24 hours) and a logout endpoint that invalidates tokens server-side (token blacklist in Redis).
- Tokens are larger than session IDs (≈200 bytes vs 20 bytes).
- The signing secret must be protected. If leaked, attackers can forge tokens.

---

## 5. React and TypeScript: Type-Safe Frontend

The frontend uses React 18 with TypeScript 5, mirroring C#'s static typing on the backend.

### Why TypeScript Over JavaScript?

**JavaScript errors appear at runtime:**
```typescript
// JavaScript: no warning, error at runtime when user clicks
function getTicketPriority(ticket) {
    return ticket.priorityLevel; // Oops, field is called "priority" not "priorityLevel"
}
```

**TypeScript catches errors at compile time:**
```typescript
interface Ticket {
    id: string;
    title: string;
    priority: 'Low' | 'Medium' | 'High' | 'Critical';
}

function getTicketPriority(ticket: Ticket): string {
    return ticket.priorityLevel; // Compiler error: property 'priorityLevel' does not exist on type 'Ticket'
}
```

### React Query for Server State

React Query (TanStack Query) manages data fetched from the API. It handles caching, background sync, and stale-while-revalidate patterns.

```typescript
// Custom hook for fetching a technician's assigned tickets
function useAssignedTickets(technicianId: string) {
    return useQuery({
        queryKey: ['tickets', 'assigned', technicianId],
        queryFn: async () => {
            const response = await axios.get(`/api/tickets`, {
                params: { assignedToUserId: technicianId }
            });
            return response.data as PaginatedTicketList;
        },
        staleTime: 5 * 60 * 1000, // Cache for 5 minutes
        gcTime: 30 * 60 * 1000,    // Keep in memory for 30 minutes
        retry: 2,
        retryDelay: 1000
    });
}

// Component
function TechnicianDashboard() {
    const { data: tickets, isLoading, error } = useAssignedTickets(userId);
    
    if (isLoading) return <Spinner />;
    if (error) return <ErrorMessage error={error} />;
    
    return (
        <div>
            <h1>Your Assigned Tickets</h1>
            {tickets?.data.map(ticket => (
                <TicketCard key={ticket.id} ticket={ticket} />
            ))}
        </div>
    );
}
```

### TypeScript Interfaces for Domain Models

```typescript
// Mirrors backend domain
interface Ticket {
    id: string;
    title: string;
    description: string;
    priority: TicketPriority;
    status: TicketStatus;
    assignedToTechnicianId?: string;
    assignedToTechnician?: User;
    originatingDepartment: Department;
    createdAt: string; // ISO date
    resolvedAt?: string;
}

type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';
type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed';
type Department = 'ITNetworking' | 'ITHelp' | 'ITSecurity' | 'ITInfrastructure';
```

### Benefits

1. **Compile-time safety:** TypeScript catches type errors before the app runs.
2. **IDE support:** Autocomplete and inline documentation work better.
3. **Mirrors backend:** Both frontend and backend use static typing, making data contracts explicit.
4. **Reduced bugs:** Many common mistakes (accessing undefined properties, wrong method names) are caught early.

### Trade-offs

**Cost:**
- TypeScript adds a compilation step (though Vite makes this fast).
- More verbose than JavaScript (types take extra lines).
- Developers need to learn both React and TypeScript.

**Benefit:** Long-term maintainability. A TypeScript codebase is easier to refactor and understand, especially as it grows beyond 10k lines.

---

## 6. Docker: Reproducible Deployments

Docker packages the application and its dependencies into a container that runs identically on any system: a developer's Windows machine, a Linux server, or a cloud platform.

### Problem It Solves

Without Docker:
- App works on developer's machine (Windows 11, Node 18, npm 9)
- Doesn't work on tester's machine (macOS, Node 16, npm 8)
- Doesn't work in production (Ubuntu 20.04, Node 18, npm 10)

With Docker:
- One container runs everywhere
- Developer runs `docker-compose up`, full stack starts
- Same container deployed to production
- No "works on my machine" excuses

### docker-compose.yml

```yaml
version: '3.8'

services:
  # SQL Server database
  sql-server:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "YourSecurePassword123!"
    ports:
      - "1433:1433"
    volumes:
      - sqlserver-data:/var/opt/mssql
    healthcheck:
      test: /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P $$SA_PASSWORD -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 5

  # ASP.NET Core API
  api:
    build: ./backend
    depends_on:
      sql-server:
        condition: service_healthy
    environment:
      ConnectionStrings__DefaultConnection: "Server=sql-server;Database=TicketSystemDb;User Id=sa;Password=YourSecurePassword123;"
      Jwt__Secret: "your-super-secret-key-change-in-production"
      ASPNETCORE_ENVIRONMENT: "Development"
    ports:
      - "5000:8080"
    volumes:
      - ./backend:/app

  # React frontend
  frontend:
    build: ./frontend
    depends_on:
      - api
    environment:
      VITE_API_URL: "http://localhost:5000"
    ports:
      - "3000:80"
    volumes:
      - ./frontend:/app

volumes:
  sqlserver-data:
```

Run the entire stack with one command:
```bash
docker-compose up
# Frontend: http://localhost:3000
# API: http://localhost:5000
# Swagger: http://localhost:5000/swagger
# SQL Server: localhost:1433
```

### Benefits

1. **Reproducibility:** Same container in all environments.
2. **Isolation:** Each service (database, API, frontend) runs in its own container.
3. **Onboarding:** New developers can start the stack without installing .NET, Node, SQL Server, etc.
4. **CI/CD:** Docker images can be built, tested, and deployed automatically.

### Trade-offs

**Cost:**
- Docker images consume disk space (backend image ≈500MB, frontend ≈200MB, SQL Server ≈5GB for the base image).
- Small performance overhead compared to native execution.
- Networking and volumes add complexity.

**Benefit:** The consistency and portability justify the cost for any team larger than one developer.

---

## 7. GitHub Actions: Automated Testing and Deployment

GitHub Actions runs a CI/CD pipeline on every push and pull request. The pipeline builds, tests, and optionally deploys the application.

### Pipeline Stages

```
1. Build backend (.NET 8)
   ↓
2. Build frontend (Node 18)
   ↓
3. Run backend unit tests (xUnit)
   ↓
4. Run frontend tests (Vitest)
   ↓
5. Build Docker images
   ↓
6. Push images to registry (if all tests pass)
   ↓
7. Deploy to cloud (if all steps pass)
```

### Workflow File (.github/workflows/ci.yml)

```yaml
name: CI Pipeline

on:
  push:
    branches: [ main, develop ]
  pull_request:
    branches: [ main, develop ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      # Backend: .NET
      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore backend dependencies
        run: dotnet restore backend/
      
      - name: Build backend
        run: dotnet build backend/ --configuration Release --no-restore
      
      - name: Run backend tests
        run: dotnet test backend/TicketSystem.Tests/ --no-build --verbosity normal --logger "trx"
      
      # Frontend: Node
      - name: Setup Node 18
        uses: actions/setup-node@v4
        with:
          node-version: '18'
          cache: 'npm'
          cache-dependency-path: frontend/package-lock.json
      
      - name: Install frontend dependencies
        run: cd frontend && npm ci
      
      - name: Build frontend
        run: cd frontend && npm run build
      
      - name: Run frontend tests
        run: cd frontend && npm run test
      
      # Docker
      - name: Build Docker images
        run: docker-compose build
      
      - name: Push images to registry (main branch only)
        if: github.ref == 'refs/heads/main' && github.event_name == 'push'
        run: |
          echo ${{ secrets.DOCKER_PASSWORD }} | docker login -u ${{ secrets.DOCKER_USERNAME }} --password-stdin
          docker tag ticket-system-api:latest yourusername/ticket-system-api:latest
          docker tag ticket-system-frontend:latest yourusername/ticket-system-frontend:latest
          docker push yourusername/ticket-system-api:latest
          docker push yourusername/ticket-system-frontend:latest
```

### Benefits

1. **Automated checks:** Tests run before code is merged. Broken builds are caught immediately.
2. **Audit trail:** Every build is logged with what tests passed/failed.
3. **Enforced quality:** PR can't be merged until CI passes.
4. **Deployments:** Merge to main automatically deploys to production.

### Trade-offs

**Cost:**
- Workflow configuration takes time to write and debug.
- GitHub provides limited free CI minutes per month (2000/month for private repos).
- Full pipeline can take 5-10 minutes per commit.

**Benefit:** The overhead is worth it. Broken code never reaches production.

---

## 8. Soft Deletes and Audit Timestamps

Instead of permanently deleting records, we mark them as deleted with a timestamp. Records stay in the database, enabling recovery and compliance audits.

### Soft Delete Pattern

```csharp
public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public TicketStatus Status { get; set; }
    
    // Soft delete: record is logically deleted but physically remains
    public DateTimeOffset? DeletedAt { get; set; }
    
    public bool IsDeleted => DeletedAt.HasValue;
}

// Query: only fetch active records
var activeTickets = await _dbContext.Tickets
    .Where(t => t.DeletedAt == null)
    .ToListAsync();

// Soft delete: mark as deleted
ticket.DeletedAt = DateTimeOffset.UtcNow;
await _ticketRepository.Update(ticket);
```

### Audit Timestamps

Every ticket tracks when it was created, last updated, and resolved:

```csharp
public class Ticket
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
```

EF Core automatically updates `UpdatedAt` on every save:

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var entries = ChangeTracker
        .Entries<Ticket>()
        .Where(e => e.State == EntityState.Modified);

    foreach (var entry in entries)
    {
        entry.Entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    return await base.SaveChangesAsync(ct);
}
```

### Benefits

1. **Data recovery:** Deleted tickets can be restored.
2. **Compliance:** Audit trail shows who did what, when.
3. **Analytics:** Historical data is preserved for reporting.

### Trade-offs

Every query must filter deleted records:
```csharp
// Every query must include this check
var tickets = _dbContext.Tickets.Where(t => t.DeletedAt == null);
```

Mitigation: Create a base repository method:
```csharp
public IQueryable<Ticket> GetActiveTickets()
{
    return _dbContext.Tickets.Where(t => t.DeletedAt == null);
}
```

---

## Decision Summary Table

| Decision | Rationale | Trade-off |
|---|---|---|
| **Clean Architecture** | Separates domain rules from infrastructure; enables testing | More files, more layers |
| **CQRS (MediatR)** | Each handler does one thing (command or query); clear intent | More handlers, more files |
| **Entity Framework Core** | Type-safe SQL, prevents injection, migrations track schema | Small performance overhead |
| **JWT Authentication** | Stateless, scalable, works for mobile/SPA | Tokens can't be revoked instantly |
| **React + TypeScript** | Type safety matches C#, IDE support, fewer runtime errors | Compilation step, verbose |
| **Docker** | Consistent across all environments, enables CI/CD | Disk space, performance overhead |
| **GitHub Actions** | Automated testing, prevents broken code from reaching main | Setup complexity, CI minutes limited |
| **Soft Deletes** | Data recovery, compliance audit trail | Must filter deleted records in queries |

---

## Future Enhancements

As the system evolves, consider:

1. **Event Sourcing:** Instead of just tracking `UpdatedAt`, store a complete event log. Every change (created, assigned, resolved, etc.) becomes a queryable event. Enables perfect audit trail and temporal queries ("what was this ticket's status on May 15?").

2. **Read Database (CQRS-full):** Currently, queries read from the same SQL Server database as writes. For high-traffic scenarios, replicate writes to a read-optimized database (e.g., a denormalized reporting table) and read from that.

3. **Background Job Processing (Hangfire):** Send notifications, generate reports, and trigger escalations in the background without blocking the request-response cycle.

4. **Caching (Redis):** Cache frequently-read data (user list, department mappings, status enums) in Redis. Invalidate on writes.

5. **Salesforce Integration:** Bi-directional sync between IT tickets and Salesforce service cloud cases. Commands trigger Salesforce API calls; Salesforce events trigger ticket updates.

6. **GraphQL:** Complement the REST API with GraphQL for mobile apps and frontend flexibility.

---

## References and Learning

- **Clean Architecture:** Robert C. Martin's *Clean Code* and *Clean Architecture*
- **CQRS & MediatR:** Jimmy Bogard's MediatR repository and blog posts
- **Entity Framework Core:** Microsoft Learn documentation on EF Core
- **JWT Authentication:** Auth0 blog on JWT best practices
- **Docker:** Docker documentation and "Docker Deep Dive" by Nigel Poulton
- **GitHub Actions:** GitHub documentation on Actions workflows
