# Phase 2: Application Layer - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the Application layer use-case contracts: `IPersonRepository` port interface in `Application/Ports/`, six CQRS command and query records each with a handler, three request DTOs, one response DTO with static mapping factory, and a `ValidationBehavior<TRequest, TResponse>` pipeline behavior wired through FluentValidation. Nothing is persisted or served over HTTP here — this layer purely defines what operations exist, validates their inputs, and delegates domain work to the entity.

</domain>

<decisions>
## Implementation Decisions

### PATCH Command Design
- **D-01:** The controller (Phase 4) receives `JsonPatchDocument<UpdatePersonDto>`, applies it to a fresh `UpdatePersonDto`, then dispatches `PatchPersonCommand(int Id, UpdatePersonDto Dto)` to the mediator. The Application layer stays free of `Microsoft.AspNetCore.JsonPatch` — no ASP.NET Core types bleed past the controller boundary.
- **D-02:** `UpdatePersonDto` has four nullable fields: `string? FirstName`, `string? PaternalLastName`, `string? MaternalLastName`, `DateOnly? DateOfBirth`. The `PatchPersonHandler` applies only non-null fields by calling `Person.UpdateName()` or `Person.UpdateDateOfBirth()` on the fields that are populated. CLAUDE.md's C# 14 null-conditional assignment (`??=`) fits naturally here.

### Not-Found Contract
- **D-03:** `IPersonRepository.GetByIdAsync(int id)` returns `Person?` (null when not found). The Application layer — not the repository — decides what "not found" means for each use case.
- **D-04:** `PersonNotFoundException` lives in `PersonsAPI.Application/Exceptions/PersonNotFoundException.cs`. Handlers throw it when `GetByIdAsync` returns null. The API layer (Phase 4) catches `PersonNotFoundException` and maps it to 404 Problem Details. This is an application-layer concern, not a domain invariant.

### DTO Design
- **D-05:** Three request types: `CreatePersonRequest` (all four fields required), `UpdatePersonRequest` (all four fields required, for PUT), `UpdatePersonDto` (all four fields nullable, for PATCH after patch application). Distinct types express distinct intent clearly; validators can be field-exact without conditionals.
- **D-06:** One response type: `PersonResponse { int Id, string FirstName, string PaternalLastName, string MaternalLastName, DateOnly DateOfBirth, int Age }`. Age is read from the domain entity's computed property and surfaced in every response — demonstrating the computed-property pattern is an explicit project goal.
- **D-07:** Mapping lives in a static factory: `PersonResponse.FromDomain(Person p)`. No AutoMapper. Consistent with CLAUDE.md's manual mapping preference and keeps the mapping visible and debuggable. Lives in `PersonsAPI.Application/DTOs/PersonResponse.cs`.

### Validator Scope
- **D-08:** Validators for write commands only: `CreatePersonCommandValidator`, `UpdatePersonCommandValidator`, `PatchPersonCommandValidator`. Read queries (`GetAllPersonsQuery`, `GetPersonByIdQuery`) carry no user-supplied body data that needs validation — route-param validation is handled by model binding and is the controller's concern.
- **D-09:** `CreatePersonCommandValidator` and `UpdatePersonCommandValidator` mirror the domain invariants: name fields NotEmpty + length 2–100, `DateOfBirth` not in the future. This intentional duplication is correct layering — Application validates input for field-level 400 detail (ERR-02), Domain enforces invariants as the second line of defense.
- **D-10:** `ValidationBehavior<TRequest, TResponse>` short-circuits gracefully (no error) when no `IValidator<T>` is registered for the request type. Validators are registered via `AddValidatorsFromAssembly(typeof(IApplicationMarker).Assembly)` in `AddApplication()`.

### Claude's Discretion
- Folder structure within `PersonsAPI.Application` (e.g., `Commands/`, `Queries/`, `DTOs/`, `Ports/`, `Behaviors/`, `Exceptions/`) — Claude chooses idiomatic organization.
- `IApplicationMarker` interface or equivalent for assembly scanning — Claude selects the cleanest approach.
- Whether commands and queries are records or classes — records are strongly preferred for CQRS in C# 14 (immutable, value equality).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Requirements
- `.planning/REQUIREMENTS.md` §Read Operations (READ-01, READ-02), §Write Operations (WRITE-01–04), §Validation (VAL-01), §Infrastructure (INFRA-03) — 8 requirements for this phase
- `.planning/PROJECT.md` §Constraints — all-English code, rich models, no Minimal API, no AutoMapper, no MediatR 13+
- `.planning/PROJECT.md` §What NOT to Use — explicit list of prohibited libraries and patterns

### Architecture & Phase Context
- `.planning/ROADMAP.md` §Phase 2 — goal, success criteria (4 observable criteria this phase must satisfy)
- `.planning/phases/01-domain-layer/01-CONTEXT.md` — Phase 1 decisions: D-01 (int ID), D-03 (DomainException / no Result<T>), D-06 (plain strings), D-07 (DateOnly), D-13/D-14/D-15 (Person.Create, protected ctor, update methods)

### Prior Phase Output
- `src/PersonsAPI.Domain/Entities/Person.cs` — entity interface: what Create() expects, what UpdateName/UpdateDateOfBirth accept, what properties are readable
- `src/PersonsAPI.Domain/Exceptions/DomainException.cs` — exception type handlers must let bubble to API layer

### Technology
- CLAUDE.md §Recommended Stack — Mediator 3.0.2 (martinothamar source-generator, MIT) is the CQRS dispatcher; MediatR 12.5 is the acceptable fallback but frozen. FluentValidation 12.1.1 with `FluentValidation.DependencyInjectionExtensions` for `AddValidatorsFromAssembly()`.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/PersonsAPI.Domain/Entities/Person.cs` — `Person.Create()` factory, `UpdateName()`, `UpdateDateOfBirth()`, all computed `Age`. Handlers call these methods; never set properties directly.
- `src/PersonsAPI.Domain/Exceptions/DomainException.cs` — Application handlers let this bubble; `PersonNotFoundException` (new) is separate.

### Established Patterns
- **Exceptions as error contract (D-03 from Phase 1):** No Result<T>, no Option<T>. Throw typed exceptions; catch by type at the API boundary.
- **Plain records for data transfer:** Phase 1 used no value objects. Continue with record types for commands/queries/DTOs.
- **Project naming:** `PersonsAPI.{Layer}` in `src/PersonsAPI.{Layer}/` — Application project goes at `src/PersonsAPI.Application/`.

### Integration Points
- `PersonsAPI.Application` references `PersonsAPI.Domain` (and Mediator.Abstractions, FluentValidation). No reference to Infrastructure or ASP.NET Core packages.
- Phase 3 (Infrastructure): `PersonRepository : IPersonRepository` implements the port defined here.
- Phase 4 (API): Controller dispatches commands/queries defined here; catches `PersonNotFoundException` for 404 mapping; applies JSON Patch before dispatching `PatchPersonCommand`.

</code_context>

<specifics>
## Specific Ideas

- `AddApplication(this IServiceCollection services)` extension method in `PersonsAPI.Application` is the single registration entry point for handlers, validators, and the pipeline behavior.
- `PatchPersonHandler` should call `UpdateName()` only when at least one name field is non-null, and `UpdateDateOfBirth()` only when `DateOfBirth` is non-null — avoid calling update methods with partial data.
- `PersonResponse.FromDomain(Person p)` is a static factory on the record itself — one line per field, no external mapper type.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 2-Application Layer*
*Context gathered: 2026-05-29*
