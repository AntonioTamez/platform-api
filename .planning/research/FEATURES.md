# Feature Landscape

**Domain:** .NET 10 REST API — Clean Architecture + Hexagonal Architecture (learning/reference project)
**Researched:** 2026-05-27
**Confidence:** HIGH — all claims verified against official Microsoft docs, Context7-resolved library docs, and multiple credible community sources.

---

## Table Stakes

Features the project must have for the learning goal to be meaningful. Missing any of these means the project does not demonstrate the stated patterns.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| GET all persons | Baseline CRUD query | Low | Returns list of PersonDto — no pagination needed at this scale |
| GET person by ID | Baseline CRUD query | Low | Returns 404 (Problem Details) when not found |
| POST person | Creates new aggregate root | Low-Med | Validates invariants in domain, maps to CreatePersonCommand |
| PUT person | Full replacement update | Low-Med | Maps to UpdatePersonCommand; all fields required |
| PATCH person | Partial update | Med | Use JSON Patch (RFC 6902) via `Microsoft.AspNetCore.JsonPatch.SystemTextJson` — the .NET 10 System.Text.Json-native package, not the legacy Newtonsoft one |
| DELETE person | Remove by ID | Low | Returns 204 No Content or 404 |
| Calculated Age property | Core domain rule — Age derives from DateOfBirth, never stored | Low | Lives on the domain entity as a computed property; never in the DB |
| Seeded in-memory data | Required for zero-setup demo | Low | EF Core `HasData` or `OnModelCreating` seed; at least 3-5 persons |
| Rich domain entity (no anemic model) | Learning objective — business rules in entity, not service | Med | Person encapsulates its own invariants; factory method or guarded constructor enforces them |
| EF Core InMemory persistence | Simulates real EF patterns without a real DB | Low | DbContext, IQueryable, repository pattern over EF |
| Controller-based API (not Minimal API) | Explicit requirement; maps cleanly to Presentation layer | Low | `[ApiController]` attribute enables automatic model-state validation and Problem Details wiring |
| Problem Details error responses (RFC 7807 / RFC 9457) | Standard machine-readable error format — built into ASP.NET Core | Low | `AddProblemDetails()` + `UseExceptionHandler()` + `UseStatusCodePages()` in Program.cs covers all error paths with zero custom middleware |
| Command/Query separation (CQRS via MediatR) | Core learning objective — separates read and write concerns | Med | MediatR (`/luckypennysoftware/mediatr`) dispatches commands (writes) and queries (reads) through handlers; each handler is one use-case |
| FluentValidation in Application layer | Input validation decoupled from domain | Low-Med | Validators live in Application layer, registered as a MediatR `ValidationBehavior<TRequest, TResponse>` pipeline behavior |
| Global exception handler | Catches unhandled exceptions, maps to Problem Details | Low | `IExceptionHandler` (introduced .NET 8, available in .NET 10) registered via `AddExceptionHandler<T>()`; cleaner than middleware for this pattern |
| HTTP semantics (status codes) | REST correctness | Low | 200 GET, 201 POST with Location header, 204 DELETE, 400 validation, 404 not found, 409 conflict where applicable |

---

## Differentiators

Features that make this reference project genuinely instructive beyond a generic tutorial. Not required, but add real learning value.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Result\<T\> pattern (ErrorOr or FluentResults) | Replaces exception-for-flow-control with typed return values; handlers return `Result<PersonDto>` instead of throwing | Med | Domain and application layers return `Result<T>`; controller translates to HTTP responses. Use `ErrorOr` (v1.x) — DDD-aligned, ASP.NET Core-friendly, smallest footprint for a learning project |
| Domain invariant enforcement via private setters + factory methods | Forces callers through guarded entry points — demonstrates "no invalid state" discipline | Med | `Person.Create(firstName, paternalLastName, maternalLastName, dateOfBirth)` returns `Result<Person>` and validates at construction time |
| Value objects for name fields | Demonstrates primitive obsession cure — `PersonName` value object wrapping first/paternal/maternal names | Med-High | Use C# `record` with validation in constructor. Genuinely instructive but increases complexity — mark as optional if time-boxed |
| MediatR logging behavior | Cross-cutting concern demo — logs every command/query without touching handlers | Low | `LoggingBehavior<TRequest, TResponse>` wraps every handler; pure infrastructure concern that the domain never sees |
| Swagger / OpenAPI with Scalar or Swashbuckle | Immediate discoverability without Postman | Low | ASP.NET Core 10 ships with built-in OpenAPI generation (`AddOpenApi()`) — no Swashbuckle needed for basic cases |
| Response DTOs vs domain objects | Prevents domain model leakage through the API boundary | Low | `PersonDto` in Application layer; mappers (manual or AutoMapper) in Presentation; never expose domain entities directly |
| Port and adapter naming conventions | Makes the Hexagonal pattern visible in code — `IPersonRepository` is a port; `EfPersonRepository` is the adapter | Low | Naming discipline enforced in project structure; no code complexity overhead |

---

## Anti-Features

Features to explicitly NOT build in this project. Building these derails the learning goal or adds complexity that obscures patterns.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Authentication / Authorization | Out of scope per PROJECT.md; adds JWT/OAuth complexity that competes with architecture learning | Leave endpoints open; add a comment marker `// AUTH: add [Authorize] here in production` |
| Real database (SQL Server, SQLite, PostgreSQL) | EF InMemory satisfies the learning goal; a real DB adds connection strings, migrations, transaction management | Keep InMemory; the DbContext and repository patterns are identical — the adapter swaps, not the port |
| Pagination on GET all | With seeded InMemory data, pagination adds complexity for zero real benefit | Return full list; document pagination as a "next evolution" item in a README comment |
| Custom response envelope (wrapping every response in `{ data: ..., meta: ... }`) | Conflicts with RFC 7807 Problem Details; creates two response shapes clients must handle; no standard backing it | Use raw HTTP semantics for success responses + Problem Details for errors — consistent, standard, no custom parser needed |
| Soft delete / audit fields (CreatedAt, UpdatedAt, IsDeleted) | Adds cross-cutting persistence concerns that need interceptors or SaveChanges overrides | Hard delete only; mark audit fields as a "Phase N+1" concern in comments |
| CQRS with separate read/write databases | Event sourcing, projections, eventual consistency — a different learning scope entirely | One DbContext, one InMemory store; CQRS here means command/query handler separation, not physical database split |
| Generic repository (IRepository\<T\>) | Anti-pattern when using EF Core — EF's DbSet is already a generic repository; wrapping it again adds indirection for no gain | Use `IPersonRepository` (specific port) over `IRepository<Person>` (generic abstraction) |
| Anemic domain service that holds all logic | Defeats the rich model requirement; puts business rules outside the entity | All Person-specific rules go in the Person entity; Application layer orchestrates, never implements domain rules |
| AutoMapper for simple DTOs | Adds a dependency and "magic" mapping that obscures what the Presentation layer does | Manual mapping extension methods (`PersonDto.FromDomain(person)`) are explicit, debuggable, and educationally clearer |
| Versioning (v1/v2 API routes) | No clients to support; versioning is operational concern, not architectural | Single version; document as evolution concern |
| Unit + Integration test project (in initial build) | Valuable eventually but adds project setup complexity that delays first working API | Add test projects in a dedicated later phase |

---

## Feature Dependencies

```
EF Core InMemory (DbContext, seed data)
    └── IPersonRepository port (Application layer)
            └── EfPersonRepository adapter (Infrastructure layer)
                    └── GET all / GET by ID / DELETE handlers

MediatR dispatcher
    ├── GetAllPersonsQuery → GetAllPersonsQueryHandler
    ├── GetPersonByIdQuery → GetPersonByIdQueryHandler
    ├── CreatePersonCommand → CreatePersonCommandHandler
    ├── UpdatePersonCommand → UpdatePersonCommandHandler
    ├── PatchPersonCommand → PatchPersonCommandHandler   ← depends on JSON Patch library
    └── DeletePersonCommand → DeletePersonCommandHandler

FluentValidation validators (Application layer)
    └── ValidationBehavior<TRequest, TResponse> (MediatR pipeline)
            └── registered before every Command handler (Queries typically skip validation)

Person domain entity (rich model, private setters)
    ├── Age computed property  ← no DB column, no storage
    ├── Create() factory method returning Result<Person>
    └── Domain invariants (name not empty, DOB not future)
            └── enforced before any command handler persists

AddProblemDetails() + UseExceptionHandler() + UseStatusCodePages()
    └── Global exception handler (IExceptionHandler)
            └── Maps domain exceptions / Result failures → Problem Details responses

[ApiController] attribute on controllers
    └── Automatic ModelState → ValidationProblemDetails (400) for bad requests
```

---

## PATCH-Specific Detail

PATCH deserves a dedicated note because it has two valid RFC approaches with meaningfully different complexity profiles for this project:

**JSON Patch (RFC 6902) — RECOMMENDED for this project**
- Operation-based: `[{ "op": "replace", "path": "/firstName", "value": "Ana" }]`
- ASP.NET Core 10 ships `Microsoft.AspNetCore.JsonPatch.SystemTextJson` — the new System.Text.Json native implementation (replaces Newtonsoft dependency). Install with `dotnet add package Microsoft.AspNetCore.JsonPatch.SystemTextJson`.
- Controller action receives `JsonPatchDocument<PersonUpdateDto>`, calls `patchDoc.ApplyTo(dto, ModelState)`, then dispatches a `PatchPersonCommand`.
- Teaches the explicit operation model; shows how Presentation layer bridges an HTTP mechanism to an Application layer command.
- Confidence: HIGH — verified in official .NET 10 docs.

**JSON Merge Patch (RFC 7396) — simpler but less instructive**
- Payload is a partial JSON object; nulls signal deletion. Simpler client experience, harder to distinguish "field omitted" from "field set to null" in C#.
- No built-in ASP.NET Core support; requires a third-party library. Not recommended for a learning project where controlling dependencies matters.

**Recommendation:** Implement JSON Patch (RFC 6902) with `Microsoft.AspNetCore.JsonPatch.SystemTextJson`. It is harder to implement than Merge Patch but directly teachable, officially documented, and idiomatic for .NET 10.

---

## Validation Layer Placement

This project uses a two-tier validation model (not three) to keep the learning surface manageable:

| Tier | What Validates | Technology | Purpose |
|------|---------------|------------|---------|
| Application layer | Input DTOs / command properties (non-null, string length, date range) | FluentValidation + MediatR `ValidationBehavior` pipeline | Catches invalid input before it touches the domain |
| Domain layer | Business invariants (names non-empty, DOB not in future, age > 0) | Domain entity constructor / factory method returning `Result<T>` | Protects the aggregate root's internal consistency |

Do NOT validate in: controllers (beyond model binding), Infrastructure layer, or the MediatR handlers themselves. Controllers dispatch; handlers orchestrate; domain enforces.

---

## MVP Recommendation

Build in this order — each step produces a running, testable API:

1. Domain entity `Person` with rich model (private setters, computed Age, factory method, basic invariants)
2. Application ports: `IPersonRepository`, command/query objects, DTOs
3. MediatR handlers for GET all and GET by ID (read path first — no validation complexity)
4. EF Core InMemory DbContext + `EfPersonRepository` adapter + seed data
5. `PersonsController` wired to MediatR for the two GET endpoints — confirm the full vertical slice works
6. Problem Details plumbing: `AddProblemDetails()`, `UseExceptionHandler()`, `UseStatusCodePages()`
7. POST with FluentValidation + ValidationBehavior (write path, first mutation)
8. PUT (full update command — straightforward after POST)
9. DELETE
10. PATCH with `Microsoft.AspNetCore.JsonPatch.SystemTextJson`

**Defer entirely:** value objects, Result\<T\> pattern, logging behavior — add in a second pass once the full CRUD surface is working. These are differentiators, not table stakes.

---

## Sources

- Microsoft Docs — Handle errors in ASP.NET Core APIs (.NET 10): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0
- Microsoft Docs — JSON Patch in ASP.NET Core (.NET 10): https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch?view=aspnetcore-10.0
- Milan Jovanovic — Problem Details for ASP.NET Core APIs: https://www.milanjovanovic.tech/blog/problem-details-for-aspnetcore-apis
- Milan Jovanovic — CQRS Validation with MediatR Pipeline and FluentValidation: https://www.milanjovanovic.tech/blog/cqrs-validation-with-mediatr-pipeline-and-fluentvalidation
- Milan Jovanovic — Value Objects in .NET (DDD Fundamentals): https://www.milanjovanovic.tech/blog/value-objects-in-dotnet-ddd-fundamentals
- Code Maze — CQRS Validation Pipeline with MediatR and FluentValidation: https://code-maze.com/cqrs-mediatr-fluentvalidation/
- codewithmukesh — CQRS and MediatR in ASP.NET Core: https://codewithmukesh.com/blog/cqrs-and-mediatr-in-aspnet-core/
- codewithmukesh — ProblemDetails in ASP.NET Core: https://codewithmukesh.com/blog/problem-details-in-aspnet-core/
- DEV Community — Building Rich Domain Models: https://dev.to/cristofima/building-rich-domain-models-a-practical-guide-to-ddd-in-net-5952
- CodingDroplets — ErrorOr vs OneOf vs FluentResults in .NET: https://codingdroplets.com/erroror-vs-oneof-vs-fluentresults-dotnet-result-pattern
- Microsoft Learn — Designing a microservice domain model: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/microservice-domain-model
- Goat Review — Rethinking MediatR Validation: Moving from Pipeline to Domain Objects: https://goatreview.com/rethinking-mediatr-pipeline-validation-pattern/
