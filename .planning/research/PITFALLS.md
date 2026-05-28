# Domain Pitfalls

**Domain:** .NET 10 Clean Architecture + Hexagonal Architecture Web API (Personal Data / Person entity)
**Researched:** 2026-05-27
**Confidence:** HIGH — pitfalls verified against official Microsoft docs, multiple .NET-specific community sources, and EF Core documentation

---

## Critical Pitfalls

Mistakes that cause rewrites, invalidate the learning goals, or produce silently wrong behavior.

---

### Pitfall 1: Accidentally Anemic Person Entity

**What goes wrong:** The Person entity is declared with `public` property setters and no behavior methods. Business logic — validation, state transitions, Age derivation — migrates to the Application layer handler or the controller. The entity becomes a data bag.

**Why it happens:** EF Core's default conventions encourage parameterless constructors and public setters so the ORM can materialize objects. Developers satisfy EF's requirements by opening up the entity and never close it back down. The project compiles and works, so no one notices.

**Consequences:**
- Age calculation scatters into handlers, controllers, or extension methods — inconsistency guaranteed.
- Any handler can set `FirstName = ""` without validation. Domain invariants are unenforced.
- Unit testing domain logic requires spinning up Application services instead of testing the entity directly.
- The explicit requirement "no anemic models" is violated while the code looks structurally correct.

**Warning signs:**
- `Person` has `public set` on every property.
- `PersonService` or a handler contains `if (person.DateOfBirth > today) throw ...` instead of the entity throwing.
- `Age` is computed in the Application layer, not in a property getter on `Person`.
- No public factory method or constructor with required parameters exists on `Person`.

**Prevention strategy:**
1. Give `Person` a single public constructor that requires all fields: `public Person(string firstName, string paternalLastName, string maternalLastName, DateOnly dateOfBirth)`. Validate inside it.
2. Use `private set` (or `init`-only setters) for all properties.
3. Make `Age` a computed property `public int Age => CalculateAge(DateOfBirth);` — no setter at all, never stored.
4. Add a private parameterless constructor for EF: `private Person() { }` — this satisfies EF without exposing mutation.
5. Add intention-revealing update methods: `public void UpdateName(string firstName, ...)` with guards inside.

**Phase/layer:** Domain project — must be correct before any other layer is built. Infrastructure and Application depend on whatever contract Domain establishes.

---

### Pitfall 2: Business Logic Placed in Application Layer Instead of Domain

**What goes wrong:** Developers correctly identify "Application layer handles use cases" but interpret this as "Application layer contains all logic." Validation rules, Age derivation, and domain invariants end up in command handlers (`CreatePersonCommandHandler`, etc.).

**Why it happens:** The Application layer is where orchestration happens — it is the most active layer developers write. It is tempting to put logic there because it is "close to where things happen." The distinction between *orchestration* (Application) and *invariant enforcement* (Domain) is subtle.

**Consequences:**
- The same invariant must be duplicated in every handler that touches Person — e.g., every update handler repeats the date-of-birth validation.
- Domain entities cannot be tested in isolation — you must go through the handler.
- Future handlers that bypass the Application layer (background jobs, seeding) violate invariants silently.

**Warning signs (the wrong pattern):**
```csharp
// Handler: business rule incorrectly in Application
public async Task Handle(UpdatePersonCommand cmd, CancellationToken ct)
{
    var person = await _repo.GetByIdAsync(cmd.Id);
    if (cmd.DateOfBirth > DateOnly.FromDateTime(DateTime.Today))
        throw new ValidationException("Date of birth cannot be in the future.");
    person.DateOfBirth = cmd.DateOfBirth; // public setter: also wrong
    await _repo.UpdateAsync(person);
}
```

**Correct pattern:**
```csharp
// Handler: orchestrates only
public async Task Handle(UpdatePersonCommand cmd, CancellationToken ct)
{
    var person = await _repo.GetByIdAsync(cmd.Id);
    person.UpdateDateOfBirth(cmd.DateOfBirth); // entity enforces its own rule
    await _repo.UpdateAsync(person);
}

// Domain entity: enforces the invariant
public void UpdateDateOfBirth(DateOnly dateOfBirth)
{
    if (dateOfBirth >= DateOnly.FromDateTime(DateTime.Today))
        throw new DomainException("Date of birth must be in the past.");
    DateOfBirth = dateOfBirth;
}
```

**Prevention strategy:**
- Rule of thumb: if the logic is about whether a Person *can* be in a certain state, it belongs in Domain. If it is about *how to coordinate* fetching, saving, and returning, it belongs in Application.
- Application layer handlers should read like prose: get entity, call entity method, persist. No conditionals about domain invariants.

**Phase/layer:** Domain layer (invariant methods on `Person`). Application layer only orchestrates.

---

### Pitfall 3: EF Core Leaking into the Domain Layer

**What goes wrong:** EF Core persistence concerns bleed into the Domain project through three distinct vectors:

**Vector A — Data annotations on the domain entity:**
```csharp
[Required]           // EF/validation attribute — belongs in Infrastructure config
[MaxLength(100)]     // persistence concern — belongs in IEntityTypeConfiguration
[Column("first_name")] // database concern — should not touch Domain
public string FirstName { get; set; }
```
The Domain project now has a direct `Microsoft.EntityFrameworkCore` dependency.

**Vector B — Navigation properties:**
```csharp
public class Person
{
    public ICollection<Address> Addresses { get; set; } = new List<Address>(); // EF navigation
}
```
Navigation properties are persistence-graph concepts. They encourage callers to use `.Include()` from Application or Presentation, leaking query knowledge out of the repository.

**Vector C — `IQueryable<T>` from repositories:**
```csharp
// Port in Application/Domain — DO NOT return IQueryable
public interface IPersonRepository
{
    IQueryable<Person> GetAll(); // caller can add any LINQ — no abstraction exists
}
```
Any handler can call `.Where()`, `.Include()`, `.OrderBy()` on the result — the repository abstraction is meaningless.

**Why it happens:** EF Core documentation shows data annotations on entity classes because it is the quickest path. Developers copy examples directly. `IQueryable` feels convenient and lazy — "let the caller decide the query."

**Consequences:**
- Domain project gets a NuGet dependency on EF Core — testability of Domain without EF infrastructure is broken.
- Swapping persistence (e.g., to a real DB later) requires touching Domain.
- `IQueryable` leakage: Application handlers become tightly coupled to EF LINQ — query logic is not encapsulated.

**Warning signs:**
- Domain `.csproj` references `Microsoft.EntityFrameworkCore`.
- Entity class file has `using Microsoft.EntityFrameworkCore` or attribute imports.
- Repository interface returns `IQueryable<Person>` instead of `IEnumerable<Person>` or `Task<IReadOnlyList<Person>>`.
- Application handlers call `.Include()` or `.Where()` on what the repository returned.

**Prevention strategy:**
1. Zero EF Core references in the Domain project — enforce this with a `.csproj` audit.
2. Configure EF mappings exclusively in Infrastructure using `IEntityTypeConfiguration<Person>` and the Fluent API. `OnModelCreating` in `PersonDbContext` (Infrastructure) is where `HasMaxLength`, `HasColumnName`, etc. live.
3. Repository interface returns materialized collections: `Task<IReadOnlyList<Person>>` or `Task<Person?>`. Never `IQueryable`.
4. For InMemory: even `IQueryable` works with EF InMemory — that does not make it correct. Use `.ToListAsync()` inside the repository implementation and return `IReadOnlyList<Person>` across the boundary.

**Phase/layer:** Domain project (zero EF references). Infrastructure project (all EF configuration). Application project (depends on port interfaces, never on EF types).

---

### Pitfall 4: Circular Project References

**What goes wrong:** A developer adds a reference in the wrong direction — most commonly, the Domain project references Application (to use a DTO or service), or Infrastructure references Presentation.

```
// Wrong: Domain → Application (Domain should have zero outward references)
// Wrong: Application → Infrastructure (breaks the inversion)
// Wrong: Infrastructure → Presentation
```

In .NET, the compiler prevents actual circular references (A → B → A). However, developers work around this by:
- Dumping shared types (DTOs, interfaces, enums) into Domain to avoid reference issues — polluting Domain with non-domain concerns.
- Merging Application and Infrastructure into one project to avoid wiring up DI — losing the boundary entirely.

**Why it happens:** When a type is "needed everywhere," placing it in the innermost project (Domain) feels logical. Shared DTOs, response models, and error types end up in Domain even though Domain should not know about HTTP responses or command objects.

**Warning signs:**
- Domain project contains `PersonDto`, `CreatePersonRequest`, or any type with the word "Request", "Response", "Command", or "DTO" — these belong in Application or Presentation.
- Application project has `using PersonsAPI.Infrastructure` anywhere.
- A single `PersonsAPI.Core` project contains both domain entities and application services mixed together.

**Prevention strategy:**
- Enforce the dependency direction: Domain → (nothing). Application → Domain. Infrastructure → Application + Domain. Presentation → Application.
- Permitted project reference graph for this solution:
  ```
  PersonsAPI.Presentation  →  PersonsAPI.Application
  PersonsAPI.Infrastructure →  PersonsAPI.Application + PersonsAPI.Domain
  PersonsAPI.Application   →  PersonsAPI.Domain
  PersonsAPI.Domain        →  (no project references)
  ```
- Interfaces (ports) live in the layer that *consumes* them. `IPersonRepository` lives in Application (or Domain if it is truly domain-driven), implemented in Infrastructure. No reference from Application to Infrastructure is ever needed because DI wires it at startup in the Presentation/Host project.
- Shared primitives (Result types, DomainException base class) stay in Domain as they are domain concerns.

**Phase/layer:** Solution structure — must be correct at project creation. Extremely costly to fix after code is written across layers.

---

### Pitfall 5: Conflating Clean Architecture Layers with Hexagonal Ports and Adapters

**What goes wrong:** Developers treat Clean Architecture and Hexagonal Architecture as synonyms with different names. They implement Clean Architecture's four layers and declare it "also Hexagonal" — but never think in terms of primary/driving ports vs. secondary/driven ports.

**The conceptual distinction that must be preserved:**

| Concept | Clean Architecture | Hexagonal Architecture |
|---------|-------------------|----------------------|
| Structure | Concentric rings (Domain → Application → Infrastructure → Presentation) | Application core surrounded by ports; adapters outside |
| Dependency direction | Always inward | Core depends on nothing external |
| Interface role | Contract between adjacent layers | Port: explicit entry/exit point for a specific actor type |
| Adapter concept | Not named explicitly | Driving adapter (calls in) vs. driven adapter (called by core) |
| "Layer" vs. "Port" | Layers enforce layering | Ports enforce actor isolation |

**What conflation looks like in code:**
- Every interface is called a "port" without distinction — `IPersonRepository` and `IPersonController` are both called "ports" even though controllers are driving adapters, not ports.
- The application core calls `IPersonRepository` but the developer places that interface in Presentation, which makes no topological sense.
- "Hexagonal" is treated as just a synonym for "dependency inversion" — no distinction between driving and driven sides.

**Correct mapping for this project:**
```
Driving (Primary) side — Presentation layer:
  Controller (HTTP adapter) → calls → IPersonService / ICreatePersonUseCase (primary port in Application)

Driven (Secondary) side — Infrastructure layer:
  Application core calls → IPersonRepository (secondary port in Application/Domain)
  PersonRepository (EF Core adapter) implements IPersonRepository
```

**Warning signs:**
- The developer cannot explain the difference between a "driving adapter" and a "driven adapter" for this specific project.
- `IPersonRepository` is defined in the Infrastructure project (wrong — the port lives with the consumer, the implementation lives in Infrastructure).
- Controllers are described as "primary ports" — they are adapters that *use* primary ports.

**Prevention strategy:**
- Explicitly label interfaces in code comments: `// Primary port (driving): called by HTTP adapter (controller)` and `// Secondary port (driven): implemented by EF Core adapter (repository)`.
- Place secondary port interfaces (IPersonRepository) in the Application project — Infrastructure references Application to implement them. This is correct and avoids circular dependencies.
- Ports are defined by the hexagon (Application core). Adapters live outside and implement or consume ports.

**Phase/layer:** Application project (defines ports). Infrastructure project (implements driven/secondary adapters). Presentation project (contains driving/primary adapters — controllers).

---

### Pitfall 6: PATCH Implementation Mistakes in a Layered Architecture

**What goes wrong:** PATCH is the most architecturally tricky operation in a layered API. Three distinct failure modes exist:

**Failure Mode A — JsonPatchDocument typed to the Domain entity:**
```csharp
// Controller: WRONG — domain entity crosses into Presentation
[HttpPatch("{id}")]
public IActionResult Patch(int id, [FromBody] JsonPatchDocument<Person> patchDoc)
{
    var person = _repo.GetById(id); // infrastructure leaking into Presentation
    patchDoc.ApplyTo(person);       // patch applied directly to domain entity
}
```
The domain entity now needs public setters to be patchable — this destroys the rich model.

**Failure Mode B — Skipping model validation before applying the patch:**
`ModelState.IsValid` is `true` even when a JSON Patch document targets non-existent properties. Validation must happen *after* `ApplyTo()`, not before. Developers check `ModelState` at the top of the action (before `ApplyTo`) and consider themselves done — invalid patch paths get applied silently.

**Failure Mode C — Applying the patch to the Application DTO/command, then re-validating before passing to domain:**
This is actually the correct pattern but developers skip the re-validation step after applying the patch, allowing invalid state (empty first name, future birth date) to reach the domain without domain guard methods catching it.

**Correct pattern for .NET 10:**
```csharp
// In .NET 10, use Microsoft.AspNetCore.JsonPatch.SystemTextJson package
// Controller action — typed to a mutable Presentation DTO, not to Person domain entity
[HttpPatch("{id}")]
public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)
{
    // 1. Fetch current state as DTO (via Application layer query)
    var current = await _mediator.Send(new GetPersonQuery(id));
    if (current is null) return NotFound();

    var dto = _mapper.Map<UpdatePersonDto>(current);

    // 2. Apply patch to the DTO (not the domain entity)
    patchDoc.ApplyTo(dto, ModelState);

    // 3. Validate AFTER ApplyTo — ModelState may now have errors
    if (!ModelState.IsValid) return UnprocessableEntity(ModelState);

    // 4. Send the validated DTO to Application layer
    await _mediator.Send(new UpdatePersonCommand(id, dto));
    return NoContent();
}
```

**Warning signs:**
- `JsonPatchDocument<Person>` (domain entity) in controller parameter.
- `ModelState.IsValid` checked *before* `patchDoc.ApplyTo()`.
- `ApplyTo` called without passing `ModelState` — errors are swallowed.
- No separate `UpdatePersonDto` or `PatchPersonDto` — the domain entity doubles as the patch target.

**Prevention strategy:**
- Define a dedicated mutable `UpdatePersonDto` (or `PatchPersonDto`) in Application layer. PATCH applies to this DTO.
- The Application command handler receives the validated DTO and calls domain entity methods — domain never sees `JsonPatchDocument`.
- In .NET 10: install `Microsoft.AspNetCore.JsonPatch.SystemTextJson` (not the Newtonsoft version). It is not a drop-in replacement — it does not support dynamic types.
- Always pass `ModelState` to `ApplyTo()`: `patchDoc.ApplyTo(dto, ModelState)`.

**Phase/layer:** Presentation project handles PATCH binding and `JsonPatchDocument`. Application project defines `UpdatePersonDto` and the command. Domain project exposes update methods that the command handler calls.

---

## Moderate Pitfalls

Mistakes that produce incorrect behavior or architectural drift but do not force a complete rewrite.

---

### Pitfall 7: Age Calculation Off-by-One and Timezone Errors

**What goes wrong:** Age calculated incorrectly in one of three ways:

**Error A — Simple year subtraction:**
```csharp
// WRONG: returns 31 on Dec 31 for someone born Jan 1, 1995, but also returns 31 on Jan 1 before midnight
public int Age => DateTime.Today.Year - DateOfBirth.Year;
```
Returns the wrong age if the birthday has not yet occurred this calendar year. A person born December 31, 1994 shows age 31 on January 1, 2026 — they are still 30.

**Error B — DateTime.Now with timezone:**
```csharp
// WRONG: server timezone affects "today" — a UTC server shows wrong date for users in UTC+8 past midnight
public int Age => DateTime.Now.Year - DateOfBirth.Year; // Now includes time component
```
On a UTC server, a user in Tokyo at 1 AM on their birthday gets an incorrect age for most of the day.

**Error C — Leap year birthday (Feb 29):**
A person born February 29 in a leap year — if the code does a month/day comparison in a non-leap year, `02/29` does not exist and naive comparisons fail or throw.

**Correct algorithm using DateOnly (no timezone issue):**
```csharp
public int Age
{
    get
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow); // or DateTime.Today — see note
        var age = today.Year - DateOfBirth.Year;
        // Subtract 1 if birthday has not yet occurred this year
        if (DateOfBirth.DayOfYear > today.DayOfYear
            // Handle leap year: Feb 29 birthday in non-leap year — treat as Feb 28
            || (DateTime.IsLeapYear(DateOfBirth.Year)
                && DateOfBirth.Month == 2
                && DateOfBirth.Day == 29
                && !DateTime.IsLeapYear(today.Year)
                && today.Month == 2
                && today.Day == 28))
        {
            // Already handled by DayOfYear comparison in most cases
        }
        // Cleaner: compare month+day directly
        if (new DateOnly(today.Year, DateOfBirth.Month, 1).AddDays(DateOfBirth.Day - 1) > today)
            age--;
        return age;
    }
}
```

**Simplest correct implementation:**
```csharp
public int Age
{
    get
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        int age = today.Year - DateOfBirth.Year;
        // Check if birthday has not occurred yet this year
        if (DateOfBirth.Month > today.Month
            || (DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day))
        {
            age--;
        }
        return age;
    }
}
```

**Warning signs:**
- `DateTime.Now.Year - DateOfBirth.Year` with no birthday-occurrence check.
- Age computed using `DateTime` (time-of-day included) instead of `DateOnly` or `DateTime.Today`.
- No unit test for a date-of-birth on December 31 checked on January 1.
- No consideration for what "today" means on a UTC server.

**Prevention strategy:**
- Use `DateOnly` for `DateOfBirth` storage — removes the time component problem by design.
- Use `DateTime.UtcNow` (not `DateTime.Now`) for the "today" reference to avoid server timezone issues. For a learning project with InMemory, `DateTime.Today` is acceptable but note the limitation.
- Unit test three cases: birthday today (correct age), birthday tomorrow (age - 1), birthday yesterday (correct age).
- Keep Age computation in the `Person` entity — it is domain logic, not infrastructure.

**Phase/layer:** Domain entity `Person` — `Age` property getter. Test coverage at Domain unit test level.

---

### Pitfall 8: Over-Engineering vs. Under-Engineering for a Learning Project

**What goes wrong:** Two opposite failure modes exist for a learning exercise:

**Over-engineering (adds complexity without learning value):**
- Implementing CQRS with MediatR when the goal is to understand Hexagonal boundaries — MediatR hides the port/adapter relationship behind a message bus.
- Adding Event Sourcing, Domain Events, or Outbox patterns before the base architecture is understood.
- Creating separate read models and write models (CQRS projections) for a 5-field entity.
- Adding FluentValidation when C# guard clauses in the entity constructor demonstrate the same concept more clearly.
- Implementing Unit of Work on top of EF Core's built-in `SaveChanges` transaction — EF already provides this.

**Under-engineering (misses the learning goals):**
- Using a `List<Person>` instead of EF Core InMemory — skips the DbContext/repository pattern the project exists to demonstrate.
- Putting all code in a single project — no layer boundaries to observe.
- Making `Age` a stored field in the database — misses the "derived/computed domain property" lesson.
- Using a static helper class for Age calculation instead of an entity method — misses the rich model lesson.
- Skipping interfaces (ports) and directly injecting `PersonRepository` into handlers — the hexagonal boundary disappears.

**Warning signs (over-engineering):**
- The solution has more than 5 projects for a 1-entity learning app.
- Every simple query goes through a Command/Query object + Handler + Dispatcher when the goal is not to learn CQRS.
- Adding infrastructure concerns (Redis, Serilog structured logging, health checks) before the architecture itself is demonstrated.

**Warning signs (under-engineering):**
- `PersonsController` directly instantiates `new PersonRepository()` — no DI, no port.
- `PersonRepository` returns `List<Person>` and is referenced directly by the controller.
- Age is a settable property on the entity.

**Prevention strategy:**
- Scope by goal: this project's learning goal is "how do Clean and Hexagonal coexist." Every design choice should teach that. MediatR obscures it; direct service interfaces reveal it.
- Keep it to four projects: `Domain`, `Application`, `Infrastructure`, `Presentation` (Web API host).
- Use constructor injection everywhere (this teaches DI and port/adapter wiring).
- Do implement: port interfaces in Application, repository in Infrastructure, controllers as adapters in Presentation, domain entity with behavior. These are the learning targets.
- Do not implement: CQRS, Domain Events, FluentValidation, AutoMapper (manual mapping is fine and more transparent for learning).

**Phase/layer:** Solution architecture decision — made once at project start. Review before adding any new abstraction.

---

## Minor Pitfalls

---

### Pitfall 9: EF Core InMemory Behavioral Differences vs. Real Databases

**What goes wrong:** Developers write application logic that only works with InMemory and silently fails when switching to a real database.

**Specific InMemory behaviors that differ from SQL:**
- No referential integrity enforcement — foreign keys are not validated.
- No SQL transactions — `BeginTransaction()` exists but does nothing useful.
- Case-sensitive string comparisons by default — `WHERE firstName = 'john'` might not match 'John' in InMemory but does in SQL Server with default collation.
- No `LIKE` SQL translation for complex contains — may behave differently.
- `IQueryable` queries over InMemory work in-process — this makes it appear that returning `IQueryable` from repositories is fine, when it would be a real problem against SQL Server where the query must be fully expressible in SQL.

**Warning signs:**
- String comparisons in LINQ without `.ToLower()` normalization.
- Tests pass with InMemory but logic is not verifiable against SQL behavior.
- Repository returns `IQueryable` — "works fine" in InMemory hides the abstraction violation.

**Prevention strategy:**
- Treat InMemory as a simulation tool, not a test for SQL correctness.
- Write queries as if they will run against SQL Server — use translated LINQ, not in-memory tricks.
- The `IQueryable` abstraction violation is still wrong even if InMemory makes it work — enforce the boundary now.
- Document: "Switch to SQL Server by replacing the InMemory registration in DI — no other code should change." This is the test of whether the architecture is truly clean.

**Phase/layer:** Infrastructure project (repository implementations). DI registration in Presentation (host).

---

### Pitfall 10: Missing Private Constructor for EF Core Materialization

**What goes wrong:** Developer removes the parameterless constructor from `Person` (correctly, to enforce a rich model) but forgets to add a private one for EF Core. EF Core throws at runtime when attempting to materialize entities from the database.

```csharp
// EF Core needs a way to create instances during query materialization
// It will use the private parameterless constructor — this is fine
public class Person
{
    private Person() { } // for EF Core only

    public Person(string firstName, ...)  // enforced public constructor
    {
        // validation here
    }
}
```

**Warning signs:**
- `InvalidOperationException: No suitable constructor found for entity type 'Person'` at runtime.
- Developer adds `public Person() { }` to fix the error — reopening the model to invalid state.

**Prevention strategy:**
- Always add `private Person() { }` alongside the parameterless constructor removal. This is documented EF Core behavior — the ORM uses the most specific constructor it can, falling back to the parameterless one.
- The private constructor satisfies EF without exposing it to application code.

**Phase/layer:** Domain entity `Person`. Verified when Infrastructure (EF Core) is first wired up.

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Person entity creation | Public setters + parameterless constructor satisfying EF requests | Add `private Person()` for EF, `public Person(...)` for code, `private set` everywhere |
| Age property | Year-subtraction-only algorithm | Month+day comparison; DateOnly type; unit test edge cases |
| IPersonRepository interface | Returning IQueryable or placing interface in Infrastructure | Place in Application; return `Task<IReadOnlyList<Person>>` |
| EF Core DbContext setup | Data annotations on Person entity | Fluent API in `IEntityTypeConfiguration<Person>` inside Infrastructure only |
| PATCH endpoint | JsonPatchDocument typed to domain entity | Define `UpdatePersonDto` in Application; patch applies to DTO, not entity |
| DI registration / Startup | Injecting Infrastructure types directly into Application handlers | Program.cs wires adapters to ports; Application sees only interfaces |
| Hexagonal labeling | Calling everything a "port" without distinguishing driving vs. driven | Controllers = driving adapters; IPersonRepository = driven port; label clearly |
| Adding features | Reaching for MediatR, AutoMapper, FluentValidation | Ask: "does this teach the architecture, or hide it?" — prefer transparency |

---

## Sources

- [Clean Architecture in .NET 10: The Infrastructure Layer — EF Core Without the Leakage](https://dev.to/bspann/clean-architecture-in-net-10-the-infrastructure-layer-ef-core-without-the-leakage-55dn) — HIGH confidence (DEV, .NET 10 specific)
- [3 Ways To Avoid An Anemic Domain Model In Entity Framework](https://www.devtrends.co.uk/blog/3-ways-to-avoid-an-anemic-domain-model-in-ef-core) — HIGH confidence (EF Core focused, practical code examples)
- [The Real Difference Between Domain and Application Layer in Clean Architecture](https://bytecrafted.dev/domain-vs-application-layer-clean-architecture/) — HIGH confidence (concrete C# examples verified)
- [JsonPatch in ASP.NET Core web API — Microsoft Learn (.NET 10)](https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch?view=aspnetcore-10.0) — HIGH confidence (official Microsoft documentation)
- [When (not) use JSON Patch in ASP.NET Core](https://inwedo.com/blog/when-not-use-json-patch-in-asp-net-core/) — MEDIUM confidence (real-world testimony, single source)
- [Comparison of Ports in Hexagonal Architecture and Interfaces in Clean Architecture](https://leaders.tec.br/article/8793e4) — MEDIUM confidence (conceptual analysis, not .NET-specific)
- [DDD + Clean Architecture: Stop Putting Business Logic in the Application Layer](https://journal.optivem.com/p/ddd-clean-architecture-dont-put-business-logic-in-application-layer) — HIGH confidence (widely cited, matches official Clean Architecture doctrine)
- [EF Core: Effectively decouple the data and domain model](https://dev.to/thecodewrapper/ef-core-effectively-decouple-the-data-and-domain-model-4h8j) — HIGH confidence (.NET specific, IQueryable analysis verified)
- [How to calculate age in C#](https://www.clintmcmahon.com/blog/how-to-calculate-age-in-c) — MEDIUM confidence (algorithm verified against multiple sources)
- [How to use DateOnly and TimeOnly — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly) — HIGH confidence (official Microsoft documentation)
- [Rich vs Anemic domain model](https://medium.com/@mr.karegar/rich-vs-anemic-domain-model-d4bd8cbe221a) — MEDIUM confidence (conceptual, corroborated by other sources)
- [Hexagonal Architecture and Clean Architecture (with examples)](https://dev.to/dyarleniber/hexagonal-architecture-and-clean-architecture-with-examples-48oi) — MEDIUM confidence (examples, not .NET 10 specific)
