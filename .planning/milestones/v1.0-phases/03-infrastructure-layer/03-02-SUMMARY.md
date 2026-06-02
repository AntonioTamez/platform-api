---
phase: 03-infrastructure-layer
plan: "02"
subsystem: infrastructure
tags: [seeder, ef-core, di, startup, idempotency]
dependency_graph:
  requires:
    - 03-01  # PersonDbContext and AddInfrastructure must exist before DataSeeder can resolve context
  provides:
    - DataSeeder.SeedAsync — IServiceProvider extension that populates 3 Person records at startup
  affects:
    - Phase 4 Program.cs (caller of await app.Services.SeedAsync() before app.Run())
tech_stack:
  added: []
  patterns:
    - Static class extension method on IServiceProvider (D-04 startup seeder pattern)
    - Explicit DI scope creation to resolve scoped services from root provider (RESEARCH.md Pitfall 4)
    - Idempotent seeder with if (context.Persons.Any()) return; guard (D-05)
key_files:
  created:
    - src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs
  modified: []
decisions:
  - D-04 honored: SeedAsync is an extension on IServiceProvider; creates its own scope via services.CreateScope()
  - D-05 honored: idempotency guard uses if (context.Persons.Any()) return; exactly as specified
  - D-06 honored: DataSeeder is a static class; not registered in DI; ServiceCollectionExtensions.cs unchanged
  - Threat T-03-08 mitigated: services.CreateScope() prevents InvalidOperationException from root-scope resolve
metrics:
  duration_seconds: 86
  completed_date: "2026-05-31"
  tasks_completed: 1
  files_created: 1
  files_modified: 0
---

# Phase 03 Plan 02: DataSeeder Implementation Summary

**One-liner:** Static `DataSeeder` class with idempotent `SeedAsync(this IServiceProvider)` extension seeding exactly 3 `Person` records via `Person.Create()` inside a dedicated DI scope.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Implement static DataSeeder.SeedAsync extension on IServiceProvider | d6a7f33 | src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs |

## What Was Built

`src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs` — a `public static class DataSeeder` in `namespace PersonsAPI.Infrastructure.Seeder` exposing a single method:

```csharp
public static async Task SeedAsync(this IServiceProvider services)
```

The method:
1. Creates a dedicated DI scope via `services.CreateScope()` (D-04 / RESEARCH.md Pitfall 4 mitigation)
2. Resolves `PersonDbContext` from the scoped provider — never from the root
3. Guards idempotency with `if (context.Persons.Any()) return;` (D-05)
4. Calls `context.Persons.AddRange(...)` with exactly 3 `Person.Create()` calls (D-01, D-02, D-03)
5. Persists with `await context.SaveChangesAsync()`

## Seed Records (D-01, D-02, D-03)

| # | First Name | Paternal Last | Maternal Last | DOB | Age (~) |
|---|------------|---------------|---------------|-----|---------|
| 1 | María | García | López | 1994-06-15 | ~32 |
| 2 | Carlos | Ramírez | Martínez | 1979-03-22 | ~47 |
| 3 | Ana | Flores | Mendoza | 1963-11-08 | ~62 |

Ages are intentionally varied to exercise the month-and-day-aware `Age` computation across distinct date ranges (D-03).

## Verification Results

| Check | Result |
|-------|--------|
| `dotnet build src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` exits 0 | PASS |
| `grep -c 'Person.Create('` returns 3 (exactly 3 seed records) | PASS |
| `grep -c 'services.CreateScope()'` returns 1 | PASS |
| `if (context.Persons.Any()) return;` literal present | PASS |
| `ServiceCollectionExtensions.cs` has no DI registration of DataSeeder (D-06) | PASS |

Build produced 0 errors. Only warning: CS0628 (protected member in sealed class for `Person()`) — pre-existing accepted warning documented in STATE.md.

## Note for Phase 4

`Program.cs` (Phase 4) must call `await app.Services.SeedAsync()` before `app.Run()`. The namespace `PersonsAPI.Infrastructure.Seeder` must be imported. `DataSeeder` does NOT need to be registered with `builder.Services.AddInfrastructure()`.

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None — the three seed records are fully wired with concrete `Person.Create()` calls.

## Threat Flags

No new threat surface introduced beyond what was already modeled in the plan's `<threat_model>` section. All four threats (T-03-06 through T-03-09) were mitigated as specified:
- T-03-06: All seed records go through `Person.Create()` factory — domain invariants enforced
- T-03-07: Idempotency guard `if (context.Persons.Any()) return;` prevents duplication
- T-03-08: `services.CreateScope()` + `using` prevents root-provider scoped-service resolve error
- T-03-09: Seed names are fictional; no real PII

## Self-Check: PASSED

- [x] `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs` exists
- [x] Commit d6a7f33 exists in git log
- [x] Build exits 0 with 0 errors
