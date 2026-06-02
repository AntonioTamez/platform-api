# Phase 4: API Layer - Context

**Gathered:** 2026-05-31
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the `PersonsAPI.Api` ASP.NET Core Web API project — `PersonsController` exposing all six HTTP endpoints with correct semantics, `Program.cs` as the sole composition root wiring all three layers, Problem Details (RFC 9457) as the only error response format via two `IExceptionHandler` implementations, and OpenAPI + Scalar available for interactive exploration. No business logic lives here — the controller dispatches commands and queries to the Application layer via Mediator.

</domain>

<decisions>
## Implementation Decisions

### Exception → Problem Details Mapping
- **D-01:** Use **two `IExceptionHandler` implementations** (ASP.NET Core 8+/10 pattern) registered via `services.AddExceptionHandler<T>()`:
  - `PersonNotFoundExceptionHandler` — catches `PersonNotFoundException`, writes 404 Problem Details
  - `ValidationExceptionHandler` — catches `FluentValidation.ValidationException`, writes 400 Problem Details
  Registered in order: NotFound handler first, Validation handler second. `app.UseExceptionHandler()` activates the chain.
- **D-02:** **`services.AddProblemDetails()`** is registered globally in Program.cs. Each handler fills `ProblemDetails` fields without duplicating the serializer setup. Both handlers use `IProblemDetailsService` to write the response.
- **D-03:** **404 Problem Details shape** — minimum RFC 9457 fields only: `type`, `title`, `status`, `detail`. No `instance` field. Example: `{ "type": "about:blank", "title": "Not Found", "status": 404, "detail": "Person with ID 99 was not found." }`. The `detail` message comes from `PersonNotFoundException.Message` (already includes the ID).

### Validation Error Structure (400)
- **D-04:** **400 Problem Details shape** — `errors` extension field as a **dictionary keyed by property name**, values as arrays of error message strings. Mirrors the default `[ApiController]` format. Example:
  ```json
  {
    "type": "about:blank",
    "title": "Validation Failed",
    "status": 400,
    "detail": "One or more validation errors occurred.",
    "errors": {
      "FirstName": ["must not be empty"],
      "DateOfBirth": ["cannot be in the future"]
    }
  }
  ```
  `ValidationExceptionHandler` builds this from `ValidationException.Errors` grouped by `PropertyName`.
- **D-05:** `detail` field of 400 = `"One or more validation errors occurred."` — consistent with the default [ApiController] behavior familiar to .NET consumers.

### PATCH Endpoint Pattern
- **D-06:** Package: **`Microsoft.AspNetCore.JsonPatch.SystemTextJson`** (pre-roadmap decision from STATE.md). Not the Newtonsoft-based `Microsoft.AspNetCore.JsonPatch`.
- **D-07:** Controller action signature:
  ```csharp
  [HttpPatch("{id:int}")]
  public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)
  ```
  `[FromBody]` is **explicit** — avoids binding ambiguity and makes intent clear.
- **D-08:** PATCH applies to a **fresh empty DTO** (`new UpdatePersonDto(null, null, null, null)`), not a DTO preloaded with the person's current values. Only fields in the patch document are populated; the handler's null-check logic applies only those fields to the domain entity. This is correct PATCH semantics.
- **D-09:** After `patchDoc.ApplyTo(dto, ModelState)`, check `if (!ModelState.IsValid)` and return `ValidationProblem(ModelState)` immediately. The FluentValidation pipeline in `ValidationBehavior` handles content validation on the dispatched `PatchPersonCommand`.

### Claude's Discretion
- `PersonsAPI.Api.csproj` package references (exact versions of `Mediator.SourceGenerator`, `Microsoft.AspNetCore.JsonPatch.SystemTextJson`, `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore` — check CLAUDE.md for versions)
- Middleware pipeline order in Program.cs (standard ASP.NET Core conventions: UseExceptionHandler → UseHttpsRedirection → UseAuthorization → MapControllers)
- Scalar configuration details (title, description)
- Controller base class (`ControllerBase`), route attribute (`[Route("api/[controller]")]`), `[ApiController]` attribute
- Whether to suppress the default 400 response from `[ApiController]` automatic model validation (not needed if PATCH validation is handled manually and other endpoints don't use ModelState)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Requirements & Architecture
- `.planning/REQUIREMENTS.md` §Error Handling (ERR-01, ERR-02, ERR-03), §API Documentation (DOC-01, DOC-02) — 5 requirements this phase satisfies
- `.planning/ROADMAP.md` §Phase 4 — goal, success criteria (4 observable criteria)
- `.planning/PROJECT.md` §Constraints — controllers only, no Minimal API, all-English code
- `CLAUDE.md` §Recommended Stack — exact package versions: Mediator 3.0.2, Scalar.AspNetCore 2.14.14, Microsoft.AspNetCore.OpenApi 10.0.8

### Prior Phase Decisions (MUST read before implementing)
- `.planning/phases/02-application-layer/02-CONTEXT.md` — **D-01** (PATCH: controller receives `JsonPatchDocument<UpdatePersonDto>`, dispatches `PatchPersonCommand(id, dto)`), **D-04** (`PersonNotFoundException` lives in Application, API catches it for 404), **D-07** (`PersonResponse.FromDomain()` static factory — no AutoMapper)
- `.planning/phases/03-infrastructure-layer/03-CONTEXT.md` — **D-04** (SeedAsync called from Program.cs before app.Run()), **D-06** (DataSeeder is NOT registered in DI — call directly as static extension)

### Existing Source (read to understand contracts)
- `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` — `AddApplication()` pattern + Phase 4 responsibilities documented in XML comment (AddMediator wiring, ValidationBehavior pipeline, exception handlers)
- `src/PersonsAPI.Application/Commands/PatchPersonCommand.cs` — `PatchPersonCommand(int Id, UpdatePersonDto Dto)` exact signature the controller must dispatch
- `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` — `record UpdatePersonDto(string? FirstName, ...)` — target type for `JsonPatchDocument<UpdatePersonDto>`
- `src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs` — `PersonId` property on the exception for building 404 detail message
- `src/PersonsAPI.Application/DTOs/PersonResponse.cs` — `PersonResponse.FromDomain(Person)` static factory (used by handlers, not the controller)
- `src/PersonsAPI.Infrastructure/Seeder/DataSeeder.cs` — `SeedAsync(this IServiceProvider)` extension method — called in Program.cs

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `AddApplication()` (`src/PersonsAPI.Application/ServiceCollectionExtensions.cs`) — registers validators. Program.cs calls this after `AddMediator()`.
- `AddInfrastructure()` (`src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs`) — registers DbContext + PersonRepository. Program.cs calls this independently.
- `DataSeeder.SeedAsync(IServiceProvider)` — static extension, called as `await app.Services.SeedAsync()` before `app.Run()`.
- All 6 command/query types already exist in the Application layer and are ready to dispatch.

### Established Patterns
- **`AddX(this IServiceCollection services)` DI extension** — established by Application and Infrastructure. Api layer follows the same convention if it needs its own registrations.
- **Exception contract (no Result<T>):** Handlers throw `PersonNotFoundException` (404) or let `DomainException` bubble (500). API layer catches by type via IExceptionHandler.
- **PersonResponse.FromDomain() mapping** — handlers return this already. Controller receives `PersonResponse` from mediator dispatch and returns it directly.
- **Project naming:** `PersonsAPI.Api` in `src/PersonsAPI.Api/`.

### Integration Points
- `PersonsAPI.Api.csproj` references `PersonsAPI.Application` and `PersonsAPI.Infrastructure`. Domain comes transitively.
- `Mediator.SourceGenerator` is installed **only** in the Api project (source-generator runs against it). Application installs only `Mediator.Abstractions`.
- `Program.cs` calls: `AddMediator(options => options.PipelineBehaviors = [typeof(ValidationBehavior<,>)])` → `AddApplication()` → `AddInfrastructure()`.

</code_context>

<specifics>
## Specific Ideas

- PATCH action example pattern:
  ```csharp
  [HttpPatch("{id:int}")]
  public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)
  {
      var dto = new UpdatePersonDto(null, null, null, null);
      patchDoc.ApplyTo(dto, ModelState);
      if (!ModelState.IsValid)
          return ValidationProblem(ModelState);
      var result = await _mediator.Send(new PatchPersonCommand(id, dto));
      return Ok(result);
  }
  ```
- 400 Problem Details errors dictionary built from FluentValidation:
  ```csharp
  errors = exception.Errors
      .GroupBy(e => e.PropertyName)
      .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
  ```
- Scalar at `/scalar` (locked by ROADMAP success criteria #4).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 4-API Layer*
*Context gathered: 2026-05-31*
