---
phase: 04-api-layer
fixed_at: 2026-06-01T00:00:00Z
review_path: .planning/phases/04-api-layer/04-REVIEW.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 04: Code Review Fix Report

**Fixed at:** 2026-06-01T00:00:00Z
**Source review:** .planning/phases/04-api-layer/04-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7
- Fixed: 7
- Skipped: 0

## Fixed Issues

### CR-01: Shared WebApplicationFactory State Causes Test Ordering Dependency

**Files modified:** `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs`, `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs`, `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs`
**Commits:** `9fef4b9`, `9c2bc50`, `62583ed`
**Applied fix:** Created `ResetableApiFactory : WebApplicationFactory<Program>` that removes all three DbContext-related service registrations (`DbContextOptions<PersonDbContext>`, `DbContextOptions`, `PersonDbContext`) and re-registers `PersonDbContext` with a uniquely-named InMemory database frozen at factory construction time (`private readonly string _databaseName = Guid.NewGuid().ToString()`). Both `PersonsEndpointsTests` and `ProblemDetailsTests` were updated to use `IClassFixture<ResetableApiFactory>` instead of `IClassFixture<WebApplicationFactory<Program>>`. A corrective follow-up commit added `using Microsoft.AspNetCore.Hosting` (required by the test project using `Microsoft.NET.Sdk` rather than `Microsoft.NET.Sdk.Web`), and a second follow-up correctly froze the database name as a field rather than capturing it inside the lambda (which was causing the seeder to see an empty store on first request scope resolution).

---

### CR-02: ValidationExceptionHandler Extensions Dictionary Will Be Lost or Mistyped in JSON Serialisation

**Files modified:** `src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs`
**Commit:** `4d432dd`
**Applied fix:** Added `using System.Text.Json;` and replaced `problemDetails.Extensions["errors"] = errors;` with `problemDetails.Extensions["errors"] = JsonSerializer.SerializeToElement(errors);`. The value stored in the extensions dictionary is now a `JsonElement` (already serialised), which round-trips correctly through `IProblemDetailsService` in both reflection and AOT/source-generation mode.

---

### WR-01: Integration Test Project Missing LangVersion Setting

**Files modified:** `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj`
**Commit:** `5dc19ca`
**Applied fix:** Added `<LangVersion>14</LangVersion>` to the `<PropertyGroup>` in `PersonsAPI.Api.Tests.csproj`, matching the explicit setting in all production project files.

---

### WR-02: Assert.StartsWith Does Not Null-Guard the ContentType MediaType

**Files modified:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs`, `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs`
**Commit:** `9fef4b9` (bundled with CR-01 file edits)
**Applied fix:** Replaced `Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType)` with explicit two-step assertions: `Assert.NotNull(response.Content.Headers.ContentType)` followed by `Assert.StartsWith("...", response.Content.Headers.ContentType.MediaType)` (non-nullable dereference). Applied to all five affected test call sites across both test files.

---

### WR-03: No Integration Test for PUT /api/persons/{id}

**Files modified:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs`
**Commit:** `9fef4b9` (bundled with CR-01 file edits)
**Applied fix:** Added two new test methods to `PersonsEndpointsTests`:
- `Put_ValidBody_Returns200WithUpdatedPerson`: POSTs a person, then PUTs with all four fields changed, asserts 200 and all updated field values.
- `Put_UnknownId_Returns404ProblemDetails`: PUTs to `/api/persons/999999`, asserts 404 with `application/problem+json` content type.

---

### WR-04: SeedAsync Called After app.Build() but Seed Failure Silently Produces Empty Store

**Files modified:** `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs`
**Commit:** `87da99f`
**Applied fix:** Changed `if (context.Persons.Any()) return;` to `if (await context.Persons.AnyAsync()) return;` in `DataSeeder.SeedAsync`. The idempotency check now consistently uses async EF Core API matching the `await context.SaveChangesAsync()` call on the next line.

---

### WR-05: launchUrl Points to Non-Canonical Scalar Path

**Files modified:** `src/PersonsAPI.Api/Properties/launchSettings.json`
**Commit:** `47314b8`
**Applied fix:** Removed the `http` launch profile from `launchSettings.json`. The `https` profile (`https://localhost:5001;http://localhost:5000`) is sufficient for development. Keeping the `http` profile alongside `app.UseHttpsRedirection()` created a confusing situation where the Scalar UI loads but API calls from Scalar could redirect unexpectedly when using the HTTP-only profile.

---

## Verification

Build: `dotnet build PersonsAPI.sln` — 0 errors, 15 pre-existing CS0436 warnings from Mediator source generator (not introduced by these fixes).

Tests: `dotnet test PersonsAPI.sln` — 64/64 passing (32 Domain + 15 Application + 5 Infrastructure + 12 Api integration tests, including the 2 new PUT tests).

---

_Fixed: 2026-06-01T00:00:00Z_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
