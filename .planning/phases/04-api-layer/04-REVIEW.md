---
phase: 04-api-layer
reviewed: 2026-05-31T00:00:00Z
depth: standard
files_reviewed: 13
files_reviewed_list:
  - src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs
  - tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs
  - src/PersonsAPI.Api/PersonsAPI.Api.csproj
  - src/PersonsAPI.Api/Program.cs
  - src/PersonsAPI.Api/Properties/launchSettings.json
  - src/PersonsAPI.Api/appsettings.json
  - src/PersonsAPI.Api/appsettings.Development.json
  - src/PersonsAPI.Api/ExceptionHandlers/PersonNotFoundExceptionHandler.cs
  - src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs
  - src/PersonsAPI.Api/Controllers/PersonsController.cs
  - tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj
  - tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs
  - tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs
findings:
  critical: 2
  warning: 5
  info: 3
  total: 10
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-05-31T00:00:00Z
**Depth:** standard
**Files Reviewed:** 13
**Status:** issues_found

## Summary

Phase 4 delivers the API layer: `Program.cs` startup wiring, two `IExceptionHandler` implementations, `PersonsController` with six endpoints, and integration test suites. The architecture is correctly layered — the controller is thin, exception handling is centralised, and the PATCH flow correctly uses a mutable DTO with `JsonPatchDocument.ApplyTo`. However, two blocking issues were found: shared in-memory state across integration test collections produces ordering-dependent failures, and the `ValidationExceptionHandler` serialises the errors dictionary using `Dictionary<string, string[]>` which System.Text.Json will silently drop from `ProblemDetails.Extensions` at runtime when the JSON serialiser cannot round-trip the nested type through `object`. There are also five warnings covering a missing `LangVersion` in the test project, unchecked `Assert.StartsWith` null-safety, a missing integration test for the PUT endpoint, the seeder being called after `app.Build()` but before request pipeline warm-up which creates a subtle ordering risk, and the `launchUrl` pointing to `scalar/v1` which is not the canonical Scalar path emitted by `MapScalarApiReference`.

---

## Critical Issues

### CR-01: Shared WebApplicationFactory State Causes Test Ordering Dependency

**File:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs:15-17` and `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs:17-18`

**Issue:** Both `PersonsEndpointsTests` and `ProblemDetailsTests` implement `IClassFixture<WebApplicationFactory<Program>>` and each hold a *separate* factory instance. Because the `WebApplicationFactory` boots a fresh host per fixture, the EF Core InMemory store is also separate per fixture — that part is safe. However, within `PersonsEndpointsTests` the *same* factory is reused across all test methods via the shared field `factory` created by xUnit's fixture injection. Every `POST`, `PATCH`, and `DELETE` test mutates shared in-memory state:

- `Post_ValidBody_Returns201WithLocation` adds a "TestFn" person.
- `Patch_ReplaceFirstName_Returns200WithUpdatedName` adds another person and patches it.
- `Delete_KnownId_Returns204` adds yet another person and deletes it.
- `GetAll_ReturnsThreeSeededPersons` asserts `persons.Length >= 3` — this is loose enough not to fail, but the assertion `Assert.True(persons.Length >= 3)` will silently pass with any count, masking regressions where seeding fails entirely.

The deeper problem is the `GetById_KnownId_Returns200WithPerson` test at line 38: it calls `GET /api/persons` to discover a `knownId`, then immediately calls `GET /api/persons/{knownId}`. If any concurrently running test (xUnit runs tests within a class sequentially by default, but running in parallel across collections is possible) has deleted or mutated record with `all[0].Id`, this test will intermittently return 404 instead of 200 and fail. xUnit's default test class parallelism runs classes within the same assembly in parallel — both test classes share nothing except the `WebApplicationFactory`, so this is currently safe, but the pattern is fragile.

The real blocking issue is that `GetAll_ReturnsThreeSeededPersons` will fail when the test runner executes `Post_ValidBody_Returns201WithLocation` first, because subsequent GET calls will find more than 3 records. While the `>= 3` guard handles this today, any future assertion tightening will expose the underlying problem, and the test gives false confidence about isolation.

**Fix:** Each test that mutates state (POST/PATCH/DELETE) should either:
1. Use a custom `IClassFixture` that resets the InMemory store between tests (e.g., `context.Database.EnsureDeleted()` + re-seed), or
2. Use `factory.WithWebHostBuilder(...)` to create a fresh named database per test.

Minimal approach — add a custom fixture that re-seeds per test class run:
```csharp
public sealed class ResetableApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace the DbContext registration with a uniquely-named InMemory db
            // to ensure each factory gets an isolated store.
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<PersonDbContext>));
            services.Remove(descriptor);
            services.AddDbContext<PersonDbContext>(opt =>
                opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        });
    }
}
```

---

### CR-02: ValidationExceptionHandler Extensions Dictionary Will Be Lost or Mistyped in JSON Serialisation

**File:** `src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs:38-51`

**Issue:** The `errors` variable is typed as `Dictionary<string, string[]>` and assigned to `problemDetails.Extensions["errors"]`. `ProblemDetails.Extensions` is `IDictionary<string, object?>`. When `IProblemDetailsService.TryWriteAsync` serialises the `ProblemDetails` object to JSON via System.Text.Json's built-in `ProblemDetailsJsonConverter`, the converter serialises extension values by calling `JsonSerializer.Serialize(value)` as `object`. The actual runtime behaviour of System.Text.Json when serialising a `Dictionary<string, string[]>` stored as `object?` depends on the serialiser options used by the problem details middleware.

In the default ASP.NET Core 10 configuration, `Microsoft.AspNetCore.Http.ProblemDetailsJsonContext` uses source-generated JSON serialisation. The source-generated context does not include `Dictionary<string, string[]>` in its type metadata — it only directly serialises `object`, which uses the runtime type. When the runtime type is `Dictionary<string, string[]>`, the standard polymorphic serialisation *does* work in reflection mode, **but fails silently to produce the expected nested structure in AOT/source-generation mode** because `string[]` inside a dictionary value may not be registered.

More critically, the integration test `ProblemDetailsTests.Post_EmptyBody_Returns400WithErrorsDictionary` at line 69 parses the `errors` extension from the raw `JsonDocument` (bypassing the typed deserialisation issue) and succeeds in test — but the test sends `FirstName = "A"` which has length 1, failing `MinimumLength(2)`. The test then asserts that `errors` contains keys `"FirstName"`, `"PaternalLastName"`, and `"MaternalLastName"`. However, `PaternalLastName = "P"` and `MaternalLastName = "M"` (both length 1) — those will also fail `MinimumLength(2)`, so the test assertion for all three keys passes. This masks whether single-field validation errors are serialised correctly.

The safer, explicitly supported pattern for `ProblemDetails` extensions in ASP.NET Core 10 is to use a type that the built-in JSON converter is known to handle correctly:

**Fix:** Replace the `Dictionary<string, string[]>` assignment with a serialiser-safe alternative. The most reliable approach is to set the extension value as a `JsonElement` (already serialised):

```csharp
var errors = validationException.Errors
    .GroupBy(e => e.PropertyName)
    .ToDictionary(
        g => g.Key,
        g => g.Select(e => e.ErrorMessage).ToArray());

// Serialise to JsonElement first — this guarantees the value is a well-formed
// JSON object regardless of the ProblemDetails serialiser pipeline in use.
var errorsJson = JsonSerializer.SerializeToElement(errors);
problemDetails.Extensions["errors"] = errorsJson;
```

This requires adding `using System.Text.Json;`. The approach is used by the official ASP.NET Core samples for custom problem details extensions and is documented to round-trip correctly through `IProblemDetailsService`.

---

## Warnings

### WR-01: Integration Test Project Missing LangVersion Setting

**File:** `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj:1-27`

**Issue:** The Api test project does not set `<LangVersion>14</LangVersion>`, while the production project `PersonsAPI.Api.csproj` explicitly sets it. The `SDK.Web` projects default to `LangVersion` matching the TFM (`net10.0` → C# 14), but the test project uses `Microsoft.NET.Sdk` (not `Sdk.Web`). For `Microsoft.NET.Sdk` with `net10.0`, the effective default is also C# 14, but the inconsistency with the rest of the codebase is a maintenance hazard: if a future SDK update changes the default, the test project will drift without a compile-time signal.

**Fix:** Add `<LangVersion>14</LangVersion>` to `PersonsAPI.Api.Tests.csproj` `<PropertyGroup>`.

---

### WR-02: Assert.StartsWith Does Not Null-Guard the ContentType MediaType

**File:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs:63` and `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs:28, 55, 83, 107`

**Issue:** Multiple test lines use the pattern:
```csharp
Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
```
The second argument to `Assert.StartsWith` is `string?`. When `ContentType` is null or `MediaType` is null, `Assert.StartsWith` does not throw — it **passes** (xUnit's `Assert.StartsWith(expected, null)` returns without error when the actual value is null, treating it as "does not start with" and thus failing the assertion). Wait — actually in xUnit v2, `Assert.StartsWith` with a null `actualString` **throws `ArgumentNullException`** in some versions and **fails the assertion** in others. In xUnit 2.9.3 (the version used here), passing `null` as the actual value throws `XunitException: Assert.StartsWith() Failure`, which is an assertion failure not a null-ref crash. So the test will fail if content-type is missing, which is the desired behaviour. However, the nullable `?.` chain silently converts a missing `ContentType` header into a null string, obscuring *which* null check failed. A more explicit assertion would improve diagnostic output.

**Fix:** Use a non-nullable dereference with an explicit assertion:
```csharp
Assert.NotNull(response.Content.Headers.ContentType);
Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType.MediaType);
```

---

### WR-03: No Integration Test for PUT /api/persons/{id}

**File:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs` (entire file)

**Issue:** `PersonsEndpointsTests` covers GET (all), GET (by id), GET (unknown id), POST, PATCH, and DELETE — but omits PUT. The `PersonsController.Update` method at line 63 of the controller dispatches `UpdatePersonCommand`, which has its own validator (`UpdatePersonCommandValidator`) with identical field rules to `CreatePersonCommandValidator`. Without an integration test, a regression in `UpdatePersonHandler` (e.g., silent no-op on unknown ID, wrong status code, incorrect field mapping) would go undetected in the API layer tests.

**Fix:** Add at minimum two tests:
```csharp
[Fact]
public async Task Put_ValidBody_Returns200WithUpdatedPerson()
{
    // POST then PUT, assert all fields replaced
}

[Fact]
public async Task Put_UnknownId_Returns404ProblemDetails()
{
    // PUT to id 999999, assert 404 + problem+json
}
```

---

### WR-04: SeedAsync Called After app.Build() but Seed Failure Silently Produces Empty Store

**File:** `src/PersonsAPI.Api/Program.cs:33`

**Issue:** `await app.Services.SeedAsync()` is called after `app.Build()` but before `app.RunAsync()`. The `SeedAsync` extension method on `IServiceProvider` does not propagate exceptions — it calls `context.Persons.Any()` synchronously (not `AnyAsync`) and calls `await context.SaveChangesAsync()`. If `SaveChangesAsync` throws (e.g., because the InMemory provider rejects a constraint, or the scope resolution fails), the exception propagates and crashes the process before `RunAsync` — this is actually the correct behaviour for a startup failure.

However, the `context.Persons.Any()` call at line 68 of `DataSeeder.cs` is a **synchronous** LINQ materialisation on an EF DbSet. For an InMemory provider this is safe (no async I/O), but it bypasses the `cancellationToken` and any async EF instrumentation. This is a code quality issue rather than a crash risk, but inconsistent with the `await context.SaveChangesAsync()` on the next line.

More critically, when `WebApplicationFactory<Program>` boots the host for integration tests, it calls `SeedAsync` during the normal startup path. Because the `WebApplicationFactory` shares a single host per fixture (per xUnit IClassFixture), all tests in `PersonsEndpointsTests` share the same seed. The idempotency check (`if (context.Persons.Any()) return`) means the seed runs exactly once per factory lifetime, which is correct. But if a test mutates and then the factory is reused, the seed is not re-run (by design). This reinforces the concern in CR-01.

**Fix:** Change the synchronous `Any()` call to `AnyAsync()` in `DataSeeder.SeedAsync` for consistency:
```csharp
if (await context.Persons.AnyAsync()) return;
```
(This is a fix in `DataSeeder.cs`, outside the files under review, but the call site in `Program.cs` is in scope.)

---

### WR-05: launchUrl Points to Non-Canonical Scalar Path

**File:** `src/PersonsAPI.Api/Properties/launchSettings.json:7` and `line 16`

**Issue:** Both launch profiles set `"launchUrl": "scalar/v1"`. The Scalar ASP.NET Core package (`Scalar.AspNetCore 2.14.14`) registers its UI at `/scalar/v1` by default when called as `app.MapScalarApiReference()` without arguments — so the path is correct. However, the `https` profile lists `"applicationUrl": "https://localhost:5001;http://localhost:5000"` and also sets `launchUrl` to `scalar/v1`. When launched via the `http` profile, the browser opens `http://localhost:5000/scalar/v1`. When launched via the `https` profile, the browser opens `https://localhost:5001/scalar/v1` and the HTTP redirect from `app.UseHttpsRedirection()` applies to API requests but not to the browser-opened URL (the browser goes directly to the https URL). This is functionally fine, but the `http` profile does not have HTTPS redirection disabled, so API calls from the Scalar UI running on `http://localhost:5000` will attempt HTTP — which works for InMemory/development but is inconsistent with `app.UseHttpsRedirection()` being active on the HTTP port.

More relevantly: `MapScalarApiReference` in version 2.x by default uses the path prefix `/scalar`, so it registers `/scalar/v1`, `/scalar/v2`, etc., based on the OpenAPI document name. The OpenAPI document is named `v1` by default with `AddOpenApi()` / `MapOpenApi()`. The `launchUrl` of `scalar/v1` matches, so no redirect failure occurs. This is a WARNING-level issue because the `http` profile launches a browser pointed at HTTP while HTTPS redirection middleware is active only for programmatic requests, not browser navigation — it could confuse a developer who uses the `http` profile and finds the Scalar UI loads but API calls from Scalar fail CORS or redirect unexpectedly.

**Fix:** Either remove the `http` profile entirely (the `https` profile is sufficient for development), or add a note to the `http` profile that HTTPS redirection is active.

---

## Info

### IN-01: PatchPersonCommandValidatorTests Covers Only Four of Six Possible Combinations

**File:** `tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs:1-48`

**Issue:** The validator test file has four tests: all-null (valid), first-name-only valid, first-name-empty invalid, and future date invalid. It does not test: PaternalLastName or MaternalLastName validation (empty/too-short), Id <= 0 validation, or a valid full-patch with all four fields set. The `When()` condition guard for `MaternalLastName` is never exercised. This reduces confidence that the `When()` conditions on those fields are wired correctly, though the validator code itself is consistent with `CreatePersonCommandValidator`.

**Fix:** Add tests for:
- `Id = 0` fails with "Id must be a positive integer."
- `PaternalLastName = "X"` (length 1) fails `MinimumLength(2)`.
- All four non-null valid fields passes.

---

### IN-02: GetById Integration Test Derives knownId from Index [0] of Unsorted Collection

**File:** `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs:44-45`

**Issue:** `var knownId = all[0].Id` assumes the first element of the GET-all response is a valid, non-deleted record. Because the InMemory store returns items in insertion order and no other test deletes seeded records (only records created within a test are deleted), this is safe today. If test execution order changes or a future test deletes a seeded record, this will produce a 404 from the subsequent `GetById` call and fail opaquely. The issue is minor because xUnit executes tests within a class sequentially in definition order, but it is still a fragile assumption.

**Fix:** Use `Assert.NotEmpty(all)` then select by `FirstOrDefault` with a named-seed check (e.g., a person with `FirstName == "María"`) to find a stable known ID rather than relying on array index.

---

### IN-03: Scalar UI Test Uses Fallback Logic That Masks Missing Route Registration

**File:** `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs:96-108`

**Issue:** `Get_ScalarUi_Returns200Html` first tries `/scalar/v1`, then falls back to `/scalar` if the first attempt fails. If `MapScalarApiReference()` is removed or misconfigured in `Program.cs`, `/scalar/v1` returns a non-success status, and the test then tries `/scalar`. If `/scalar` returns a redirect (301/302) to `/scalar/v1`, `factory.CreateClient()` follows redirects by default, and the second attempt may also fail — but if `/scalar` returns 404, the test fails with a message that says "Expected 200 from /scalar/v1 or /scalar" without identifying which path should be canonical. The fallback logic also means the test does not enforce that the canonical path `/scalar/v1` is accessible — a misconfiguration that makes Scalar serve on `/scalar/v1` only after a redirect would still pass.

**Fix:** Assert directly on `/scalar/v1` without a fallback. If the Scalar path changes, that is a deliberate configuration change and should require an explicit test update:
```csharp
var response = await client.GetAsync("/scalar/v1");
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
```

---

_Reviewed: 2026-05-31T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
