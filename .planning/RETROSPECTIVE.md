# Retrospective: PersonsAPI

Living retrospective — updated at each milestone close.

---

## Milestone: v1.0 — PersonsAPI MVP

**Shipped:** 2026-06-02
**Phases:** 4 | **Plans:** 11 | **Tasks:** ~20

### What Was Built

- Zero-dependency Domain library: `Person` entity with private setters, computed `Age`, static factory `Person.Create()`, and invariant-validating update methods. `DomainException` as the typed error contract. 32 passing tests.
- Application layer: IPersonRepository port, CQRS commands/queries (6 handlers), DTOs, and a `ValidationBehavior<,>` pipeline behavior intercepting all Mediator dispatches. 15 passing tests.
- Infrastructure layer: `PersonDbContext` (EF Core InMemory), `PersonEntityConfiguration` (builder.Ignore(Age)), `PersonRepository` implementing IPersonRepository, `DataSeeder` (idempotent, scoped startup). 5 passing tests.
- API layer: `PersonsController` (6 endpoints, RFC 9457 Problem Details via IExceptionHandler, OpenAPI 3.1 + Scalar interactive UI, `application/json-patch+json` PATCH). 12 integration tests covering all endpoints + error shapes + documentation endpoints.

**Final test count:** 64/64 passing across 4 test projects.

### What Worked

- **Layer-by-layer build order** — Domain → Application → Infrastructure → Api is strictly dependency-safe. Each layer compiled and tested independently before the next was started. No circular reference surprises.
- **Rich domain model enforcement** — Keeping business logic (Age, invariants) in `Person` rather than handlers prevented the typical "service as god object" drift. The `Person.Create()` factory as the only construction path was easy to test and reason about.
- **RESEARCH.md + CONTEXT.md artifacts** — Having pitfalls (Mediator Pitfall 4: SourceGenerator scope, Pitfall 5: SeedAsync before RunAsync, etc.) documented before execution eliminated entire classes of runtime surprises.
- **Code review → fix cycle** — Running `/gsd-code-review --fix` surfaced real issues (WebApplicationFactory shared state, ValidationExceptionHandler AOT serialization) and applied all fixes in one pass. 64 tests passed after fixes.

### What Was Inefficient

- **UpdatePersonDto record → class migration** — The plan noted the fix was needed but the PatchPersonCommandValidatorTests test file wasn't in the `files_modified` list. The call-site updates were a mechanical consequence of the type change and should have been pre-identified. Minor rework.
- **Mediator ServiceLifetime default** — The Mediator.SourceGenerator 3.0.2 registers handlers as Singleton by default, which conflicts with Scoped DbContext. This DI validation error only surfaced at integration test time, not during development. RESEARCH.md should note this as a pitfall for future phases using Mediator with EF Core.
- **JsonPatch content-type discovery** — The plan specified `application/json` but the STJ package only accepts `application/json-patch+json`. This required a test fix iteration. The RESEARCH.md had a note about the STJ variant but didn't explicitly document the required content type.

### Patterns Established

- `ResetableApiFactory` (Guid-named InMemory DB per WebApplicationFactory fixture) is the standard pattern for integration tests in this project. Prevents cross-test state contamination.
- `JsonSerializer.SerializeToElement(errors)` is the correct way to set complex objects in `ProblemDetails.Extensions` for AOT/source-generation compatibility.
- `options.ServiceLifetime = ServiceLifetime.Scoped` is required on `AddMediator()` when handlers inject scoped services (EF Core DbContext).
- PATCH requests require `Content-Type: application/json-patch+json` with the STJ-based JsonPatch package.

### Key Lessons

1. **DI lifetime mismatch is a runtime error, not a compile error** — Add `ServiceLifetime.Scoped` to `AddMediator()` whenever handlers depend on scoped services. Will catch this in RESEARCH.md for future phases.
2. **Integration tests need isolated state by default** — Any test project using `WebApplicationFactory<T>` + EF InMemory should start from `ResetableApiFactory`, not the bare factory.
3. **STJ `ProblemDetails.Extensions` serialization is fragile** — Always pre-serialize complex extension values to `JsonElement`. The default `Dictionary<string,string[]>` as `object?` works in reflection mode but breaks in AOT. Use `JsonSerializer.SerializeToElement()`.
4. **Phase-level planning overhead is justified** — The CONTEXT.md + RESEARCH.md + PATTERNS.md artifacts added upfront time but paid off during execution: the controller pattern, exception handler shapes, and composition root wiring were already fully specified before a line of code was written.

### Cost Observations

- 4 phases executed sequentially over ~5 days (2026-05-27 → 2026-06-02)
- 96 git commits; 25 `feat()` commits
- 1,435 LOC production C#; 1,251 LOC test C#
- Model: claude-sonnet-4-6 (executor + verifier + reviewer)

---

## Cross-Milestone Trends

| Metric | v1.0 |
|--------|------|
| Phases | 4 |
| Plans | 11 |
| Tests | 64 |
| Production LOC | 1,435 |
| Test LOC | 1,251 |
| Days | 6 |
| Code review critical findings | 2 (both fixed) |
| UAT pass rate | 11/11 (100%) |
