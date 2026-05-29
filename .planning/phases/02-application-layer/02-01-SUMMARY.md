---
phase: 02-application-layer
plan: "01"
subsystem: application
tags: [application-layer, cqrs, ports-adapters, dto, fluentvalidation, mediator]
dependency_graph:
  requires:
    - 01-01: PersonsAPI.Domain project and Person entity
    - 01-02: DomainException pattern
  provides:
    - PersonsAPI.Application class library (net10.0) with three package references
    - IPersonRepository secondary port interface (INFRA-03)
    - PersonNotFoundException (D-04)
    - Four DTO records: PersonResponse, CreatePersonRequest, UpdatePersonRequest, UpdatePersonDto (D-05, D-06, D-07)
    - PersonsAPI.Application.Tests xUnit test harness
  affects:
    - 02-02: Requires Application project, DTOs, IPersonRepository, PersonNotFoundException
    - 03-01: Requires IPersonRepository port defined here
tech_stack:
  added:
    - Mediator.Abstractions 3.0.2 (MIT) — CQRS message/handler interfaces
    - FluentValidation 12.1.1 (Apache 2.0) — validator base class
    - FluentValidation.DependencyInjectionExtensions 12.1.1 (Apache 2.0) — AddValidatorsFromAssembly
  patterns:
    - C# record types for DTOs (immutable, value equality)
    - Static FromDomain factory on response record — no AutoMapper
    - File-scoped namespaces throughout
    - sealed classes for exception types
    - TDD RED/GREEN for DTO mapping verification
key_files:
  created:
    - src/PersonsAPI.Application/PersonsAPI.Application.csproj
    - src/PersonsAPI.Application/IApplicationMarker.cs
    - src/PersonsAPI.Application/Ports/IPersonRepository.cs
    - src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs
    - src/PersonsAPI.Application/DTOs/PersonResponse.cs
    - src/PersonsAPI.Application/DTOs/CreatePersonRequest.cs
    - src/PersonsAPI.Application/DTOs/UpdatePersonRequest.cs
    - src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs
    - tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj
    - tests/PersonsAPI.Application.Tests/DTOs/PersonResponseTests.cs
  modified:
    - PersonsAPI.sln
decisions:
  - "D-03: IPersonRepository.GetByIdAsync returns Person? (nullable) — Application layer decides not-found semantics"
  - "D-04: PersonNotFoundException is a sealed Exception subclass in PersonsAPI.Application.Exceptions with PersonId property"
  - "D-05: Three distinct request DTO records — CreatePersonRequest/UpdatePersonRequest (non-nullable), UpdatePersonDto (nullable for PATCH)"
  - "D-06: PersonResponse record includes Age field populated from domain computed property"
  - "D-07: PersonResponse.FromDomain(Person) is the single static mapping factory — no AutoMapper"
  - "INFRA-03: IPersonRepository declared in Application.Ports namespace, not in Infrastructure"
  - "Mediator.SourceGenerator excluded from Application.csproj — belongs only in Api project (prevents CS0436)"
metrics:
  duration: "~7 minutes"
  completed: "2026-05-29"
  tasks_completed: 3
  files_created: 10
  files_modified: 1
---

# Phase 2 Plan 1: Application Foundation — Class Library, Port, and DTOs

**One-liner:** PersonsAPI.Application class library with IPersonRepository secondary port, PersonNotFoundException, four DTO records with static FromDomain factory, and xUnit test harness proving computed-Age mapping.

## What Was Built

### Application Class Library (Task 1)

`src/PersonsAPI.Application/PersonsAPI.Application.csproj` targets `net10.0` with `LangVersion 14`, `Nullable enable`, `ImplicitUsings enable`. It references `PersonsAPI.Domain` and installs exactly three packages:

- `Mediator.Abstractions 3.0.2` — CQRS message/handler interfaces for Application layer
- `FluentValidation 12.1.1` — validator base class
- `FluentValidation.DependencyInjectionExtensions 12.1.1` — for `AddValidatorsFromAssembly()`

**Pitfall 1 avoided:** `Mediator.SourceGenerator` is NOT installed here — it belongs only in the Api project (Phase 4) to prevent CS0436 `AssemblyReference` type conflicts.

The test project `PersonsAPI.Application.Tests` mirrors the existing `PersonsAPI.Domain.Tests` shape: xUnit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4, coverlet.collector 6.0.4. Both projects are wired into `PersonsAPI.sln` under the existing `src` and `tests` solution folders.

### Marker Interface and Foundational Types (Task 2)

**IApplicationMarker** (`src/PersonsAPI.Application/IApplicationMarker.cs`): Empty public interface in the root `PersonsAPI.Application` namespace. Used as `typeof(IApplicationMarker).Assembly` anchor for `AddValidatorsFromAssembly()` in Plan 03's `AddApplication()` extension.

**IPersonRepository** (`src/PersonsAPI.Application/Ports/IPersonRepository.cs`): Secondary port interface implementing INFRA-03. Five methods all returning `Task<T>` (not `ValueTask<T>`) to match EF Core async semantics:

```
Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken = default)
Task<Person?> GetByIdAsync(int id, CancellationToken = default)     // D-03: nullable
Task AddAsync(Person person, CancellationToken = default)
Task UpdateAsync(Person person, CancellationToken = default)
Task DeleteAsync(Person person, CancellationToken = default)
```

`UpdateAsync` and `DeleteAsync` receive the full `Person` entity (handlers fetch first). No `IRepository<T>` generic base.

**PersonNotFoundException** (`src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs`): `sealed class : Exception` (not DomainException — D-04). Exposes `int PersonId { get; }` for Phase 4 Problem Details. Two constructors: `(int id)` and `(int id, Exception innerException)`.

### DTO Records with TDD (Task 3)

Four DTO records in `src/PersonsAPI.Application/DTOs/`:

| Type | Fields | Nullability | Purpose |
|------|--------|-------------|---------|
| `PersonResponse` | Id, FirstName, PaternalLastName, MaternalLastName, DateOfBirth, Age | all non-nullable | Response DTO for all handlers |
| `CreatePersonRequest` | FirstName, PaternalLastName, MaternalLastName, DateOfBirth | all non-nullable | POST /api/persons |
| `UpdatePersonRequest` | FirstName, PaternalLastName, MaternalLastName, DateOfBirth | all non-nullable | PUT /api/persons/{id} |
| `UpdatePersonDto` | FirstName, PaternalLastName, MaternalLastName, DateOfBirth | all nullable | PATCH after patch application |

**PersonResponse.FromDomain(Person person)** static factory maps all six fields including `person.Age` (the domain computed property). No AutoMapper anywhere in the assembly (D-07).

**TDD gate — RED/GREEN:**
- RED: `PersonResponseTests.cs` written first; build failed with CS0234 (DTOs namespace not found)
- GREEN: Four DTO files authored; both tests pass

## IPersonRepository Signature Surface (for downstream plans)

```csharp
namespace PersonsAPI.Application.Ports;

public interface IPersonRepository
{
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Person person, CancellationToken cancellationToken = default);
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);
    Task DeleteAsync(Person person, CancellationToken cancellationToken = default);
}
```

## Verification Results

- `dotnet build PersonsAPI.sln -c Debug`: succeeded, 0 errors, 1 pre-existing warning (CS0628 in Domain project)
- `dotnet test PersonsAPI.Application.Tests`: Passed: 2, Failed: 0
- `grep -rE "Microsoft.(EntityFrameworkCore|AspNetCore)" src/PersonsAPI.Application/`: no matches
- `grep -c "Mediator.SourceGenerator" PersonsAPI.Application.csproj`: 0

## Commits

| Hash | Message |
|------|---------|
| daf6e06 | chore(02-01): scaffold Application class library and test project |
| 1d72637 | feat(02-01): add IApplicationMarker, IPersonRepository port, and PersonNotFoundException |
| 209c0d6 | test(02-01): add failing tests for PersonResponse.FromDomain and record equality (RED) |
| a8e664f | feat(02-01): add four DTO records with PersonResponse.FromDomain factory (D-05, D-06, D-07) |

## Deviations from Plan

None — plan executed exactly as written.

## TDD Gate Compliance

- RED commit: `209c0d6` — `test(02-01): add failing tests for PersonResponse.FromDomain and record equality`
- GREEN commit: `a8e664f` — `feat(02-01): add four DTO records with PersonResponse.FromDomain factory`
- Both gates satisfied.

## Known Stubs

None — all artifacts are fully implemented. No placeholder text or empty wiring.

## Threat Flags

No new security-relevant surface introduced beyond what was analyzed in the plan's threat model (T-02-01 through T-02-SC). The explicit DTO records and typed port interface satisfy T-02-02 (mass assignment prevention). The nullable GetByIdAsync return satisfies T-02-03 (controlled not-found handling).

## Self-Check: PASSED

All created files exist and all commits are present in git log.
