---
phase: 02-application-layer
plan: "02"
subsystem: application
tags: [application-layer, cqrs, queries, commands, fluentvalidation, mediator, valuetask]
dependency_graph:
  requires:
    - 02-01: PersonsAPI.Application class library, IPersonRepository port, PersonNotFoundException, DTOs
    - 01-01: Person.Create, UpdateName, UpdateDateOfBirth, private-set properties
  provides:
    - GetAllPersonsQuery + GetAllPersonsHandler (READ-01)
    - GetPersonByIdQuery + GetPersonByIdHandler (READ-02)
    - CreatePersonCommand + CreatePersonCommandValidator + CreatePersonHandler (WRITE-01)
    - UpdatePersonCommand + UpdatePersonCommandValidator + UpdatePersonHandler (WRITE-02)
    - PatchPersonCommand + PatchPersonCommandValidator + PatchPersonHandler (WRITE-03)
    - DeletePersonCommand + DeletePersonHandler (WRITE-04)
    - CreatePersonCommandValidatorTests (happy path + all failure modes)
    - PatchPersonCommandValidatorTests (null-skip behavior + non-null rules)
  affects:
    - 02-03: ValidationBehavior pipeline behavior dispatches to validators defined here
    - 03-01: PersonRepository implements IPersonRepository; handlers call it via the port
    - 04-01: PersonsController dispatches commands/queries defined here via IMediator.Send()
tech_stack:
  added: []
  patterns:
    - CQRS command/query records implementing ICommand<T>/IQuery<T> from Mediator.Abstractions 3.0.2
    - ICommandHandler/IQueryHandler with ValueTask<T> (Mediator 3.x, NOT Task<T>)
    - Primary constructors (C# 14) for all handler classes
    - sealed concrete handler and validator classes
    - FluentValidation AbstractValidator<T> with RuleFor chains mirroring domain invariants (D-09)
    - FluentValidation When() conditions for nullable-field PATCH validation (Pitfall 5 guard)
    - Null-fallback pattern: dto.Field ?? person.Field before UpdateName (Pitfall 6 guard)
    - Null-coalescing throw: ?? throw new PersonNotFoundException(id) (D-04)
    - Static PersonResponse.FromDomain factory as the sole mapping path (D-07)
key_files:
  created:
    - src/PersonsAPI.Application/Queries/GetAllPersonsQuery.cs
    - src/PersonsAPI.Application/Queries/GetPersonByIdQuery.cs
    - src/PersonsAPI.Application/Commands/CreatePersonCommand.cs
    - src/PersonsAPI.Application/Commands/UpdatePersonCommand.cs
    - src/PersonsAPI.Application/Commands/PatchPersonCommand.cs
    - src/PersonsAPI.Application/Commands/DeletePersonCommand.cs
    - tests/PersonsAPI.Application.Tests/Commands/CreatePersonCommandValidatorTests.cs
    - tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs
  modified: []
decisions:
  - "D-08: Validators for write commands only — CreatePersonCommandValidator, UpdatePersonCommandValidator, PatchPersonCommandValidator. Read queries and DeletePersonCommand have no validator."
  - "D-09: Validator rules intentionally mirror domain invariants (NotEmpty + length 2–100 + DateOfBirth <= today). Application validates for field-level 400 detail; Domain is the second line of defense."
  - "Pitfall 5 guard: PatchPersonCommandValidator uses When(field is not null, ...) blocks so null fields skip NotEmpty/length rules entirely."
  - "Pitfall 6 guard: PatchPersonHandler uses dto.Field ?? person.Field before calling Person.UpdateName(); UpdateName is called only when at least one name field is non-null."
  - "Pitfall 2 guard: All six handlers declare ValueTask<T> Handle(...) — not Task<T>."
metrics:
  duration: "~8 minutes"
  completed: "2026-05-29"
  tasks_completed: 3
  files_created: 8
  files_modified: 0
---

# Phase 2 Plan 2: CQRS Use-Cases — Queries, Commands, Validators, and Handler Tests

**One-liner:** Six CQRS handlers (two queries, four commands) plus three FluentValidation validators and twelve passing unit tests closing READ-01, READ-02, WRITE-01, WRITE-02, WRITE-03, and WRITE-04 at the Application layer level.

## What Was Built

### Task 1: Query Handlers (READ-01, READ-02)

**`src/PersonsAPI.Application/Queries/GetAllPersonsQuery.cs`**

`GetAllPersonsQuery : IQuery<IReadOnlyList<PersonResponse>>` — parameterless record. `GetAllPersonsHandler(IPersonRepository repository)` implements `IQueryHandler<GetAllPersonsQuery, IReadOnlyList<PersonResponse>>` using a C# 14 primary constructor. The `Handle` method returns `ValueTask<IReadOnlyList<PersonResponse>>` and projects `repository.GetAllAsync()` through `PersonResponse.FromDomain` via LINQ `Select`. No validator registered (D-08 — no user-supplied body).

**`src/PersonsAPI.Application/Queries/GetPersonByIdQuery.cs`**

`GetPersonByIdQuery(int Id) : IQuery<PersonResponse>` — single positional int Id. `GetPersonByIdHandler` uses the canonical null-coalescing throw: `await repository.GetByIdAsync(query.Id, ct) ?? throw new PersonNotFoundException(query.Id)`. Closes D-03/D-04 (Application layer decides not-found semantics; Phase 4 catches `PersonNotFoundException` for 404 mapping).

### Task 2: Create, Update, Delete Commands (WRITE-01, WRITE-02, WRITE-04)

**`src/PersonsAPI.Application/Commands/CreatePersonCommand.cs`**

`CreatePersonCommand(string FirstName, string PaternalLastName, string MaternalLastName, DateOnly DateOfBirth) : ICommand<PersonResponse>` — four positional params matching `CreatePersonRequest` field order. `CreatePersonCommandValidator` enforces `NotEmpty().MinimumLength(2).MaximumLength(100)` on all three name fields and `Must(d => d <= DateOnly.FromDateTime(DateTime.Today)).WithMessage("DateOfBirth cannot be in the future.")` on DateOfBirth. `CreatePersonHandler` calls `Person.Create(...)` — never sets properties directly (domain entity has `private set`; direct assignment would not compile — T-02-07 mitigated by type system).

**`src/PersonsAPI.Application/Commands/UpdatePersonCommand.cs`**

`UpdatePersonCommand(int Id, UpdatePersonRequest Dto) : ICommand<PersonResponse>`. `UpdatePersonCommandValidator` applies same rules through `x.Dto.*` property paths. `UpdatePersonHandler` follows the fetch-then-throw pattern, then calls `person.UpdateName(...)` with all three name parameters from the required DTO (no null fallback needed for PUT — all fields required), then calls `person.UpdateDateOfBirth(...)` as a separate domain call.

**`src/PersonsAPI.Application/Commands/DeletePersonCommand.cs`**

`DeletePersonCommand(int Id) : ICommand<Unit>`. No validator (D-08 — route ID only). `DeletePersonHandler` fetches the entity first (EF Core tracked-entity pattern — required by `IPersonRepository.DeleteAsync(Person)` signature from Plan 01), throws `PersonNotFoundException` on null, then calls `repository.DeleteAsync(person, ct)` and returns `Unit.Value`.

**`tests/PersonsAPI.Application.Tests/Commands/CreatePersonCommandValidatorTests.cs`**

6 tests: `Validate_HappyPath_HasNoErrors`, `Validate_FirstNameEmptyOrTooShort_HasError` (Theory with `""`, `" "`, `"A"`), `Validate_FirstNameTooLong_HasError` (101-char string), `Validate_DateOfBirthInFuture_HasError` (today + 1 day, checks exact error message).

### Task 3: PatchPersonCommand — Pitfall 5 and 6 Guards (WRITE-03)

**`src/PersonsAPI.Application/Commands/PatchPersonCommand.cs`**

`PatchPersonCommand(int Id, UpdatePersonDto Dto) : ICommand<PersonResponse>` — Dto type is `UpdatePersonDto` (four nullable fields), never `UpdatePersonRequest` (Pitfall 5 guard enforced at the type level).

`PatchPersonCommandValidator` uses four `When(x => x.Dto.Field is not null, () => { RuleFor(...) })` blocks:
- `When(x => x.Dto.FirstName is not null, ...)` → `NotEmpty().MinimumLength(2).MaximumLength(100)` inside
- Same for `PaternalLastName` and `MaternalLastName`
- `When(x => x.Dto.DateOfBirth is not null, ...)` → `RuleFor(x => x.Dto.DateOfBirth!.Value).Must(...).WithMessage("DateOfBirth cannot be in the future.")`

`PatchPersonHandler` implements the Pitfall 6 guard in two parts:
1. `if (dto.FirstName is not null || dto.PaternalLastName is not null || dto.MaternalLastName is not null)` — prevents no-op UpdateName calls
2. `person.UpdateName(dto.FirstName ?? person.FirstName, dto.PaternalLastName ?? person.PaternalLastName, dto.MaternalLastName ?? person.MaternalLastName)` — the `??` fallback ensures `UpdateName` always receives non-null strings (domain invariant: `ValidateName` throws `DomainException` on null/whitespace)

**`tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs`**

4 tests proving the `When()` behavior: `Validate_AllFieldsNull_HasNoErrors` (all-null DTO → no errors, the critical Pitfall 5 proof), `Validate_OnlyFirstNameProvidedValid_HasNoErrors`, `Validate_FirstNameProvidedEmpty_HasError`, `Validate_DateOfBirthInFuture_HasError`.

## Requirement Closure

| Requirement | Handler | Closed By |
|-------------|---------|-----------|
| READ-01 | GetAllPersonsHandler | Task 1 |
| READ-02 | GetPersonByIdHandler | Task 1 |
| WRITE-01 | CreatePersonHandler | Task 2 |
| WRITE-02 | UpdatePersonHandler | Task 2 |
| WRITE-03 | PatchPersonHandler | Task 3 |
| WRITE-04 | DeletePersonHandler | Task 2 |

## Pitfall Guard Summary

| Pitfall | Guard Location | Structural Proof |
|---------|---------------|------------------|
| Pitfall 2: Task vs ValueTask | All six handlers | `grep -rE "async Task<" src/PersonsAPI.Application/Commands/ src/PersonsAPI.Application/Queries/` returns 0 matches |
| Pitfall 5: UpdatePersonDto vs UpdatePersonRequest | PatchPersonCommand record | `grep -c "UpdatePersonRequest" PatchPersonCommand.cs` returns 0 |
| Pitfall 6: null to UpdateName | PatchPersonHandler | `dto.Field ?? person.Field` fallback pattern; OR guard prevents no-op call |

## Verification Results

- `dotnet build src/PersonsAPI.Application/PersonsAPI.Application.csproj -c Debug`: succeeded, 0 errors, 1 pre-existing CS0628 warning (protected member in sealed class in Domain project)
- `dotnet test tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj`: Passed: 12, Failed: 0
- `grep -rE "async Task<" src/PersonsAPI.Application/Commands/ src/PersonsAPI.Application/Queries/`: no matches (Pitfall 2 guard)
- `grep -rE "RequestHandlerDelegate" src/PersonsAPI.Application/`: no matches (Pitfall 3 guard)
- `grep -rE "IRequest<" src/PersonsAPI.Application/`: no matches (Mediator 3.x semantic preference enforced)
- Three validator classes present: CreatePersonCommandValidator, UpdatePersonCommandValidator, PatchPersonCommandValidator
- No validator on GetAllPersonsQuery, GetPersonByIdQuery, or DeletePersonCommand (D-08)

## Commits

| Hash | Message |
|------|---------|
| 296aecc | feat(02-02): add GetAllPersonsQuery and GetPersonByIdQuery handlers (READ-01, READ-02) |
| c7eb28c | feat(02-02): add Create/Update/Delete commands with validators and handlers (WRITE-01, WRITE-02, WRITE-04) |
| ee06a71 | feat(02-02): add PatchPersonCommand with When()-conditional validator and null-fallback handler (WRITE-03) |

## Deviations from Plan

None — plan executed exactly as written. All six CQRS files, three validators, and two test files match the canonical patterns from PATTERNS.md and RESEARCH.md.

## Known Stubs

None — all handlers are fully wired. The handlers call real repository port methods and real domain entity methods. No hardcoded returns or placeholder data.

## Threat Flags

No new security-relevant surface introduced beyond what was analyzed in the plan's threat model. T-02-04, T-02-05, T-02-06, and T-02-07 were mitigated as planned:
- T-02-04: CreatePersonCommandValidator + UpdatePersonCommandValidator enforce field bounds
- T-02-05: PatchPersonCommandValidator uses When() conditions (Pitfall 5 guard)
- T-02-06: PatchPersonHandler uses dto.Field ?? person.Field + OR guard (Pitfall 6 guard)
- T-02-07: Person properties have private set; direct assignment would not compile

## Self-Check: PASSED

All created files exist and all commits are present in git log.
