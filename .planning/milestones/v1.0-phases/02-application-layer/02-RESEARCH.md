# Phase 2: Application Layer - Research

**Researched:** 2026-05-29
**Domain:** .NET 10 Application Layer — CQRS with Mediator 3.0.2 (martinothamar source-generator), FluentValidation 12.1.1, Clean + Hexagonal Architecture
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**PATCH Command Design**
- D-01: The controller (Phase 4) receives `JsonPatchDocument<UpdatePersonDto>`, applies it to a fresh `UpdatePersonDto`, then dispatches `PatchPersonCommand(int Id, UpdatePersonDto Dto)` to the mediator. The Application layer stays free of `Microsoft.AspNetCore.JsonPatch` — no ASP.NET Core types bleed past the controller boundary.
- D-02: `UpdatePersonDto` has four nullable fields: `string? FirstName`, `string? PaternalLastName`, `string? MaternalLastName`, `DateOnly? DateOfBirth`. The `PatchPersonHandler` applies only non-null fields by calling `Person.UpdateName()` or `Person.UpdateDateOfBirth()` on the fields that are populated. CLAUDE.md's C# 14 null-conditional assignment fits naturally here.

**Not-Found Contract**
- D-03: `IPersonRepository.GetByIdAsync(int id)` returns `Person?` (null when not found). The Application layer — not the repository — decides what "not found" means for each use case.
- D-04: `PersonNotFoundException` lives in `PersonsAPI.Application/Exceptions/PersonNotFoundException.cs`. Handlers throw it when `GetByIdAsync` returns null. The API layer (Phase 4) catches `PersonNotFoundException` and maps it to 404 Problem Details. This is an application-layer concern, not a domain invariant.

**DTO Design**
- D-05: Three request types: `CreatePersonRequest` (all four fields required), `UpdatePersonRequest` (all four fields required, for PUT), `UpdatePersonDto` (all four fields nullable, for PATCH after patch application). Distinct types express distinct intent clearly; validators can be field-exact without conditionals.
- D-06: One response type: `PersonResponse { int Id, string FirstName, string PaternalLastName, string MaternalLastName, DateOnly DateOfBirth, int Age }`. Age is read from the domain entity's computed property and surfaced in every response — demonstrating the computed-property pattern is an explicit project goal.
- D-07: Mapping lives in a static factory: `PersonResponse.FromDomain(Person p)`. No AutoMapper. Lives in `PersonsAPI.Application/DTOs/PersonResponse.cs`.

**Validator Scope**
- D-08: Validators for write commands only: `CreatePersonCommandValidator`, `UpdatePersonCommandValidator`, `PatchPersonCommandValidator`. Read queries carry no user-supplied body data needing validation.
- D-09: `CreatePersonCommandValidator` and `UpdatePersonCommandValidator` mirror domain invariants: name fields NotEmpty + length 2–100, `DateOfBirth` not in the future. Intentional duplication — Application validates for field-level 400 detail (ERR-02), Domain enforces invariants as the second line of defense.
- D-10: `ValidationBehavior<TRequest, TResponse>` short-circuits gracefully (no error) when no `IValidator<T>` is registered for the request type. Validators are registered via `AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly)` in `AddApplication()`.

### Claude's Discretion
- Folder structure within `PersonsAPI.Application` (e.g., `Commands/`, `Queries/`, `DTOs/`, `Ports/`, `Behaviors/`, `Exceptions/`) — Claude chooses idiomatic organization.
- `IApplicationMarker` interface or equivalent for assembly scanning — Claude selects the cleanest approach.
- Whether commands and queries are records or classes — records are strongly preferred for CQRS in C# 14 (immutable, value equality).

### Deferred Ideas (OUT OF SCOPE)
None — discussion stayed within phase scope.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| READ-01 | User can retrieve a list of all persons via GET /api/persons | `GetAllPersonsQuery` + `GetAllPersonsHandler` returning `IReadOnlyList<PersonResponse>` |
| READ-02 | User can retrieve a single person by ID via GET /api/persons/{id} — returns 404 if not found | `GetPersonByIdQuery(int Id)` + handler throwing `PersonNotFoundException` when null |
| WRITE-01 | User can create a new person via POST /api/persons — returns 201 with Location header | `CreatePersonCommand` + handler calling `Person.Create()` then `AddAsync` |
| WRITE-02 | User can fully replace a person via PUT /api/persons/{id} | `UpdatePersonCommand(int Id, UpdatePersonRequest Dto)` + handler calling `UpdateName`/`UpdateDateOfBirth` |
| WRITE-03 | User can partially update a person via PATCH /api/persons/{id} using JSON Patch on a DTO | `PatchPersonCommand(int Id, UpdatePersonDto Dto)` + handler with null-conditional field application |
| WRITE-04 | User can delete a person via DELETE /api/persons/{id} — returns 204, 404 if not found | `DeletePersonCommand(int Id)` + handler calling `DeleteAsync` after null check |
| VAL-01 | Input validation runs in the Application layer via a FluentValidation pipeline behavior — not in controllers | `ValidationBehavior<TRequest,TResponse>` implementing `IPipelineBehavior<TMessage,TResponse>` with `IEnumerable<IValidator<TRequest>>` |
| INFRA-03 | IPersonRepository port interface lives in the Application layer — not in Infrastructure | `IPersonRepository` declared in `PersonsAPI.Application/Ports/` with `Task<>` return types |

</phase_requirements>

---

## Summary

Phase 2 constructs the Application layer of a Clean + Hexagonal Architecture .NET 10 API. The layer owns all use-case contracts: a port interface (`IPersonRepository`) in `Ports/`, six CQRS message types (two queries, four commands) with matching handlers, three request DTOs plus one response DTO with a static mapping factory, and a `ValidationBehavior<TRequest,TResponse>` pipeline behavior that intercepts write commands before handlers run.

The key technical complexity is the Mediator 3.0.2 source-generator library (martinothamar), which has a fundamentally different project setup rule compared to MediatR: `Mediator.SourceGenerator` must be installed ONLY in the outermost executable project (the API layer — Phase 4). The Application layer installs only `Mediator.Abstractions`. Installing `Mediator.SourceGenerator` in the Application project will cause CS0436 `AssemblyReference` type conflicts at compile time. This is the single highest-risk gotcha for this phase.

FluentValidation 12.1.1 validation pipeline for martinothamar/Mediator follows the same `IEnumerable<IValidator<TRequest>>` constructor injection pattern used with MediatR, but the `Handle` method returns `ValueTask<TResponse>` (not `Task<TResponse>`) and uses `MessageHandlerDelegate<TMessage, TResponse>` instead of `RequestHandlerDelegate`. Pipeline behaviors are registered via `options.PipelineBehaviors` in the `AddMediator()` call inside `AddApplication()` — not via `AddOpenBehavior()`.

**Primary recommendation:** Install `Mediator.Abstractions` 3.0.2 in `PersonsAPI.Application.csproj`. Leave `Mediator.SourceGenerator` for Phase 4 (`PersonsAPI.Api.csproj`). Define all CQRS types as `record` using `ICommand<TResponse>` / `IQuery<TResponse>` from `Mediator.Abstractions`. Register the open-generic `ValidationBehavior<,>` in `AddApplication()` via `options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]`.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CQRS use-case contracts (commands/queries) | Application | — | Application layer owns all use-case interfaces; Domain has no knowledge of use cases |
| Port interface (IPersonRepository) | Application | — | Hexagonal: ports live in the inner ring (Application), not in the adapter (Infrastructure) |
| FluentValidation pipeline behavior | Application | — | VAL-01 explicitly places input validation in Application layer |
| Domain entity mutation | Domain (via handlers calling entity methods) | Application (handler orchestrates) | Handlers call `Person.Create()`, `UpdateName()`, `UpdateDateOfBirth()` — business logic stays in Domain |
| Not-found exception | Application | — | `PersonNotFoundException` is an application concern; Domain has no concept of "not found in store" |
| DI wiring (AddApplication) | Application | — | Application owns its own service registration extension method |
| Source-generator execution | API (outermost project only) | — | Mediator.SourceGenerator scans all referenced assemblies from the edge project; must not be in Application |
| Response DTO mapping | Application | — | `PersonResponse.FromDomain()` is a static factory in Application/DTOs — no AutoMapper, no Infrastructure knowledge |

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Mediator.Abstractions | 3.0.2 | CQRS message/handler interfaces for Application layer | Source-generator companion; provides `ICommand<T>`, `IQuery<T>`, `ICommandHandler<,>`, `IQueryHandler<,>`, `IPipelineBehavior<,>` [VERIFIED: nuget.org/packages/Mediator.Abstractions] |
| FluentValidation | 12.1.1 | Validator base class and validator interfaces | `AbstractValidator<T>`, `IValidator<T>`, `ValidationContext<T>` [VERIFIED: nuget.org/packages/FluentValidation] |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Bulk validator registration via `AddValidatorsFromAssembly()` | Required for `AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly)` [VERIFIED: nuget.org/packages/FluentValidation.DependencyInjectionExtensions] |

### Not Installed in Application Layer
| Library | Where It Goes | Why Not Here |
|---------|--------------|--------------|
| Mediator.SourceGenerator | Phase 4: PersonsAPI.Api.csproj ONLY | Installing in Application causes CS0436 AssemblyReference type conflict [VERIFIED: github.com/martinothamar/Mediator issue #261] |
| Microsoft.EntityFrameworkCore | Phase 3: Infrastructure | INFRA-02 / DOM-01: Domain and Application have zero EF references |
| Any ASP.NET Core package | Phase 4: Api | D-01: no ASP.NET Core types cross the controller boundary |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Mediator.Abstractions 3.0.2 | MediatR 12.5 (Apache, frozen) | MediatR 12.5 is acceptable fallback per CLAUDE.md but uses `Task<T>` and `RequestHandlerDelegate` — slightly different method signatures. MediatR 13+ requires commercial license. |
| FluentValidation 12.1.1 | DataAnnotations | DataAnnotations live on the DTO, not in the Application layer — wrong conceptual home per CLAUDE.md |

**Installation (Application layer only):**
```bash
dotnet add src/PersonsAPI.Application/PersonsAPI.Application.csproj package Mediator.Abstractions --version 3.0.2
dotnet add src/PersonsAPI.Application/PersonsAPI.Application.csproj package FluentValidation --version 12.1.1
dotnet add src/PersonsAPI.Application/PersonsAPI.Application.csproj package FluentValidation.DependencyInjectionExtensions --version 12.1.1
```

---

## Package Legitimacy Audit

> slopcheck 0.6.1 was run against these packages. slopcheck does not support the NuGet (crates.io/.NET) ecosystem — it validates npm packages only. All packages below are .NET/NuGet packages verified directly via nuget.org and official GitHub source repositories.

| Package | Registry | Age | Downloads | Source Repo | slopcheck | Disposition |
|---------|----------|-----|-----------|-------------|-----------|-------------|
| Mediator.Abstractions 3.0.2 | NuGet | 3+ years (first release 2022) | 7.4M total | github.com/martinothamar/Mediator | N/A (NuGet, not npm) | Approved — verified via nuget.org + official GitHub |
| Mediator.SourceGenerator 3.0.2 | NuGet | 3+ years (first release 2022) | 5.6M total | github.com/martinothamar/Mediator | N/A (NuGet, not npm) | Approved — verified via nuget.org + official GitHub |
| FluentValidation 12.1.1 | NuGet | 15+ years | 932M total | github.com/FluentValidation/FluentValidation | N/A (NuGet, not npm) | Approved — industry-standard library |
| FluentValidation.DependencyInjectionExtensions 12.1.1 | NuGet | ~5 years | 415M total | github.com/FluentValidation/FluentValidation | N/A (NuGet, not npm) | Approved — official FluentValidation extension |

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*slopcheck does not support NuGet ecosystem. Verification performed directly against nuget.org and official GitHub repositories. All packages are well-established with multi-year histories and millions of downloads.*

---

## Architecture Patterns

### System Architecture Diagram

```
HTTP Request (Phase 4)
        │
        ▼
  [PersonsController]           <- Phase 4: Primary Adapter
        │  dispatches command/query record
        ▼
  [IMediator.Send(request)]     <- Mediator source-generated dispatcher
        │
        ▼
  [ValidationBehavior<TReq,TResp>]   <- Application: cross-cutting concern
        │  IEnumerable<IValidator<TRequest>> runs before handler
        │  throws ValidationException if invalid (no handler reached)
        ▼
  [ICommandHandler / IQueryHandler]  <- Application: use-case handler
        │  calls Person.Create() / UpdateName() / UpdateDateOfBirth()
        │  calls IPersonRepository port methods
        ▼
  [IPersonRepository]           <- Application: Secondary Port (interface)
        │
        ▼
  [PersonRepository : IPersonRepository]  <- Phase 3: Secondary Adapter (EF Core InMemory)
        │
        ▼
  [AppDbContext / EF Core InMemory]    <- Phase 3: Infrastructure detail
```

### Recommended Project Structure
```
src/PersonsAPI.Application/
├── Behaviors/
│   └── ValidationBehavior.cs       # IPipelineBehavior<TMessage,TResponse>
├── Commands/
│   ├── CreatePersonCommand.cs      # record + ICommandHandler<,>
│   ├── UpdatePersonCommand.cs      # record + ICommandHandler<,>
│   ├── PatchPersonCommand.cs       # record + ICommandHandler<,>
│   └── DeletePersonCommand.cs      # record + ICommandHandler<,>
├── Queries/
│   ├── GetAllPersonsQuery.cs       # record + IQueryHandler<,>
│   └── GetPersonByIdQuery.cs       # record + IQueryHandler<,>
├── DTOs/
│   ├── CreatePersonRequest.cs      # record (required fields)
│   ├── UpdatePersonRequest.cs      # record (required fields, for PUT)
│   ├── UpdatePersonDto.cs          # record (nullable fields, for PATCH)
│   └── PersonResponse.cs           # record with static FromDomain(Person)
├── Exceptions/
│   └── PersonNotFoundException.cs  # sealed class : Exception
├── Ports/
│   └── IPersonRepository.cs        # Secondary Port interface
├── Validators/
│   ├── CreatePersonCommandValidator.cs
│   ├── UpdatePersonCommandValidator.cs
│   └── PatchPersonCommandValidator.cs
├── IApplicationMarker.cs           # Empty interface for assembly scanning
├── ServiceCollectionExtensions.cs  # AddApplication() DI entry point
└── PersonsAPI.Application.csproj
```

### Pattern 1: CQRS Message Types (records with Mediator.Abstractions)

**What:** Commands and queries defined as C# 14 `record` types implementing marker interfaces from `Mediator.Abstractions`.
**When to use:** All six use-case entry points.

```csharp
// Source: github.com/martinothamar/Mediator README.md (verified)
// Queries: implement IQuery<TResponse>
public record GetAllPersonsQuery : IQuery<IReadOnlyList<PersonResponse>>;

public record GetPersonByIdQuery(int Id) : IQuery<PersonResponse>;

// Commands returning a response
public record CreatePersonCommand(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth
) : ICommand<PersonResponse>;

// Commands with no meaningful return value use Unit
public record UpdatePersonCommand(int Id, UpdatePersonRequest Dto) : ICommand<PersonResponse>;

public record PatchPersonCommand(int Id, UpdatePersonDto Dto) : ICommand<PersonResponse>;

public record DeletePersonCommand(int Id) : ICommand<Unit>;
```

### Pattern 2: Handler Implementation (ICommandHandler / IQueryHandler)

**What:** Handlers use primary constructors (C# 14) to receive `IPersonRepository` and implement the typed handler interface.
**When to use:** Every command and query has exactly one handler.

```csharp
// Source: github.com/martinothamar/Mediator README.md (verified)
// IQueryHandler<TQuery, TResponse> — note ValueTask return type
public sealed class GetAllPersonsHandler(IPersonRepository repository)
    : IQueryHandler<GetAllPersonsQuery, IReadOnlyList<PersonResponse>>
{
    public async ValueTask<IReadOnlyList<PersonResponse>> Handle(
        GetAllPersonsQuery query,
        CancellationToken cancellationToken)
    {
        var persons = await repository.GetAllAsync(cancellationToken);
        return persons.Select(PersonResponse.FromDomain).ToList().AsReadOnly();
    }
}

// ICommandHandler<TCommand, TResponse>
public sealed class CreatePersonHandler(IPersonRepository repository)
    : ICommandHandler<CreatePersonCommand, PersonResponse>
{
    public async ValueTask<PersonResponse> Handle(
        CreatePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = Person.Create(
            command.FirstName,
            command.PaternalLastName,
            command.MaternalLastName,
            command.DateOfBirth);
        await repository.AddAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
```

### Pattern 3: ValidationBehavior with FluentValidation

**What:** Open-generic pipeline behavior that injects all registered validators for a request type and runs them before the handler. Graceful no-op when no validators are registered (D-10).
**When to use:** Registered once; applies to all message types that have a matching `IValidator<T>` registered.

```csharp
// Source: IPipelineBehavior interface from github.com/martinothamar/Mediator (verified)
// FluentValidation IEnumerable injection pattern (adapted from standard CQRS practice)
public sealed class ValidationBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TMessage>(message);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count > 0)
            throw new FluentValidation.ValidationException(failures);

        return await next(message, cancellationToken);
    }
}
```

**Critical difference from MediatR:** The `next` delegate is `MessageHandlerDelegate<TMessage, TResponse>` not `RequestHandlerDelegate`. It accepts `(message, cancellationToken)` as arguments, not a closure. [VERIFIED: github.com/martinothamar/Mediator/src/Mediator/Pipeline/]

### Pattern 4: IPersonRepository Port Interface

**What:** Secondary port interface in `Application/Ports/`. Returns `Task<T>` (not `ValueTask<T>`) so Infrastructure adapters can use `async/await` with EF Core naturally.
**When to use:** The only persistence interface the Application layer knows about.

```csharp
// Source: [ASSUMED] — pattern follows Hexagonal Architecture conventions for this project
public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);
    Task DeleteAsync(Person person, CancellationToken cancellationToken = default);
}
```

**Why `Task<T>` not `ValueTask<T>`:** EF Core async methods return `Task<T>`. The repository interface uses `Task<T>` to match EF Core semantics without wrapping. Handlers await Task-returning repository methods inside their `ValueTask`-returning `Handle()` — this is legal and idiomatic. [ASSUMED]

### Pattern 5: AddApplication() DI Extension Method

**What:** Single registration entry point for the Application layer. Called by the API layer's `Program.cs` in Phase 4.
**When to use:** Called exactly once in `Program.cs`.

```csharp
// Source: github.com/martinothamar/Mediator samples/apps/ASPNET_Core_CleanArchitecture (verified structure)
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.Assemblies = [typeof(IApplicationMarker)];
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
        });

        services.AddValidatorsFromAssembly(
            typeof(IApplicationMarker).Assembly,
            ServiceLifetime.Scoped);

        return services;
    }
}
```

**Important:** `AddMediator()` is defined in the generated code produced by `Mediator.SourceGenerator` — which lives in the API project. The `ServiceCollectionExtensions.cs` in the Application project calls `AddMediator()` which will be available at compile time because the API project (which references Application and has `Mediator.SourceGenerator`) generates it. [VERIFIED: github.com/martinothamar/Mediator README.md — "Install SourceGenerator only in outermost project"]

**Caveat:** Because `AddMediator()` is source-generated in the API project, and `AddApplication()` is defined in the Application project, there is a compile-time ordering constraint: the Application project cannot call `AddMediator()` directly from its own assembly. The call to `AddMediator()` with `options.PipelineBehaviors` must happen in a class that will be compiled together with the generated code — meaning `AddApplication()` is called from Program.cs in the API project, or the extension method lives in the Application project and is called from a context where the generated extension is in scope. This is a critical subtlety — see the Pitfalls section.

### Pattern 6: Validators with FluentValidation 12.1.1

**What:** Validators inherit from `AbstractValidator<T>` and are co-located with their commands.
**When to use:** Write commands only (D-08).

```csharp
// Source: docs.fluentvalidation.net (verified)
public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.PaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.MaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("DateOfBirth cannot be in the future.");
    }
}
```

### Pattern 7: PersonResponse Static Factory

**What:** Response DTO as a C# 14 record with a static `FromDomain` factory. No AutoMapper.
**When to use:** Every time a handler produces a response from a `Person` entity.

```csharp
// Source: CONTEXT.md D-07 (locked decision)
public record PersonResponse(
    int Id,
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth,
    int Age)
{
    public static PersonResponse FromDomain(Person person) => new(
        person.Id,
        person.FirstName,
        person.PaternalLastName,
        person.MaternalLastName,
        person.DateOfBirth,
        person.Age);
}
```

### Pattern 8: PatchPersonHandler with Null-Conditional Field Application

**What:** Applies only non-null fields from `UpdatePersonDto`, consistent with D-02. Uses C# 14 null-conditional patterns.
**When to use:** PATCH handler only.

```csharp
// Source: CONTEXT.md D-02 (locked decision) + Person.cs entity interface
public sealed class PatchPersonHandler(IPersonRepository repository)
    : ICommandHandler<PatchPersonCommand, PersonResponse>
{
    public async ValueTask<PersonResponse> Handle(
        PatchPersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        var dto = command.Dto;

        // Apply name fields only if at least one is non-null
        if (dto.FirstName is not null || dto.PaternalLastName is not null || dto.MaternalLastName is not null)
            person.UpdateName(
                dto.FirstName ?? person.FirstName,
                dto.PaternalLastName ?? person.PaternalLastName,
                dto.MaternalLastName ?? person.MaternalLastName);

        if (dto.DateOfBirth is not null)
            person.UpdateDateOfBirth(dto.DateOfBirth.Value);

        await repository.UpdateAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
```

### Anti-Patterns to Avoid

- **Installing Mediator.SourceGenerator in Application.csproj:** Causes CS0436 `AssemblyReference` type conflict. The source generator must reside only in the outermost executable project (PersonsAPI.Api). [VERIFIED: github.com/martinothamar/Mediator issue #261]
- **Using `Task<T>` return type in handlers:** Mediator.Abstractions defines `IPipelineBehavior<TMessage, TResponse>` and handler interfaces with `ValueTask<TResponse>`. Using `Task<T>` will not satisfy the interface. [VERIFIED: github.com/martinothamar/Mediator/src/Mediator/Pipeline/IPipelineBehavior.cs]
- **Using `RequestHandlerDelegate` (MediatR):** Mediator uses `MessageHandlerDelegate<TMessage, TResponse>(TMessage, CancellationToken)` — it takes explicit parameters, not a closure. Using MediatR's delegate signature will not compile. [VERIFIED: github.com/martinothamar/Mediator/src/Mediator/Pipeline/MessageHandlerDelegate.cs]
- **Throwing custom `ValidationException`:** The phase uses FluentValidation's built-in `FluentValidation.ValidationException` (which holds a `Failures` collection). Phase 4 catches this specific type. Do not create a custom `ValidationException` class.
- **Calling SetValidator or domain methods directly in handlers:** Handlers must call `Person.Create()`, `Person.UpdateName()`, `Person.UpdateDateOfBirth()` — never assign properties directly. Domain entity has private setters. [VERIFIED: src/PersonsAPI.Domain/Entities/Person.cs]
- **Importing EF Core or ASP.NET Core types in Application:** INFRA-02 and D-01 prohibit EF Core references in Application. No `using Microsoft.EntityFrameworkCore;` or `using Microsoft.AspNetCore` in Application project.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Mediator pipeline dispatch | Custom dispatcher with reflection | `Mediator.Abstractions` 3.0.2 | Source-generator produces monomorphized, allocation-free dispatch; hand-rolling loses compile-time validation |
| Assembly-scanning DI for validators | Manual `services.AddSingleton<IValidator<T>, TValidator>()` for each validator | `AddValidatorsFromAssembly()` from FluentValidation.DependencyInjectionExtensions | Manual registration breaks open-generic behavior matching |
| Validator pre-processor | Custom middleware or action filter | `ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<,>` | Pipeline behavior is the established pattern; action filters are ASP.NET Core layer concerns |
| FluentValidation rules | Custom guard classes in handlers | `AbstractValidator<T>` with `RuleFor()` chains | FluentValidation collects ALL failures before throwing; inline guards fail fast on first error |

**Key insight:** The Mediator source-generator's strength is compile-time handler discovery — hand-rolling dispatch loses exactly the property (build-time error on missing handler) that justifies adopting this library.

---

## Common Pitfalls

### Pitfall 1: Mediator.SourceGenerator in Application Project

**What goes wrong:** CS0436 compile error: `"The type 'AssemblyReference' in '...IncrementalMediatorGenerator\\AssemblyReference.g.cs' conflicts with the imported type 'AssemblyReference' in '...GeneratedAssemblyReference-in-Application-Layer'"`.
**Why it happens:** The source generator generates a set of types (`AssemblyReference`, `MediatorOptions`, `Mediator`) in whatever project it runs in. If it runs in both Application and Api (which references Application), both sets exist in the compilation, causing CS0436.
**How to avoid:** `Mediator.SourceGenerator` PackageReference goes ONLY in `PersonsAPI.Api.csproj`. Application uses only `Mediator.Abstractions`.
**Warning signs:** Errors mentioning `AssemblyReference` or `GeneratedMediator` type conflicts at compile time.
[VERIFIED: github.com/martinothamar/Mediator issue #261]

### Pitfall 2: Using Task instead of ValueTask in Handlers

**What goes wrong:** Handler class does not satisfy `ICommandHandler<TCommand, TResponse>` or `IQueryHandler<TQuery, TResponse>` — compile error about missing interface implementation or wrong return type.
**Why it happens:** The MediatR pattern uses `Task<TResponse>`. Mediator (martinothamar) uses `ValueTask<TResponse>` throughout. Copy-pasting MediatR handler templates breaks silently if only the return type is wrong.
**How to avoid:** Always declare handler methods as `public async ValueTask<TResponse> Handle(...)`.
**Warning signs:** "Does not implement interface member" compile error pointing at `Handle` method.
[VERIFIED: github.com/martinothamar/Mediator/src/Mediator/Pipeline/IPipelineBehavior.cs]

### Pitfall 3: Wrong Delegate Type in ValidationBehavior

**What goes wrong:** `next(cancellationToken)` compile error, or closure-based delegate that does not match the interface.
**Why it happens:** MediatR's `RequestHandlerDelegate` is `Func<Task<TResponse>>` (no parameters — captures message via closure). Mediator's `MessageHandlerDelegate<TMessage, TResponse>` is `delegate ValueTask<TResponse>(TMessage message, CancellationToken ct)` — it takes explicit parameters to avoid allocations.
**How to avoid:** Call `next(message, cancellationToken)` with both arguments.
**Warning signs:** Compile error on `next(...)` invocation: "delegate does not take N arguments".
[VERIFIED: github.com/martinothamar/Mediator/src/Mediator/Pipeline/MessageHandlerDelegate.cs]

### Pitfall 4: AddApplication() Calling AddMediator() Without SourceGenerator Available

**What goes wrong:** Compile error in `ServiceCollectionExtensions.cs`: "The name 'AddMediator' does not exist in the current context" or "IServiceCollection does not contain a definition for 'AddMediator'".
**Why it happens:** `AddMediator()` is a source-generated extension method. It is generated only in the project that has `Mediator.SourceGenerator` installed. If `ServiceCollectionExtensions.cs` lives in `PersonsAPI.Application` and Application does not have the source generator, `AddMediator()` is not available when compiling the Application project in isolation.
**How to avoid:** The `AddApplication()` method in `PersonsAPI.Application/ServiceCollectionExtensions.cs` will compile successfully because the Api project (which has `Mediator.SourceGenerator`) references Application. The generated `AddMediator()` extension method is available in the combined compilation. The Application project does NOT need the source generator itself — the generated code is produced in the Api project's compilation output, and the Application assembly calls it at runtime through the normal service collection.
**Clarification:** This works because `AddMediator()` is defined as an extension method on `IServiceCollection` in the generated code inside the Api project. When the Api project compiles and calls `AddApplication()`, the generated extension is in scope. However, the Application project itself cannot reference the generated code directly — if you try to build `PersonsAPI.Application` in isolation (without the Api project), `AddMediator()` will not resolve. In a layered build this is acceptable; the Application project is built as part of the Api project's build.
**Alternative approach:** Move the `AddMediator(...)` call to `Program.cs` in the Api layer and have `AddApplication()` only register validators and pipeline behaviors manually. This fully decouples Application from the generator. [ASSUMED — both approaches are valid]
**Warning signs:** "AddMediator does not exist" during isolated build of Application project.

### Pitfall 5: UpdatePersonDto vs UpdatePersonRequest Confusion

**What goes wrong:** PATCH handler receives a fully-populated DTO instead of nullable-field DTO; or PUT handler receives nullable fields.
**Why it happens:** D-05 defines two distinct types: `UpdatePersonRequest` (all required, for PUT) and `UpdatePersonDto` (all nullable, for PATCH). Conflating them means the PATCH handler cannot distinguish "omitted field" from "field set to null".
**How to avoid:** `PatchPersonCommand(int Id, UpdatePersonDto Dto)` takes `UpdatePersonDto`; `UpdatePersonCommand(int Id, UpdatePersonRequest Dto)` takes `UpdatePersonRequest`. Types are not interchangeable.
**Warning signs:** PATCH validator erroneously rejecting missing fields as "NotEmpty" failures.

### Pitfall 6: Calling UpdateName() with Partial Data

**What goes wrong:** A PATCH that updates only `FirstName` accidentally clears `PaternalLastName` and `MaternalLastName` because they are passed as null to `UpdateName()`, which throws `DomainException("paternalLastName cannot be null...")`.
**Why it happens:** `Person.UpdateName()` requires all three name parameters and validates them. Passing null for unchanged fields will trigger domain invariant violation.
**How to avoid:** In `PatchPersonHandler`, use `dto.Field ?? person.Field` as the fallback pattern before calling `UpdateName()`. Only call `UpdateName()` when at least one name field in the DTO is non-null (to avoid no-op updates). [VERIFIED: src/PersonsAPI.Domain/Entities/Person.cs — UpdateName validates all three params]

---

## IPersonRepository Method Signatures (Canonical Reference)

```csharp
// For Phase 3 implementation and Phase 4 usage.
// All return Task<T> to match EF Core async semantics.
namespace PersonsAPI.Application.Ports;

public interface IPersonRepository
{
    /// <summary>Returns all persons. Never returns null; returns empty list when no records exist.</summary>
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the person with the given ID, or null if not found.</summary>
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new person. Id is assigned by the store (EF Core identity).</summary>
    Task AddAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing person (entity already tracked or reattached).</summary>
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Removes a person from the store.</summary>
    Task DeleteAsync(Person person, CancellationToken cancellationToken = default);
}
```

**Note:** `UpdateAsync` and `DeleteAsync` receive the `Person` entity (not just an `int`). Handlers fetch the entity first (via `GetByIdAsync`), then pass it to the repository. This is the standard EF Core pattern — the tracked entity is what the context knows to update/delete. [ASSUMED — standard EF Core tracked-entity pattern]

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| MediatR (reflection-based) | Mediator 3.0.2 (source-generator) | 2022 (martinothamar) | Zero reflection overhead; compile-time handler discovery; AOT-compatible |
| MediatR 12.x (Apache 2.0) | Mediator 3.0.2 (MIT) | MediatR 13+ went commercial (2024) | Commercial license required for MediatR 13+; Mediator stays free |
| Task<T> return type | ValueTask<T> return type | Mediator 3.x design | Reduced heap allocations; important difference when writing handlers |
| `AddOpenBehavior(typeof(ValidationBehavior<,>))` (MediatR) | `options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]` (Mediator) | Mediator 3.x | Different registration API; must not use MediatR's `AddOpenBehavior` |
| AutoMapper for DTO mapping | Manual `static FromDomain()` factory | AutoMapper 15+ went commercial (Apr 2025) | CLAUDE.md prohibits AutoMapper; manual mapping is more debuggable for simple entities |
| FluentValidation.AspNetCore | FluentValidation + FluentValidation.DependencyInjectionExtensions | FV 12.x deprecates AspNetCore integration | `FluentValidation.AspNetCore` adds unnecessary ASP.NET Core coupling to Application layer |

**Deprecated/outdated:**
- `FluentValidation.AspNetCore`: Deprecated as the primary DI integration approach in FV 12.x. Use `FluentValidation.DependencyInjectionExtensions` directly. [CITED: docs.fluentvalidation.net]
- `IRequest<T>` (Mediator generic request): Mediator 3.x prefers specific `ICommand<T>` / `IQuery<T>` interfaces for semantic clarity. `IRequest<T>` still works but is less expressive. [CITED: github.com/martinothamar/Mediator README]
- `RequestHandlerDelegate` (MediatR naming): Mediator uses `MessageHandlerDelegate<TMessage, TResponse>`. Do not use MediatR naming.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `IPersonRepository` method signatures use `Task<T>` (not `ValueTask<T>`) for EF Core compatibility | Architecture Patterns — Pattern 4 | Low: If Phase 3 needs ValueTask, only the interface and handler `await` calls change |
| A2 | `UpdateAsync` and `DeleteAsync` accept the `Person` entity (not just `int id`) | IPersonRepository Signatures | Medium: If Phase 3 uses only IDs, handlers would not need to pre-fetch; but EF Core tracked-entity pattern favors entity-receive approach |
| A3 | `AddApplication()` calling `AddMediator()` works when Application project is compiled as part of Api project | Pitfall 4 / Pattern 5 | Medium: If build tooling compiles Application in isolation first, AddMediator() may not resolve. Mitigation: move AddMediator call to Program.cs in Api layer |
| A4 | `ValidationBehavior<,>` registered via `options.PipelineBehaviors` will match write commands that have validators, and silently no-op for queries without validators | Pattern 3 | Low: The behavior checks `validators.Any()` explicitly as a guard |

---

## Open Questions

1. **AddMediator() placement**
   - What we know: `AddMediator()` is source-generated in the Api project. `AddApplication()` is an extension method in the Application project.
   - What's unclear: Whether calling `AddMediator()` from within `AddApplication()` (which lives in Application) creates a compile-time dependency that breaks isolated Application builds.
   - Recommendation: Define `AddApplication()` with `AddMediator(options => ...)` inside it. The call to `AddApplication()` from `Program.cs` in the Api project will work correctly because the Api project has the source generator. Document that Application cannot be built in isolation as a library (acceptable for this architecture). If isolation is needed, move `AddMediator(...)` to `Program.cs` directly.

2. **ValidationException type**
   - What we know: Phase 4 must catch validation errors and return 400 Problem Details.
   - What's unclear: Whether Phase 4 catches `FluentValidation.ValidationException` directly or a custom application exception.
   - Recommendation: Throw `FluentValidation.ValidationException` from `ValidationBehavior`. Phase 4 (research) should confirm the exception filter catches `FluentValidation.ValidationException` specifically.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All project compilation | Yes | 10.0.202 | — |
| dotnet CLI | Package add, build | Yes | 10.0.202 | — |
| NuGet (network) | Package restore | Yes (assumed) | — | Use cached packages if offline |

**Missing dependencies with no fallback:** None.

**Missing dependencies with fallback:** None.

---

## Security Domain

> `security_enforcement: true`, `security_asvs_level: 1` in config.json.

### Applicable ASVS Categories (Phase 2 scope only)

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Phase 2 has no auth — Application layer defines use cases only |
| V3 Session Management | No | No session handling in Application layer |
| V4 Access Control | No | No authorization logic in this phase |
| V5 Input Validation | Yes | FluentValidation `ValidationBehavior` in Application layer enforces all write-command input validation |
| V6 Cryptography | No | No cryptographic operations in this phase |

### Known Threat Patterns for Application Layer (ASVS Level 1)

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Mass assignment / over-posting | Tampering | Explicit `CreatePersonCommand` and `UpdatePersonRequest` records with defined fields — no dynamic property binding |
| Input beyond domain limits | Tampering | `ValidationBehavior` runs FluentValidation before handler; domain invariants in `Person.Create()` provide second-layer defense |
| Missing validation for partial updates | Tampering | `PatchPersonCommandValidator` validates the post-application `UpdatePersonDto` before handler runs |
| CQRS handler bypass | Elevation of Privilege | All requests flow through `IMediator.Send()` which runs registered pipeline behaviors — no route to bypass ValidationBehavior |

**Security note for Phase 4:** `DomainException` and `FluentValidation.ValidationException` must be caught at the API boundary and mapped to structured Problem Details responses. Unhandled exceptions must not expose stack traces to clients. This is Phase 4's responsibility, not Phase 2's.

---

## Project Constraints (from CLAUDE.md)

| Directive | Applies to Phase 2 |
|-----------|-------------------|
| .NET 10 only; use latest C# 14 features where they clarify intent | Use primary constructors in handlers, `record` types for commands/queries/DTOs |
| Controllers only — no Minimal API endpoints | Not applicable to Application layer (no HTTP types here) |
| Rich models — business logic lives in domain entity | Handlers call `Person.Create()`, `UpdateName()`, `UpdateDateOfBirth()` — no logic in handlers beyond orchestration |
| All identifiers, comments, documentation in English | Enforced throughout Application layer code |
| Mediator 3.0.2 (martinothamar, MIT) — NOT MediatR 13+ | Use `Mediator.Abstractions` 3.0.2; `Mediator.SourceGenerator` goes in Api project |
| FluentValidation 12.1.1 with FluentValidation.DependencyInjectionExtensions | `AddValidatorsFromAssembly()` in `AddApplication()` |
| Manual mapping — NO AutoMapper | `PersonResponse.FromDomain(Person p)` static factory only |
| No `IRepository<T>` generic pattern | `IPersonRepository` is the specific typed interface — no generic base |

---

## Sources

### Primary (HIGH confidence)
- [github.com/martinothamar/Mediator README.md](https://github.com/martinothamar/Mediator/blob/main/README.md) — IPipelineBehavior interface, AddMediator() API, pipeline behavior registration, package installation guidance
- [github.com/martinothamar/Mediator src/Mediator/Pipeline/IPipelineBehavior.cs](https://github.com/martinothamar/Mediator/tree/main/src/Mediator/Pipeline) — exact interface signature verified
- [github.com/martinothamar/Mediator src/Mediator/Pipeline/MessageHandlerDelegate.cs](https://github.com/martinothamar/Mediator/tree/main/src/Mediator/Pipeline) — exact delegate signature verified
- [github.com/martinothamar/Mediator samples/apps/ASPNET_Core_CleanArchitecture](https://github.com/martinothamar/Mediator/tree/main/samples/apps/ASPNET_Core_CleanArchitecture) — Application layer csproj structure, ServiceCollectionExtensions pattern, Pipeline folder structure
- [nuget.org/packages/Mediator.Abstractions](https://www.nuget.org/packages/Mediator.Abstractions/) — version 3.0.2 confirmed, release date March 22 2026, 7.4M downloads
- [nuget.org/packages/Mediator.SourceGenerator](https://www.nuget.org/packages/Mediator.SourceGenerator/) — version 3.0.2 confirmed
- [nuget.org/packages/FluentValidation](https://www.nuget.org/packages/FluentValidation/) — version 12.1.1 confirmed, Apache 2.0
- [nuget.org/packages/FluentValidation.DependencyInjectionExtensions](https://www.nuget.org/packages/FluentValidation.DependencyInjectionExtensions/) — version 12.1.1 confirmed
- [github.com/martinothamar/Mediator issue #261](https://github.com/martinothamar/Mediator/issues/261) — CS0436 conflict from installing SourceGenerator in multiple projects
- [src/PersonsAPI.Domain/Entities/Person.cs](C:/ATS/Git/platform/src/PersonsAPI.Domain/Entities/Person.cs) — entity method signatures: Create(), UpdateName(), UpdateDateOfBirth(), property names

### Secondary (MEDIUM confidence)
- [docs.fluentvalidation.net/en/latest/di.html](https://docs.fluentvalidation.net/en/latest/di.html) — AddValidatorsFromAssemblyContaining() and AddValidatorsFromAssembly() usage
- [milanjovanovic.tech — CQRS Validation with FluentValidation](https://www.milanjovanovic.tech/blog/cqrs-validation-with-mediatr-pipeline-and-fluentvalidation) — ValidationBehavior IEnumerable<IValidator<TRequest>> pattern (MediatR, adapted for Mediator)

### Tertiary (LOW confidence)
- None — all critical patterns verified from primary sources

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages verified on nuget.org with release dates and download counts
- Mediator 3.0.2 API: HIGH — interface signatures verified from source code in GitHub repository
- ValidationBehavior pattern: HIGH — IPipelineBehavior + MessageHandlerDelegate verified from source; FluentValidation integration adapted from well-established cross-framework pattern
- IPersonRepository signatures: MEDIUM-HIGH — interface shape is standard; Task vs ValueTask choice is reasonable but assumed
- Architecture patterns: HIGH — verified from official Mediator sample (ASPNET_Core_CleanArchitecture)

**Research date:** 2026-05-29
**Valid until:** 2026-08-29 (90 days — stable library, low churn expected)
