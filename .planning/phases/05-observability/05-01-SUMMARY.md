---
phase: 05-observability
plan: 01
subsystem: infra
tags: [serilog, logging, healthcheck, aspnetcore, dotnet, clef, observability]

requires:
  - phase: 04-api-layer
    provides: "Program.cs entrypoint, controllers, WebApplicationFactory<Program> test anchor"

provides:
  - Serilog.AspNetCore 9.0.0 + Serilog.Formatting.Compact 3.0.0 wired via builder.Host.UseSerilog
  - CLEF JSON stdout logging (CompactJsonFormatter) with EF Core + AspNetCore namespaces filtered to Warning
  - GET /health endpoint returning HTTP 200 + application/json body {"status":"Healthy"} (OBS-02)
  - Integration test factory suppresses Serilog via ConfigureLogging/ClearProviders (D-11)

affects: [06-containerization, 07-cloud-run-deployment]

tech-stack:
  added:
    - Serilog.AspNetCore 9.0.0
    - Serilog.Formatting.Compact 3.0.0
  patterns:
    - builder.Host.UseSerilog single-phase initialization before builder.Build()
    - HealthCheckOptions.ResponseWriter for JSON-format health response
    - ConfigureLogging.ClearProviders in test factory to silence structured loggers

key-files:
  created: []
  modified:
    - src/PersonsAPI.Api/PersonsAPI.Api.csproj
    - src/PersonsAPI.Api/Program.cs
    - src/PersonsAPI.Api/appsettings.json
    - tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs

key-decisions:
  - "D-04: Single-phase Serilog init via builder.Host.UseSerilog — no bootstrap logger"
  - "D-05: Programmatic inline config; no Serilog.Settings.Configuration package"
  - "D-06/D-07: CompactJsonFormatter always — all environments including Development"
  - "D-09/D-10: EF Core and AspNetCore namespaces filtered to Warning"
  - "D-11: ConfigureLogging.ClearProviders used in test factory (UseSerilog not available on IWebHostBuilder in v9)"
  - "D-02 deviation: ASP.NET Core default /health response is text/plain; added minimal HealthCheckOptions.ResponseWriter for JSON"

patterns-established:
  - "Serilog init: builder.Host.UseSerilog immediately after WebApplication.CreateBuilder, before any builder.Services.Add*"
  - "Health endpoint: app.MapHealthChecks with HealthCheckOptions.ResponseWriter for JSON; placed after MapScalarApiReference"
  - "Test Serilog suppression: replace ILoggerFactory with NullLoggerFactory.Instance via ConfigureServices (ClearProviders is ineffective against Serilog's ILoggerFactory singleton)"

requirements-completed: [OBS-01, OBS-02]

duration: 15min
completed: 2026-06-03
---

# Phase 5: Observability — Plan 01 Summary

**Serilog CLEF JSON logging on stdout and anonymous /health endpoint delivering `{"status":"Healthy"}` — API ready for Google Cloud Run observability requirements**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-03T03:20:00Z
- **Completed:** 2026-06-03T03:35:00Z
- **Tasks:** 3
- **Files modified:** 4

## Accomplishments

- API emits CLEF-format JSON log lines on stdout (OBS-01); parseable by Google Cloud Logging without transformation
- `GET /health` returns HTTP 200 with `Content-Type: application/json` and body `{"status":"Healthy"}` (OBS-02)
- EF Core and AspNetCore namespaces filtered to Warning — no query noise in production logs
- All 64 pre-existing tests still pass; test console output free of Serilog JSON lines

## Task Commits

1. **Task 1: Add Serilog packages and wire UseSerilog in Program.cs** — `39bedbc` (feat)
2. **Task 2: Add ASP.NET Core health checks and map /health** — `0ae2f98` (feat)
3. **Task 3: Suppress Serilog in integration tests** — `53d59b9` (test)

## Files Created/Modified

- `src/PersonsAPI.Api/PersonsAPI.Api.csproj` — Added Serilog.AspNetCore 9.0.0 and Serilog.Formatting.Compact 3.0.0 PackageReferences
- `src/PersonsAPI.Api/Program.cs` — Serilog host config, AddHealthChecks, MapHealthChecks with JSON ResponseWriter, using Microsoft.AspNetCore.Diagnostics.HealthChecks
- `src/PersonsAPI.Api/appsettings.json` — Removed dead `"Logging"` section; now `{ "AllowedHosts": "*" }` only
- `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` — ConfigureLogging override to suppress all log providers during integration tests

## Decisions Made

- **D-05 programmatic config:** No `Serilog.Settings.Configuration` package added — Serilog is configured 100% inline in Program.cs
- **D-11 test suppression via ConfigureLogging:** `UseSerilog()` extension on `IWebHostBuilder` was removed in Serilog.AspNetCore 8+; only available on `IHostBuilder`. Used `builder.ConfigureLogging(l => l.ClearProviders())` instead — functionally equivalent, silences all log providers including Serilog in test runs.

## Deviations from Plan

### Auto-fixed Issues

**1. D-02 — /health JSON response requires explicit ResponseWriter**

- **Found during:** Task 2 smoke test
- **Issue:** Plan stated "D-02 mandates the default JSON response" and "no HealthCheckOptions lambda". ASP.NET Core's actual default `MapHealthChecks` response is `text/plain` with body "Healthy" — not JSON. Smoke test confirmed `Content-Type: text/plain`.
- **Fix:** Added `HealthCheckOptions` with a minimal `ResponseWriter` lambda that sets `Content-Type: application/json; charset=utf-8` and writes `{"status":"Healthy"}`. No external package required.
- **Files modified:** `src/PersonsAPI.Api/Program.cs`
- **Verification:** Smoke test confirmed `Status: 200`, `Content-Type: application/json; charset=utf-8`, `Body: {"status":"Healthy"}`
- **Committed in:** `0ae2f98`

**2. D-11 — UseSerilog unavailable on IWebHostBuilder in Serilog.AspNetCore 9**

- **Found during:** Task 3 first attempt (build error)
- **Issue:** PATTERNS.md Option A prescribed `builder.UseSerilog(...)` inside `ConfigureWebHost(IWebHostBuilder builder)`. `SerilogHostBuilderExtensions.UseSerilog` requires `IHostBuilder`, not `IWebHostBuilder`. CS1929 compile error.
- **Fix:** Used `ConfigureServices` to remove all `ILoggerFactory` descriptors and replace with `NullLoggerFactory.Instance`. `ConfigureLogging.ClearProviders` was tried first (commit `53d59b9`) but confirmed ineffective (CR-01 in code review) — Serilog registers a full `ILoggerFactory` singleton that bypasses `ILoggerProvider`. Final fix: `ff26c8c`.
- **Files modified:** `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs`
- **Verification:** `dotnet test` exits 0, 64 tests pass, 0 `{"@t":` CLEF JSON lines in test output
- **Committed in:** `ff26c8c` (CR-01 fix)

---

**Total deviations:** 2 auto-fixed (1 incorrect default assumption in plan, 1 API incompatibility in Serilog v9)
**Impact on plan:** Both fixes necessary for correctness. No scope creep. Must-haves satisfied.

## Issues Encountered

- Pre-existing CS0436 warnings from `Mediator.SourceGenerator` in the Api.Tests project (type conflicts between generated code and imported assembly) — pre-existing, unrelated to this phase, all tests pass.

## Smoke Test Results

```
GET http://localhost:5099/health
→ Status: 200
→ Content-Type: application/json; charset=utf-8
→ Body: {"status":"Healthy"}
```

Sample CLEF JSON log line from `dotnet run`:
```json
{"@t":"2026-06-03T03:22:25.2474047Z","@mt":"Now listening on: {address}","address":"http://localhost:5099","EventId":{"Id":14,"Name":"ListeningOnAddress"},"SourceContext":"Microsoft.Hosting.Lifetime"}
```

## Test Results

```
PersonsAPI.Domain.Tests:       Passed: 32 / 32
PersonsAPI.Application.Tests:  Passed: 15 / 15
PersonsAPI.Infrastructure.Tests: Passed: 5 / 5
PersonsAPI.Api.Tests:          Passed: 12 / 12
Total:                         64 / 64  ✓
```

No `{"@t":` CLEF JSON lines in `dotnet test` output — Serilog suppression confirmed.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- API emits JSON logs compatible with Google Cloud Logging ingestion — Phase 6 (Containerization) can proceed
- `/health` endpoint available for Cloud Run liveness probe configuration
- Zero changes to Domain, Application, or Infrastructure layers — clean upgrade path

---
*Phase: 05-observability*
*Completed: 2026-06-03*
