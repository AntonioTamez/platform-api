# Milestones

## v2.0 Cloud Deployment (Shipped: 2026-06-06)

**Phases completed:** 4 phases (5-8), 6 plans, ~14 tasks
**Files changed:** 58 | **Net additions:** +10,142 / -1,391 lines
**Timeline:** 2026-06-02 → 2026-06-05 (4 days) | **Commits:** 67
**Git range:** `f4fcbaa` (milestone start) → `2d74a48` (phase-08 complete)

**Key accomplishments:**

- Serilog CLEF JSON logging on stdout and anonymous `/health` endpoint — structured logs compatible with Google Cloud Logging, health probe ready for Cloud Run liveness check
- Multi-stage Dockerfile (sdk:10.0 build → aspnet:10.0 final) with restore-first layer caching, non-root user, and HTTP-only pipeline (UseHttpsRedirection removed)
- `docker-compose.yml` enabling one-command local container parity via `docker compose up` — all endpoints at port 8080 with JSON logs
- DEPLOYMENT.md 374-line Cloud Run runbook covering full GCP setup (project, Artifact Registry, Service Account, deploy, verify); `key.json` gitignored
- PersonsAPI deployed and publicly reachable on Google Cloud Run (us-central1) — all 4 success criteria confirmed: `/health` → 200, `/api/persons` → 3 persons, no crash loop, JSON logs in Cloud Logging
- Three-job GitHub Actions CI/CD pipeline (build-and-test → push-image → deploy) — every push to `master` automatically builds, tests all 64 tests, pushes `:latest` to Artifact Registry, and deploys to Cloud Run

---

## v1.0 PersonsAPI MVP (Shipped: 2026-06-02)

**Phases completed:** 4 phases, 11 plans, 20 tasks

**Key accomplishments:**

- Zero-dependency PersonsAPI.Domain class library with sealed DomainException error contract and xUnit test harness verified green
- Person rich domain entity implemented test-first: static factory with private setters, month/day-aware computed Age, and invariant-validating update methods — 32 tests green, zero EF references in Domain project
- PersonsAPI.Application class library with IPersonRepository secondary port, PersonNotFoundException, four DTO records with static FromDomain factory, and xUnit test harness proving computed-Age mapping.
- Six CQRS handlers (two queries, four commands) plus three FluentValidation validators and twelve passing unit tests closing READ-01, READ-02, WRITE-01, WRITE-02, WRITE-03, and WRITE-04 at the Application layer level.
- FluentValidation pipeline behavior implementing Mediator 3.x's exact IPipelineBehavior<TMessage,TResponse> signature with D-10 short-circuit, plus AddApplication() DI extension that registers validators and the ValidationBehavior open generic while deferring AddMediator() to Phase 4 (Open Question 1 fallback).
- EF Core InMemory secondary adapter with PersonDbContext, PersonEntityConfiguration (builder.Ignore Age), PersonRepository implementing IPersonRepository, and AddInfrastructure DI extension.
- Static `DataSeeder` class with idempotent `SeedAsync(this IServiceProvider)` extension seeding exactly 3 `Person` records via `Person.Create()` inside a dedicated DI scope.
- xUnit test project with 5 isolated InMemory CRUD tests validating PersonRepository against the IPersonRepository contract end-to-end.
- The plan listed only `UpdatePersonDto.cs` as the file to modify, but the test file `tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs` used the positional constructor form and required update. This was a necessary consequence of the type change — no behavioral deviation.
- PersonNotFoundExceptionHandler
- `application/json-patch+json` (RFC 6902 standard) is required, not `application/json`. The STJ JsonPatch package registers a formatter for `application/json-patch+json`. The plan said to use `application/json` but that returns 415 UnsupportedMediaType — the STJ formatter doesn't accept plain JSON.

---
