---
plan: 04-01
phase: 04-api-layer
status: complete
completed: 2026-05-31
tasks_total: 3
tasks_completed: 3
---

## Summary

Plan 04-01 established the PersonsAPI.Api project as the composition root for Phase 4, fixed the JsonPatch incompatibility in UpdatePersonDto, and wired the full ASP.NET Core middleware + DI pipeline.

## What Was Built

### Task 1: UpdatePersonDto Migration

Converted `UpdatePersonDto` from a positional record to a mutable class with `{ get; set; }` properties. `JsonPatchDocument<T>.ApplyTo()` requires settable properties at runtime — positional records with init-only setters throw `InvalidOperationException` on patch application.

Updated `PatchPersonCommandValidatorTests` to use object initializer syntax instead of the positional constructor (4 call sites updated). No other downstream files required changes.

### Task 2: PersonsAPI.Api Project

Created `src/PersonsAPI.Api/PersonsAPI.Api.csproj` with:
- SDK: `Microsoft.NET.Sdk.Web`
- Framework: `net10.0`, `LangVersion: 14`, `Nullable: enable`
- Project references: Application + Infrastructure (Domain transitive)
- Packages (exact pinned versions):
  - `Mediator.SourceGenerator` 3.0.2 (analyzer scope — OutputItemType="Analyzer", ReferenceOutputAssembly="false")
  - `Microsoft.AspNetCore.JsonPatch.SystemTextJson` 10.0.8
  - `Microsoft.AspNetCore.OpenApi` 10.0.8
  - `Scalar.AspNetCore` 2.14.14
- `Mediator.SourceGenerator` installed ONLY in this project (Pitfall 4 — generator scope)

Launch profiles: `http` on port 5000, `https` on 5001/5000, both targeting `scalar/v1` as launch URL.

Registered in `PersonsAPI.sln` under the existing `src` solution folder (`{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`). Full solution build succeeds.

### Task 3: Program.cs Composition Root

Top-level statements composition root with service registrations in this exact order:
1. `AddControllers()`
2. `AddProblemDetails()` (D-02)
3. `AddExceptionHandler<PersonNotFoundExceptionHandler>()` (D-01 — NotFound first)
4. `AddExceptionHandler<ValidationExceptionHandler>()` (D-01 — Validation second)
5. `AddOpenApi()` (DOC-01)
6. `AddMediator(options => options.PipelineBehaviors = [typeof(ValidationBehavior<,>)])` (Mediator + pipeline)
7. `AddApplication()` (FluentValidation validators)
8. `AddInfrastructure()` (DbContext + Repository)

Middleware pipeline:
1. `UseExceptionHandler()` — no route argument (Pitfall 2)
2. `UseHttpsRedirection()`
3. `MapControllers()`
4. `MapOpenApi()` (DOC-01)
5. `MapScalarApiReference()` — Map not Use (Pitfall 8)

`await app.Services.SeedAsync()` called BEFORE `await app.RunAsync()` (Pitfall 5).

`public partial class Program { }` declared at end of file for `WebApplicationFactory<Program>` in Plan 03.

**Note:** Api project does not compile until Plan 02 ships the two `IExceptionHandler` implementations referenced in Program.cs. This is expected — structural grep verification gates Task 3, not a full build.

## Deviations

**PatchPersonCommandValidatorTests updated (not in plan files list):** The plan listed only `UpdatePersonDto.cs` as the file to modify, but the test file `tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs` used the positional constructor form and required update. This was a necessary consequence of the type change — no behavioral deviation.

## key-files.created

- `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` (modified — mutable class)
- `src/PersonsAPI.Api/PersonsAPI.Api.csproj`
- `src/PersonsAPI.Api/Program.cs`
- `src/PersonsAPI.Api/Properties/launchSettings.json`
- `src/PersonsAPI.Api/appsettings.json`
- `src/PersonsAPI.Api/appsettings.Development.json`
- `PersonsAPI.sln` (Api project added)
- `tests/PersonsAPI.Application.Tests/Commands/PatchPersonCommandValidatorTests.cs` (updated)

## Self-Check: PASSED
