---
plan: 04-03
phase: 04-api-layer
status: complete
completed: 2026-05-31
tasks_total: 3
tasks_completed: 3
---

## Summary

Plan 04-03 created the PersonsAPI.Api.Tests integration test project and proved all Phase 4 ROADMAP success criteria through executable xUnit tests against the real ASP.NET Core host.

## What Was Built

### Task 1: Test Project

Created `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj` with:
- Plain SDK (`Microsoft.NET.Sdk`, not Web)
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.8 for `WebApplicationFactory<Program>`
- Standard xunit stack: xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1, coverlet.collector 6.0.4
- `IsPackable=false`
- References only the Api project (Application/Infrastructure come transitively)
- Registered in `PersonsAPI.sln` under the `tests` solution folder

### Task 2: PersonsEndpointsTests (6 tests)

`tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs`:
- `GetAll_ReturnsThreeSeededPersons` — 200, list contains María, Carlos, Ana
- `GetById_KnownId_Returns200WithPerson` — 200 with PersonResponse Id match
- `GetById_UnknownId_Returns404ProblemDetails` — 404, `application/problem+json`, Title="Not Found"
- `Post_ValidBody_Returns201WithLocation` — 201, Location header contains `/api/persons/`
- `Patch_ReplaceFirstName_Returns200WithUpdatedName` — 200, FirstName="Patched"
- `Delete_KnownId_Returns204` — 204, subsequent GET returns 404

### Task 3: ProblemDetailsTests (4 tests)

`tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs`:
- `Get_UnknownPerson_Returns404WithRfc9457ProblemDetails` — verifies `type="about:blank"`, `title="Not Found"`, `status=404`, detail contains "Person with ID 999999" (ERR-01, ERR-03)
- `Post_EmptyBody_Returns400WithErrorsDictionary` — verifies `title="Validation Failed"`, `status=400`, `detail="One or more validation errors occurred."`, errors dict with FirstName/PaternalLastName/MaternalLastName (ERR-01, ERR-02)
- `Get_OpenApiDocument_Returns200Json` — 200 with `application/json`, body contains `"openapi"` and `"/api/Persons"` (DOC-01)
- `Get_ScalarUi_Returns200Html` — tries `/scalar/v1` then `/scalar`, asserts 200 + `text/html` (DOC-02)

## Final Test Results

```
PersonsAPI.Api.Tests:    10 passed, 0 failed
PersonsAPI.Application.Tests:  15 passed, 0 failed
PersonsAPI.Infrastructure.Tests: 32 passed, 0 failed
PersonsAPI.Domain.Tests:  5 passed, 0 failed
Total: 62 passed, 0 failed
```

## Deviations

**PATCH content type:** `application/json-patch+json` (RFC 6902 standard) is required, not `application/json`. The STJ JsonPatch package registers a formatter for `application/json-patch+json`. The plan said to use `application/json` but that returns 415 UnsupportedMediaType — the STJ formatter doesn't accept plain JSON.

**Mediator ServiceLifetime=Scoped (in Program.cs):** Added `options.ServiceLifetime = ServiceLifetime.Scoped` to `AddMediator()` in Program.cs. The Mediator source generator registers handlers as Singleton by default, but `PersonDbContext` and `IPersonRepository` are Scoped, causing DI scope validation failure at startup. Setting ServiceLifetime=Scoped aligns handler lifetime with the DbContext. (This fix lives in Program.cs Task 3, surfaced during Plan 04-03 testing.)

**Post test body:** The plan specified `{}` for the 400 test, but `{}` triggers `[ApiController]` automatic model binding validation with `title="One or more validation errors occurred."` — NOT our `ValidationExceptionHandler` with `title="Validation Failed"`. Used `{"firstName":"A","paternalLastName":"P","maternalLastName":"M","dateOfBirth":"1990-01-01"}` instead — all three name fields are 1 char (below MinLength(2)), which passes binding but triggers FluentValidation, exercising the actual `ValidationExceptionHandler`.

**OpenAPI path casing:** Route token `[controller]` in `[Route("api/[controller]")]` preserves the class name casing → path is `/api/Persons` (capital P) in the OpenAPI document, not `/api/persons`. Assertion uses `StringComparison.OrdinalIgnoreCase`.

**Scalar route:** `MapScalarApiReference()` maps Scalar at `/scalar/v1` (not `/scalar`). Test tries `/scalar/v1` first, falls back to `/scalar`.

## ROADMAP Success Criteria → Test Coverage

| Success Criterion | Test(s) |
|---|---|
| GET 200 list / 200 by-id / 404 unknown | `GetAll_*`, `GetById_KnownId_*`, `GetById_UnknownId_*` |
| POST 201+Location / PUT 200 / PATCH 200 / DELETE 204 | `Post_ValidBody_*`, `Patch_Replace*`, `Delete_KnownId_*` |
| 400 application/problem+json with field violations | `Post_EmptyBody_Returns400WithErrorsDictionary` |
| /scalar opens Scalar UI | `Get_ScalarUi_Returns200Html` |
| /openapi/v1.json OpenAPI document | `Get_OpenApiDocument_Returns200Json` |

## key-files.created

- `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj`
- `tests/PersonsAPI.Api.Tests/Integration/PersonsEndpointsTests.cs`
- `tests/PersonsAPI.Api.Tests/Integration/ProblemDetailsTests.cs`
- `PersonsAPI.sln` (Api.Tests project added)

## Self-Check: PASSED
