# Architecture Patterns: Clean Architecture + Hexagonal Architecture in .NET 10

**Domain:** Learning/reference Web API — person management
**Researched:** 2026-05-27
**Overall confidence:** HIGH (all claims verified against official Microsoft docs, well-established community references, and authoritative .NET architecture guides)

---

## How Clean Architecture Layers Map to Hexagonal Ports and Adapters

Clean Architecture provides the layering model (rings). Hexagonal Architecture provides the port/adapter vocabulary for what lives at each boundary. Combined, each layer has a defined role:

| Clean Layer | Hexagonal Role | What Lives Here |
|-------------|---------------|-----------------|
| Domain | Application core (innermost hexagon) | Entities, value objects, domain logic, domain events |
| Application | Application core + port definitions | Use-case handlers, CQRS commands/queries, port interfaces (IPersonRepository, IUnitOfWork) |
| Infrastructure | Driven/secondary adapters | EF Core DbContext, repository implementations, seeding |
| Presentation/Api | Driving/primary adapters | Controllers, request models, DI wiring (Program.cs) |

### Where Port Interfaces Live

This is the most commonly misunderstood placement decision. The answer per Herberto Graca's "Explicit Architecture" (the canonical synthesis of all these patterns) and confirmed by Microsoft's DDD/CQRS eBook:

**Driven port interfaces (e.g., `IPersonRepository`) live in the Application layer.**

Rationale: the Application layer defines what it needs from the outside world. The interface is shaped by use-case requirements, not by the storage technology. Domain entities remain persistence-ignorant — they do not reference `IPersonRepository` at all.

Domain layer holds only: entity classes, value objects, and optionally domain service interfaces that express pure business rules (e.g., `IPersonAgeCalculator` if extracted). For a simple CRUD domain like PersonsAPI, the domain contains only the `Person` entity.

**Driving port interfaces** in strict Hexagonal Architecture sit in the Application layer too (e.g., `IPersonService`). However, when using MediatR, `IRequest<T>` + `IRequestHandler<TRequest, TResponse>` replace the need for explicit service interfaces — the command/query contract is the port. This is the standard .NET community approach.

---

## Recommended Solution Structure

### Projects (4 total — no over-engineering)

```
PersonsAPI.sln
src/
  PersonsAPI.Domain/           -- Clean: Domain layer | Hexagonal: application core
  PersonsAPI.Application/      -- Clean: Application layer | Hexagonal: core + port definitions
  PersonsAPI.Infrastructure/   -- Clean: Infrastructure layer | Hexagonal: driven adapters
  PersonsAPI.Api/              -- Clean: Presentation layer | Hexagonal: driving adapters
```

No shared kernel project is needed at this scope. No separate "Contracts" project — the Application project serves that role.

### Project References (enforced by compiler — no circular refs possible)

```
PersonsAPI.Domain
  references: (nothing — zero external dependencies)

PersonsAPI.Application
  references: PersonsAPI.Domain

PersonsAPI.Infrastructure
  references: PersonsAPI.Application
              (which transitively brings in Domain)

PersonsAPI.Api
  references: PersonsAPI.Application
              PersonsAPI.Infrastructure
```

The Api project references both Application and Infrastructure so it can:
1. Call `builder.Services.AddApplication()` (registers MediatR, validators)
2. Call `builder.Services.AddInfrastructure()` (registers DbContext, repositories)
3. Controllers depend only on `IMediator`/`ISender` — no direct Infrastructure dependency at the code level

This reference pattern is confirmed by Jason Taylor's CleanArchitecture template (Web.csproj references Application + Infrastructure) and codewithmukesh's .NET 10 guide.

### Folder Layout Inside Each Project

```
PersonsAPI.Domain/
  Entities/
    Person.cs            -- rich domain entity, Age computed here

PersonsAPI.Application/
  Persons/
    Commands/
      CreatePerson/
        CreatePersonCommand.cs
        CreatePersonCommandHandler.cs
      UpdatePerson/
        UpdatePersonCommand.cs
        UpdatePersonCommandHandler.cs
      PatchPerson/
        PatchPersonCommand.cs
        PatchPersonCommandHandler.cs
      DeletePerson/
        DeletePersonCommand.cs
        DeletePersonCommandHandler.cs
    Queries/
      GetAllPersons/
        GetAllPersonsQuery.cs
        GetAllPersonsQueryHandler.cs
      GetPersonById/
        GetPersonByIdQuery.cs
        GetPersonByIdQueryHandler.cs
    DTOs/
      PersonDto.cs          -- what leaves the Application layer
      CreatePersonRequest.cs
      UpdatePersonRequest.cs
      PatchPersonRequest.cs
  Ports/                    -- driven port interfaces
    IPersonRepository.cs
  DependencyInjection.cs    -- AddApplication() extension method

PersonsAPI.Infrastructure/
  Persistence/
    AppDbContext.cs          -- EF Core DbContext (scoped lifetime)
    PersonEntityConfiguration.cs  -- Fluent API config (IEntityTypeConfiguration<Person>)
    PersonRepository.cs      -- implements IPersonRepository
    DataSeeder.cs            -- seeds in-memory data
  DependencyInjection.cs     -- AddInfrastructure() extension method

PersonsAPI.Api/
  Controllers/
    PersonsController.cs     -- driving adapter, uses ISender
  Program.cs                 -- DI composition root, UseInfrastructure + UseApplication
```

---

## How EF Core Fits as a Driven Adapter Without Leaking Into the Domain

### The Core Rule

The `Person` domain entity must be a plain C# class with no EF attributes, no `[Key]`, no `[Column]` — nothing from `Microsoft.EntityFrameworkCore`. This is the Persistence Ignorance principle.

EF Core is configured entirely through Fluent API in the Infrastructure layer via `IEntityTypeConfiguration<Person>`. The domain entity never references EF.

### What Lives Where

**Domain — Person entity (persistence-ignorant):**
```csharp
// PersonsAPI.Domain/Entities/Person.cs
public class Person
{
    public Guid Id { get; private set; }
    public string FirstName { get; private set; }
    public string PaternalLastName { get; private set; }
    public string MaternalLastName { get; private set; }
    public DateOnly DateOfBirth { get; private set; }

    public int Age => CalculateAge(DateOfBirth);  // computed — never stored

    protected Person() { }  // required by EF Core for materialization

    public Person(string firstName, string paternalLastName,
                  string maternalLastName, DateOnly dateOfBirth)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        PaternalLastName = paternalLastName;
        MaternalLastName = maternalLastName;
        DateOfBirth = dateOfBirth;
    }

    private static int CalculateAge(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age;
    }

    public void Update(string firstName, string paternalLastName,
                       string maternalLastName, DateOnly dateOfBirth)
    {
        FirstName = firstName;
        PaternalLastName = paternalLastName;
        MaternalLastName = maternalLastName;
        DateOfBirth = dateOfBirth;
    }
}
```

**Infrastructure — EF configuration (keeps EF out of domain):**
```csharp
// PersonsAPI.Infrastructure/Persistence/PersonEntityConfiguration.cs
public class PersonEntityConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.PaternalLastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.MaternalLastName).IsRequired().HasMaxLength(100);
        builder.Property(p => p.DateOfBirth).IsRequired();
        builder.Ignore(p => p.Age);  // computed — do not persist
    }
}
```

**Infrastructure — DbContext:**
```csharp
// PersonsAPI.Infrastructure/Persistence/AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

**Application — driven port (what the application layer needs):**
```csharp
// PersonsAPI.Application/Ports/IPersonRepository.cs
public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default);
    Task<Person?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Person person, CancellationToken ct = default);
    Task UpdateAsync(Person person, CancellationToken ct = default);
    Task DeleteAsync(Person person, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

**Infrastructure — driven adapter (implements the port):**
```csharp
// PersonsAPI.Infrastructure/Persistence/PersonRepository.cs
public class PersonRepository : IPersonRepository
{
    private readonly AppDbContext _context;

    public PersonRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken ct = default)
        => await _context.Persons.ToListAsync(ct);

    public async Task<Person?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Persons.FindAsync([id], ct);

    public async Task AddAsync(Person person, CancellationToken ct = default)
        => await _context.Persons.AddAsync(person, ct);

    public Task UpdateAsync(Person person, CancellationToken ct = default)
    {
        _context.Persons.Update(person);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Person person, CancellationToken ct = default)
    {
        _context.Persons.Remove(person);
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}
```

---

## Where DTOs vs Domain Entities Live

| Artifact | Layer | Reasoning |
|----------|-------|-----------|
| `Person` (domain entity) | Domain | Pure business object. Never crosses the API boundary. |
| `PersonDto` (response) | Application | Shapes what handlers return. Controller maps or returns directly. |
| `CreatePersonRequest` | Application | Input contract for create command. |
| `UpdatePersonRequest` | Application | Input contract for full update. |
| `PatchPersonRequest` | Application | Input contract for partial update. |

**Rule:** Controllers in the Presentation layer receive and return Application DTOs. They never touch domain entities directly. Domain entities only cross Application layer boundaries when being passed to repository calls.

**Mapping location:** Inside command/query handlers. The handler receives a command (DTO input), creates or fetches a domain entity, performs business operations, then maps the result entity to a DTO for return. No AutoMapper required at this scale — explicit mapping in the handler is clearer.

```csharp
// Mapping stays inside the handler
private static PersonDto ToDto(Person p) => new(
    p.Id, p.FirstName, p.PaternalLastName, p.MaternalLastName, p.DateOfBirth, p.Age);
```

---

## How MediatR Fits with Hexagonal Ports

MediatR is registered in the Application layer and replaces the need for explicit driving port service interfaces. The CQRS command/query objects become the port contracts.

### Driving side (primary adapter — Controller)

The controller is the driving adapter. It:
1. Receives an HTTP request
2. Maps it to a command or query (Application layer type)
3. Sends it via `ISender.Send()` — this is the only dependency the controller needs

```csharp
// PersonsAPI.Api/Controllers/PersonsController.cs
[ApiController]
[Route("api/[controller]")]
public class PersonsController : ControllerBase
{
    private readonly ISender _sender;

    public PersonsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PersonDto>>> GetAll(CancellationToken ct)
        => Ok(await _sender.Send(new GetAllPersonsQuery(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PersonDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new GetPersonByIdQuery(id), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PersonDto>> Create(
        CreatePersonRequest request, CancellationToken ct)
    {
        var dto = await _sender.Send(new CreatePersonCommand(request), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PersonDto>> Update(
        Guid id, UpdatePersonRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdatePersonCommand(id, request), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<PersonDto>> Patch(
        Guid id, PatchPersonRequest request, CancellationToken ct)
    {
        var result = await _sender.Send(new PatchPersonCommand(id, request), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var found = await _sender.Send(new DeletePersonCommand(id), ct);
        return found ? NoContent() : NotFound();
    }
}
```

### Handler as use-case coordinator (Application layer)

```csharp
// PersonsAPI.Application/Persons/Commands/CreatePerson/CreatePersonCommandHandler.cs
public record CreatePersonCommand(CreatePersonRequest Request) : IRequest<PersonDto>;

public class CreatePersonCommandHandler : IRequestHandler<CreatePersonCommand, PersonDto>
{
    private readonly IPersonRepository _repo;

    public CreatePersonCommandHandler(IPersonRepository repo) => _repo = repo;

    public async Task<PersonDto> Handle(CreatePersonCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        var person = new Person(r.FirstName, r.PaternalLastName, r.MaternalLastName, r.DateOfBirth);
        await _repo.AddAsync(person, ct);
        await _repo.SaveChangesAsync(ct);
        return ToDto(person);
    }

    private static PersonDto ToDto(Person p) =>
        new(p.Id, p.FirstName, p.PaternalLastName, p.MaternalLastName, p.DateOfBirth, p.Age);
}
```

### MediatR Registration

```csharp
// PersonsAPI.Application/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        return services;
    }
}

// PersonsAPI.Infrastructure/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("PersonsDb"));

        services.AddScoped<IPersonRepository, PersonRepository>();

        return services;
    }
}

// PersonsAPI.Api/Program.cs
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
```

---

## Dependency Inversion: Wiring in the API Project

The Composition Root is `Program.cs` in the Api project. This is the only place where concrete implementations are linked to their interfaces.

The Api project references both Application and Infrastructure for this reason: it needs to call `AddInfrastructure()` which registers `AppDbContext` and `PersonRepository : IPersonRepository`. At runtime, when MediatR dispatches a command to a handler, the handler receives `IPersonRepository` — which resolves to `PersonRepository` — which uses `AppDbContext` with InMemory. The handler has no idea EF Core exists.

The dependency graph at runtime:

```
HTTP Request
  → PersonsController (ISender)
    → MediatR (IMediator — wired in Application)
      → CreatePersonCommandHandler
          → IPersonRepository  ← resolved to PersonRepository (Infrastructure)
              → AppDbContext    ← UseInMemoryDatabase (Infrastructure)
                  → Person      ← Domain entity (persisted via EF, no EF knowledge in entity)
```

No layer calls upward. No layer skips a layer. Domain never sees EF or MediatR.

---

## Suggested Build Order

Build order that guarantees no circular dependency can emerge:

### Phase 1 — Domain
Build `PersonsAPI.Domain` first. No project references. Contains only the `Person` entity with rich logic and computed `Age`. Verifiable standalone: the entity compiles with zero external dependencies.

### Phase 2 — Application
Build `PersonsAPI.Application`. References only Domain. Contains:
- `IPersonRepository` port interface
- All CQRS command/query records and handler skeletons
- All DTO types (PersonDto, CreatePersonRequest, etc.)
- `DependencyInjection.cs` (AddApplication)
- MediatR NuGet package reference here

If Application compiles, it proves domain is clean.

### Phase 3 — Infrastructure
Build `PersonsAPI.Infrastructure`. References Application. Contains:
- `AppDbContext` (EF Core InMemory)
- `PersonEntityConfiguration` (Fluent API)
- `PersonRepository` implementing `IPersonRepository`
- `DataSeeder` for startup seed data
- `DependencyInjection.cs` (AddInfrastructure)
- EF Core InMemory NuGet packages reference here

If Infrastructure compiles with no Domain import of EF types, the boundary is intact.

### Phase 4 — Api
Build `PersonsAPI.Api` last. References Application + Infrastructure. Contains:
- `PersonsController`
- `Program.cs` composition root

### Build Order Summary

```
1. PersonsAPI.Domain         (zero deps)
2. PersonsAPI.Application    (refs Domain)
3. PersonsAPI.Infrastructure (refs Application)
4. PersonsAPI.Api            (refs Application + Infrastructure)
```

This order eliminates circular references by design. If a developer accidentally tries to reference Infrastructure from Application, the project reference would create a cycle and the solution would fail to load.

---

## Component Boundaries and Data Flow

### Request lifecycle (write path — create)

```
[HTTP POST /api/persons]
  → PersonsController.Create(CreatePersonRequest)
  → new CreatePersonCommand(request)        [Application DTO]
  → ISender.Send(command)
  → CreatePersonCommandHandler.Handle()
     → new Person(...)                      [Domain entity created]
     → IPersonRepository.AddAsync(person)
     → IPersonRepository.SaveChangesAsync()
        → PersonRepository (Infrastructure)
           → AppDbContext.Persons.AddAsync()
           → AppDbContext.SaveChangesAsync()
     → ToDto(person)                        [Domain → Application DTO]
  → return PersonDto
  → 201 Created + PersonDto body
```

### Request lifecycle (read path — get all)

```
[HTTP GET /api/persons]
  → PersonsController.GetAll()
  → new GetAllPersonsQuery()
  → ISender.Send(query)
  → GetAllPersonsQueryHandler.Handle()
     → IPersonRepository.GetAllAsync()
        → PersonRepository (Infrastructure)
           → AppDbContext.Persons.ToListAsync()
     → persons.Select(ToDto)               [Domain → Application DTOs]
  → return IReadOnlyList<PersonDto>
  → 200 OK + array body
```

### Data flow rules (explicit)

- Domain entities flow: Domain → Application (inside handlers only)
- DTOs flow: Application → Api (controller responses)
- HTTP models: Api controller method parameters → Application command/query constructors
- EF types (DbContext, DbSet): Infrastructure only — never cross into Application or Domain
- MediatR types (IRequest, IRequestHandler): Application only — never in Domain

---

## Anti-Patterns to Avoid

### Anti-Pattern 1: Repository interface in Domain
**What goes wrong:** Domain project references a repository interface, forcing Infrastructure (which implements it) to reference Domain. This works structurally but mixes concerns — domain entities should not "know" they are being persisted.
**Correct:** Repository interfaces belong in Application, shaping what the use cases need.

### Anti-Pattern 2: Returning domain entities from handlers
**What goes wrong:** Domain entities leak to the API surface. Callers can mutate internal state; computed properties (Age) may serialize unexpectedly; breaking changes to the entity break the API contract.
**Correct:** Handlers always return DTOs. Map inside the handler.

### Anti-Pattern 3: EF attributes on domain entity
**What goes wrong:** `[Key]`, `[Column]`, `[Required]` attributes create a hard dependency on `Microsoft.EntityFrameworkCore` in the Domain project.
**Correct:** Use Fluent API in `IEntityTypeConfiguration<Person>` inside Infrastructure. Domain entity stays attribute-free.

### Anti-Pattern 4: Injecting AppDbContext into Application handlers
**What goes wrong:** Application layer takes a hard dependency on the EF Core DbContext, coupling use cases to a specific ORM.
**Correct:** Inject `IPersonRepository` (port). The repository implementation (Infrastructure) owns the DbContext.

### Anti-Pattern 5: Controller logic
**What goes wrong:** Business validation, entity creation, or mapping logic placed in the controller rather than in the command handler.
**Correct:** Controllers are thin routing adapters. All logic lives in handlers.

### Anti-Pattern 6: Scoped DbContext registered as Singleton
**What goes wrong:** EF Core DbContext tracks entity state per request. Singleton lifetime causes shared state across requests, leading to data corruption.
**Correct:** Register as Scoped (the AddDbContext default). Register repositories as Scoped too.

---

## Scalability Considerations

| Concern | Current (InMemory) | If migrating to SQL Server |
|---------|-------------------|---------------------------|
| Persistence swap | Change `UseInMemoryDatabase` to `UseSqlServer` in Infrastructure's `DependencyInjection.cs` only | Zero changes in Domain or Application |
| Adding new entity | Add to Domain, add port in Application, add EF config in Infrastructure | Same procedure |
| Adding use case | New command/query + handler in Application, no other layers change | Same |
| Cross-cutting (logging, validation) | MediatR pipeline behaviors in Application | Same |
| Multiple data sources | Add second repository port + adapter | Application unchanged |

---

## Sources

**Confidence: HIGH** — primary sources used:

- Microsoft .NET Architecture Guide: [Infrastructure persistence layer with EF Core](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core) (updated April 2026)
- Herberto Graca: [DDD, Hexagonal, Onion, Clean, CQRS — How I put it all together](https://herbertograca.com/2017/11/16/explicit-architecture-01-ddd-hexagonal-onion-clean-cqrs-how-i-put-it-all-together/) — canonical synthesis of all patterns
- codewithmukesh: [Clean Architecture in .NET 10](https://codewithmukesh.com/blog/clean-architecture-dotnet/) — .NET 10 specific guidance
- codewithmukesh: [CQRS and MediatR in ASP.NET Core](https://codewithmukesh.com/blog/cqrs-and-mediatr-in-aspnet-core/) — MediatR integration patterns
- Code Maze: [Hexagonal Architectural Pattern in C#](https://code-maze.com/csharp-hexagonal-architectural-pattern/) — .NET solution structure with ports/adapters folders
- Jason Taylor CleanArchitecture template: [Web.csproj](https://github.com/jasontaylordev/CleanArchitecture/blob/main/src/Web/Web.csproj) — reference implementation confirming Api refs Application + Infrastructure
- DEV Community: [Clean Architecture in .NET 10 — Application Layer CQRS](https://dev.to/bspann/clean-architecture-in-net-10-the-application-layer-cqrs-without-the-ceremony-3j1l) — folder-per-feature CQRS structure
- Paulovich.NET: [Hexagonal and Clean Architecture Styles with .NET Core Reviewed](https://paulovich.net/hexagonal-and-clean-architecture-styles-with-net-core-reviewed/) — layer-to-hexagon mapping
