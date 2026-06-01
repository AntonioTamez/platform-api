# Phase 4: API Layer - Pattern Map

**Mapped:** 2026-05-31
**Files analyzed:** 8 (new/modified files)
**Analogs found:** 8 / 8

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `src/PersonsAPI.Api/PersonsAPI.Api.csproj` | config | n/a | `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` | exact (same sdk, same property group pattern) |
| `src/PersonsAPI.Api/Program.cs` | config (composition root) | request-response | `src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs` + `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` | role-match (same AddX chaining idiom) |
| `src/PersonsAPI.Api/Controllers/PersonsController.cs` | controller | request-response | `src/PersonsAPI.Application/Commands/CreatePersonCommand.cs` (handler dispatch pattern) | partial (no existing controller; handler command-dispatch pattern is the closest) |
| `src/PersonsAPI.Api/ExceptionHandlers/PersonNotFoundExceptionHandler.cs` | middleware | request-response | `src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs` + `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` | partial (exception types defined; handler structure is new) |
| `src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs` | middleware | request-response | `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` | partial (ValidationException thrown here; handler is new) |
| `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` | model (DTO) | transform | `src/PersonsAPI.Application/DTOs/UpdatePersonRequest.cs` | exact (same namespace, same four fields — only mutability changes) |
| `PersonsAPI.sln` | config | n/a | `PersonsAPI.sln` (existing entries for Infrastructure project) | exact (follow same nested-folder pattern) |
| `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj` | config | n/a | `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` | exact (same test project structure) |

---

## Pattern Assignments

### `src/PersonsAPI.Api/PersonsAPI.Api.csproj` (config)

**Analog:** `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj`

**Csproj structure pattern** (lines 1-18 of analog):
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\PersonsAPI.Application\PersonsAPI.Application.csproj" />
    <ProjectReference Include="..\PersonsAPI.Infrastructure\PersonsAPI.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Mediator.SourceGenerator" Version="3.0.2" />
    <PackageReference Include="Microsoft.AspNetCore.JsonPatch.SystemTextJson" Version="10.0.8" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
    <PackageReference Include="Scalar.AspNetCore" Version="2.14.14" />
  </ItemGroup>

</Project>
```

**Key differences from Infrastructure .csproj:**
- SDK is `Microsoft.NET.Sdk.Web` (not `Microsoft.NET.Sdk`) — required for ASP.NET Core host
- References both `PersonsAPI.Application` AND `PersonsAPI.Infrastructure` (Domain is transitive)
- `Mediator.SourceGenerator` installs HERE and ONLY here (source generator scope — RESEARCH.md Pitfall 4)
- No `<IsPackable>false</IsPackable>` — that is only for test projects (see test .csproj analog)

---

### `src/PersonsAPI.Api/Program.cs` (config, composition root)

**Analogs:** `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` (lines 58-71) and `src/PersonsAPI.Infrastructure/ServiceCollectionExtensions.cs` (lines 53-62) — both show the `AddX` chaining idiom and XML-documented responsibilities.

**AddApplication() chaining pattern** (Application ServiceCollectionExtensions.cs lines 58-71):
```csharp
public static IServiceCollection AddApplication(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(
        typeof(IApplicationMarker).Assembly,
        ServiceLifetime.Scoped);

    return services;
}
```

**AddInfrastructure() chaining pattern** (Infrastructure ServiceCollectionExtensions.cs lines 53-62):
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services)
{
    services.AddDbContext<PersonDbContext>(options =>
        options.UseInMemoryDatabase("PersonsDb"));

    services.AddScoped<IPersonRepository, PersonRepository>();

    return services;
}
```

**DataSeeder startup call pattern** (DataSeeder.cs lines 63-76):
```csharp
// Static extension on IServiceProvider — called from Program.cs, not DI-registered
public static async Task SeedAsync(this IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<PersonDbContext>();
    if (context.Persons.Any()) return;
    // ... seed records
    await context.SaveChangesAsync();
}
```

**Program.cs composition pattern** (derived from CONTEXT.md D-01, D-02, Phase 3 D-04/D-06):
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();  // D-01: NotFound first
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();      // D-01: Validation second
builder.Services.AddOpenApi();                                            // DOC-01
builder.Services.AddMediator(options =>
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]);        // SourceGenerator in this project
builder.Services.AddApplication();                                        // FluentValidation validators
builder.Services.AddInfrastructure();                                     // DbContext + Repository

var app = builder.Build();

app.UseExceptionHandler();          // NO argument — activates IExceptionHandler chain (Pitfall 2)
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapOpenApi();                   // /openapi/v1.json
app.MapScalarApiReference();        // /scalar — Pitfall 8: MapScalar not UseScalar

await app.Services.SeedAsync();     // BEFORE RunAsync (Phase 3 D-04, Pitfall 5)
await app.RunAsync();
```

**Namespace pattern** (follow existing layers — Application ServiceCollectionExtensions.cs line 5):
```csharp
namespace PersonsAPI.Api;
```

---

### `src/PersonsAPI.Api/Controllers/PersonsController.cs` (controller, request-response)

**Closest analogs:** `src/PersonsAPI.Application/Commands/CreatePersonCommand.cs` (handler dispatch pattern, lines 52-69) and `src/PersonsAPI.Application/Commands/DeletePersonCommand.cs` (Unit return, lines 17-32). No existing controller in codebase — controller pattern derived from CONTEXT.md and RESEARCH.md.

**Command dispatch pattern used by handlers** (CreatePersonCommand.cs lines 52-69):
```csharp
// Handler receives command, delegates to repository, returns PersonResponse
public async ValueTask<PersonResponse> Handle(
    CreatePersonCommand command,
    CancellationToken cancellationToken)
{
    var person = Person.Create(...);
    await repository.AddAsync(person, cancellationToken);
    return PersonResponse.FromDomain(person);
}
```

**PersonResponse shape** (PersonResponse.cs lines 10-16):
```csharp
// Controller receives this from mediator dispatch — return directly
public record PersonResponse(
    int Id,
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth,
    int Age)
```

**PersonsController full skeleton pattern** (from CONTEXT.md D-07, D-08, D-09; RESEARCH.md Pattern 5):
```csharp
using Mediator;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using PersonsAPI.Application.Commands;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Queries;

namespace PersonsAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PersonsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await mediator.Send(new GetAllPersonsQuery());
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await mediator.Send(new GetPersonByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonRequest request)
    {
        var result = await mediator.Send(new CreatePersonCommand(
            request.FirstName,
            request.PaternalLastName,
            request.MaternalLastName,
            request.DateOfBirth));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);  // 201, not 200 — Pitfall 7
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonRequest request)
    {
        var result = await mediator.Send(new UpdatePersonCommand(id, request));
        return Ok(result);
    }

    [HttpPatch("{id:int}")]                                           // D-07
    public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc)
    {
        var dto = new UpdatePersonDto();                              // D-08: fresh empty DTO, all props null
        patchDoc.ApplyTo(dto, ModelState);
        if (!ModelState.IsValid)                                     // D-09: JsonPatch structural errors only
            return ValidationProblem(ModelState);
        var result = await mediator.Send(new PatchPersonCommand(id, dto));
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        await mediator.Send(new DeletePersonCommand(id));
        return NoContent();                                          // 204 for successful delete
    }
}
```

**Primary constructor injection pattern** (mirrors ValidationBehavior.cs line 33, PersonRepository uses same):
```csharp
// Primary constructor — C# 14 pattern used throughout codebase
public sealed class PersonsController(IMediator mediator) : ControllerBase
```

---

### `src/PersonsAPI.Api/ExceptionHandlers/PersonNotFoundExceptionHandler.cs` (middleware, request-response)

**Analog:** `src/PersonsAPI.Application/Exceptions/PersonNotFoundException.cs` — provides the exception type and `PersonId` property / `Message` string this handler consumes.

**Exception contract** (PersonNotFoundException.cs lines 9-25):
```csharp
public sealed class PersonNotFoundException : Exception
{
    public int PersonId { get; }                // structural property — use for clarity in 404 detail

    public PersonNotFoundException(int id)
        : base($"Person with ID {id} was not found.")  // Message already formatted — use directly
    {
        PersonId = id;
    }
}
```

**IExceptionHandler implementation pattern** (from RESEARCH.md Pattern 1):
```csharp
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PersonsAPI.Application.Exceptions;

namespace PersonsAPI.Api.ExceptionHandlers;

public sealed class PersonNotFoundExceptionHandler(
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not PersonNotFoundException notFound)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = notFound.Message  // D-03: "Person with ID {id} was not found."
            }
        });
    }
}
```

**Registration order in Program.cs** (D-01 — NotFound handler registered FIRST):
```csharp
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();  // first
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();      // second
```

---

### `src/PersonsAPI.Api/ExceptionHandlers/ValidationExceptionHandler.cs` (middleware, request-response)

**Analog:** `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` — provides the exact `FluentValidation.ValidationException` type thrown and the `Errors` collection structure this handler groups.

**ValidationException throw site** (ValidationBehavior.cs lines 81-82):
```csharp
// Exact type Phase 4 catches — do NOT use a custom exception class
if (failures.Count > 0)
    throw new ValidationException(failures);
```

**Errors collection structure** (ValidationBehavior.cs lines 69-77):
```csharp
// failures is IList<ValidationFailure>; each has .PropertyName and .ErrorMessage
var results = await Task.WhenAll(
    validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

var failures = results
    .SelectMany(r => r.Errors)
    .Where(e => e is not null)
    .ToList();
```

**ValidationExceptionHandler implementation pattern** (from CONTEXT.md D-04, D-05; RESEARCH.md Pattern 2):
```csharp
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PersonsAPI.Api.ExceptionHandlers;

public sealed class ValidationExceptionHandler(
    IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        // D-04: group by PropertyName, values as string arrays
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred."  // D-05
        };
        problemDetails.Extensions["errors"] = errors;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
```

---

### `src/PersonsAPI.Application/DTOs/UpdatePersonDto.cs` (model, transform) — MODIFY

**Analog:** `src/PersonsAPI.Application/DTOs/UpdatePersonRequest.cs` (exact same four fields, same namespace, same record style — only mutability differs).

**Current file** (UpdatePersonDto.cs lines 1-13 — BROKEN for JsonPatch):
```csharp
namespace PersonsAPI.Application.DTOs;

public record UpdatePersonDto(
    string? FirstName,
    string? PaternalLastName,
    string? MaternalLastName,
    DateOnly? DateOfBirth);
```

**UpdatePersonRequest analog** (UpdatePersonRequest.cs lines 1-8 — shows field names/types):
```csharp
namespace PersonsAPI.Application.DTOs;

public record UpdatePersonRequest(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth);
```

**Required replacement** (RESEARCH.md Critical Fix — mutable class for JsonPatchDocument.ApplyTo()):
```csharp
namespace PersonsAPI.Application.DTOs;

/// <summary>
/// Mutable DTO for PATCH /api/persons/{id}.
/// Must use { get; set; } properties (not init-only) because
/// JsonPatchDocument&lt;T&gt;.ApplyTo() mutates the target object in place.
/// </summary>
public class UpdatePersonDto
{
    public string? FirstName { get; set; }
    public string? PaternalLastName { get; set; }
    public string? MaternalLastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
```

**Impact verification — zero downstream changes needed:**
- `PatchPersonCommand(int Id, UpdatePersonDto Dto)` (PatchPersonCommand.cs line 13) — accepts class reference, unchanged
- `PatchPersonCommandValidator` (PatchPersonCommand.cs lines 19-57) — reads `x.Dto.FirstName` etc., property access unchanged
- `PatchPersonHandler` (PatchPersonCommand.cs lines 65-92) — reads `dto.FirstName`, `dto.PaternalLastName`, etc., unchanged

---

### `PersonsAPI.sln` (config) — MODIFY

**Analog:** Existing solution file entries for Infrastructure project (lines 1-18 of sln).

**Existing project entry pattern** (sln, Infrastructure project entry):
```
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "PersonsAPI.Infrastructure", "src\PersonsAPI.Infrastructure\PersonsAPI.Infrastructure.csproj", "{A1750BDC-D57F-4011-AF08-6561F4CCC597}"
EndProject
```

**Pattern to follow for Api project entry:**
- Same GUID type `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` (C# project)
- Path format: `src\PersonsAPI.Api\PersonsAPI.Api.csproj` (backslash, relative from sln root)
- Nested under the `src` solution folder (`{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`)
- New GUID generated for this project
- Test project `PersonsAPI.Api.Tests` follows the same pattern under the `tests` solution folder (`{0AB3BF05-4346-4AA6-1389-037BE0695223}`)

**Use `dotnet sln add` CLI** — safer than manual edits to avoid GUID conflicts:
```bash
dotnet sln PersonsAPI.sln add src/PersonsAPI.Api/PersonsAPI.Api.csproj --solution-folder src
dotnet sln PersonsAPI.sln add tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj --solution-folder tests
```

---

### `tests/PersonsAPI.Api.Tests/PersonsAPI.Api.Tests.csproj` (config, test)

**Analog:** `tests/PersonsAPI.Infrastructure.Tests/PersonsAPI.Infrastructure.Tests.csproj` (lines 1-27 — exact structure).

**Test csproj pattern** (Infrastructure.Tests.csproj lines 1-27):
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\PersonsAPI.Api\PersonsAPI.Api.csproj" />
  </ItemGroup>

</Project>
```

**Note:** Api.Tests will need `Microsoft.AspNetCore.Mvc.Testing` for integration tests (WebApplicationFactory pattern). Add if controller integration tests are planned:
```xml
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
```

---

## Shared Patterns

### Primary Constructor (DI Injection)

**Source:** `src/PersonsAPI.Application/Behaviors/ValidationBehavior.cs` line 33, `src/PersonsAPI.Infrastructure/Repositories/PersonRepository.cs`
**Apply to:** `PersonsController`, `PersonNotFoundExceptionHandler`, `ValidationExceptionHandler`

```csharp
// Pattern: primary constructor, no field declarations, sealed class
public sealed class PersonsController(IMediator mediator) : ControllerBase
public sealed class PersonNotFoundExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
```

### XML Documentation Comment Style

**Source:** All Application and Infrastructure source files (e.g., `src/PersonsAPI.Application/ServiceCollectionExtensions.cs` lines 7-22)
**Apply to:** All new files

```csharp
/// <summary>
/// One-line summary.
///
/// <para><b>Decision reference:</b> explanation with decision ID.</para>
/// </summary>
```

### Namespace Convention

**Source:** All existing source files
**Apply to:** All new files

```csharp
// Pattern: PersonsAPI.<Layer>[.<Subfolder>]
namespace PersonsAPI.Api;
namespace PersonsAPI.Api.Controllers;
namespace PersonsAPI.Api.ExceptionHandlers;
```

### `sealed` Class Modifier

**Source:** Every handler and behavior in the codebase (e.g., `ValidationBehavior`, `CreatePersonHandler`, `PersonNotFoundExceptionHandler`)
**Apply to:** `PersonsController`, both exception handlers
**Reason:** Prevents unintended inheritance; all concrete implementations are `sealed` in this codebase.

### Async Method Naming

**Source:** `DataSeeder.cs` line 63, all handler `Handle` methods
**Apply to:** All async controller actions, exception handler `TryHandleAsync`

```csharp
// Pattern: async Task<T> or async ValueTask<bool> — no "Async" suffix on controller actions
// Exception handlers follow IExceptionHandler interface name: TryHandleAsync
public async Task<IActionResult> GetAll()        // controller action — no suffix
public async ValueTask<bool> TryHandleAsync(...)  // follows interface contract
```

---

## No Analog Found

All files have at least a partial analog in the codebase. The controller itself (`PersonsController.cs`) has no direct analog (no controllers exist yet), but the pattern is fully specified in CONTEXT.md and RESEARCH.md.

| File | Role | Data Flow | Reason |
|---|---|---|---|
| `src/PersonsAPI.Api/Controllers/PersonsController.cs` | controller | request-response | No existing controllers; Application command handlers serve as the closest behavioral analog |

---

## Metadata

**Analog search scope:** `src/PersonsAPI.Application/`, `src/PersonsAPI.Infrastructure/`, `src/PersonsAPI.Domain/`, `tests/`
**Files scanned:** 25 source files
**Pattern extraction date:** 2026-05-31
