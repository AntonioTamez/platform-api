---
phase: 02-application-layer
plan: "03"
subsystem: application
tags: [application-layer, fluentvalidation, pipeline-behavior, mediator, di-composition, val-01]
dependency_graph:
  requires:
    - 02-01: IApplicationMarker assembly anchor, PersonsAPI.Application class library
    - 02-02: CreatePersonCommandValidator, UpdatePersonCommandValidator, PatchPersonCommandValidator
  provides:
    - ValidationBehavior<TMessage, TResponse> (VAL-01)
    - AddApplication() DI composition root
  affects:
    - 04-01: Program.cs must call AddMediator(options.PipelineBehaviors=[typeof(ValidationBehavior<,>)]) + AddApplication(); middleware must catch FluentValidation.ValidationException (400) and PersonNotFoundException (404)
    - 03-01: No direct dependency — Infrastructure implements IPersonRepository port defined in 02-01
tech_stack:
  added: []
  patterns:
    - IPipelineBehavior<TMessage, TResponse> with Mediator 3.x ValueTask signature (Pitfall 2 guard)
    - MessageHandlerDelegate<TMessage, TResponse> called with explicit (message, cancellationToken) (Pitfall 3 guard)
    - D-10 short-circuit: validators.Any() guard bypasses handler for unvalidated messages
    - AddValidatorsFromAssembly with IApplicationMarker assembly anchor
    - Open-generic IPipelineBehavior<,>/ValidationBehavior<,> DI registration
    - Open Question 1 fallback: AddMediator() excluded from Application layer (Pitfall 4 guard)
    - TDD RED/GREEN for ValidationBehavior canonical scenarios
key_files:
  created:
    - src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs
    - src/PersonsAPI.Application/ServiceCollectionExtensions.cs
    - tests/PersonsAPI.Application.Tests/Behaviors/ValidationBehaviorTests.cs
  modified: []
decisions:
  - "VAL-01: ValidationBehavior<TMessage,TResponse> implements IPipelineBehavior with ValueTask return and MessageHandlerDelegate parameter — Mediator 3.x exact signature"
  - "D-10: validators.Any() short-circuit lets read queries and DeletePersonCommand pass through without any validator invocation"
  - "T-02-10: Throws FluentValidation.ValidationException (library type), not a custom application exception — Phase 4 catches this exact type for 400 Problem Details"
  - "Open Question 1 fallback: AddApplication() excludes AddMediator() call — Application builds in isolation; Phase 4's Program.cs owns AddMediator(options.PipelineBehaviors=[typeof(ValidationBehavior<,>)])"
metrics:
  duration: "~5 minutes"
  completed: "2026-05-29"
  tasks_completed: 2
  files_created: 3
  files_modified: 0
---

# Phase 2 Plan 3: ValidationBehavior Pipeline Behavior and AddApplication() Composition Root

**One-liner:** FluentValidation pipeline behavior implementing Mediator 3.x's exact IPipelineBehavior<TMessage,TResponse> signature with D-10 short-circuit, plus AddApplication() DI extension that registers validators and the ValidationBehavior open generic while deferring AddMediator() to Phase 4 (Open Question 1 fallback).

## What Was Built

### Task 1: ValidationBehavior<TMessage, TResponse> (TDD RED/GREEN)

**`src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs`**

`public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators) : IPipelineBehavior<TMessage, TResponse> where TMessage : notnull, IMessage`

C# 14 primary constructor receives all registered `IValidator<TMessage>` instances. Single `Handle` method with the exact Mediator 3.x signature:

```csharp
public async ValueTask<TResponse> Handle(
    TMessage message,
    MessageHandlerDelegate<TMessage, TResponse> next,
    CancellationToken cancellationToken)
```

**Pitfall guards baked into the type:**
- **Pitfall 2:** `ValueTask<TResponse>` return — NOT `Task<TResponse>`. Using `Task<T>` would fail to implement `IPipelineBehavior` from Mediator.Abstractions 3.x.
- **Pitfall 3:** `MessageHandlerDelegate<TMessage, TResponse>` next parameter called with `next(message, cancellationToken)` — NOT MediatR's closure `RequestHandlerDelegate` form.

**D-10 short-circuit:**
```csharp
if (!validators.Any())
    return await next(message, cancellationToken);
```
Read queries (`GetAllPersonsQuery`, `GetPersonByIdQuery`) and `DeletePersonCommand` have no registered `IValidator<T>`, so they pass through this check and reach the handler directly.

**Failure path:**
- Runs all validators via `Task.WhenAll(validators.Select(v => v.ValidateAsync(context, ct)))`
- Aggregates failures with `SelectMany(r => r.Errors).Where(e => e is not null)`
- Throws `FluentValidation.ValidationException(failures)` — the library type carrying the full `Errors` collection. Phase 4 catches this exact type (T-02-10 anti-pattern guard: no custom ValidationException class).

**`tests/PersonsAPI.Application.Tests/Behaviors/ValidationBehaviorTests.cs`**

Three canonical scenarios (no Moq — inline stub classes with manual invocation tracking):

| Test | Scenario | Assert |
|------|----------|--------|
| `Handle_NoValidators_CallsNextAndReturnsResult` | Empty IEnumerable (D-10) | result == "ok"; next called once |
| `Handle_ValidatorPasses_CallsNext` | Validator returns no failures | result == "ok"; next called |
| `Handle_ValidatorFails_ThrowsValidationException` | Validator returns 2 failures | throws ValidationException; Errors.Count() == 2; next NOT called |

`TestCommand(string Name) : ICommand<string>` — declared inside the test file as the test message type; implementing `ICommand<string>` satisfies the `where TMessage : notnull, IMessage` constraint.

TDD gate:
- RED commit `4a67be2`: CS0234 compile failure (Behaviors namespace not found) — confirmed RED before implementation.
- GREEN commit `d8b7c64`: ValidationBehavior.cs written; all 15 tests pass.

### Task 2: AddApplication() Composition Root

**`src/PersonsAPI.Application/ServiceCollectionExtensions.cs`**

`public static class ServiceCollectionExtensions` in namespace `PersonsAPI.Application` with a single extension method:

```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(
        typeof(IApplicationMarker).Assembly,
        ServiceLifetime.Scoped);

    services.AddScoped(
        typeof(IPipelineBehavior<,>),
        typeof(ValidationBehavior<,>));

    return services;
}
```

**Registration inventory:**
1. `AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly, ServiceLifetime.Scoped)` — discovers and registers:
   - `CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>` (Plan 02)
   - `UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>` (Plan 02)
   - `PatchPersonCommandValidator : AbstractValidator<PatchPersonCommand>` (Plan 02)
2. `services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))` — registers the open-generic behavior for DI construction.

**Open Question 1 fallback (Pitfall 4 guard):** `AddApplication()` does NOT call `AddMediator()`. The source-generated `AddMediator()` extension lives only in `PersonsAPI.Api` (which has `Mediator.SourceGenerator` installed). Calling it from Application would break isolated builds and violate the "Application project is standalone-buildable" invariant. `dotnet build src/PersonsAPI.Application/PersonsAPI.Application.csproj` exits 0 with 0 errors.

## Phase 4 Integration Checklist

The following items are Phase 4's responsibility (not done here):

| Item | Detail |
|------|--------|
| `AddMediator(options)` | Must call with `options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]` in `Program.cs` |
| `AddApplication()` | Call after or within the same registration block as `AddMediator` |
| Exception middleware — 400 | Catch `FluentValidation.ValidationException`; map to RFC 9457 Problem Details with 400 status and per-field `errors` array (ERR-01, ERR-02) |
| Exception middleware — 404 | Catch `PersonsAPI.Application.Exceptions.PersonNotFoundException`; map to RFC 9457 Problem Details with 404 status |
| Exception middleware — 422/500 | Catch `PersonsAPI.Domain.Exceptions.DomainException` for domain invariant violations; map to 422 or 400 |

## Requirement Closure

| Requirement | Artifact | Plan |
|-------------|---------|------|
| VAL-01 | ValidationBehavior<TMessage,TResponse> + AddApplication() | 02-03 (this plan) |
| INFRA-03 | IPersonRepository | 02-01 |
| READ-01, READ-02 | GetAllPersonsHandler, GetPersonByIdHandler | 02-02 |
| WRITE-01, WRITE-02, WRITE-03, WRITE-04 | Create/Update/Patch/DeletePersonHandler | 02-02 |

## Verification Results

- `dotnet build PersonsAPI.sln -c Debug`: Build succeeded, 0 errors, 1 pre-existing CS0628 warning (Domain project sealed class protected constructor — known, accepted)
- `dotnet test tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj`: Passed: 15, Failed: 0 (12 Plans 01+02 + 3 new Plan 03)
- `dotnet build src/PersonsAPI.Application/PersonsAPI.Application.csproj -c Debug`: Build succeeded, 0 errors (isolated Application build — Open Question 1 fallback confirmed)
- `grep -c "throw new ValidationException" src/.../ValidationBehavior.cs`: 1 (FluentValidation type)
- `grep -rE "services\.AddMediator\(" src/PersonsAPI.Application/`: 0 matches (Phase 4 owns that call)
- `grep -c "validators.Any()" src/.../ValidationBehavior.cs`: 1 (D-10 short-circuit)
- `grep -c "async Task<" src/.../ValidationBehavior.cs`: 0 (Pitfall 2 guard)

## Commits

| Hash | Message |
|------|---------|
| 4a67be2 | test(02-03): add failing tests for ValidationBehavior D-10 short-circuit and failure/pass-through cases (RED) |
| d8b7c64 | feat(02-03): implement ValidationBehavior<TMessage,TResponse> with D-10 short-circuit and FluentValidation.ValidationException (GREEN) |
| c02134d | feat(02-03): add AddApplication() composition root registering validators and ValidationBehavior open generic (VAL-01) |

## Deviations from Plan

None — plan executed exactly as written. All three artifacts match the canonical patterns from RESEARCH.md Pattern 3 and PATTERNS.md. Open Question 1 fallback was the prescribed approach and was applied as specified.

## TDD Gate Compliance

- RED commit: `4a67be2` — `test(02-03): add failing tests for ValidationBehavior D-10 short-circuit and failure/pass-through cases (RED)` — CS0234 compile failure confirmed before implementation.
- GREEN commit: `d8b7c64` — `feat(02-03): implement ValidationBehavior<TMessage,TResponse>...` — all 15 tests pass.
- Both gates satisfied.

## Known Stubs

None — ValidationBehavior is fully implemented with real FluentValidation dispatch. AddApplication() registers real validators from the assembly. No placeholder text or empty wiring.

## Threat Flags

No new security surface beyond the plan's threat model analysis. Mitigations applied:
- T-02-09 (pipeline bypass): ValidationBehavior registered as `IPipelineBehavior<,>` open generic; Phase 4 will add it to Mediator's PipelineBehaviors list.
- T-02-10 (custom ValidationException masking): `FluentValidation.ValidationException` thrown directly — no custom exception class in the Application assembly.

## Self-Check: PASSED

All three files exist and all three commits are present in git log.
