---
phase: 03-infrastructure-layer
plan: "03"
subsystem: infrastructure-tests
tags:
  - xunit
  - efcore-inmemory
  - repository-tests
  - test-isolation
dependency_graph:
  requires:
    - 03-01  # PersonsAPI.Infrastructure (PersonRepository, PersonDbContext)
  provides:
    - INFRA-01-validation  # end-to-end proof that PersonDbContext + PersonRepository work
  affects:
    - PersonsAPI.Infrastructure.Tests project (new)
tech_stack:
  added:
    - xunit 2.9.3
    - xunit.runner.visualstudio 3.1.4
    - Microsoft.NET.Test.Sdk 17.14.1
    - coverlet.collector 6.0.4
    - Microsoft.EntityFrameworkCore.InMemory 10.0.8 (test project direct reference)
  patterns:
    - Guid-named InMemory database per test (T-02 isolation)
    - Domain factory Person.Create() in test arrange phase
    - await using var context for async DbContext disposal
    - Arrange/Act/Assert with section comments
key_files:
  created:
    - tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj
    - tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs
  modified:
    - PersonsAPI.sln  # registered new test project
decisions:
  - "Used Guid.NewGuid().ToString() as InMemory database name per test — zero state contamination (T-02)"
  - "No IClassFixture — each test is fully self-contained with its own DbContext"
  - "No mocking library — PersonRepository instantiated directly with real DbContext matching Application.Tests pattern"
  - "Microsoft.EntityFrameworkCore.InMemory added as direct PackageReference to test project so tests can call UseInMemoryDatabase independently of the Infrastructure src project"
metrics:
  duration: "145s"
  completed_date: "2026-05-31"
  tasks_completed: 2
  files_created: 2
  files_modified: 1
---

# Phase 03 Plan 03: Infrastructure.Tests — PersonRepository CRUD Tests Summary

**One-liner:** xUnit test project with 5 isolated InMemory CRUD tests validating PersonRepository against the IPersonRepository contract end-to-end.

## What Was Built

Created `PersonsAPI.Infrastructure.Tests` — a new xUnit test project at `tests/PersonsAPI.Infrastructure.Tests/` registered in `PersonsAPI.sln`. The project contains one test class (`PersonRepositoryTests`) with 5 `[Fact]` methods, one per `IPersonRepository` method.

### Task 1: PersonsAPI.Infrastructure.Tests project

- `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` — mirrors `PersonsAPI.Application.Tests.csproj` structure with packages: `coverlet.collector` 6.0.4, `Microsoft.EntityFrameworkCore.InMemory` 10.0.8, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4
- Global `<Using Include="Xunit" />` eliminates per-file `using Xunit;`
- Single `ProjectReference` to `PersonsAPI.Infrastructure.csproj` (Application and Domain come transitively)
- Registered in `PersonsAPI.sln`; `dotnet build` exits 0
- Commit: e56ab4d

### Task 2: 5 PersonRepositoryTests

- `tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs` — sealed test class with static helpers and 5 `[Fact]` methods
- `CreateContext()` — uses `UseInMemoryDatabase(Guid.NewGuid().ToString())` for per-test isolation (T-02)
- `CreateValidPerson()` — delegates to `Person.Create()` domain factory so domain invariants are part of every round-trip
- Five tests: `GetAllAsync_WhenPersonsExist_ReturnsAllPersons`, `GetByIdAsync_WhenPersonExists_ReturnsPerson`, `AddAsync_PersistsPersonAndAssignsId`, `UpdateAsync_PersistsChangesToPerson`, `DeleteAsync_RemovesPersonFromStore`
- All 5 tests pass; combined solution run: 52 tests passed, 0 failed
- Commit: 3efd065

## Test Runner Output

```
Correctas! - Con error: 0, Superado: 5, Omitido: 0, Total: 5, Duración: 2 s - PersonsAPI.Infrastructure.Tests.dll (net10.0)
```

Combined suite (full solution):
- PersonsAPI.Domain.Tests: 32 passed
- PersonsAPI.Application.Tests: 15 passed
- PersonsAPI.Infrastructure.Tests: 5 passed
- **Total: 52 passed, 0 failed, 0 skipped**

## Acceptance Criteria Verification

- [x] 5 IPersonRepository methods covered by passing tests (GetAllAsync, GetByIdAsync, AddAsync, UpdateAsync, DeleteAsync)
- [x] Test isolation via `Guid.NewGuid().ToString()` InMemory DB name verified (literal present in CreateContext helper)
- [x] PersonDbContext + PersonEntityConfiguration + PersonRepository work end-to-end (builder.Ignore(p => p.Age) does not break entity materialization)
- [x] Private-setter properties (FirstName, PaternalLastName, MaternalLastName, DateOfBirth) round-trip through EF Core InMemory without explicit UsePropertyAccessMode
- [x] No IClassFixture (T-02 explicitly rejects shared context)
- [x] No mocking library (matches Application.Tests inline pattern)
- [x] No `using Xunit;` in test file (global using in .csproj)
- [x] `dotnet test PersonsAPI.Infrastructure.Tests` exits 0
- [x] `dotnet build PersonsAPI.sln` exits 0 (1 pre-existing CS0628 warning, accepted per prior decision)

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None — all tests use real domain factory and real EF Core InMemory adapter.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced. Test project is test-only infrastructure.

## Self-Check: PASSED

- [x] `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` — EXISTS
- [x] `tests/PersonsAPI.Infrastructure.Tests/Repositories/PersonRepositoryTests.cs` — EXISTS
- [x] `tests/PersonsAPI.Infrastructure.Tests/UnitTest1.cs` — ABSENT (correctly deleted)
- [x] Commit e56ab4d — FOUND (feat(03-03): create PersonsAPI.Infrastructure.Tests xUnit project)
- [x] Commit 3efd065 — FOUND (feat(03-03): implement 5 PersonRepositoryTests covering all IPersonRepository methods)
- [x] 5 [Fact] methods — VERIFIED (grep count = 5)
- [x] `UseInMemoryDatabase(Guid.NewGuid().ToString())` — VERIFIED (1 occurrence in CreateContext helper)
- [x] `PersonsAPI.Infrastructure.Tests` in PersonsAPI.sln — VERIFIED
