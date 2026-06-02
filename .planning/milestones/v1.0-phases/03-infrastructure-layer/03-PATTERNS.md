# Phase 3: Infrastructure Layer - Pattern Map

**Mapped:** 2026-05-30
**Files analyzed:** 8 new files (6 source + 2 project files)
**Analogs found:** 8 / 8

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` | config | n/a | `src/PersonsAPI.Application/PersonsAPI.Application.csproj` | exact |
| `src/PersonsAPI.Infrastructure/Persistence/PersonDbContext.cs` | service | CRUD | `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` (structural ref only) | partial |
| `src/PersonsAPI.Infrastructure/Persistence/Configurations/PersonEntityConfiguration.cs` | config | CRUD | no codebase analog | research-only |
| `src/PersonsAPI.Infrastructure/Repositories/PersonRepository.cs` | service | CRUD | `src/PersonsAPI.Application/Ports/IPersonRepository.cs` (contract) | role-match |
| `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs` | utility | batch | `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` (static class pattern) | partial |
| `src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs` | config | n/a | `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` | exact |
| `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` | config | n/a | `tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj` | exact |
| `tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs` | test | CRUD | `tests/PersonsAPI.Domain.Tests/PersonTests.cs` | role-match |

---

## Pattern Assignments

### `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` (config)

**Analog:** `src/PersonsAPI.Application/PersonsAPI.Application.csproj`

**Project file pattern** (lines 1-20, full file):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\PersonsAPI.Application\PersonsAPI.Application.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

</Project>
```

**Key decisions:**
- `<ProjectReference>` points to Application (NOT Domain directly — Domain is transitive).
- Only `Microsoft.EntityFrameworkCore.InMemory` is needed; it pulls in `Microsoft.EntityFrameworkCore` transitively.
- `LangVersion>14</LangVersion>` matches all existing src projects (Application line 17, Domain line 7).
- No `<IsPackable>false</IsPackable>` — that flag is only on test projects.

---

### `src/PersonsAPI.Infrastructure/Persistence/PersonDbContext.cs` (service, CRUD)

**Analog:** No direct codebase analog. Pattern sourced from RESEARCH.md Pattern 1.

**Imports pattern** (derive from project namespace convention):
```csharp
using Microsoft.EntityFrameworkCore;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Infrastructure.Persistence;
```

**Core pattern** — sealed class, primary constructor, DbSet as expression-bodied property, `ApplyConfigurationsFromAssembly`:
```csharp
public sealed class PersonDbContext(DbContextOptions<PersonDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonDbContext).Assembly);
    }
}
```

**Authoring notes:**
- Use C# 14 primary constructor — consistent with Application layer handler pattern.
- `DbSet<Person> Persons => Set<Person>()` — expression-bodied, no backing field, EF convention.
- `ApplyConfigurationsFromAssembly` scans the Infrastructure assembly for all `IEntityTypeConfiguration<T>` implementations automatically.
- No explicit `base(options)` call needed with primary constructors; pass via inheritance chain.

---

### `src/PersonsAPI.Infrastructure/Persistence/Configurations/PersonEntityConfiguration.cs` (config, CRUD)

**Analog:** No codebase analog. Pattern sourced from RESEARCH.md Pattern 1 + Pitfall 1.

**Imports pattern:**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Infrastructure.Persistence.Configurations;
```

**Core pattern** — sealed, implements `IEntityTypeConfiguration<Person>`, `builder.Ignore` is mandatory:
```csharp
public sealed class PersonEntityConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Ignore(p => p.Age);   // Age is computed — never stored (Domain D-08)
        // No explicit Property() calls needed; EF convention maps remaining private-setter
        // properties (FirstName, PaternalLastName, MaternalLastName, DateOfBirth) via reflection.
    }
}
```

**Critical:** `builder.Ignore(p => p.Age)` must be present. Without it, EF will attempt to map the
getter-only computed `Age` property and throw a runtime `InvalidOperationException` during model building
(RESEARCH.md Pitfall 1).

---

### `src/PersonsAPI.Infrastructure/Repositories/PersonRepository.cs` (service, CRUD)

**Analog:** `src/PersonsAPI.Application/Ports/IPersonRepository.cs` (contract to implement exactly)

**Contract to implement** (IPersonRepository.cs lines 1-25 — full file):
```csharp
// Every method below must be implemented one-to-one:
Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);
Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
Task AddAsync(Person person, CancellationToken cancellationToken = default);
Task UpdateAsync(Person person, CancellationToken cancellationToken = default);
Task DeleteAsync(Person person, CancellationToken cancellationToken = default);
```

**Imports pattern:**
```csharp
using Microsoft.EntityFrameworkCore;
using PersonsAPI.Application.Ports;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;

namespace PersonsAPI.Infrastructure.Repositories;
```

**Core pattern** — sealed class, primary constructor, explicit interface implementation, no IQueryable leaks:
```csharp
public sealed class PersonRepository(PersonDbContext context) : IPersonRepository
{
    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Persons.ToListAsync(cancellationToken);

    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Persons.FindAsync([id], cancellationToken);

    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        await context.Persons.AddAsync(person, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Remove(person);
        await context.SaveChangesAsync(cancellationToken);
    }
}
```

**Key decisions:**
- Use C# 14 primary constructor — same pattern as Application layer handlers.
- `GetByIdAsync` returns `null` on miss (Application D-03 from Phase 2 CONTEXT.md) — `FindAsync` returns null naturally.
- `GetAllAsync` returns `IReadOnlyList<Person>` — `ToListAsync` materializes and satisfies the covariance.
- Read methods (`GetAllAsync`, `GetByIdAsync`) must NEVER call `SaveChangesAsync` (RESEARCH.md anti-pattern).
- `context.Persons.Update(person)` is the correct EF call for a disconnected-scenario update.

---

### `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs` (utility, batch)

**Analog:** `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` — static class with extension method pattern (lines 23-72)

**Static class pattern from analog** (ServiceCollectionExtensions.cs lines 23-24, 58-71):
```csharp
// Analog shows: static class with a single public static method that extends a framework type
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // ... registrations ...
        return services;
    }
}
```

**Imports pattern:**
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;

namespace PersonsAPI.Infrastructure.Seeder;
```

**Core pattern** — static class, extension on `IServiceProvider`, creates own scope (D-04), idempotent check (D-05), NOT registered in DI (D-06):
```csharp
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

**Critical:** `services.CreateScope()` is mandatory — resolving a scoped `PersonDbContext` from the
root `IServiceProvider` without a scope throws `InvalidOperationException` at startup (RESEARCH.md Pitfall 4).
`DataSeeder` must NOT be passed to `services.Add*(...)` in `AddInfrastructure()` — it is a static
startup utility, not a DI service (D-06).

---

### `src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs` (config)

**Analog:** `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` (lines 23-72, full file)

**Imports pattern from analog** (lines 1-5):
```csharp
using Microsoft.Extensions.DependencyInjection;
// Add for Infrastructure:
using Microsoft.EntityFrameworkCore;
using PersonsAPI.Application.Ports;
using PersonsAPI.Infrastructure.Persistence;
using PersonsAPI.Infrastructure.Repositories;

namespace PersonsAPI.Infrastructure;
```

**Static class and method signature from analog** (lines 23-24, 58):
```csharp
// Analog pattern — replicate exactly, changing class content:
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

**Key decisions:**
- `AddDbContext<PersonDbContext>` registers the context as Scoped (default lifetime) — matches HTTP request lifetime.
- `UseInMemoryDatabase("PersonsDb")` uses a fixed name for the running app (distinct from tests which use Guid names).
- `AddScoped<IPersonRepository, PersonRepository>()` — scoped to match DbContext lifetime; both resolve in the same HTTP scope.
- `DataSeeder` is NOT registered here (D-06).
- Returns `services` for fluent chaining — matches analog at line 71.

---

### `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` (config)

**Analog:** `tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj` (lines 1-27, full file)

**Project file pattern from analog** (full file, adapting for Infrastructure):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PersonsAPI.Infrastructure\PersonsAPI.Infrastructure.csproj" />
  </ItemGroup>

</Project>
```

**Key decisions:**
- `<Using Include="Xunit" />` — matches both existing test projects; eliminates `using Xunit;` from every test file.
- `<IsPackable>false</IsPackable>` — matches both existing test projects.
- Packages match Domain.Tests.csproj (lines 10-14) and Application.Tests.csproj (lines 10-16) exactly.
- `Microsoft.EntityFrameworkCore.InMemory` is added to the test project (not just Infrastructure src) so tests can call `UseInMemoryDatabase` directly via `DbContextOptionsBuilder`.
- Note: NO `LangVersion` in test projects — follow the same omission as both existing test project analogs.

---

### `tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs` (test, CRUD)

**Analog:** `tests/PersonsAPI.Domain.Tests/PersonTests.cs` (lines 1-327, best structural match for xUnit test class organization)

**Namespace and imports pattern from analog** (PersonTests.cs lines 1-6):
```csharp
using Microsoft.EntityFrameworkCore;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;
using PersonsAPI.Infrastructure.Repositories;

namespace PersonsAPI.Infrastructure.Tests.Repositories;
```

**Test class organization pattern from analog** (PersonTests.cs lines 8-16 — sealed class, static helpers, [Fact] methods):
```csharp
// Analog pattern — sealed test class, static helper for test setup
public sealed class PersonRepositoryTests
{
    // Static factory for isolated InMemory context (T-02)
    private static PersonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PersonDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Valid seed helper — mirrors PersonTests.cs static readonly field pattern (line 12)
    private static Person CreateValidPerson(string firstName = "María",
        string paternalLastName = "García",
        string maternalLastName = "López",
        DateOnly? dateOfBirth = null)
        => Person.Create(firstName, paternalLastName, maternalLastName,
            dateOfBirth ?? new DateOnly(1994, 6, 15));
}
```

**Happy-path test pattern from analog** (PersonTests.cs lines 21-33 — Arrange/Act/Assert, single [Fact]):
```csharp
[Fact]
public async Task GetAllAsync_WhenPersonsExist_ReturnsAllPersons()
{
    // Arrange
    await using var context = CreateContext();
    var repo = new PersonRepository(context);
    var person = CreateValidPerson();
    context.Persons.Add(person);
    await context.SaveChangesAsync();

    // Act
    var result = await repo.GetAllAsync();

    // Assert
    Assert.Single(result);
    Assert.Equal("María", result[0].FirstName);
}
```

**Test structure for all 5 CRUD methods** (one [Fact] per IPersonRepository method, per T-01):
- `GetAllAsync_WhenPersonsExist_ReturnsAllPersons`
- `GetByIdAsync_WhenPersonExists_ReturnsPerson`
- `AddAsync_PersistsPersonAndAssignsId`
- `UpdateAsync_PersistsChangesToPerson`
- `DeleteAsync_RemovesPersonFromStore`

**Key decisions:**
- Each test creates its own `CreateContext()` — never shared context (T-02, RESEARCH.md Pitfall 3).
- `await using var context` — `DbContext` implements `IAsyncDisposable`; use `await using` for correct disposal.
- Inline stub pattern (no Moq) — matches Application.Tests approach (ValidationBehaviorTests.cs lines 107-138).
- Section comment blocks (`// ---------------------------------------------------------------------------`) match PersonTests.cs organizational style.

---

## Shared Patterns

### C# 14 Primary Constructor
**Source:** `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` (established by Phase 2)
**Apply to:** `PersonDbContext`, `PersonRepository`
```csharp
// Instead of traditional constructor:
public sealed class PersonRepository(PersonDbContext context) : IPersonRepository
// EF-compatible primary constructor for DbContext:
public sealed class PersonDbContext(DbContextOptions<PersonDbContext> options) : DbContext(options)
```

### Static Class Extension Method
**Source:** `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` lines 23-71
**Apply to:** `ServiceCollectionExtensions.cs` (AddInfrastructure), `DataSeeder.cs` (SeedAsync)
```csharp
// The pattern: public static class with a single public static method extending a framework type
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // ... registrations ...
        return services;   // fluent chaining
    }
}
```

### Sealed Classes
**Source:** All existing non-interface, non-record types in `src/PersonsAPI.Domain/Entities/Person.cs` (line 9) and `src/PersonsAPI.Application/` files
**Apply to:** `PersonDbContext`, `PersonEntityConfiguration`, `PersonRepository`
```csharp
// Every concrete implementation class is sealed — prevents unintended inheritance
public sealed class PersonDbContext ...
public sealed class PersonEntityConfiguration ...
public sealed class PersonRepository ...
```

### XML Summary Documentation
**Source:** `src/PersonsAPI.Application/Ports/IPersonRepository.cs` lines 7-24; `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` lines 7-57
**Apply to:** All public types and methods in `src/PersonsAPI.Infrastructure/`
```csharp
/// <summary>
/// One-line description of class purpose in English.
/// </summary>
public sealed class PersonDbContext ...
```

### Namespace Matches Folder Path
**Source:** All existing files — e.g., `PersonsAPI.Application/Ports/IPersonRepository.cs` uses `namespace PersonsAPI.Application.Ports;` (line 3)
**Apply to:** All Infrastructure files
```csharp
// Folder: src/PersonsAPI.Infrastructure/Persistence/
namespace PersonsAPI.Infrastructure.Persistence;
// Folder: src/PersonsAPI.Infrastructure/Repositories/
namespace PersonsAPI.Infrastructure.Repositories;
// Folder: src/PersonsAPI.Infrastructure/Seeder/
namespace PersonsAPI.Infrastructure.Seeder;
// Root folder: src/PersonsAPI.Infrastructure/
namespace PersonsAPI.Infrastructure;
```

### xUnit Test Project Global Using
**Source:** `tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj` lines 19-21; `tests/PersonsAPI.Domain.Tests/PersonsAPI.Domain.Tests.csproj` lines 19-21
**Apply to:** `PersonsAPI.Infrastructure.Tests.csproj`
```xml
<ItemGroup>
  <Using Include="Xunit" />
</ItemGroup>
```
This eliminates `using Xunit;` from every test file — do not add it manually.

---

## No Analog Found

All files have analogs or are fully specified by RESEARCH.md. No files require planner to rely on
RESEARCH.md alone:

| File | Resolution |
|------|------------|
| `PersonDbContext.cs` | RESEARCH.md Pattern 1 + Pattern 2 (exact code) |
| `PersonEntityConfiguration.cs` | RESEARCH.md Pattern 1 + Pitfall 1 (exact code) |

---

## Metadata

**Analog search scope:** `C:/ATS/Git/platform/src/`, `C:/ATS/Git/platform/tests/`
**Files scanned:** 10 source files read (Domain entity, Application interface, Application SCE, Application test project files x2, Domain test file, Application test file x2, Solution file)
**Pattern extraction date:** 2026-05-30
