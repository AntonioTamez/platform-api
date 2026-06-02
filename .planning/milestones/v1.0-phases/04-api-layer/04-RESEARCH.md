# Phase 4: API Layer - Research

**Researched:** 2026-05-31
**Domain:** ASP.NET Core 10 Web API — Controllers, IExceptionHandler, Problem Details, JSON Patch (SystemTextJson), Mediator wiring, OpenAPI + Scalar
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Two `IExceptionHandler` implementations registered via `services.AddExceptionHandler<T>()`: `PersonNotFoundExceptionHandler` (404) and `ValidationExceptionHandler` (400). Registered NotFound first, Validation second. `app.UseExceptionHandler()` activates the chain.
- **D-02:** `services.AddProblemDetails()` registered globally in Program.cs. Handlers use `IProblemDetailsService` to write responses.
- **D-03:** 404 shape: `type`, `title`, `status`, `detail` only. `detail` = `PersonNotFoundException.Message`. No `instance` field.
- **D-04:** 400 shape: `errors` extension as dictionary keyed by property name, values as string arrays. Built from `ValidationException.Errors` grouped by `PropertyName`.
- **D-05:** 400 `detail` = `"One or more validation errors occurred."` (consistent with [ApiController] default).
- **D-06:** Package: `Microsoft.AspNetCore.JsonPatch.SystemTextJson` (not Newtonsoft-based).
- **D-07:** PATCH action signature: `[HttpPatch("{id:int}")] public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)`.
- **D-08:** PATCH applies to a fresh empty DTO (`new UpdatePersonDto()` with all nulls), not pre-loaded values.
- **D-09:** After `patchDoc.ApplyTo(dto, ModelState)`, check `if (!ModelState.IsValid)` and return `ValidationProblem(ModelState)`.

### Claude's Discretion

- `PersonsAPI.Api.csproj` package references (exact versions)
- Middleware pipeline order in Program.cs
- Scalar configuration details (title, description)
- Controller base class, route attribute, `[ApiController]` attribute
- Whether to suppress the default 400 response from `[ApiController]` automatic model validation

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| ERR-01 | All error responses follow RFC 9457 Problem Details format (application/problem+json) — no custom envelope for errors | IExceptionHandler + AddProblemDetails() pattern verified; IProblemDetailsService.TryWriteAsync writes correct content-type |
| ERR-02 | Validation errors return 400 with Problem Details listing all field violations | ValidationExceptionHandler groups FluentValidation.Errors by PropertyName into ProblemDetails.Extensions["errors"] dictionary |
| ERR-03 | Missing resource errors return 404 with Problem Details | PersonNotFoundExceptionHandler maps PersonNotFoundException to 404 ProblemDetails |
| DOC-01 | OpenAPI specification generated via Microsoft.AspNetCore.OpenApi | AddOpenApi() + MapOpenApi() verified against official docs; version 10.0.8 confirmed on NuGet |
| DOC-02 | Scalar interactive UI available at /scalar | MapScalarApiReference() confirmed as correct method; /scalar is default route |
</phase_requirements>

---

## Summary

Phase 4 builds the `PersonsAPI.Api` project as the outermost composition root that wires three pre-built layers together and exposes six HTTP endpoints. The research confirms the chosen stack is current and correct for .NET 10. Two critical findings emerged that require action before the standard plan tasks:

**Finding 1 (CRITICAL):** `UpdatePersonDto` is declared as a positional `record` (`record UpdatePersonDto(string? FirstName, ...)`). Positional record parameters compile to `init`-only properties. `JsonPatchDocument<TModel>.ApplyTo()` mutates the target object in place using property setters — `init`-only properties have no setter accessible after construction. This will throw a runtime error when `patchDoc.ApplyTo(dto, ModelState)` is called. The fix is to change `UpdatePersonDto` from a positional record to a mutable class (or a record with explicit `{ get; set; }` auto-properties). This change is in the Application layer (Phase 2 file) and must be the first task of Phase 4.

**Finding 2 (CONFIRMED):** The `[ApiController]` attribute activates automatic ModelState 400 responses for model binding failures (not FluentValidation). For this project, all content validation goes through the FluentValidation pipeline behavior (in Application layer, caught by `ValidationExceptionHandler`). The `[ApiController]`'s auto-400 only fires for MVC model binding errors. These should be **left active** — the automatic 400 is desirable for malformed requests (wrong types, missing required binding). Suppression via `SuppressModelStateInvalidFilter` is NOT needed. The PATCH endpoint's `ValidationProblem(ModelState)` call handles JsonPatch-specific errors separately.

**Primary recommendation:** Create the Api project, fix `UpdatePersonDto` immutability as the first task, then implement the controller and Program.cs wiring.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| HTTP endpoint routing and binding | API (Controller) | — | [ApiController] + [Route] handle all HTTP concerns |
| Request dispatching | API (Controller) | Application (Mediator) | Controller calls `_mediator.Send(command)`, Application handles |
| Input validation | Application (ValidationBehavior) | API (ModelState for JsonPatch) | FluentValidation pipeline owns content validation; ModelState owns binding errors |
| Exception → HTTP mapping | API (IExceptionHandler) | — | Exception handlers live in the outermost layer |
| Problem Details formatting | API (IProblemDetailsService) | — | Framework service; invoked from exception handlers |
| JSON Patch document application | API (Controller) | Application (UpdatePersonDto target) | Controller receives patchDoc, applies to DTO, dispatches command |
| DI composition / wiring | API (Program.cs) | — | Program.cs is the sole composition root per Clean Architecture |
| OpenAPI document generation | API (Microsoft.AspNetCore.OpenApi) | — | Runtime document served from the web host |
| Interactive API UI | API (Scalar.AspNetCore) | — | Scalar reads the OpenAPI endpoint and renders UI |
| Data seeding | API (Program.cs startup) | Infrastructure (DataSeeder) | Program.cs calls `await app.Services.SeedAsync()` before `app.Run()` |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core (built-in) | 10.0 | Web host, controllers, routing, middleware | Included in .NET 10 SDK; no separate package |
| Microsoft.AspNetCore.OpenApi | 10.0.8 | Runtime OpenAPI 3.1 document generation | First-party, ships with .NET 10 template, replaced Swashbuckle |
| Scalar.AspNetCore | 2.14.14 | Interactive API explorer UI at /scalar | Modern Swagger UI replacement; dark mode; pairs with Microsoft.AspNetCore.OpenApi |
| Microsoft.AspNetCore.JsonPatch.SystemTextJson | 10.0.8 | JSON Patch document binding and application | New STJ-based implementation for .NET 10; Newtonsoft-based package is legacy |
| Mediator.SourceGenerator | 3.0.2 | CQRS source-generator — installs ONLY in Api project | Source generator runs against outermost executable project; Application uses Mediator.Abstractions only |
| Mediator.Abstractions | 3.0.2 | Already in Application layer; not re-referenced in Api | Transitive via Application project reference |

### Supporting (Already Available Transitively)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FluentValidation | 12.1.1 | Validators (transitive via Application) | Already registered via `AddApplication()` |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.8 | Persistence (transitive via Infrastructure) | Already wired by `AddInfrastructure()` |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| IExceptionHandler (ASP.NET Core 8+) | Exception middleware (UseExceptionHandler lambda) | IExceptionHandler is more testable, type-safe, DI-friendly |
| ProblemDetails.Extensions["errors"] | Custom JSON response | Extensions["errors"] produces RFC 9457-compliant response without custom serialization |
| MapScalarApiReference() | Custom Swagger UI | Scalar is a drop-in with zero configuration; Swagger UI requires more wiring |

**Installation (new packages for Api project):**

```bash
dotnet add package Microsoft.AspNetCore.OpenApi --version 10.0.8
dotnet add package Scalar.AspNetCore --version 2.14.14
dotnet add package Microsoft.AspNetCore.JsonPatch.SystemTextJson --version 10.0.8
dotnet add package Mediator.SourceGenerator --version 3.0.2
```

---

## Package Legitimacy Audit

> slopcheck was run but identified all packages as PyPI packages (wrong ecosystem). These are .NET/NuGet packages. All five packages were manually verified on NuGet.org with download counts, publication dates, and source repositories.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Microsoft.AspNetCore.OpenApi 10.0.8 | NuGet | First-party (2024+) | Millions | github.com/dotnet/aspnetcore | N/A (wrong ecosystem check) | Approved — Microsoft first-party |
| Scalar.AspNetCore 2.14.14 | NuGet | Active (2024-2026) | 397K+ (this version) | github.com/scalar/scalar | N/A | Approved — published by scalar.com |
| Microsoft.AspNetCore.JsonPatch.SystemTextJson 10.0.8 | NuGet | 2026-05-12 | Verified | github.com/dotnet/aspnetcore | N/A | Approved — Microsoft first-party |
| Mediator.SourceGenerator 3.0.2 | NuGet | 2026-03-22 | Verified | github.com/martinothamar/Mediator | N/A | Approved — already confirmed in Application layer |

**Packages removed due to slopcheck [SLOP] verdict:** None — slopcheck ran against wrong ecosystem (PyPI). All packages verified manually via NuGet.org. [VERIFIED: nuget.org]

**Note:** slopcheck does not support NuGet ecosystem checks. Manual NuGet verification was performed as the authoritative source for all packages.

---

## Architecture Patterns

### System Architecture Diagram

```
HTTP Request
     │
     ▼
[PersonsController]
  ├── GET /api/persons ──────────────────────► [GetAllPersonsQueryHandler]
  ├── GET /api/persons/{id} ─────────────────► [GetPersonByIdQueryHandler]
  ├── POST /api/persons ─────────────────────► [CreatePersonCommandHandler]
  ├── PUT /api/persons/{id} ─────────────────► [UpdatePersonCommandHandler]
  ├── PATCH /api/persons/{id}                 │
  │     └─ patchDoc.ApplyTo(dto) ────────────► [PatchPersonCommandHandler]
  └── DELETE /api/persons/{id} ───────────────► [DeletePersonCommandHandler]
            │                                          │
            │ IMediator.Send(command/query)             │
            ▼                                          │
  [ValidationBehavior<TReq,TRes>]                      │
     └─ FluentValidation validators                    │
                  │                                    │
                  ▼                                    │
  [Application Handler]                               │
     └─ IPersonRepository ──────────────────────────► │
                  │                                   ▼
                  └──────────────────────► [PersonRepository]
                                                      │
                                                      ▼
                                             [PersonDbContext]
                                           (EF Core InMemory)

Exception Path:
  PersonNotFoundException ──────► [PersonNotFoundExceptionHandler]
  ValidationException ──────────► [ValidationExceptionHandler]
       │                                  │
       └──── IProblemDetailsService ◄─────┘
                      │
                      ▼
         RFC 9457 Problem Details response
               application/problem+json
```

### Recommended Project Structure

```
src/PersonsAPI.Api/
├── PersonsAPI.Api.csproj         # References Application + Infrastructure; SourceGenerator here
├── Program.cs                    # Sole composition root
├── Controllers/
│   └── PersonsController.cs      # All 6 endpoints
└── ExceptionHandlers/
    ├── PersonNotFoundExceptionHandler.cs
    └── ValidationExceptionHandler.cs
```

### Pattern 1: IExceptionHandler Implementation

**What:** Strongly-typed exception handler with DI-friendly IProblemDetailsService for writing RFC 9457 responses.

**When to use:** Any exception type that needs to be mapped to a specific HTTP status code.

```csharp
// Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0
// + https://okyrylchuk.dev/blog/handling-exceptions-in-asp-net-core-8/
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PersonsAPI.Api.ExceptionHandlers;

public sealed class PersonNotFoundExceptionHandler(
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not PersonNotFoundException notFound)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = notFound.Message  // "Person with ID {id} was not found."
            }
        });
    }
}
```

### Pattern 2: ValidationExceptionHandler with errors dictionary

**What:** Maps FluentValidation's `ValidationException` to 400 with field-keyed errors extension.

```csharp
// Source: Derived from https://www.milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-8
// + FluentValidation docs for ValidationException.Errors grouping
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PersonsAPI.Api.ExceptionHandlers;

public sealed class ValidationExceptionHandler(
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred."
        };
        problemDetails.Extensions["errors"] = errors;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
```

### Pattern 3: Program.cs composition root

**What:** Correct wiring order for all services and middleware.

```csharp
// Source: CONTEXT.md decisions + official docs
// AddMediator BEFORE AddApplication (ValidationBehavior pipeline registered by Mediator)
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();  // D-01: NotFound first
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();      // D-01: Validation second
builder.Services.AddOpenApi();                                            // DOC-01
builder.Services.AddMediator(options =>
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]);        // Mediator.SourceGenerator in this project
builder.Services.AddApplication();                                        // Registers FluentValidation validators
builder.Services.AddInfrastructure();                                     // Registers DbContext + Repository

var app = builder.Build();

app.UseExceptionHandler();          // Activates IExceptionHandler chain
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();                   // /openapi/v1.json
app.MapScalarApiReference();        // /scalar (DOC-02)

await app.Services.SeedAsync();     // D-04 from Phase 3: seeds 3 persons before Run()
await app.RunAsync();
```

### Pattern 4: PATCH Controller Action

**What:** Apply JsonPatchDocument to a fresh mutable DTO, then dispatch command. See Critical Fix below for UpdatePersonDto mutable requirement.

```csharp
// Source: CONTEXT.md D-07, D-08, D-09 + official Microsoft JsonPatch docs
[HttpPatch("{id:int}")]
public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)
{
    var dto = new UpdatePersonDto();  // fresh empty DTO — all properties null by default
    patchDoc.ApplyTo(dto, ModelState);
    if (!ModelState.IsValid)
        return ValidationProblem(ModelState);  // JsonPatch structural errors only
    var result = await _mediator.Send(new PatchPersonCommand(id, dto));
    return Ok(result);
}
```

### Pattern 5: PersonsController skeleton

```csharp
// Source: CLAUDE.md constraints (controllers only, [ApiController], [Route])
[ApiController]
[Route("api/[controller]")]
public sealed class PersonsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { ... }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id) { ... }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonRequest request) { ... }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonRequest request) { ... }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc) { ... }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id) { ... }
}
```

### Anti-Patterns to Avoid

- **Using `Newtonsoft.Json`-based `Microsoft.AspNetCore.JsonPatch`:** The old Newtonsoft package was the standard prior to .NET 10. The new `Microsoft.AspNetCore.JsonPatch.SystemTextJson` is required and is not a drop-in replacement (does not support `ExpandoObject`). Do not install the Newtonsoft package.
- **Registering `ValidationBehavior` in `AddApplication()`:** As documented in `ServiceCollectionExtensions.cs`, the `ValidationBehavior` open generic must be registered via `AddMediator(options => ...)`, not as a separate `AddScoped` in the Application layer. Duplicate registration causes double invocation per dispatch.
- **Calling `app.Services.SeedAsync()` after `app.Run()`:** `SeedAsync` must be called BEFORE `RunAsync()`. After `RunAsync()`, the app is already serving requests and the InMemory database may be in an inconsistent state.
- **Resolving scoped `PersonDbContext` from root provider in seeder:** Already handled in `DataSeeder.SeedAsync` via `services.CreateScope()`. The planner must not add a DI registration for DataSeeder.
- **Using `UseExceptionHandler("/error")` with a route:** For the IExceptionHandler pattern, `app.UseExceptionHandler()` with NO arguments (or empty options) is required. Passing a route path overrides the IExceptionHandler chain.
- **Placing `MapScalarApiReference()` before `MapControllers()`:** Endpoint mapping order matters; controllers must be mapped for Scalar to discover them. Map Scalar after controllers.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON Patch document parsing and application | Custom delta-patching logic | `JsonPatchDocument<T>.ApplyTo()` | RFC 6902 has edge cases (move, copy, test operations, array indexing) |
| Problem Details RFC 9457 serialization | Custom error response format | `IProblemDetailsService.TryWriteAsync()` | Handles content-type (`application/problem+json`), status codes, and RFC compliance automatically |
| Middleware-level exception routing | `try/catch` in controller actions | `IExceptionHandler` + `UseExceptionHandler()` | Framework-managed, ordered chain; DI-friendly; no boilerplate per action |
| OpenAPI document generation | Manual swagger JSON files | `Microsoft.AspNetCore.OpenApi` | Runtime generation from controller metadata; no maintenance |

**Key insight:** The ASP.NET Core 10 framework already solves every cross-cutting concern this phase needs. The controller layer's job is dispatching, not error handling, documentation, or patch parsing.

---

## Critical Fix: UpdatePersonDto Must Be Mutable

### Why it fails

`UpdatePersonDto` is declared as:

```csharp
// Current — BROKEN for JsonPatch
public record UpdatePersonDto(
    string? FirstName,
    string? PaternalLastName,
    string? MaternalLastName,
    DateOnly? DateOfBirth);
```

C# positional record syntax compiles parameters to `init`-only properties (`public string? FirstName { get; init; }`). `JsonPatchDocument<TModel>.ApplyTo()` modifies the target object **in place** using property setters. `init`-only properties have no `set` accessor, only an `init` accessor. Attempting to write to an `init` property after construction throws a runtime exception.

The official Microsoft docs confirm: "The object passed to the `ApplyTo(Object)` method is modified in place." [VERIFIED: learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch?view=aspnetcore-10.0]

### Fix: Replace with mutable class

The fix changes only `UpdatePersonDto.cs` in the Application layer. No other Application layer files change — the `PatchPersonCommand` record, `PatchPersonCommandValidator`, and `PatchPersonHandler` all continue to work identically because they use `UpdatePersonDto` by reading its properties, not constructing it.

```csharp
// Fixed — mutable class, required for JsonPatchDocument<T>.ApplyTo()
namespace PersonsAPI.Application.DTOs;

/// <summary>
/// Mutable DTO for PATCH /api/persons/{id}.
/// Must use { get; set; } properties (not init-only) because
/// JsonPatchDocument<T>.ApplyTo() mutates the target object in place.
/// </summary>
public class UpdatePersonDto
{
    public string? FirstName { get; set; }
    public string? PaternalLastName { get; set; }
    public string? MaternalLastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
```

The controller creates `new UpdatePersonDto()` (all properties null by default) before calling `ApplyTo`. The handler's `dto.Field ?? person.Field` null-fallback pattern is unaffected.

**Impact on existing code:** Zero downstream changes. `PatchPersonCommand(int Id, UpdatePersonDto Dto)` accepts a class reference. `PatchPersonCommandValidator` uses `x.Dto.FirstName` etc. — property access works identically. `PatchPersonHandler` reads properties — unchanged. No other file touches `UpdatePersonDto`.

---

## Common Pitfalls

### Pitfall 1: JsonPatchDocument with init-only record properties

**What goes wrong:** `patchDoc.ApplyTo(dto, ModelState)` throws at runtime because `UpdatePersonDto` positional record properties are `init`-only.
**Why it happens:** `ApplyTo` requires mutable `{ get; set; }` properties; positional record syntax generates `{ get; init; }` instead.
**How to avoid:** Change `UpdatePersonDto` from positional record to a mutable class with `{ get; set; }` properties. This is a Phase 4 prerequisite task.
**Warning signs:** Runtime `InvalidOperationException` or reflection error on the first PATCH request; compiles fine.

### Pitfall 2: UseExceptionHandler with a route argument

**What goes wrong:** `app.UseExceptionHandler("/error")` disables the `IExceptionHandler` chain. Exceptions are rerouted to `/error` but no endpoint exists, returning an empty 500.
**Why it happens:** The route-argument overload bypasses registered `IExceptionHandler` implementations entirely.
**How to avoid:** Use `app.UseExceptionHandler()` with no arguments.
**Warning signs:** Exception handlers never called; `/error` returns 404 or empty 500.

### Pitfall 3: ValidationBehavior double registration

**What goes wrong:** Registering `services.AddScoped<ValidationBehavior<,>>()` in `AddApplication()` AND passing it to `AddMediator(options => options.PipelineBehaviors)` causes every request to run validation twice.
**Why it happens:** `AddMediator` with pipeline behaviors registers the behavior internally. An additional `AddScoped` creates a second registration in DI.
**How to avoid:** Register `ValidationBehavior` only via `AddMediator(options => ...)`. `AddApplication()` deliberately excludes it (see XML comment in `ServiceCollectionExtensions.cs`).

### Pitfall 4: Mediator.SourceGenerator in wrong project

**What goes wrong:** Installing `Mediator.SourceGenerator` in the Application or Domain project breaks isolated builds of those projects (they have no `IMediator` registration or startup host).
**Why it happens:** The source generator emits code that assumes a startup project context.
**How to avoid:** `Mediator.SourceGenerator` installs ONLY in `PersonsAPI.Api` (the outermost executable). All other projects use `Mediator.Abstractions` only.

### Pitfall 5: Seeding after app.Run()

**What goes wrong:** Placing `await app.Services.SeedAsync()` after `await app.RunAsync()` means it never executes (RunAsync blocks until app shutdown).
**Why it happens:** `RunAsync()` blocks the calling thread.
**How to avoid:** Call `await app.Services.SeedAsync()` before `await app.RunAsync()`.

### Pitfall 6: [ApiController] auto-400 vs FluentValidation 400

**What goes wrong:** Developer confuses `[ApiController]`'s automatic ModelState 400 (for binding failures) with FluentValidation 400 responses.
**Why it happens:** Both produce 400 responses but through different paths with different formats.
**How to avoid:** Leave `SuppressModelStateInvalidFilter = false` (default). The auto-400 only fires for binding errors (wrong types, missing `[Required]` data annotations). FluentValidation content errors never reach ModelState — they are caught by `ValidationExceptionHandler`. The PATCH `ValidationProblem(ModelState)` handles JsonPatch structural errors only.

### Pitfall 7: POST returning 200 instead of 201

**What goes wrong:** `CreatePersonCommand` handler returns a `PersonResponse`. If the controller returns `Ok(result)`, the response is 200 instead of 201 with a Location header (WRITE-01).
**Why it happens:** Forgetting to use `CreatedAtAction` or `Created` for POST.
**How to avoid:** Return `CreatedAtAction(nameof(GetById), new { id = result.Id }, result)` for POST.

### Pitfall 8: UseScalarApiReference vs MapScalarApiReference

**What goes wrong:** Using `app.UseScalarApiReference()` (middleware-style) when the correct method is `app.MapScalarApiReference()` (endpoint-style).
**Why it happens:** Scalar migrated to endpoint-based mapping in recent versions. `UseScalarApiReference` does not exist in Scalar.AspNetCore 2.x.
**How to avoid:** Use `app.MapScalarApiReference()` exclusively. [VERIFIED: scalar.com/products/api-references/integrations/aspnetcore/integration]

---

## Runtime State Inventory

> SKIPPED — Phase 4 is greenfield (creating a new project). No rename, refactor, or migration involved. No existing runtime state to audit.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All build operations | Yes | 10.0.202 | — |
| dotnet CLI | Project creation, package install | Yes | 10.0.202 | — |
| NuGet registry | Package install | Yes (assumed internet) | — | — |

**Missing dependencies with no fallback:** None.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Swashbuckle.AspNetCore | Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore | .NET 9 (Swashbuckle removed from template) | Swashbuckle generates OpenAPI 3.0; new approach generates 3.1 |
| `Microsoft.AspNetCore.JsonPatch` (Newtonsoft) | `Microsoft.AspNetCore.JsonPatch.SystemTextJson` | .NET 10 | New STJ-based package; not a drop-in (no ExpandoObject support) |
| Custom error middleware | `IExceptionHandler` + `UseExceptionHandler()` | ASP.NET Core 8 | More testable, DI-friendly, ordered chain |
| `app.UseExceptionHandler()` with Newtonsoft-serialized ProblemDetails | `IProblemDetailsService.TryWriteAsync()` | ASP.NET Core 7/8 | Framework handles content-type and serialization |
| MediatR (Jimmy Bogard) | Mediator (martinothamar) 3.0.2 | Decided pre-roadmap (MediatR 13 = commercial) | Source-generated, MIT, zero reflection overhead |

**Deprecated/outdated:**
- `Swashbuckle.AspNetCore`: Removed from `dotnet new webapi` in .NET 9; generates OpenAPI 3.0 only; use Microsoft.AspNetCore.OpenApi instead.
- `Microsoft.AspNetCore.JsonPatch` (Newtonsoft): Legacy for .NET 9 and below; `Microsoft.AspNetCore.JsonPatch.SystemTextJson` is required for .NET 10.
- `FluentValidation.AspNetCore` (automatic validation integration): Deprecated by FluentValidation team; manual validation via pipeline behavior is the current approach.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `MapScalarApiReference()` with no arguments defaults to `/scalar` route | Patterns, Pitfalls | Scalar UI would be at a different path; ROADMAP success criterion 4 specifies /scalar explicitly |
| A2 | `app.UseExceptionHandler()` with no arguments activates the `IExceptionHandler` chain in .NET 10 (same as .NET 8 behavior) | Patterns, Pitfalls | Exception handlers would not fire; errors would return empty 500 |
| A3 | The `[ApiController]` auto-400 path does not intercept FluentValidation exceptions (they are thrown from handler code, not model binding) | Pitfall 6 | If wrong, double 400 responses possible; but test would reveal this immediately |

**All three assumptions are HIGH confidence based on verified documentation patterns. The Scalar route default is the only one without explicit official verification of the exact default path string.**

---

## Open Questions

1. **Does UseExceptionHandler() require both AddProblemDetails() AND AddControllers() to be called before it?**
   - What we know: Official docs show `AddProblemDetails()` called before `UseExceptionHandler()`, and `AddControllers()` is needed for controller routing. The registration order of services in `builder.Services` typically does not matter for services (only middleware order in `app.*` matters).
   - What's unclear: Whether `UseExceptionHandler()` has any dependency on `AddControllers()` being registered in services.
   - Recommendation: Call `AddControllers()` first in services registration. This is standard practice and poses no risk.

2. **Should `MapOpenApi()` be called unconditionally or only in Development?**
   - What we know: The official Microsoft docs show `app.MapOpenApi()` inside `if (app.Environment.IsDevelopment())`. For this learning project there is no production environment.
   - What's unclear: The ROADMAP success criteria says "Navigating to /scalar in a browser opens the Scalar interactive UI" without specifying environment.
   - Recommendation: Call `MapOpenApi()` and `MapScalarApiReference()` unconditionally (no environment guard) since this is a learning project with no production deployment concern.

---

## Validation Architecture

> `workflow.nyquist_validation` is explicitly `false` in `.planning/config.json`. This section is skipped per configuration.

---

## Security Domain

> `security_enforcement: true` and `security_asvs_level: 1` in `.planning/config.json`.

### Applicable ASVS Categories (Level 1)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Out of scope for v1 (see REQUIREMENTS.md Out of Scope) |
| V3 Session Management | No | No session management in this stateless API |
| V4 Access Control | No | No authorization in v1 |
| V5 Input Validation | Yes | FluentValidation via ValidationBehavior; JSON Patch structural validation via ModelState |
| V6 Cryptography | No | No cryptographic operations |
| V7 Error Handling | Yes | IExceptionHandler + Problem Details; no stack traces or internals exposed to clients |

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| JSON Patch DoS via copy amplification | Denial of Service | Per-operation count limit on patch document (optional for learning scope); documented in Microsoft security guidance |
| JSON Patch business logic subversion | Tampering | Patching a DTO (UpdatePersonDto) not the domain entity; handler enforces domain invariants via Person.UpdateName |
| Stack trace disclosure in 500 responses | Information Disclosure | IProblemDetailsService writes only Problem Details shape; no exception details in production |

**Security note for the planner:** The JSON Patch security guidance from Microsoft docs explicitly recommends using POCOs (Plain Old CLR Objects) with only safe-to-modify properties exposed. `UpdatePersonDto` (when converted to a mutable class) satisfies this — it exposes only the four patchable fields and nothing else. The domain entity (`Person`) is never the patch target.

---

## Sources

### Primary (HIGH confidence)

- [learn.microsoft.com — JsonPatch in ASP.NET Core 10.0](https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch?view=aspnetcore-10.0) — Package name, ApplyTo behavior, mutable-object requirement, security risks
- [learn.microsoft.com — Handle errors in ASP.NET Core 10.0](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0) — IExceptionHandler interface, TryHandleAsync signature, IProblemDetailsService, UseExceptionHandler
- [learn.microsoft.com — Generate OpenAPI documents 10.0](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0) — AddOpenApi(), MapOpenApi(), configuration options
- [NuGet: Microsoft.AspNetCore.JsonPatch.SystemTextJson 10.0.8](https://www.nuget.org/packages/Microsoft.AspNetCore.JsonPatch.SystemTextJson) — Version confirmed, .NET 10 target, no dependencies
- [NuGet: Microsoft.AspNetCore.OpenApi 10.0.8](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) — Version confirmed, .NET 10 target
- [NuGet: Scalar.AspNetCore 2.14.14](https://www.nuget.org/packages/Scalar.AspNetCore) — Version confirmed, .NET 10 support
- [NuGet: Mediator.SourceGenerator 3.0.2](https://www.nuget.org/packages/Mediator.SourceGenerator) — Version confirmed, 2026-03-22 publication
- [learn.microsoft.com — JsonPatchDocument<TModel> API reference](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.jsonpatch.systemtextjson.jsonpatchdocument-1?view=aspnetcore-10.0) — Namespace, ApplyTo overloads, TModel : class constraint
- [github.com/martinothamar/Mediator](https://github.com/martinothamar/Mediator) — AddMediator() options, PipelineBehaviors registration, SourceGenerator scope

### Secondary (MEDIUM confidence)

- [scalar.com — Scalar.AspNetCore integration guide](https://scalar.com/products/api-references/integrations/aspnetcore/integration) — MapScalarApiReference() name, /scalar default route, configuration options
- [okyrylchuk.dev — IExceptionHandler in ASP.NET Core 8](https://okyrylchuk.dev/blog/handling-exceptions-in-asp-net-core-8/) — Full implementation example, registration pattern
- [milanjovanovic.tech — Global Error Handling ASP.NET Core 8](https://www.milanjovanovic.tech/blog/global-error-handling-in-aspnetcore-8) — AddExceptionHandler<T> registration order, AddProblemDetails

### Tertiary (LOW confidence)

- Community articles on IExceptionHandler + ValidationException pattern (structure verified against official docs, code pattern derived)

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all package versions verified on NuGet.org
- IExceptionHandler API: HIGH — verified against official Microsoft Learn docs
- JsonPatch SystemTextJson: HIGH — official docs + API reference confirmed
- Mediator wiring: HIGH — official GitHub README confirmed
- OpenAPI + Scalar: HIGH — official docs + Scalar integration guide confirmed
- UpdatePersonDto record → class fix: HIGH — confirmed by "modified in place" requirement in official docs + C# record init-only property semantics

**Research date:** 2026-05-31
**Valid until:** 2026-08-31 (stable .NET release; packages unlikely to change significantly)
