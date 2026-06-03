# Phase 5: Observability - Context

**Gathered:** 2026-06-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Add Serilog structured JSON logging and an ASP.NET Core `/health` endpoint to the running API. All changes land in the Api layer (Program.cs, csproj, integration test factory). Zero changes to Domain, Application, or Infrastructure layers.

</domain>

<decisions>
## Implementation Decisions

### Health Check Endpoint
- **D-01:** Use `AddHealthChecks()` + `MapHealthChecks("/health")` — ASP.NET Core built-in middleware, not a controller. This is infrastructure plumbing, not a business endpoint; the "controllers only" constraint applies to domain/API endpoints, not health infrastructure.
- **D-02:** Response format: JSON default from ASP.NET Core — `{"status":"Healthy"}`, content-type `application/json`. No custom ResponseWriter.
- **D-03:** The `/health` endpoint must be anonymous (no auth required). Cloud Run liveness probe calls it without credentials; if auth is added in the future, `/health` must remain excluded.

### Serilog Configuration
- **D-04:** Use `builder.Host.UseSerilog()` single-phase initialization — no bootstrap logger. Simpler, sufficient for this learning project.
- **D-05:** Configuration is programmatic inline in `Program.cs` — no `Serilog.Settings.Configuration` package. Only 2 new NuGet packages: `Serilog.AspNetCore` and `Serilog.Formatting.Compact`.
- **D-06:** Use `CompactJsonFormatter` from `Serilog.Formatting.Compact` as the formatter — outputs CLEF (Compact Log Event Format) JSON, parseable by Google Cloud Logging without transformation.

### Log Output Behavior
- **D-07:** JSON format always — all environments including Development. No environment-conditional formatting. Success criteria explicitly requires `dotnet run` to produce JSON.
- **D-08:** Minimum log level: `Information` in all environments.
- **D-09:** Filter `Microsoft.EntityFrameworkCore` namespace to `Warning` — EF Core InMemory emits verbose Information-level query events; filtering reduces noise in Cloud Logging without losing meaningful signals.
- **D-10:** Filter `Microsoft.AspNetCore` namespace to `Warning` (carry forward from existing appsettings.json pattern).

### Integration Tests
- **D-11:** Suppress or silence Serilog in integration tests. In `ResetableApiFactory`, override the logging configuration to use `NullLogger` or set the minimum level to `Fatal` so test output stays clean. The 64 existing tests must continue passing after logging changes.

### Claude's Discretion
- Exact `UseSerilog()` lambda code style in Program.cs — Claude can choose idiomatic C# 14 style
- Whether to keep or remove the `"Logging"` section from `appsettings.json` (the Serilog programmatic config supersedes it)
- Exact sink configuration options within `CompactJsonFormatter` (default options are fine)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope and Requirements
- `.planning/ROADMAP.md` — Phase 5 goal, success criteria, and dependency chain (Phase 5 → 6 → 7 → 8)
- `.planning/REQUIREMENTS.md` — OBS-01 (structured JSON logs) and OBS-02 (/health endpoint) definitions; Out of Scope section (no `Serilog.Sinks.GoogleCloudLogging`, no Alpine base image, no liveness/readiness separation)

### Project Constraints
- `.planning/PROJECT.md` — Framework constraints (controllers only, .NET 10, C# 14), tech stack decisions, Key Decisions table
- `CLAUDE.md` — Technology stack table (exact package versions: Serilog.AspNetCore, Serilog.Formatting.Compact), conventions

### Existing Code to Extend
- `src/PersonsAPI.Api/Program.cs` — Current entrypoint; Serilog and AddHealthChecks must be wired here
- `src/PersonsAPI.Api/PersonsAPI.Api.csproj` — Add 2 NuGet PackageReferences here
- `src/PersonsAPI.Api/appsettings.json` — Current logging config (standard Microsoft.Extensions.Logging); may be simplified after Serilog takes over
- `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` — Integration test factory; must suppress Serilog to keep test output clean

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ResetableApiFactory` in `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs`: uses `WebApplicationFactory<Program>` with `Guid.NewGuid()` per instance for DB isolation — the logging override goes here via `builder.ConfigureLogging()` or `builder.UseSerilog()` override

### Established Patterns
- `Program.cs` uses the standard `builder.Services.Add*()` / `app.Map*()` pattern — `AddHealthChecks()` goes in the services block, `MapHealthChecks("/health")` goes in the middleware pipeline block
- `app.MapOpenApi()` and `app.MapScalarApiReference()` show the MapXxx pattern already in use — `MapHealthChecks("/health")` follows the same convention
- All services registered via `builder.Services` — consistent placement for `AddHealthChecks()`

### Integration Points
- `Program.cs` line ordering: Serilog must be added to `builder.Host` before `builder.Build()` is called
- `MapHealthChecks("/health")` must be placed in the middleware pipeline — after `app.UseExceptionHandler()` and before `app.RunAsync()`
- `WebApplicationFactory<Program>` in tests needs a `ConfigureLogging` override to prevent Serilog from writing JSON to test console output

</code_context>

<specifics>
## Specific Ideas

- Cloud Run's liveness probe checks `/health` — HTTP 200 is the pass condition; Cloud Run does not inspect the response body
- Google Cloud Logging ingests stdout lines that are valid JSON — `CompactJsonFormatter` CLEF format satisfies this without a GCP-native sink
- The `{"status":"Healthy"}` JSON body from `MapHealthChecks` satisfies OBS-02's acceptance criteria; future liveness/readiness split (OBS-03/OBS-04, deferred to v3) can extend `AddHealthChecks()` without replacing it

</specifics>

<deferred>
## Deferred Ideas

- **Separate liveness/readiness endpoints** (`/health/live`, `/health/ready`) — deferred to v3 as OBS-03/OBS-04 in REQUIREMENTS.md; the `AddHealthChecks()` approach chosen here supports this extension without refactoring
- **Serilog severity mapping to Cloud Logging** (INFO/WARNING/ERROR icons in Console) — deferred to v3 as OBS-03; stdout JSON without `Serilog.Sinks.GoogleCloudLogging` is explicitly out of scope per REQUIREMENTS.md
- **Bootstrap logger (two-phase Serilog init)** — noted as the production-grade pattern; deferred as it adds complexity beyond this learning scope

</deferred>

---

*Phase: 5-Observability*
*Context gathered: 2026-06-02*
