---
phase: 05-observability
verified: 2026-06-03T04:00:00Z
status: complete
score: 7/7 must-haves verified
overrides_applied: 0
human_verified: 2026-06-05
human_verification:
  - test: "Confirm `dotnet run` stdout contains CLEF JSON lines"
    expected: "Each log line on stdout is a valid JSON object with `@t`, `@mt` fields (CLEF format)"
    result: "CONFIRMED by developer — 2026-06-05"
---

# Phase 5: Observability Verification Report

**Phase Goal:** The running API emits structured JSON logs and responds to health checks
**Verified:** 2026-06-03T04:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | `dotnet run` produces stdout lines that are valid JSON (CLEF format, one object per line) — OBS-01 | VERIFIED | Confirmed by developer on 2026-06-05: `dotnet run` stdout produces valid CLEF JSON objects with `@t`, `@mt` fields. |
| 2 | `GET /health` returns HTTP 200 with content-type `application/json` and body `{"status":"Healthy"}` — OBS-02 | VERIFIED | `app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = ... })` at line 42-49 of Program.cs. ResponseWriter sets `ContentType = "application/json; charset=utf-8"` and writes `{"status":"Healthy"}`. No `RequireAuthorization` present. |
| 3 | `/health` requires no authentication — anonymous access works directly | VERIFIED | No `RequireAuthorization` call on `MapHealthChecks`. No auth middleware in the pipeline. D-03 comment present on the MapHealthChecks call. |
| 4 | All 64 existing tests still pass after Serilog and health endpoint are added | VERIFIED | `dotnet test --nologo --verbosity quiet` result: Domain 32/32, Application 15/15, Infrastructure 5/5, Api 12/12. Total: 64/64 passed, 0 failed, 0 skipped. Confirmed live. |
| 5 | EF Core query noise filtered to Warning level (D-09) | VERIFIED | Program.cs line 18: `.MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)` present in the `UseSerilog` lambda. |
| 6 | Microsoft.AspNetCore namespace logs filtered to Warning level (D-10) | VERIFIED | Program.cs line 17: `.MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)` present in the `UseSerilog` lambda. |
| 7 | Integration test output is not polluted by Serilog JSON lines (D-11) | VERIFIED | `dotnet test` output contains zero lines matching `{"@t":`. ResetableApiFactory replaces `ILoggerFactory` with `NullLoggerFactory.Instance` — confirmed effective. |

**Score:** 7/7 truths verified (human-confirmed 2026-06-05)

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `src/PersonsAPI.Api/PersonsAPI.Api.csproj` | Serilog package references | VERIFIED | Contains `<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />` (line 21) and `<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />` (line 22). |
| `src/PersonsAPI.Api/Program.cs` | Serilog host config + AddHealthChecks + MapHealthChecks | VERIFIED | `builder.Host.UseSerilog` (line 15), `CompactJsonFormatter` (line 19), `builder.Services.AddHealthChecks()` (line 33), `app.MapHealthChecks("/health", ...)` (line 42). `public partial class Program { }` preserved (line 57). |
| `src/PersonsAPI.Api/appsettings.json` | No dead Logging section; only AllowedHosts | VERIFIED | File content: `{ "AllowedHosts": "*" }`. No `"Logging"` key present. Valid JSON. |
| `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` | Serilog suppression in tests | VERIFIED (via alternate implementation) | Does NOT contain `UseSerilog` (PLAN artifact spec miss). Instead replaces `ILoggerFactory` with `NullLoggerFactory.Instance`. This achieves the same behavioral outcome — confirmed by zero `{"@t":` lines in `dotnet test` output. See deviation note below. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Program.cs` | Serilog pipeline | `builder.Host.UseSerilog` before `builder.Build()` | VERIFIED | `builder.Host.UseSerilog(...)` at line 15; `builder.Build()` at line 35. Order is correct. Pattern `builder\.Host\.UseSerilog` found. |
| `Program.cs` | `/health` endpoint | `app.MapHealthChecks("/health")` before `await app.Services.SeedAsync()` | VERIFIED | `MapHealthChecks` at line 42; `SeedAsync()` at line 51. Order is correct. Pattern `MapHealthChecks\("/health"\)` found. |
| `ResetableApiFactory.cs` | Suppressed logging pipeline | PLAN specified `builder.UseSerilog with MinimumLevel.Fatal`; actual uses `NullLoggerFactory.Instance` | WARNING — behavioral goal met via alternate mechanism | `UseSerilog` pattern NOT found. Actual implementation removes all `ILoggerFactory` descriptors and registers `NullLoggerFactory.Instance`. Behavioral outcome identical: zero Serilog output in tests. This is a documented deviation in SUMMARY.md (D-11). The API incompatibility (`UseSerilog` not available on `IWebHostBuilder` in Serilog.AspNetCore 9) makes this the correct resolution. |

### Data-Flow Trace (Level 4)

Not applicable. No components rendering dynamic data were introduced. The `/health` endpoint emits a static string, not user/DB data. Serilog writes to stdout (external sink), not to a UI component.

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Build succeeds with 0 errors | `dotnet build --nologo` | 0 errors, 16 pre-existing CS0436 warnings from Mediator.SourceGenerator | PASS |
| 64 tests pass | `dotnet test --nologo --verbosity quiet` | 64/64 passed, 0 failed, 0 skipped | PASS |
| Test output free of CLEF JSON | `dotnet test` output grep for `{"@t":` | 0 matches | PASS |
| CLEF JSON runtime output (OBS-01) | `dotnet run` stdout | CLEF JSON confirmed by developer on 2026-06-05 | PASS |

### Probe Execution

No probe scripts declared in PLAN or found at `scripts/*/tests/probe-*.sh`. Step skipped.

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| OBS-01 | 05-01-PLAN.md | Developer can see structured JSON logs from the running API in Google Cloud Logging | VERIFIED | `builder.Host.UseSerilog` + `CompactJsonFormatter` wired. CLEF JSON stdout confirmed by developer run on 2026-06-05. |
| OBS-02 | 05-01-PLAN.md | `/health` endpoint returns HTTP 200 OK and enables Cloud Run liveness probe | VERIFIED | `app.MapHealthChecks("/health", HealthCheckOptions)` with `ResponseWriter` writing `{"status":"Healthy"}` and `application/json` content type. Anonymous access confirmed (no auth). |

**Note:** REQUIREMENTS.md traceability table still shows OBS-01 and OBS-02 as "Pending" status — this is the static document not updated post-phase. The implementation evidence satisfies both requirements.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| — | — | None | — | No TBD, FIXME, XXX, HACK, PLACEHOLDER, or stub patterns found in any file modified by this phase. |

Pre-existing CS0436 warnings from `Mediator.SourceGenerator` in `PersonsAPI.Api.Tests` are unrelated to this phase — they existed before Phase 5 and all tests pass.

---

## Deviation Note: D-11 Test Suppression

**PLAN specified:** `builder.UseSerilog(ctx, lc) => lc.MinimumLevel.Fatal()` inside `ConfigureWebHost(IWebHostBuilder builder)`.

**Actual implementation:** `NullLoggerFactory.Instance` replacement via `ConfigureServices`.

**Why the deviation is correct:** `SerilogHostBuilderExtensions.UseSerilog` requires `IHostBuilder`, not `IWebHostBuilder`. In `WebApplicationFactory.ConfigureWebHost`, the builder parameter is `IWebHostBuilder` — the extension method is simply not available. The `NullLoggerFactory` approach directly replaces Serilog's custom `ILoggerFactory` singleton (which bypasses the standard `ILoggerProvider` system), making it more robust than `ClearProviders()`. The SUMMARY documented a second approach (`ConfigureLogging.ClearProviders`) but the actual code is a third approach (`NullLoggerFactory`). The behavioral outcome — zero CLEF lines in test output — is confirmed by the live test run.

**Recommendation:** Accept this deviation. The implementation is demonstrably correct and more robust than either the PLAN or SUMMARY described.

---

## Human Verification Required

### 1. CLEF JSON Stdout Emission (OBS-01)

**Test:** Run `dotnet run --project src/PersonsAPI.Api/PersonsAPI.Api.csproj` and observe stdout.
**Expected:** Each log line emitted to stdout is a valid JSON object in CLEF format — containing at minimum `@t` (timestamp), `@mt` (message template), and `SourceContext` fields. No plain-text log lines should appear. Sample expected line: `{"@t":"2026-06-03T03:22:25.2474047Z","@mt":"Now listening on: {address}","address":"http://localhost:5099",...}`
**Why human:** Cannot start a live server process during static verification. All code wiring is in place and verified, but the actual stdout emission of JSON requires a running process.

---

## Summary

**Phase goal is substantively achieved.** Both OBS-01 and OBS-02 requirements are fully wired in code:

- OBS-01 (structured JSON logs): `builder.Host.UseSerilog` with `CompactJsonFormatter` is present, EF Core and AspNetCore namespaces are filtered to Warning, and the pipeline is in place before `builder.Build()`.
- OBS-02 (/health endpoint): `app.MapHealthChecks("/health", HealthCheckOptions)` with a custom `ResponseWriter` returns `{"status":"Healthy"}` with `application/json` content-type and no authentication requirement.
- 64/64 tests pass with clean output.
- Zero changes outside the Api layer.

The only item requiring human confirmation is the live runtime emission of CLEF JSON to stdout (OBS-01 observable behavior), which requires a running process and cannot be verified statically. The static code analysis provides strong confidence this works correctly given the complete wiring.

---

_Verified: 2026-06-03T04:00:00Z_
_Verifier: Claude (gsd-verifier)_
