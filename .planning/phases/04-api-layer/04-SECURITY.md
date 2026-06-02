# Security Audit — Phase 04: API Layer

**Audit Date:** 2026-06-01
**Auditor:** gsd-security-auditor (claude-sonnet-4-6)
**ASVS Level:** 1
**Block On:** critical (open mitigate threats)
**Plans Audited:** 04-01, 04-02, 04-03
**Result:** SECURED — 10/10 threats closed, 0 open

---

## Threat Verification

| Threat ID | Category | Disposition | Status | Evidence |
|-----------|----------|-------------|--------|----------|
| T-04-01-01 | Information Disclosure | mitigate | CLOSED | `AddProblemDetails()` at Program.cs:13; `UseExceptionHandler()` with no route argument at Program.cs:27; no string passed to `UseExceptionHandler` (grep confirms zero matches for `UseExceptionHandler("`) |
| T-04-01-02 | Tampering | mitigate | CLOSED | `AddMediator(options => { options.PipelineBehaviors = [typeof(ValidationBehavior<,>)] })` at Program.cs:17-21; `ValidationBehavior` source class confirmed at `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs`; no duplicate `AddScoped<ValidationBehavior>` registration found |
| T-04-01-03 | Denial of Service | accept | CLOSED | Accepted risk — see Accepted Risks log below |
| T-04-01-SC | Tampering | mitigate | CLOSED | All four packages pinned at exact versions in `src/PersonsAPI.Api/PersonsAPI.Api.csproj`: `Mediator.SourceGenerator` 3.0.2 (line 17), `Microsoft.AspNetCore.JsonPatch.SystemTextJson` 10.0.8 (line 18), `Microsoft.AspNetCore.OpenApi` 10.0.8 (line 19), `Scalar.AspNetCore` 2.14.14 (line 20); `Mediator.SourceGenerator` absent from Application and Infrastructure csproj files |
| T-04-02-01 | Information Disclosure | mitigate | CLOSED | `PersonNotFoundExceptionHandler`: emits only hard-coded `Type`, `Title`, `Status`, `Detail=notFound.Message`; no stack trace, `InnerException`, or `GetType()` calls found (grep: zero matches); `ValidationExceptionHandler`: emits only hard-coded fields plus `errors` dictionary built from `PropertyName`/`ErrorMessage` — no stack trace or type disclosure found |
| T-04-02-02 | Tampering | mitigate | CLOSED | Patch target is `UpdatePersonDto` (mutable class, 4 nullable fields only — confirmed at `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs:16`); controller constructs `new UpdatePersonDto()` (line 78) before applying patch; `JsonPatchDocument<UpdatePersonDto>` type constraint prevents patching domain entity directly (PersonsController.cs:76) |
| T-04-02-03 | Denial of Service | accept | CLOSED | Accepted risk — see Accepted Risks log below |
| T-04-02-04 | Tampering | mitigate | CLOSED | `ValidationBehavior<,>` registered in `AddMediator` pipeline (Program.cs:20); `ValidationBehavior.cs` present in Application layer; `AddScoped` duplicate registration absent — single pipeline registration via `options.PipelineBehaviors` confirmed |
| T-04-03-01 | Information Disclosure | accept | CLOSED | Accepted risk — see Accepted Risks log below |
| T-04-03-02 | Tampering | mitigate | CLOSED | Mutation tests (`Patch_ReplaceFirstName_Returns200WithUpdatedName`, `Delete_KnownId_Returns204`) each POST a fresh person and use the returned `Id`; `GetAll` asserts `persons.Length >= 3` with named containment (PersonsEndpointsTests.cs:30); shared InMemory store does not break tests on concurrent mutations |
| T-04-03-SC | Tampering | mitigate | CLOSED | `Microsoft.AspNetCore.Mvc.Testing` 10.0.8 pinned in `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj:12`; Microsoft first-party package; no `[ASSUMED]`/`[SUS]`/`[SLOP]` classification per RESEARCH.md Package Legitimacy Audit |

---

## Accepted Risks Log

| Threat ID | Category | Risk | Rationale | Owner |
|-----------|----------|------|-----------|-------|
| T-04-01-03 | Denial of Service | DataSeeder runs once at startup on an InMemory store; no external data persistence; bounded 3-record dataset | Low-value attack surface for a single-process learning project; `DataSeeder.SeedAsync()` is idempotent and does not accept external input; InMemory provider scope-bounded. Documented in 04-01-PLAN.md threat register. | Learning project — accept |
| T-04-02-03 | Denial of Service | `JsonPatchDocument` accepts copy/move operations; no per-document operation count limit enforced | No real database or durable storage; impact bounded to single in-process InMemory store; Microsoft guidance notes this as an optional hardening step. Out of scope for v1. Documented in 04-02-PLAN.md threat register and RESEARCH.md Known Threat Patterns. | Learning project — accept |
| T-04-03-01 | Information Disclosure | `/openapi/v1.json` and `/scalar/v1` are unconditionally exposed (not gated by `IsDevelopment()`) | Mandated by ROADMAP success criterion 4 (DOC-01/DOC-02). Document contains no PII or secrets. Learning project; intentional exposure per RESEARCH.md Open Question 2. Documented in 04-03-PLAN.md threat register. | Learning project — accept |

---

## Unregistered Flags

**None.** No unregistered attack surface was identified in the SUMMARY.md `## Threat Flags` sections or during implementation file review. All threat flags map to registered threat IDs or document known deviations (JsonPatch `ApplyTo` API difference, Mediator `ServiceLifetime.Scoped` addition) that do not introduce new attack surface.

---

## Implementation Notes

### T-04-01-01 — UseExceptionHandler no-arg variant
`UseExceptionHandler()` is called with no arguments (Program.cs:27), which activates the registered `IExceptionHandler` chain without exposing an error route. A route-string variant would expose an internal redirect endpoint; the no-arg variant is correctly absent.

### T-04-02-01 — Hard-coded ProblemDetails fields
Both exception handlers contain only `Type`, `Title`, `Status`, `Detail`, and (for validation) a field-keyed `errors` dictionary. No reflection on exception type, no `exception.StackTrace`, no `exception.InnerException`, and no dynamic string interpolation beyond `notFound.Message` (which is a controlled format string set in `PersonNotFoundException`'s constructor). Stack trace disclosure is structurally absent.

### T-04-02-02 — Patch surface limited to UpdatePersonDto
`[FromBody] JsonPatchDocument<UpdatePersonDto>` at the controller signature constrains the patch target type at compile time. `UpdatePersonDto` exposes exactly four nullable fields that correspond to patchable person attributes. Domain entity invariants are enforced by `Person.UpdateName` / `Person.UpdateDateOfBirth` in the handler layer as a second line of defense, consistent with the mitigation plan.

### T-04-03-02 — Shared InMemory store test isolation
The GetAll assertion (`persons.Length >= 3`) combined with named containment (`Contains(persons, p => p.FirstName == "María")` etc.) is resilient to test execution order. Mutation tests (Patch, Delete) each create independent persons via POST before operating on them, so no test depends on a specific seeded record being in a particular state.

### Deviation from Plan (non-security impact)
The `patchDoc.ApplyTo(dto, ModelState)` overload does not exist in `Microsoft.AspNetCore.JsonPatch.SystemTextJson` 10.0.8. The executor correctly used `patchDoc.ApplyTo(dto, error => ModelState.AddModelError(...))` instead. This achieves identical security behavior: structural patch errors populate ModelState and trigger `ValidationProblem(ModelState)` before dispatching to the handler. The deviation has no security impact.

`options.ServiceLifetime = ServiceLifetime.Scoped` was added to `AddMediator` in Program.cs to prevent DI scope validation failure (Scoped `PersonDbContext` inside a Singleton handler). This is a correctness fix, not a security concern.
