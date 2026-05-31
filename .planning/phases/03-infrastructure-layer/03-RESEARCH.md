# Phase 3: Infrastructure Layer - Research

**Researched:** 2026-05-30
**Domain:** EF Core InMemory persistence adapter, Repository pattern, DI wiring
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Seeder Data**
- D-01: Seed exactly **3 persons** on startup.
- D-02: Use realistic Mexican-style names. Concrete records:
  1. `María García López` — born `1994-06-15`
  2. `Carlos Ramírez Martínez` — born `1979-03-22`
  3. `Ana Flores Mendoza` — born `1963-11-08`
- D-03: Ages intentionally varied (~32, ~47, ~62) to exercise the computed `Age` property.

**DataSeeder API**
- D-04: `DataSeeder` exposes `SeedAsync(this IServiceProvider services)` extension method. `Program.cs` calls `await app.Services.SeedAsync()` before `app.Run()`. Seeder resolves scoped `PersonDbContext` from a new DI scope internally.
- D-05: Seeder is idempotent — checks `!context.Persons.Any()` before inserting.
- D-06: `DataSeeder` is NOT registered in DI. It is a static class with extension method. `AddInfrastructure()` registers only `PersonDbContext` and `PersonRepository`.

**Test Project**
- T-01: Phase 3 includes `PersonsAPI.Infrastructure.Tests` (xUnit 2.9.3) with CRUD-complete repository tests: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` — one test per method, happy path.
- T-02: Each test uses isolated InMemory database: `new DbContextOptionsBuilder<PersonDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())`.

### Claude's Discretion
- Folder structure inside `PersonsAPI.Infrastructure/` (e.g., `Persistence/`, `Repositories/`, `Seeder/`)
- Internal EF Core property access strategy for private setters — either `HasField`/`UsePropertyAccessMode` or relying on InMemory's reflection-based approach with `protected` constructor
- Whether `PersonEntityConfiguration` is in a `Configurations/` subfolder or alongside `PersonDbContext`
- `AddInfrastructure()` method naming and file placement

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| INFRA-01 | EF Core InMemory provider is used as the persistence adapter — no real database required | PersonDbContext configured with UseInMemoryDatabase; PersonEntityConfiguration applies builder.Ignore for Age; AddInfrastructure() registers context and repository via AddDbContext |
| INFRA-04 | Application seeds 3–5 hardcoded Person records on startup for immediate manual testing | DataSeeder static class with idempotent SeedAsync extension method; creates 3 Person records via Person.Create() factory |
</phase_requirements>

---

## Summary

Phase 3 builds the secondary adapter in Hexagonal Architecture: the EF Core InMemory persistence layer that implements `IPersonRepository` (defined in Application). The layer consists of four concrete artifacts: `PersonDbContext`, `PersonEntityConfiguration`, `PersonRepository`, and `DataSeeder`. Each is straightforward to implement given the locked decisions in CONTEXT.md and the existing contracts from Phases 1 and 2.

The key technical insight for this phase is that EF Core InMemory works seamlessly with `private set` properties. EF Core accesses private setters via reflection during entity materialization — no `UsePropertyAccessMode` configuration is required when properties have private (not init-only) setters. The `protected Person()` constructor already in place from Phase 1 enables EF Core to instantiate entities before populating their properties.

The `builder.Ignore(p => p.Age)` call in `PersonEntityConfiguration` is the critical correctness gate for the computed `Age` property. Without it, EF Core would attempt to map the getter-only `Age` property to a column (or shadow property in InMemory), which is architecturally wrong and would cause a runtime model-building error since `Age` has no setter.

**Primary recommendation:** Follow the established AddApplication() DI extension pattern. Use `AddDbContext<PersonDbContext>` (scoped lifetime), `ApplyConfigurationsFromAssembly` in `OnModelCreating`, and `Guid.NewGuid().ToString()` as the InMemory database name in tests.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| EF Core DbContext | Infrastructure | — | DbContext is the secondary adapter; Application never sees it directly |
| IPersonRepository implementation | Infrastructure | — | Secondary port (Application) implemented by secondary adapter (Infrastructure) |
| DataSeeder | Infrastructure (startup) | — | Startup initialization step wired by Api layer (Phase 4); logic lives in Infrastructure |
| DI registration (`AddInfrastructure`) | Infrastructure | — | Extension method on IServiceCollection; called from Api's Program.cs in Phase 4 |
| IPersonRepository interface (port) | Application | — | Already exists from Phase 2; Infrastructure references Application, not vice versa |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.EntityFrameworkCore | 10.0.8 | EF Core base types (DbContext, ModelBuilder) | First-party Microsoft; matches Application layer version |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.8 | InMemory database provider | Only package needed for zero-setup persistence simulation |

[VERIFIED: nuget.org/packages/Microsoft.EntityFrameworkCore.InMemory — latest stable 10.0.8, published 2026-05-12]

### Supporting (Test Project Only)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit | 2.9.3 | Test framework | Matches all existing test projects in solution |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test runner host | Required to run dotnet test |
| xunit.runner.visualstudio | 3.1.4 | VS Test Explorer integration | Matches existing test projects |
| coverlet.collector | 6.0.4 | Code coverage | Matches existing test projects |

[VERIFIED: All test package versions match the already-approved versions in PersonsAPI.Domain.Tests and PersonsAPI.Application.Tests .csproj files — consistency is the driver]

**Note on xunit 2.9.3 deprecation:** xunit 2.x is marked deprecated on NuGet in favor of xunit.v3. However, the project has already standardized on xunit 2.9.3 in two existing test projects. Introducing xunit.v3 would create an inconsistency. The Infrastructure test project MUST use the same versions as the existing test projects. [ASSUMED: xunit.v3 would be the forward-looking choice but consistency wins here]

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `UseInMemoryDatabase(Guid.NewGuid().ToString())` in tests | `UseInMemoryDatabase("fixed-name")` + `EnsureDeleted()` | Guid name is simpler — no cleanup step needed per test; tests are fully isolated by default |
| `ApplyConfigurationsFromAssembly` | Inline `OnModelCreating` configuration | `ApplyConfigurationsFromAssembly` scales better as entities grow; keeps DbContext clean |
| Static `DataSeeder` class | `IHostedService` background seeder | Static extension method is simpler and sufficient for InMemory/testing scenario; IHostedService adds complexity only needed for production resilience |

**Installation (Infrastructure project):**
```bash
dotnet add src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.8
```

**Installation (Test project):**
```bash
dotnet add tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj package Microsoft.EntityFrameworkCore.InMemory --version 10.0.8
dotnet add tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj package xunit --version 2.9.3
dotnet add tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj package Microsoft.NET.Test.Sdk --version 17.14.1
dotnet add tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj package xunit.runner.visualstudio --version 3.1.4
dotnet add tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj package coverlet.collector --version 6.0.4
```

---

## Package Legitimacy Audit

> slopcheck does not support the NuGet ecosystem. All packages are Microsoft first-party or long-established community packages already approved and used in this solution.

| Package | Registry | Age | Publisher | Disposition |
|---------|----------|-----|-----------|-------------|
| Microsoft.EntityFrameworkCore.InMemory 10.0.8 | NuGet | ~10 yrs (EF Core) | Microsoft | Approved — first-party, [VERIFIED: nuget.org] |
| Microsoft.EntityFrameworkCore 10.0.8 | NuGet | ~10 yrs | Microsoft | Approved — first-party, [VERIFIED: nuget.org] |
| xunit 2.9.3 | NuGet | ~12 yrs | xUnit.net | Approved — already in solution (Domain.Tests, Application.Tests) [VERIFIED: existing .csproj] |
| Microsoft.NET.Test.Sdk 17.14.1 | NuGet | ~10 yrs | Microsoft | Approved — already in solution [VERIFIED: existing .csproj] |
| xunit.runner.visualstudio 3.1.4 | NuGet | ~10 yrs | xUnit.net | Approved — already in solution [VERIFIED: existing .csproj] |
| coverlet.collector 6.0.4 | NuGet | ~6 yrs | coverlet | Approved — already in solution [VERIFIED: existing .csproj] |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none
*slopcheck not used (NuGet not supported); all packages verified against existing solution .csproj files and nuget.org.*

---

## Architecture Patterns

### System Architecture Diagram

```
Phase 4 (Api) — Program.cs
      |
      | calls AddInfrastructure()
      | calls await app.Services.SeedAsync()
      v
PersonsAPI.Infrastructure
      |
      +-- PersonDbContext (DbContext)
      |       |
      |       +-- DbSet<Person> Persons
      |       |
      |       +-- OnModelCreating()
      |               |
      |               +-- ApplyConfigurationsFromAssembly()
      |                       |
      |                       v
      |               PersonEntityConfiguration (IEntityTypeConfiguration<Person>)
      |                       |
      |                       +-- builder.HasKey(p => p.Id)
      |                       +-- builder.Ignore(p => p.Age)  <-- CRITICAL
      |
      +-- PersonRepository : IPersonRepository (Application Port)
      |       |
      |       +-- GetAllAsync()    --> context.Persons.ToListAsync() --> IReadOnlyList<Person>
      |       +-- GetByIdAsync()   --> context.Persons.FindAsync(id) --> Person?
      |       +-- AddAsync()       --> context.Persons.AddAsync() + SaveChangesAsync()
      |       +-- UpdateAsync()    --> context.Persons.Update() + SaveChangesAsync()
      |       +-- DeleteAsync()    --> context.Persons.Remove() + SaveChangesAsync()
      |
      +-- DataSeeder (static class)
              |
              +-- SeedAsync(this IServiceProvider services)
                      |
                      +-- CreateScope()
                      +-- GetRequiredService<PersonDbContext>()
                      +-- if (!context.Persons.Any()) --> AddRange + SaveChangesAsync()
```

### Recommended Project Structure

```
src/PersonsAPI.Infrastructure/
├── Persistence/
│   ├── PersonDbContext.cs              # DbContext with DbSet<Person> Persons
│   └── Configurations/
│       └── PersonEntityConfiguration.cs  # IEntityTypeConfiguration<Person>
├── Repositories/
│   └── PersonRepository.cs            # Implements IPersonRepository
├── Seeder/
│   └── DataSeeder.cs                  # Static class with SeedAsync extension
├── ServiceCollectionExtensions.cs     # AddInfrastructure() extension method
└── PersonsAPI.Infrastructure.csproj   # References Application project

tests/PersonsAPI.Infrastructure.Tests/
├── Repositories/
│   └── PersonRepositoryTests.cs       # 5 tests, one per IPersonRepository method
└── PersonsAPI.Infrastructure.Tests.csproj
```

### Pattern 1: DbContext with IEntityTypeConfiguration

**What:** Separate entity configuration into `IEntityTypeConfiguration<T>` classes, apply via `ApplyConfigurationsFromAssembly` in `OnModelCreating`.
**When to use:** Standard pattern for all EF Core projects with more than trivial configuration.

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration

// PersonDbContext.cs
public sealed class PersonDbContext : DbContext
{
    public PersonDbContext(DbContextOptions<PersonDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonDbContext).Assembly);
    }
}

// PersonEntityConfiguration.cs
public sealed class PersonEntityConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Ignore(p => p.Age);   // Age is computed — never mapped
        // No explicit Property() calls needed; EF convention maps remaining properties
    }
}
```

### Pattern 2: AddDbContext DI Registration (Scoped)

**What:** Register DbContext as scoped service, matching the HTTP request lifetime in ASP.NET Core.
**When to use:** All ASP.NET Core applications — scoped is the correct default lifetime.

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/

// ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<PersonDbContext>(options =>
            options.UseInMemoryDatabase("PersonsDb"));

        services.AddScoped<IPersonRepository, PersonRepository>();

        return services;
    }
}
```

### Pattern 3: Repository Implementation — No IQueryable Leaks

**What:** Repository wraps EF context, materializes queries to `IReadOnlyList<T>` before returning.
**When to use:** Always. Callers must not compose LINQ over repository results.

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database

public sealed class PersonRepository : IPersonRepository
{
    private readonly PersonDbContext _context;

    public PersonRepository(PersonDbContext context) => _context = context;

    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Persons.ToListAsync(cancellationToken);

    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await _context.Persons.FindAsync([id], cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        await _context.Persons.AddAsync(person, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        _context.Persons.Update(person);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Person person, CancellationToken cancellationToken = default)
    {
        _context.Persons.Remove(person);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
```

### Pattern 4: DataSeeder as IServiceProvider Extension

**What:** Static class with extension method on `IServiceProvider`; creates own DI scope to resolve scoped `PersonDbContext`.
**When to use:** Startup initialization that must not share the root scope.

```csharp
// Source: CONTEXT.md D-04 / D-05

public static class DataSeeder
{
    public static async Task SeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PersonDbContext>();

        if (context.Persons.Any()) return;   // Idempotent — D-05

        context.Persons.AddRange(
            Person.Create("María",  "García",   "López",    new DateOnly(1994,  6, 15)),
            Person.Create("Carlos", "Ramírez",  "Martínez", new DateOnly(1979,  3, 22)),
            Person.Create("Ana",    "Flores",   "Mendoza",  new DateOnly(1963, 11,  8))
        );
        await context.SaveChangesAsync();
    }
}
```

### Pattern 5: Isolated InMemory Test Database

**What:** Each test creates its own InMemory database with a unique name, preventing state contamination.
**When to use:** All repository tests in `PersonsAPI.Infrastructure.Tests`.

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database
// Confirmed: CONTEXT.md T-02

private static PersonDbContext CreateContext() =>
    new(new DbContextOptionsBuilder<PersonDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);
```

### Anti-Patterns to Avoid

- **Exposing IQueryable from repository:** `GetAllAsync` must return `IReadOnlyList<Person>`, not `IQueryable<Person>`. Callers would compose LINQ over the return value, creating a hidden dependency on EF Core internals and making tests impossible without a real EF context.
- **Using context.Update() on a freshly queried entity:** For UpdateAsync, the entity was queried and is already tracked. Calling `_context.Update(person)` on a tracked entity marks all properties as modified — harmless for InMemory but misleading. The correct pattern is `_context.Persons.Update(person)` for a disconnected scenario. For this phase, entities come through Application layer handlers which query first, so tracking is in effect.
- **Registering DataSeeder in DI (D-06):** DataSeeder is a static class with an extension method. Adding it to DI as a service creates lifetime confusion and is unnecessary for a startup initialization step.
- **Calling SaveChangesAsync in GetAllAsync or GetByIdAsync:** Read operations must never call SaveChanges. The repository methods that mutate state (Add, Update, Delete) are the only ones that call SaveChangesAsync.
- **Seeding via OnModelCreating:** EF Core's `HasData()` seeder is for migrations with real databases. For InMemory with idempotent runtime seeding, `DataSeeder.SeedAsync()` is the correct approach.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| InMemory database lifetime management | Custom in-memory store or static Dictionary<> | `UseInMemoryDatabase` | EF handles change tracking, identity generation, concurrency |
| Test isolation | `[Collection]` fixtures with shared context | `Guid.NewGuid().ToString()` database name | New database per test with zero coordination overhead |
| DI scope management in seeder | Manual scope bookkeeping | `services.CreateScope()` + `using` | DI handles lifetime; `using` ensures scoped context is disposed |
| Property ignore configuration | Custom DTO/mapper exclusion | `builder.Ignore(p => p.Age)` | One-line fluent API; EF will error if computed property accidentally mapped |
| Auto-increment Id | Setting Id manually | EF convention on `int Id` | EF InMemory assigns sequential integer Ids automatically via value generation |

**Key insight:** EF Core InMemory already implements the patterns that look like they need custom code. Delegate everything (Id generation, change tracking, scoping) to EF rather than duplicating it.

---

## Common Pitfalls

### Pitfall 1: Forgetting builder.Ignore(p => p.Age)

**What goes wrong:** If `Age` is not ignored, EF Core tries to include it in the model. Since `Age` has no setter (getter-only computed property), EF Core will throw a runtime exception during model building: `The property 'Person.Age' could not be mapped because it is of type 'int', which is not a supported primitive type or a valid entity type.` Actually EF's behavior depends on the version — in some versions it silently creates a shadow property. Either way it is wrong.
**Why it happens:** By convention, EF Core maps all public properties with a getter and setter. `Age` has only a getter, so EF may still attempt to map it as a computed column or shadow property.
**How to avoid:** `PersonEntityConfiguration.Configure()` MUST include `builder.Ignore(p => p.Age)` as its first explicit configuration line.
**Warning signs:** Runtime `InvalidOperationException` during DbContext initialization, or unexpected "Age" shadow property in the model debug view.

### Pitfall 2: Private Setters vs Init-Only Setters — EF Core Behavior Differs

**What goes wrong:** Developers confuse `private set` (writable via reflection) with `init` (write-once, not writable via reflection after construction). EF Core can set `private set` properties during materialization using reflection. It cannot set `init`-only properties after object construction without special configuration.
**Why it happens:** C# 9+ `init` accessors look like setters but are construction-time-only at the CLR level.
**How to avoid:** The existing `Person` entity uses `private set` — this works with EF Core InMemory out of the box. No `UsePropertyAccessMode` configuration is needed. Do NOT change `private set` to `init` in the domain entity.
**Warning signs:** If `init` were used, EF would throw during materialization (entity comes back from query with all default property values).

### Pitfall 3: Fixed InMemory Database Name Across Tests

**What goes wrong:** If all repository tests share the same `UseInMemoryDatabase("PersonsDb")` name, EF InMemory reuses the same in-memory store across all test instances. State from one test leaks into subsequent tests, causing non-deterministic failures.
**Why it happens:** InMemory database is keyed by name. The same name within the same process and service provider equals the same database.
**How to avoid:** Use `Guid.NewGuid().ToString()` as the database name in each `CreateContext()` call. This is the established pattern for this project (T-02 in CONTEXT.md).
**Warning signs:** Tests pass in isolation but fail when run together; order-dependent test failures.

### Pitfall 4: Resolving PersonDbContext from Root IServiceProvider in DataSeeder

**What goes wrong:** If `SeedAsync` resolves `PersonDbContext` directly from the root `IServiceProvider` (without creating a scope), ASP.NET Core throws: `Cannot resolve scoped service 'PersonDbContext' from root provider.`
**Why it happens:** `AddDbContext` registers `PersonDbContext` as scoped. The root `IServiceProvider` does not allow resolving scoped services — this is a DI safety guard.
**How to avoid:** Always use `services.CreateScope()` inside `SeedAsync`, then resolve from `scope.ServiceProvider`. The `using` ensures the scoped context is properly disposed. This is the exact pattern in D-04 of CONTEXT.md.
**Warning signs:** `InvalidOperationException: Cannot resolve scoped service...` at application startup.

### Pitfall 5: Missing Project Reference from Infrastructure to Application

**What goes wrong:** `PersonsAPI.Infrastructure.csproj` must reference `PersonsAPI.Application.csproj` to access `IPersonRepository` and the `Person` entity (transitively via Domain). Without this reference, the compiler cannot resolve these types.
**Why it happens:** Project references must be explicit in .NET SDK projects — transitive references do not cross project boundaries for compilation.
**How to avoid:** Add `<ProjectReference Include="..\PersonsAPI.Application\PersonsAPI.Application.csproj" />` to the Infrastructure .csproj during project creation. Do NOT reference Domain directly — Application already references it, and the transitive dependency covers it.
**Warning signs:** Build errors: `The type or namespace name 'IPersonRepository' could not be found`.

### Pitfall 6: Adding Microsoft.EntityFrameworkCore.InMemory to Domain or Application

**What goes wrong:** INFRA-02 (already satisfied in Phase 1) requires that Domain has zero EF Core references. If EF InMemory is accidentally added to Domain or Application, it violates the architectural isolation rule.
**Why it happens:** Developers installing EF packages to the wrong project.
**How to avoid:** Install EF Core packages ONLY to `PersonsAPI.Infrastructure` and the Infrastructure test project. Verify .csproj files after `dotnet add package` commands.

---

## Code Examples

Verified patterns from official sources:

### DbContext Constructor Pattern (DI-Compatible)

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
// Use the generic DbContextOptions<TContext> overload for DI compatibility
public sealed class PersonDbContext : DbContext
{
    public PersonDbContext(DbContextOptions<PersonDbContext> options) : base(options) { }
}
```

### AddDbContext Registration

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
// Registers as Scoped by default — matches HTTP request lifetime
services.AddDbContext<PersonDbContext>(options =>
    options.UseInMemoryDatabase("PersonsDb"));
```

### ApplyConfigurationsFromAssembly

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonDbContext).Assembly);
}
```

### builder.Ignore for Computed Property

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/modeling/entity-types (excluding types section)
// Prevents EF from mapping Age — the computed domain property
builder.Ignore(p => p.Age);
```

### FindAsync for Single-Entity Lookup (returns null if not found)

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/saving/basic
// FindAsync uses the PK to look up by tracked state first, then queries the store.
// Returns null when not found — matches IPersonRepository.GetByIdAsync contract (Application D-03).
await _context.Persons.FindAsync([id], cancellationToken);
```

### IEntityTypeConfiguration<T> Signature

```csharp
// Source: https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration
public sealed class PersonEntityConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        // configuration here
    }
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Inline OnModelCreating for all entity config | `IEntityTypeConfiguration<T>` + `ApplyConfigurationsFromAssembly` | EF Core 2.0 | DbContext stays clean; configurations scale per-entity |
| xUnit 2.x (now deprecated) | xUnit v3 (xunit.v3 package) | 2024 | v3 is MIT, v2 is deprecated legacy; project standardized on 2.9.3 — keep consistent |
| `HasData()` for seed data | Runtime `DataSeeder` with idempotency check | — | `HasData()` is migration-oriented; not appropriate for InMemory or startup seeding |

**Deprecated/outdated:**
- **xunit 2.9.3**: Marked deprecated by xUnit.net; xunit.v3 is the recommended successor. However, this solution already has two test projects on 2.9.3, so changing now would create inconsistency. This is a v2 upgrade item.
- **`EnsureDeleted()` + fixed name for test isolation**: Older pattern — `Guid.NewGuid().ToString()` eliminates the need for explicit cleanup.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | xunit 2.9.3 is the correct choice for the Infrastructure test project (consistency with existing projects outweighs deprecation) | Standard Stack | Low: xunit 2.9.3 works on .NET 10; only risk is missing v3 improvements |
| A2 | `private set` properties on Person work with EF Core InMemory via reflection without explicit `UsePropertyAccessMode` configuration | Common Pitfalls / Architecture | Medium: If wrong, `UsePropertyAccessMode(PropertyAccessMode.Field)` would be needed; easy fix |

**All other claims were verified against official Microsoft Learn documentation or directly observed in the existing codebase.**

---

## Open Questions

1. **EF Core private setter access mode (discretion area)**
   - What we know: EF Core uses reflection to set `private set` properties by convention; `protected Person()` constructor exists for materialization; InMemory provider does not enforce relational constraints
   - What's unclear: Whether any configuration at the `PersonEntityConfiguration` level is needed to ensure EF Core correctly populates private-setter properties on the InMemory provider (convention should handle it automatically)
   - Recommendation: Start without `UsePropertyAccessMode` configuration. If repository tests fail with properties returning default values after a query, add `builder.Property(p => p.FirstName).UsePropertyAccessMode(PropertyAccessMode.Property)` (and similar for other properties) to force setter access via the property accessor rather than a backing field.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All compilation and test execution | Yes | 10.0.202 | — |
| NuGet (dotnet add package) | Package installation | Yes | via SDK 10.0.202 | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

---

## Project Constraints (from CLAUDE.md)

Directives from `./CLAUDE.md` that constrain this phase:

| Directive | Impact on Phase 3 |
|-----------|------------------|
| `.NET 10` / `net10.0` target framework | All .csproj files use `<TargetFramework>net10.0</TargetFramework>` |
| `LangVersion 14` | All .csproj files use `<LangVersion>14</LangVersion>` |
| `Nullable enable` | All .csproj files use `<Nullable>enable</Nullable>` |
| `ImplicitUsings enable` | Reduces using directives in source files |
| `Controllers only — no Minimal API` | Not applicable to this phase (no HTTP) |
| `Rich models — business logic in domain entity` | PersonRepository must not embed domain logic; just delegates to IPersonRepository contract |
| `All identifiers and comments in English` | Code, XML doc comments, and class names all in English |
| `EF Core InMemory 10.0.8` | Package version is locked |
| `No generic IRepository<T>` | PersonRepository implements IPersonRepository specifically, not a generic base |
| `Domain project has zero EF Core references (INFRA-02)` | EF Core packages installed only in Infrastructure and Infrastructure.Tests |
| `IPersonRepository port lives in Application` | Already satisfied in Phase 2; Infrastructure references Application |
| `Manual static mapping (no AutoMapper)` | Not applicable to this phase (Infrastructure does not map to DTOs) |
| `xUnit for tests` | Infrastructure.Tests uses xunit 2.9.3 (matching existing projects) |

---

## Security Domain

> `security_enforcement: true` and `security_asvs_level: 1` per .planning/config.json.

### Applicable ASVS Categories (ASVS Level 1)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No user-facing auth in Infrastructure layer |
| V3 Session Management | No | No session state in this layer |
| V4 Access Control | No | No access control decisions in repository layer |
| V5 Input Validation | Partial | Domain entity validates invariants in Person.Create() (already done in Phase 1); repository accepts pre-validated Person instances only |
| V6 Cryptography | No | No secrets, passwords, or cryptographic operations |
| V7 Error Handling | Yes | Repository must not expose EF Core internals in exceptions; let EF exceptions propagate naturally — Phase 4 (exception middleware) handles them |
| V14 Configuration | Partial | No connection strings or secrets — InMemory has no credentials to protect |

### Known Threat Patterns for EF Core InMemory

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SQL injection | Tampering | Not applicable — InMemory has no SQL surface |
| Mass assignment via tracked entity | Tampering | Application layer handlers call domain update methods (UpdateName, UpdateDateOfBirth) — never direct property assignment |
| Exposing EF DbContext to Application | Info Disclosure | PersonDbContext is internal to Infrastructure; Application interacts only via IPersonRepository |

**Security note:** The InMemory provider is zero-risk from a credential/connection-string perspective. The primary security concern for this phase is architectural: ensuring `PersonDbContext` does not leak beyond the Infrastructure project boundary. This is enforced at compile time by project reference topology (Application does not reference Infrastructure).

---

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn — EF Core DbContext Configuration](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/) — DbContext lifetime, AddDbContext, DI patterns, constructor requirements
- [Microsoft Learn — EF Core Entity Types](https://learn.microsoft.com/en-us/ef/core/modeling/entity-types) — Fluent API exclude (Ignore), table mapping conventions
- [Microsoft Learn — EF Core Entity Properties](https://learn.microsoft.com/en-us/ef/core/modeling/entity-properties) — Property inclusion conventions, private setter behavior, NRT handling
- [Microsoft Learn — EF Core Backing Fields](https://learn.microsoft.com/en-us/ef/core/modeling/backing-field) — PropertyAccessMode, private setter access patterns
- [Microsoft Learn — EF Core InMemory Provider](https://learn.microsoft.com/en-us/ef/core/providers/in-memory/) — Provider capabilities and limitations
- [Microsoft Learn — EF Core Testing Without Production Database](https://learn.microsoft.com/en-us/ef/core/testing/testing-without-the-database) — InMemory test isolation patterns, Guid-named databases
- [Microsoft Learn — EF Core Basic SaveChanges](https://learn.microsoft.com/en-us/ef/core/saving/basic) — Add/Update/Remove patterns
- [Microsoft Learn — EF Core Grouping Configuration](https://learn.microsoft.com/en-us/ef/core/modeling/#grouping-configuration) — IEntityTypeConfiguration, ApplyConfigurationsFromAssembly
- [NuGet — Microsoft.EntityFrameworkCore.InMemory 10.0.8](https://www.nuget.org/packages/microsoft.entityframeworkcore.inmemory) — version confirmed 10.0.8, published 2026-05-12

### Secondary (MEDIUM confidence)
- Existing solution .csproj files (PersonsAPI.Domain.Tests, PersonsAPI.Application.Tests) — xunit 2.9.3, coverlet 6.0.4, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4 confirmed as established package versions

### Tertiary (LOW confidence)
- None — all claims in this research are HIGH or MEDIUM confidence.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — packages confirmed on nuget.org; versions match existing solution
- Architecture: HIGH — verified against official EF Core Microsoft Learn documentation
- Pitfalls: HIGH — verified against official EF Core docs; pitfall 2 (private vs init setters) is a subtle but documented behavior
- Test patterns: HIGH — matches established project conventions from Phases 1 and 2

**Research date:** 2026-05-30
**Valid until:** 2026-08-30 (stable ecosystem; EF Core 10.x is LTS-aligned)
