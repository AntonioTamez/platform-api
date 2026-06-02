# Phase 2: Application Layer - Pattern Map

**Mapped:** 2026-05-29
**Files analyzed:** 16 (14 source files + 1 project file + 1 marker interface)
**Analogs found:** 3 / 16 — partial analog coverage; remaining files use RESEARCH.md patterns

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/PersonsAPI.Application/PersonsAPI.Application.csproj` | config | n/a | `src/PersonsAPI.Domain/PersonsAPI.Domain.csproj` | role-match |
| `src/PersonsAPI.Application/IApplicationMarker.cs` | utility | n/a | None — no marker interfaces in codebase | no analog |
| `src/PersonsAPI.Application/Ports/IPersonRepository.cs` | utility (port) | CRUD | None — no repository interfaces yet | no analog |
| `src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs` | utility | n/a | `src/PersonsAPI.Domain/Exceptions/DomainException.cs` | role-match |
| `src/PersonsAPI.Application/DTOs/PersonResponse.cs` | model (DTO) | transform | None — no DTOs yet | no analog |
| `src/PersonsAPI.Application/DTOs/CreatePersonRequest.cs` | model (DTO) | request-response | None — no request DTOs yet | no analog |
| `src/PersonsAPI.Application/DTOs/UpdatePersonRequest.cs` | model (DTO) | request-response | None — no request DTOs yet | no analog |
| `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` | model (DTO) | request-response | None — no patch DTOs yet | no analog |
| `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` | middleware | request-response | None — no pipeline behaviors yet | no analog |
| `src/PersonsAPI.Application/Commands/CreatePersonCommand.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/Commands/UpdatePersonCommand.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/Commands/PatchPersonCommand.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/Commands/DeletePersonCommand.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/Queries/GetAllPersonsQuery.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/Queries/GetPersonByIdQuery.cs` | controller (handler) | CRUD | None — no CQRS handlers yet | no analog |
| `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` | config | n/a | None — no DI extensions yet | no analog |

---

## Pattern Assignments

### `src/PersonsAPI.Application/PersonsAPI.Application.csproj` (config)

**Analog:** `src/PersonsAPI.Domain/PersonsAPI.Domain.csproj` (lines 1-10)

**Csproj base pattern** (lines 1-10 of Domain csproj — copy PropertyGroup verbatim, add ProjectReference and PackageReferences):
```xml
<!-- Copy from: src/PersonsAPI.Domain/PersonsAPI.Domain.csproj lines 1-10 -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

</Project>
```

**Additions required for Application layer** (from RESEARCH.md §Standard Stack):
```xml
  <ItemGroup>
    <ProjectReference Include="..\PersonsAPI.Domain\PersonsAPI.Domain.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Mediator.Abstractions ONLY — do NOT add Mediator.SourceGenerator here (CS0436 conflict) -->
    <PackageReference Include="Mediator.Abstractions" Version="3.0.2" />
    <PackageReference Include="FluentValidation" Version="12.1.1" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
  </ItemGroup>
```

**Rules:**
- `LangVersion` stays `14` — same as Domain
- `Nullable` stays `enable`
- `Mediator.SourceGenerator` must NOT be added here — it goes in the Api project only (Pitfall 1 in RESEARCH.md)
- No `Microsoft.EntityFrameworkCore`, no ASP.NET Core packages
- One `ProjectReference` to Domain — the only inward dependency

---

### `src/PersonsAPI.Application/IApplicationMarker.cs` (utility, assembly marker)

**Analog:** None — derive from RESEARCH.md §Architecture Patterns Pattern 5

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 5 — AddApplication() uses typeof(IApplicationMarker).Assembly
namespace PersonsAPI.Application;

/// <summary>
/// Empty marker interface used for assembly scanning.
/// Pass <c>typeof(IApplicationMarker).Assembly</c> to
/// <c>AddValidatorsFromAssembly()</c> and <c>AddMediator(options)</c>.
/// </summary>
public interface IApplicationMarker { }
```

**Rules:**
- Empty body — no members
- `public` so Api project can reference it in Program.cs if needed
- Namespace is `PersonsAPI.Application` (root, no subfolder)

---

### `src/PersonsAPI.Application/Ports/IPersonRepository.cs` (port interface, CRUD)

**Analog:** None — derive from RESEARCH.md §IPersonRepository Method Signatures

**Canonical pattern:**
```csharp
// Source: RESEARCH.md §IPersonRepository Method Signatures (canonical reference)
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.Ports;

/// <summary>
/// Secondary port: persistence contract for Person aggregates.
/// Implemented in the Infrastructure layer (Phase 3) by PersonRepository using EF Core.
/// </summary>
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

**Rules:**
- All methods return `Task<T>` (not `ValueTask<T>`) — matches EF Core async semantics (RESEARCH.md A1)
- `GetByIdAsync` returns `Person?` (nullable) — caller (handler) decides "not found" meaning (D-03)
- `UpdateAsync` and `DeleteAsync` take the full `Person` entity, not an `int id` (RESEARCH.md A2)
- No `IRepository<T>` base interface — this is a specific typed port (CLAUDE.md constraint)

---

### `src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs` (utility, exception type)

**Analog:** `src/PersonsAPI.Domain/Exceptions/DomainException.cs` (lines 1-15)

**Exception class pattern** (copy structure from DomainException.cs lines 1-15, change namespace and message):
```csharp
// Copy structure from: src/PersonsAPI.Domain/Exceptions/DomainException.cs lines 1-15
// Change: namespace, class name, message format, base class
namespace PersonsAPI.Application.Exceptions;

/// <summary>
/// Thrown by Application handlers when a Person with the requested ID does not exist in the store.
/// Caught by the API layer (Phase 4) and mapped to 404 Problem Details.
/// </summary>
public sealed class PersonNotFoundException : Exception
{
    public int PersonId { get; }

    public PersonNotFoundException(int id)
        : base($"Person with ID {id} was not found.")
    {
        PersonId = id;
    }

    public PersonNotFoundException(int id, Exception innerException)
        : base($"Person with ID {id} was not found.", innerException)
    {
        PersonId = id;
    }
}
```

**Differences from DomainException analog:**
- Adds `PersonId` property — callers can extract the ID without parsing the message string (useful for Phase 4 Problem Details)
- Constructor takes `int id`, not `string message` — encapsulates the standard message format
- Namespace is `PersonsAPI.Application.Exceptions`, not `PersonsAPI.Domain.Exceptions`
- `sealed` modifier — same convention as Domain exceptions (Phase 1 PATTERNS.md)

---

### `src/PersonsAPI.Application/DTOs/PersonResponse.cs` (model/DTO, transform)

**Analog:** None — derive from RESEARCH.md §Architecture Patterns Pattern 7 and CONTEXT.md D-06/D-07

**Canonical pattern:**
```csharp
// Source: CONTEXT.md D-06, D-07; RESEARCH.md Pattern 7
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.DTOs;

/// <summary>
/// Response DTO returned by all Person handlers.
/// <see cref="Age"/> is read from the domain entity's computed property — never stored.
/// </summary>
public record PersonResponse(
    int Id,
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth,
    int Age)
{
    /// <summary>Maps a domain <see cref="Person"/> entity to a <see cref="PersonResponse"/>.</summary>
    public static PersonResponse FromDomain(Person person) => new(
        person.Id,
        person.FirstName,
        person.PaternalLastName,
        person.MaternalLastName,
        person.DateOfBirth,
        person.Age);
}
```

**Rules:**
- C# `record` type — immutable, value equality, concise positional syntax
- Static `FromDomain()` factory on the record itself — no external mapper class (D-07)
- `Age` maps `person.Age` (the computed domain property) — demonstrates the computed-property design goal (D-06)
- All six fields mandatory positional parameters — no optional members

---

### `src/PersonsAPI.Application/DTOs/CreatePersonRequest.cs` (model/DTO, request-response)

**Analog:** None — derive from CONTEXT.md D-05 and RESEARCH.md Pattern 6

**Canonical pattern:**
```csharp
// Source: CONTEXT.md D-05 (required fields for POST)
namespace PersonsAPI.Application.DTOs;

/// <summary>Request body for POST /api/persons. All four fields are required.</summary>
public record CreatePersonRequest(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth);
```

**Rules:**
- All four fields non-nullable — this is a POST request where all fields are mandatory (D-05)
- `record` type — same pattern as PersonResponse
- No validation attributes — validation lives in `CreatePersonCommandValidator` (D-09)

---

### `src/PersonsAPI.Application/DTOs/UpdatePersonRequest.cs` (model/DTO, request-response)

**Analog:** None — same shape as CreatePersonRequest, different semantic intent

**Canonical pattern:**
```csharp
// Source: CONTEXT.md D-05 (required fields for PUT)
namespace PersonsAPI.Application.DTOs;

/// <summary>Request body for PUT /api/persons/{id}. All four fields are required (full replacement).</summary>
public record UpdatePersonRequest(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth);
```

**Rules:**
- Identical shape to `CreatePersonRequest` — distinct type to express different intent (D-05)
- Distinct type means `UpdatePersonCommandValidator` can be declared independently without conditionals
- Do NOT merge with `CreatePersonRequest` even though the shape is identical today

---

### `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` (model/DTO, request-response for PATCH)

**Analog:** None — derive from CONTEXT.md D-02/D-05

**Canonical pattern:**
```csharp
// Source: CONTEXT.md D-02, D-05 (nullable fields for PATCH — only set fields are applied)
namespace PersonsAPI.Application.DTOs;

/// <summary>
/// DTO for PATCH /api/persons/{id}.
/// The controller (Phase 4) applies a <c>JsonPatchDocument&lt;UpdatePersonDto&gt;</c> to a fresh
/// instance of this type, then dispatches <see cref="Commands.PatchPersonCommand"/> with the result.
/// Only non-null fields are applied to the domain entity by the handler.
/// </summary>
public record UpdatePersonDto(
    string? FirstName,
    string? MaternalLastName,
    string? PaternalLastName,
    DateOnly? DateOfBirth);
```

**Rules:**
- All four fields are nullable (`string?`, `DateOnly?`) — null means "not patched" (D-02)
- `record` type — consistent with other DTO types
- No `JsonPatchDocument` import — Application layer stays free of ASP.NET Core types (D-01)

---

### `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` (middleware, request-response)

**Analog:** None — derive from RESEARCH.md §Architecture Patterns Pattern 3

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 3; IPipelineBehavior verified against
//         github.com/martinothamar/Mediator/src/Mediator/Pipeline/IPipelineBehavior.cs
using FluentValidation;
using Mediator;

namespace PersonsAPI.Application.Behaviors;

/// <summary>
/// Pipeline behavior that runs all registered FluentValidation validators for a message
/// before the handler executes. Short-circuits gracefully (no error) when no validator
/// is registered for the message type (D-10).
/// </summary>
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
            throw new ValidationException(failures);

        return await next(message, cancellationToken);
    }
}
```

**Critical rules (from RESEARCH.md Pitfalls 2 and 3):**
- Return type is `ValueTask<TResponse>` — NOT `Task<TResponse>` (Pitfall 2)
- Next delegate signature is `MessageHandlerDelegate<TMessage, TResponse>` — NOT `RequestHandlerDelegate` (Pitfall 3)
- Call `next(message, cancellationToken)` with both arguments — NOT `next()` or `next(cancellationToken)` (Pitfall 3)
- Throw `FluentValidation.ValidationException` (the library type) — do NOT define a custom `ValidationException` (RESEARCH.md §Anti-Patterns)

---

### `src/PersonsAPI.Application/Queries/GetAllPersonsQuery.cs` (handler, CRUD read)

**Analog:** None — derive from RESEARCH.md Pattern 1 and Pattern 2

**Canonical pattern (query record + handler in same file):**
```csharp
// Source: RESEARCH.md Pattern 1 (record definition) + Pattern 2 (handler structure)
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Queries;

/// <summary>Returns all persons as a read-only list.</summary>
public record GetAllPersonsQuery : IQuery<IReadOnlyList<PersonResponse>>;

/// <summary>Handles <see cref="GetAllPersonsQuery"/>.</summary>
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
```

**Rules:**
- Query record implements `IQuery<TResponse>` (not `IRequest<TResponse>`) — Mediator 3.x semantic preference
- Handler implements `IQueryHandler<TQuery, TResponse>` — not `IRequestHandler`
- Handler uses primary constructor (C# 14) — same pattern as all other handlers
- Handler method returns `ValueTask<TResponse>` — NOT `Task<TResponse>` (Pitfall 2)
- No validator registered — read queries carry no user-supplied body (D-08)
- Command and handler can live in the same file (consistent with Phase 1 single-file approach where files are small)

---

### `src/PersonsAPI.Application/Queries/GetPersonByIdQuery.cs` (handler, CRUD read)

**Analog:** None — derive from RESEARCH.md Pattern 1 and Pattern 2; CONTEXT.md D-03/D-04

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 1 + Pattern 2; CONTEXT.md D-03, D-04
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Queries;

/// <summary>Returns a single person by ID. Throws <see cref="PersonNotFoundException"/> when not found.</summary>
public record GetPersonByIdQuery(int Id) : IQuery<PersonResponse>;

/// <summary>Handles <see cref="GetPersonByIdQuery"/>.</summary>
public sealed class GetPersonByIdHandler(IPersonRepository repository)
    : IQueryHandler<GetPersonByIdQuery, PersonResponse>
{
    public async ValueTask<PersonResponse> Handle(
        GetPersonByIdQuery query,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new PersonNotFoundException(query.Id);

        return PersonResponse.FromDomain(person);
    }
}
```

**Rules:**
- Null-coalescing throw pattern: `?? throw new PersonNotFoundException(id)` — idiomatic C# one-liner (CONTEXT.md D-04)
- No validator — query has no user-supplied body (D-08)

---

### `src/PersonsAPI.Application/Commands/CreatePersonCommand.cs` (handler, CRUD write)

**Analog:** None — derive from RESEARCH.md Pattern 1, Pattern 2, Pattern 6

**Canonical pattern (command record + validator + handler in same file):**
```csharp
// Source: RESEARCH.md Pattern 1 (record), Pattern 2 (handler), Pattern 6 (validator)
using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Ports;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.Commands;

/// <summary>Creates a new person. Returns the created <see cref="PersonResponse"/>.</summary>
public record CreatePersonCommand(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth) : ICommand<PersonResponse>;

/// <summary>Validates <see cref="CreatePersonCommand"/> inputs before the handler runs.</summary>
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

/// <summary>Handles <see cref="CreatePersonCommand"/>.</summary>
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

**Rules:**
- Command record implements `ICommand<PersonResponse>` — returns the created entity in the response
- Handler calls `Person.Create()` — never sets properties directly (Domain entity has `private set`)
- Validator mirrors domain invariants intentionally (D-09) — Application validates for field-level 400 detail; Domain is the second line of defense
- `ValueTask<PersonResponse>` return — not `Task<TResponse>` (Pitfall 2)

---

### `src/PersonsAPI.Application/Commands/UpdatePersonCommand.cs` (handler, CRUD write)

**Analog:** None — same structure as CreatePersonCommand, uses UpdatePersonRequest DTO

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 1 + Pattern 2 + Pattern 6; CONTEXT.md D-05, D-09
using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>Fully replaces a person's data (PUT). Returns the updated <see cref="PersonResponse"/>.</summary>
public record UpdatePersonCommand(int Id, UpdatePersonRequest Dto) : ICommand<PersonResponse>;

/// <summary>Validates <see cref="UpdatePersonCommand"/> inputs before the handler runs.</summary>
public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.Dto.FirstName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.PaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.MaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("DateOfBirth cannot be in the future.");
    }
}

/// <summary>Handles <see cref="UpdatePersonCommand"/>.</summary>
public sealed class UpdatePersonHandler(IPersonRepository repository)
    : ICommandHandler<UpdatePersonCommand, PersonResponse>
{
    public async ValueTask<PersonResponse> Handle(
        UpdatePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        person.UpdateName(
            command.Dto.FirstName,
            command.Dto.PaternalLastName,
            command.Dto.MaternalLastName);

        person.UpdateDateOfBirth(command.Dto.DateOfBirth);

        await repository.UpdateAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
```

**Rules:**
- Fetch-then-throw pattern: `GetByIdAsync() ?? throw new PersonNotFoundException(id)` — same as GetPersonByIdHandler
- Calls `person.UpdateName()` with all three name fields (from the required DTO) — no null fallback needed for PUT
- Calls `person.UpdateDateOfBirth()` after `UpdateName()` — two separate domain method calls

---

### `src/PersonsAPI.Application/Commands/PatchPersonCommand.cs` (handler, CRUD write)

**Analog:** None — derive from RESEARCH.md Pattern 8 (PatchPersonHandler) and CONTEXT.md D-02

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 8; CONTEXT.md D-02
// Critical: null-fallback pattern prevents Pitfall 6 (calling UpdateName with partial data)
using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>Partially updates a person's data (PATCH). Returns the updated <see cref="PersonResponse"/>.</summary>
public record PatchPersonCommand(int Id, UpdatePersonDto Dto) : ICommand<PersonResponse>;

/// <summary>
/// Validates non-null fields in <see cref="PatchPersonCommand"/> before the handler runs.
/// Null fields (not patched) are skipped — "When" conditions prevent NotEmpty failures on omitted fields.
/// </summary>
public sealed class PatchPersonCommandValidator : AbstractValidator<PatchPersonCommand>
{
    public PatchPersonCommandValidator()
    {
        When(x => x.Dto.FirstName is not null, () =>
        {
            RuleFor(x => x.Dto.FirstName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.PaternalLastName is not null, () =>
        {
            RuleFor(x => x.Dto.PaternalLastName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.MaternalLastName is not null, () =>
        {
            RuleFor(x => x.Dto.MaternalLastName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.DateOfBirth is not null, () =>
        {
            RuleFor(x => x.Dto.DateOfBirth!.Value)
                .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
                .WithMessage("DateOfBirth cannot be in the future.");
        });
    }
}

/// <summary>Handles <see cref="PatchPersonCommand"/>.</summary>
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

        // Apply name fields only when at least one name field is non-null (Pitfall 6 guard)
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

**Critical rules (from RESEARCH.md Pitfall 6 and CONTEXT.md D-02):**
- Use `dto.Field ?? person.Field` as fallback — never pass null to `UpdateName()`, it throws `DomainException`
- Only call `UpdateName()` when at least one name field is non-null — avoids no-op domain calls
- Validator uses FluentValidation `When()` conditions — null fields must not trigger `NotEmpty` failures (Pitfall 5 guard)
- `dto.DateOfBirth.Value` (not `dto.DateOfBirth`) when calling `UpdateDateOfBirth()` — unwrap the nullable

---

### `src/PersonsAPI.Application/Commands/DeletePersonCommand.cs` (handler, CRUD write)

**Analog:** None — derive from RESEARCH.md Pattern 1 and Pattern 2

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 1 + Pattern 2; CONTEXT.md WRITE-04
using Mediator;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>Deletes a person by ID. Returns <see cref="Unit"/> (no content). Throws <see cref="PersonNotFoundException"/> if not found.</summary>
public record DeletePersonCommand(int Id) : ICommand<Unit>;

/// <summary>Handles <see cref="DeletePersonCommand"/>.</summary>
public sealed class DeletePersonHandler(IPersonRepository repository)
    : ICommandHandler<DeletePersonCommand, Unit>
{
    public async ValueTask<Unit> Handle(
        DeletePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        await repository.DeleteAsync(person, cancellationToken);
        return Unit.Value;
    }
}
```

**Rules:**
- Returns `ICommand<Unit>` — no meaningful return value from delete
- Return `Unit.Value` from handler — Mediator's unit type, not `MediatR.Unit`
- No validator registered — no user-supplied body data (D-08 extended reasoning: only route ID, controller concern)
- Fetch entity first (`GetByIdAsync`), then pass entity to `DeleteAsync` — EF Core tracked-entity pattern (RESEARCH.md A2)

---

### `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` (config, DI registration)

**Analog:** None — derive from RESEARCH.md §Architecture Patterns Pattern 5

**Canonical pattern:**
```csharp
// Source: RESEARCH.md Pattern 5; github.com/martinothamar/Mediator samples/ASPNET_Core_CleanArchitecture
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Application.Behaviors;

namespace PersonsAPI.Application;

/// <summary>
/// Extension methods for registering Application layer services.
/// Called exactly once from <c>Program.cs</c> in the Api layer (Phase 4).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });

        services.AddValidatorsFromAssembly(
            typeof(IApplicationMarker).Assembly,
            ServiceLifetime.Scoped);

        return services;
    }
}
```

**Critical note on `AddMediator()` and pipeline behavior registration (RESEARCH.md Pitfall 4 and Open Question 1):**

`AddMediator()` is source-generated in the Api project (where `Mediator.SourceGenerator` lives). The `AddApplication()` method in the Application project can reference it only when compiled together with the Api project — it cannot be built in isolation. This is architecturally acceptable for this project.

The `options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]` registration option shown in RESEARCH.md Pattern 5 may or may not compile in the Application project depending on Mediator version. **Safe fallback:** If `AddMediator()` is unavailable in the Application project's isolated compilation, move the `AddMediator(...)` call to `Program.cs` in the Api layer and register the behavior manually:

```csharp
// Fallback — add in Program.cs of Api layer if AddMediator() is not resolvable from Application
services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});
```

The planner should note this open question and provide a clear fallback step in the plan actions.

---

## Shared Patterns

### Exception as Error Contract
**Source:** `src/PersonsAPI.Domain/Exceptions/DomainException.cs` (lines 1-15)
**Apply to:** `PersonNotFoundException.cs`, all handlers

```csharp
// Copy structure from: src/PersonsAPI.Domain/Exceptions/DomainException.cs lines 9-15
// Pattern: sealed class : Exception, two constructors, English message
public sealed class PersonNotFoundException : Exception
{
    public PersonNotFoundException(int id) : base($"Person with ID {id} was not found.") { }
    public PersonNotFoundException(int id, Exception inner) : base($"Person with ID {id} was not found.", inner) { }
}
```

All handlers use the null-coalescing throw pattern:
```csharp
// Copy from: GetPersonByIdQuery.cs handler body
var person = await repository.GetByIdAsync(id, cancellationToken)
    ?? throw new PersonNotFoundException(id);
```

### Mediator Handler Signature
**Apply to:** All six handler classes (GetAllPersons, GetPersonById, CreatePerson, UpdatePerson, PatchPerson, DeletePerson)

The canonical handler method signature — copy exactly, change only type parameters:
```csharp
// Source: RESEARCH.md Pattern 2; verified against martinothamar/Mediator source
public async ValueTask<TResponse> Handle(
    TMessage message,                          // named for clarity in your specific handler
    CancellationToken cancellationToken)
```

Key differences from MediatR that must be copy-pasted correctly:
- `ValueTask<TResponse>` not `Task<TResponse>`
- Interface is `ICommandHandler<TCommand, TResponse>` or `IQueryHandler<TQuery, TResponse>` — not `IRequestHandler`

### Primary Constructor Pattern
**Source:** CLAUDE.md §C# 14 Features — Primary Constructors
**Apply to:** All handler classes

```csharp
// C# 14 primary constructor — dependency injected directly on the class declaration line
public sealed class SomeHandler(IPersonRepository repository)
    : ICommandHandler<SomeCommand, PersonResponse>
```

No `private readonly IPersonRepository _repository` field needed. The parameter is captured automatically.

### File-Scoped Namespace
**Source:** `src/PersonsAPI.Domain/Entities/Person.cs` (line 3), `src/PersonsAPI.Domain/Exceptions/DomainException.cs` (line 1)
**Apply to:** All files in this phase

```csharp
// Copy from: src/PersonsAPI.Domain/Entities/Person.cs line 3
namespace PersonsAPI.Application.{Subfolder};   // no braces, file-scoped
```

### XML Documentation Comments
**Source:** `src/PersonsAPI.Domain/Entities/Person.cs` (lines 5-8, 15-17, 55-58, 64-73)
**Apply to:** All public types and interfaces

Use `/// <summary>` on every public class, interface, record, and non-obvious public method. Single-line summary is sufficient. Pattern from Domain:
```csharp
// Copy comment style from: src/PersonsAPI.Domain/Entities/Person.cs lines 5-8
/// <summary>
/// Brief description of what this type/member does.
/// </summary>
```

### `sealed` Class Modifier
**Source:** `src/PersonsAPI.Domain/Entities/Person.cs` (line 9), `src/PersonsAPI.Domain/Exceptions/DomainException.cs` (line 9)
**Apply to:** All concrete handler classes, exception class, validator classes, behavior class

```csharp
// Copy pattern from: src/PersonsAPI.Domain/Entities/Person.cs line 9
public sealed class ...   // no planned subclasses → always seal concrete types
```

---

## No Analog Found

Files with no close match in the codebase (planner uses RESEARCH.md patterns instead):

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `IApplicationMarker.cs` | utility | n/a | No marker interfaces exist yet — Phase 1 had no need for assembly scanning |
| `Ports/IPersonRepository.cs` | utility (port) | CRUD | No repository interfaces exist yet — Infrastructure is Phase 3 |
| `DTOs/PersonResponse.cs` | model | transform | No DTOs exist yet — Phase 1 is pure domain |
| `DTOs/CreatePersonRequest.cs` | model | request-response | No request DTOs exist yet |
| `DTOs/UpdatePersonRequest.cs` | model | request-response | No request DTOs exist yet |
| `DTOs/UpdatePersonDto.cs` | model | request-response | No patch DTOs exist yet |
| `Behaviors/ValidationBehavior.cs` | middleware | request-response | No pipeline behaviors exist yet |
| `Commands/CreatePersonCommand.cs` | handler | CRUD | No CQRS handlers exist yet |
| `Commands/UpdatePersonCommand.cs` | handler | CRUD | No CQRS handlers exist yet |
| `Commands/PatchPersonCommand.cs` | handler | CRUD | No CQRS handlers exist yet |
| `Commands/DeletePersonCommand.cs` | handler | CRUD | No CQRS handlers exist yet |
| `Queries/GetAllPersonsQuery.cs` | handler | CRUD | No CQRS handlers exist yet |
| `Queries/GetPersonByIdQuery.cs` | handler | CRUD | No CQRS handlers exist yet |
| `ServiceCollectionExtensions.cs` | config | n/a | No DI extension methods exist yet |

---

## Metadata

**Analog search scope:** `C:/ATS/Git/platform/src/` (all .cs and .csproj files)
**Files scanned:** 5 (`Person.cs`, `DomainException.cs`, `PersonsAPI.Domain.csproj`, plus obj/ generated files ignored)
**Analog files used:** 3 (`Person.cs`, `DomainException.cs`, `PersonsAPI.Domain.csproj`)
**Pattern source for no-analog files:** RESEARCH.md §Architecture Patterns (Patterns 1–8), §IPersonRepository Method Signatures, CONTEXT.md §Implementation Decisions (D-01 through D-10)
**Pattern extraction date:** 2026-05-29
