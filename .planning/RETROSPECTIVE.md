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

## Milestone: v2.0 — Cloud Deployment

**Shipped:** 2026-06-05
**Phases:** 4 (5-8) | **Plans:** 6 | **Timeline:** 4 days (2026-06-02 → 2026-06-05) | **Commits:** 67

### What Was Built

- **Phase 5 (Observability):** Serilog CLEF JSON stdout logging via `builder.Host.UseSerilog` + `CompactJsonFormatter`, anonymous `/health` endpoint returning `{"status":"Healthy"}` (application/json). EF Core and AspNetCore namespaces filtered to Warning. Serilog suppressed in integration tests via `NullLoggerFactory` replacement.
- **Phase 6 (Containerization):** Multi-stage `Dockerfile` (sdk:10.0 build → aspnet:10.0 final, restore-first layer caching, curl for healthcheck, non-root `app` user, port 8080). `.dockerignore` excluding .git, .planning, .claude, tests. `docker-compose.yml` with healthcheck probe.
- **Phase 7 (Cloud Run):** `DEPLOYMENT.md` 374-line runbook covering full GCP stack (project, Artifact Registry, SA key, deploy, verify). Live deployment to Cloud Run `persons-api` service (us-central1, scale-to-zero, 512MiB) — all 4 success criteria confirmed in production.
- **Phase 8 (CI/CD):** Three-job GitHub Actions pipeline (`build-and-test` → `push-image` → `deploy`) triggered on push to `master`. SA key auth via `google-github-actions/auth@v2`. Test gate via `needs:` dependency chain. `DEPLOYMENT.md` Step 9 documents secret setup.

### What Worked

- **Inside-out phase ordering** — Observability first (local), then containerize (local), then cloud manual, then cloud automated. Each phase's output was a precondition for the next, making integration problems impossible to ignore.
- **DEPLOYMENT.md runbook as living doc** — Authoring the runbook (Phase 7 Plan 01) before executing it (Phase 7 Plan 02) let the execution be mechanical. Additions from Phase 8 (Step 9 for GitHub secrets) extended naturally.
- **Code review catching critical issues before verification** — `branches: [main]` instead of `branches: [master]` was a silent failure that would never have triggered. CR-01 caught it in code review, fixed before human UAT.
- **Minimal scope** — Only 2 new NuGet packages (Serilog.AspNetCore + Serilog.Formatting.Compact). Zero changes to Domain, Application, or Infrastructure layers. The infra change was entirely contained in Program.cs and new deployment files.

### What Was Inefficient

- **Serilog test suppression discovery** — Plan specified `builder.UseSerilog(MinimumLevel.Fatal)` on `IWebHostBuilder`, but that extension isn't available in Serilog.AspNetCore 9 on `IWebHostBuilder` (only `IHostBuilder`). Two iterations to land on `NullLoggerFactory` replacement. Could have been caught in RESEARCH.md with a version check.
- **REQUIREMENTS.md traceability table not updated** — OBS-01, OBS-02, and CLOUD-01 remained "Pending" in the traceability table throughout v2.0 even after being verified. This is a process gap: traceability rows should be updated at phase completion, not only at milestone close.
- **Phase 6 Plan 02 partial** — The SUMMARY recorded "Task 2 pending human verification" but the phase was marked complete. Human UAT was documented separately. A clearer convention for when a phase is "complete" vs "pending human" would help.

### Patterns Established

- `ASPNETCORE_HTTP_PORTS=8080` is the .NET 8+ canonical port config via ENV in Dockerfile (not `ASPNETCORE_URLS`).
- Restore-first Dockerfile layer caching: `COPY *.sln *.csproj -> RUN dotnet restore -> COPY src/ -> dotnet publish --no-restore`.
- `UseHttpsRedirection` must be removed unconditionally in containerized APIs — Cloud Run handles TLS termination.
- GitHub Actions `needs:` chain as the test gate: downstream jobs don't start if upstream fails — no explicit `if: success()` needed.
- SA key must be minified (`cat key.json | tr -d '\r\n'`) before storing as GitHub secret — `google-github-actions/auth@v2` rejects multi-line JSON.

### Key Lessons

1. **Check extension method signatures against package version** — `UseSerilog` on `IWebHostBuilder` was removed in Serilog.AspNetCore 8+. The RESEARCH.md should note which host builder interfaces each major extension attaches to.
2. **Code review before human UAT is mandatory** — The branch name bug (`main` vs `master`) was invisible to static analysis and would have silently broken every push. Code review as a gate before human verification caught it.
3. **Traceability table hygiene** — Update REQUIREMENTS.md traceability rows at phase completion, not milestone close. Stale "Pending" rows cause confusion during milestone audit.
4. **Scope discipline pays off** — Touching only Program.cs, appsettings.json, and new infra files kept the Domain/Application/Infrastructure layers green throughout. The test suite was the proof — 64/64 passing after every phase.

### Cost Observations

- 4 phases over 4 days (2026-06-02 → 2026-06-05)
- 67 git commits since v1.0
- 58 files changed (+10,142 / -1,391 lines net)
- Model: claude-sonnet-4-6
- No domain/application/infrastructure code changes — all net-new infra files

---

## Cross-Milestone Trends

| Metric | v1.0 | v2.0 |
|--------|------|------|
| Phases | 4 | 4 |
| Plans | 11 | 6 |
| Tests | 64 | 64 (unchanged) |
| Production LOC | 1,435 | ~1,609 (+174 App layer) |
| Days | 6 | 4 |
| Commits | 96 | 67 |
| Code review critical findings | 2 (both fixed) | 2 (both fixed) |
| UAT pass rate | 100% | 100% |
| New NuGet packages | 10+ | 2 |
| Infra scope | None | Docker + GCP + GitHub Actions |
