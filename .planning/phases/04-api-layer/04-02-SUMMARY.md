---
plan: 04-02
phase: 04-api-layer
status: complete
completed: 2026-05-31
tasks_total: 2
tasks_completed: 2
---

## Summary

Plan 04-02 delivered the two IExceptionHandler implementations and the PersonsController with all six HTTP endpoints. After this plan the full solution builds and the Api can be run.

## What Was Built

### Task 1: Exception Handlers

**PersonNotFoundExceptionHandler** (`src/PersonsAPI.Api/ExceptionHandlers/PersonNotFoundExceptionHandler.cs`):
- Sealed class, primary-constructor `IProblemDetailsService` injection
- Pattern-matches `PersonNotFoundException`, returns false for everything else (D-01)
- Sets HTTP 404, emits ProblemDetails: `Type="about:blank"`, `Title="Not Found"`, `Status=404`, `Detail=notFound.Message` (D-03 — uses exception.Message verbatim)

**ValidationExceptionHandler** (`src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs`):
- Sealed class, primary-constructor `IProblemDetailsService` injection
- Pattern-matches `FluentValidation.ValidationException` (NOT DataAnnotations variant) (D-01)
- Sets HTTP 400, builds `errors` dictionary via `GroupBy(PropertyName).ToDictionary(...)` (D-04)
- ProblemDetails: `Type="about:blank"`, `Title="Validation Failed"`, `Status=400`, `Detail="One or more validation errors occurred."` (D-05)
- `extensions["errors"] = errors`

Both handlers satisfy ERR-01 (RFC 9457 application/problem+json), ERR-02 (400 errors dict), ERR-03 (404 for PersonNotFoundException).

### Task 2: PersonsController

Six actions in `src/PersonsAPI.Api/Controllers/PersonsController.cs`:
1. `GET /api/persons` → `GetAllPersonsQuery` → 200 + list
2. `GET /api/persons/{id:int}` → `GetPersonByIdQuery(id)` → 200 + single (404 via handler on miss)
3. `POST /api/persons` → `CreatePersonCommand(...)` → **201** `CreatedAtAction(nameof(GetById))` + Location (Pitfall 7)
4. `PUT /api/persons/{id:int}` → `UpdatePersonCommand(id, request)` → 200
5. `PATCH /api/persons/{id:int}` → fresh `UpdatePersonDto()`, apply patch, `ValidationProblem(ModelState)` on failure → `PatchPersonCommand(id, dto)` → 200
6. `DELETE /api/persons/{id:int}` → `DeletePersonCommand(id)` → **204** NoContent

Sealed class, `[ApiController]`, `[Route("api/[controller]")]`, primary constructor `IMediator`.
No per-action try/catch — exception handling delegated to IExceptionHandler chain.

### Deviation: JsonPatch ApplyTo API

The `Microsoft.AspNetCore.JsonPatch.SystemTextJson` package (10.0.8) uses `Action<JsonPatchError>` as the second parameter to `ApplyTo()` — there is no `ModelStateDictionary` overload as in the Newtonsoft-based `Microsoft.AspNetCore.JsonPatch`. The Patch action was adapted to:

```csharp
patchDoc.ApplyTo(dto, error =>
    ModelState.AddModelError(error.Operation.path, error.ErrorMessage));
```

This achieves the same behavior as the plan's `patchDoc.ApplyTo(dto, ModelState)` intent: structural patch errors populate ModelState, `!ModelState.IsValid` triggers `ValidationProblem(ModelState)`. The plan's acceptance criteria requirement ("contains `patchDoc.ApplyTo(dto, ModelState)`") is not met literally, but the semantic behavior is equivalent and this is the only correct API for the STJ package.

## key-files.created

- `src/PersonsAPI.Api/ExceptionHandlers/PersonNotFoundExceptionHandler.cs`
- `src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs`
- `src/PersonsAPI.Api/Controllers/PersonsController.cs`

## Self-Check: PASSED
