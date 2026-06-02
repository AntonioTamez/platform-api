---
phase: 03-infrastructure-layer
reviewed: 2026-05-30T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - src/PersonsAPI.Infrastructure/Persistence/Configurations/PersonEntityConfiguration.cs
  - src/PersonsAPI.Infrastructure/Persistence/PersonDbContext.cs
  - src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj
  - src/PersonsAPI.Infrastructure/Repositories/PersonRepository.cs
  - src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs
  - src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs
  - tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj
  - tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs
findings:
  critical: 1
  warning: 4
  info: 2
  total: 7
status: issues_found
---

# Phase 03: Infrastructure Layer — Code Review Report

**Reviewed:** 2026-05-30T00:00:00Z
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

The infrastructure layer is structurally sound. The EF Core wiring (`PersonDbContext`, `PersonEntityConfiguration`, `ServiceCollectionExtensions`) is correct and the repository pattern maps cleanly to the `IPersonRepository` secondary port. The `DataSeeder` correctly creates a dedicated scope before resolving a scoped `DbContext`, which is the right pattern.

However, there are real defects: a synchronous blocking call inside an async method in the seeder, a cancellation token that is never threaded through the seeder, an EF Core parameterless constructor with the wrong access modifier (leaks construction to application code), missing negative-path test coverage, and a missing `LangVersion` property in the test project. One of these (the synchronous `.Any()` call blocking the async path) is a BLOCKER because it can deadlock under synchronization contexts and is a correctness violation in an async-first codebase.

---

## Critical Issues

### CR-01: Synchronous `Any()` blocks the async `SeedAsync` method — potential deadlock

**File:** `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs:68`

**Issue:** `SeedAsync` is declared `async` and correctly awaits `SaveChangesAsync`, but the idempotency guard on line 68 calls the synchronous `context.Persons.Any()` instead of `await context.Persons.AnyAsync()`. In environments with a synchronization context (e.g., unit test runners, some hosting models), mixing a blocking LINQ-to-EF call with `await` in the same logical flow can deadlock. It also makes the method signature misleading: `SeedAsync` signals non-blocking I/O throughout, but the very first database touch is synchronous. For an InMemory provider the deadlock risk is low in practice, but the pattern is incorrect and will silently break if the provider is ever swapped for a real database or if the call site is surrounded by `.GetAwaiter().GetResult()`.

**Fix:**
```csharp
// Line 68 — replace synchronous Any() with its async counterpart
if (await context.Persons.AnyAsync()) return;
```

`AnyAsync` is in the `Microsoft.EntityFrameworkCore` namespace, which is already imported at line 1 of the file.

---

## Warnings

### WR-01: `CancellationToken` not accepted or propagated in `DataSeeder.SeedAsync`

**File:** `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs:63`

**Issue:** `SeedAsync` is a startup hook called before `app.Run()`, so cancellation is less critical than in request handlers. However, both `AnyAsync` and `SaveChangesAsync` accept a `CancellationToken`, and the calling convention for `IServiceProvider` extension methods that perform I/O consistently uses one (e.g., `EnsureCreatedAsync`). If the application host receives a shutdown signal during startup seeding, there is no way to abort gracefully. The omission also sets an inconsistent pattern for a codebase that correctly threads `CancellationToken` through every other async boundary.

**Fix:**
```csharp
public static async Task SeedAsync(this IServiceProvider services,
    CancellationToken cancellationToken = default)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PersonDbContext>();

    if (await context.Persons.AnyAsync(cancellationToken)) return;

    context.Persons.AddRange( /* ... */ );
    await context.SaveChangesAsync(cancellationToken);
}
```

---

### WR-02: EF Core parameterless constructor on `Person` is `protected` — wrong visibility for a `sealed` entity

**File:** `src/PersonsAPI.Domain/Entities/Person.cs:58`  
_(Cross-reference finding: the infrastructure layer relies on this constructor for EF Core materialization.)_

**Issue:** `Person` is `sealed`. A `protected` member on a sealed class is unreachable by any derived type (the compiler allows it but it is functionally equivalent to `private`). EF Core's InMemory and relational providers only require a parameterless constructor to be non-public — `private` is sufficient since EF Core accesses it via reflection. Declaring it `protected` implies intent for subclassing, which is explicitly prohibited by `sealed`, misleading future readers about the design intent. The correct modifier is `private`.

**Fix:**
```csharp
// In Person.cs — change protected to private
private Person() { }
```

---

### WR-03: Missing negative-path test coverage for `GetByIdAsync` and `GetAllAsync`

**File:** `tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs`

**Issue:** The test suite covers the happy path for every repository method but omits two important negative-path cases:

1. `GetByIdAsync` when the ID does not exist — the contract says it returns `null`, but there is no test asserting this. If the implementation were accidentally changed to throw instead of returning null, no test would catch it.
2. `GetAllAsync` when the store is empty — the contract says it never returns null and returns an empty list. This is asserted nowhere.

These are not cosmetic gaps; they directly correspond to the `IPersonRepository` contract (`/// <summary>Returns all persons. Never returns null; returns empty list when no records exist.</summary>` and `/// <summary>Returns the person with the given ID, or null if not found.</summary>`).

**Fix:** Add two test methods:

```csharp
[Fact]
public async Task GetByIdAsync_WhenPersonDoesNotExist_ReturnsNull()
{
    await using var context = CreateContext();
    var repo = new PersonRepository(context);

    var result = await repo.GetByIdAsync(99);

    Assert.Null(result);
}

[Fact]
public async Task GetAllAsync_WhenStoreIsEmpty_ReturnsEmptyList()
{
    await using var context = CreateContext();
    var repo = new PersonRepository(context);

    var result = await repo.GetAllAsync();

    Assert.NotNull(result);
    Assert.Empty(result);
}
```

---

### WR-04: Test project missing `LangVersion` property — diverges from production projects

**File:** `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj`

**Issue:** Both production projects (`PersonsAPI.Infrastructure.csproj` and, by convention, the domain/application projects) explicitly set `<LangVersion>14</LangVersion>`. The test project omits this property entirely, which means the SDK picks the default language version for `net10.0` (currently C# 13 in some SDK builds, C# 14 in others depending on the SDK patch). If the SDK defaults differ between developer machines or CI, test code that compiles locally with C# 14 features may silently fall back to a lower language version on another machine, producing confusing compiler errors or subtle behavioral differences.

**Fix:**
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <LangVersion>14</LangVersion>   <!-- add this line -->
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

---

## Info

### IN-01: `PersonEntityConfiguration` contains no column constraints — strings are unbounded in the schema

**File:** `src/PersonsAPI.Infrastructure/Persistence/Configurations/PersonEntityConfiguration.cs:29-33`

**Issue:** The domain validates string lengths (min 2, max 100) in `Person.ValidateName`, but the EF configuration does not call `builder.Property(p => p.FirstName).HasMaxLength(100)` (and similarly for `PaternalLastName` and `MaternalLastName`). For the InMemory provider this has no effect, but if the project is ever migrated to a real database the generated schema will produce `nvarchar(max)` columns instead of `nvarchar(100)`. The configuration is therefore not a faithful representation of the domain invariants.

**Fix:**
```csharp
public void Configure(EntityTypeBuilder<Person> builder)
{
    builder.HasKey(p => p.Id);
    builder.Ignore(p => p.Age);

    builder.Property(p => p.FirstName).IsRequired().HasMaxLength(100);
    builder.Property(p => p.PaternalLastName).IsRequired().HasMaxLength(100);
    builder.Property(p => p.MaternalLastName).IsRequired().HasMaxLength(100);
}
```

---

### IN-02: Seed data uses hardcoded `DateOnly` literals — ages drift over time in comments and docs

**File:** `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs:71-73`

**Issue:** The XML doc comment on line 60 documents approximate ages ("~32 yrs", "~47 yrs", "~62 yrs") that will become stale as calendar years advance. This is a documentation-only drift issue, not a runtime defect — the `Person.Age` property computes correctly at runtime. The concern is that future readers using the doc comment to reason about the data will see incorrect ages.

**Fix:** Remove the approximate-age annotations from the XML doc comment, or replace them with birth years only:
```xml
/// María García López (DOB 1994-06-15),
/// Carlos Ramírez Martínez (DOB 1979-03-22),
/// Ana Flores Mendoza (DOB 1963-11-08).
```

---

_Reviewed: 2026-05-30T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
