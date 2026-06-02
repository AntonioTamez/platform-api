# Phase 3: Infrastructure Layer - Context

**Gathered:** 2026-05-30
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the EF Core InMemory persistence adapter: `PersonDbContext`, `PersonEntityConfiguration` (with `builder.Ignore(p => p.Age)`), `PersonRepository` implementing `IPersonRepository`, and a `DataSeeder` that populates exactly 3 persons on startup. This layer is the secondary adapter in Hexagonal Architecture — it implements the port (`IPersonRepository`) defined by the Application layer. Nothing about HTTP, controllers, or ASP.NET Core middleware belongs here.

</domain>

<decisions>
## Implementation Decisions

### Seeder Data
- **D-01:** Seed exactly **3 persons** on startup. Three is the minimum in the INFRA-04 range (3–5) that is sufficient for GET all vs. GET by ID testing without noise.
- **D-02:** Use **realistic Mexican-style names** (FirstName + PaternalLastName + MaternalLastName) — the naming model the project was designed to represent. Concrete seed records:
  1. `María García López` — born `1994-06-15` (~32 years old)
  2. `Carlos Ramírez Martínez` — born `1979-03-22` (~47 years old)
  3. `Ana Flores Mendoza` — born `1963-11-08` (~62 years old)
- **D-03:** Ages are **intentionally varied** (~30, ~47, ~62) to exercise the computed `Age` property across different date ranges and confirm the month-and-day-aware algorithm produces distinct values.

### DataSeeder API
- **D-04:** The `DataSeeder` exposes its operation as an **extension method on `IServiceProvider`**: `SeedAsync(this IServiceProvider services)`. Program.cs (Phase 4) calls `await app.Services.SeedAsync()` before `app.Run()`. The seeder resolves a scoped `PersonDbContext` from a new DI scope internally.
- **D-05:** The seeder is **idempotent**: checks `!context.Persons.Any()` before inserting. If data already exists, it returns immediately without inserting. This is the correct pattern even for InMemory (which resets on restart) because it teaches the production-safe pattern.
- **D-06:** The `DataSeeder` is **not registered in DI**. It is a static class with an extension method — a startup initialization step, not a service with a managed lifetime. `AddInfrastructure()` registers only `PersonDbContext` and `PersonRepository`.

### Test Project
- **T-01:** Phase 3 includes `PersonsAPI.Infrastructure.Tests` (xUnit 2.9.3) with **CRUD-complete repository tests**: `GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync` — one test per repository method, covering the happy path.
- **T-02:** Each test uses an **isolated InMemory database**: `new DbContextOptionsBuilder<PersonDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())`. This prevents state contamination between tests without requiring `IClassFixture` coordination.

### Claude's Discretion
- Folder structure inside `PersonsAPI.Infrastructure/` (e.g., `Persistence/`, `Repositories/`, `Seeder/`) — Claude chooses idiomatic organization.
- Internal EF Core property access strategy for private setters — either `HasField`/`UsePropertyAccessMode` or relying on InMemory's reflection-based approach with `protected` constructor.
- Whether `PersonEntityConfiguration` is in a `Configurations/` subfolder or alongside `PersonDbContext`.
- `AddInfrastructure()` method naming and file placement.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Requirements
- `.planning/REQUIREMENTS.md` §Infrastructure — INFRA-01 (EF Core InMemory provider) and INFRA-04 (3–5 seeded persons on startup) are the two requirements this phase satisfies
- `.planning/ROADMAP.md` §Phase 3 — goal, success criteria (3 observable criteria), depends-on chain

### Project Constraints
- `.planning/PROJECT.md` §Constraints — all-English code, rich models, no Minimal API, no generic repository
- `.planning/PROJECT.md` §Key Decisions — EF Core InMemory rationale (applies real EF patterns with zero setup)
- `CLAUDE.md` §Recommended Stack — EF Core 10.0.8 + InMemory 10.0.8; exact package versions to use

### Prior Phase Decisions (MUST read before implementing)
- `.planning/phases/01-domain-layer/01-CONTEXT.md` — **D-08** (`Age` is computed, never stored — EF must ignore it via `builder.Ignore(p => p.Age)`), **D-14** (`protected Person()` constructor exists for EF materialization), **D-01** (`int Id` auto-increment assigned by Infrastructure/EF Core)
- `.planning/phases/02-application-layer/02-CONTEXT.md` — **D-03** (`IPersonRepository.GetByIdAsync` returns `Person?`, null when not found — repository must return null, not throw)

### Existing Source (read to understand contracts to implement)
- `src/PersonsAPI.Domain/Entities/Person.cs` — entity shape: which properties have private setters, the `protected Person()` constructor, `Age` getter (to confirm what must be ignored)
- `src/PersonsAPI.Application/Ports/IPersonRepository.cs` — the 5 async methods `PersonRepository` must implement exactly (signatures, return types, nullability)
- `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` — `AddApplication()` pattern to replicate as `AddInfrastructure()`

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Person` entity (`src/PersonsAPI.Domain/Entities/Person.cs`): `protected Person()` constructor is already present for EF Core materialization. `Person.Create()` is the factory used by the seeder to build valid seed records.
- `IPersonRepository` (`src/PersonsAPI.Application/Ports/IPersonRepository.cs`): defines the exact 5-method contract — `PersonRepository` implements this interface one-to-one.

### Established Patterns
- **`AddX(this IServiceCollection services)` DI extension method** — established by `AddApplication()` in Phase 2. `AddInfrastructure()` follows the same pattern, registering `PersonDbContext` (scoped) and `PersonRepository` (scoped, as `IPersonRepository`).
- **Project naming**: `PersonsAPI.Infrastructure` in `src/PersonsAPI.Infrastructure/`, test project in `tests/PersonsAPI.Infrastructure.Tests/`.
- **xUnit test isolation**: Phase 1 and 2 tests use constructor-based setup. Infrastructure tests use `Guid.NewGuid().ToString()` DB names for isolation instead of shared fixtures.
- **Exception contract (no Result<T>)**: `PersonRepository` returns `null` from `GetByIdAsync` when not found. It does not throw `PersonNotFoundException` — that is the Application layer's responsibility.

### Integration Points
- `PersonsAPI.Infrastructure.csproj` references `PersonsAPI.Application.csproj` (gets `PersonsAPI.Domain` transitively). No reference to `PersonsAPI.Api`.
- Phase 4 (API): `Program.cs` calls `builder.Services.AddInfrastructure()` and `await app.Services.SeedAsync()`. The Infrastructure project does not know about Phase 4 — dependency flows one way only.
- `PersonDbContext` is the concrete EF Core context. It must not be exposed beyond Infrastructure — Application interacts only via `IPersonRepository`.

</code_context>

<specifics>
## Specific Ideas

- Concrete seed data (D-01–D-03):
  ```csharp
  Person.Create("María",  "García",   "López",   new DateOnly(1994,  6, 15)),
  Person.Create("Carlos", "Ramírez",  "Martínez",new DateOnly(1979,  3, 22)),
  Person.Create("Ana",    "Flores",   "Mendoza", new DateOnly(1963, 11,  8)),
  ```
- `SeedAsync` resolves the DbContext from a DI scope — pattern:
  ```csharp
  public static async Task SeedAsync(this IServiceProvider services)
  {
      using var scope = services.CreateScope();
      var context = scope.ServiceProvider.GetRequiredService<PersonDbContext>();
      if (context.Persons.Any()) return;
      context.Persons.AddRange(/* 3 persons */);
      await context.SaveChangesAsync();
  }
  ```
- InMemory test setup pattern (per T-02):
  ```csharp
  private static PersonDbContext CreateContext() =>
      new(new DbContextOptionsBuilder<PersonDbContext>()
          .UseInMemoryDatabase(Guid.NewGuid().ToString())
          .Options);
  ```

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 3-Infrastructure Layer*
*Context gathered: 2026-05-30*
