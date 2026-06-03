# Phase 5: Observability - Pattern Map

**Mapped:** 2026-06-02
**Files analyzed:** 4 (3 modified source files + 1 modified test file)
**Analogs found:** 4 / 4

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `src/PersonsAPI.Api/Program.cs` | config/entrypoint | request-response | `src/PersonsAPI.Api/Program.cs` (self — extend in place) | exact |
| `src/PersonsAPI.Api/PersonsAPI.Api.csproj` | config | n/a | `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` | exact |
| `src/PersonsAPI.Api/appsettings.json` | config | n/a | `src/PersonsAPI.Api/appsettings.Development.json` (self-family) | exact |
| `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` | test-infrastructure | request-response | `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` (self — extend in place) | exact |

---

## Pattern Assignments

### `src/PersonsAPI.Api/Program.cs` — extend in place

**Analog:** Same file — `src/PersonsAPI.Api/Program.cs` (lines 1–39 already in context)

**Existing imports pattern** (lines 1–9):
```csharp
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Api.ExceptionHandlers;
using PersonsAPI.Application;
using PersonsAPI.Application.Behaviors;
using PersonsAPI.Infrastructure;
using PersonsAPI.Infrastructure.Seeder;
using Scalar.AspNetCore;
```
New `using` statements to prepend:
```csharp
using Serilog;
using Serilog.Formatting.Compact;
```

**Services registration block pattern** (lines 12–23) — `builder.Services.Add*()` calls in order:
```csharp
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddMediator(options => { ... });
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
```
`AddHealthChecks()` follows the same pattern — append as one more `builder.Services.Add*()` call after `AddInfrastructure()`.

**Host configuration placement** — `builder.Host.*` calls must precede `builder.Build()`. The `UseSerilog()` lambda goes here:
```csharp
// Pattern: configure builder.Host before builder.Build()
// (no existing builder.Host call in the file; this is the first one)
builder.Host.UseSerilog((ctx, lc) =>
    lc.MinimumLevel.Information()
      .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
      .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
      .WriteTo.Console(new CompactJsonFormatter()));
```

**Middleware pipeline pattern** (lines 27–33) — `app.Map*()` / `app.Use*()` calls in order:
```csharp
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();
```
`MapHealthChecks("/health")` follows the same `app.Map*()` convention already used for `MapOpenApi` and `MapScalarApiReference`. Insert after `MapScalarApiReference()` and before the `SeedAsync` call.

**Post-build sequence** (lines 33–34):
```csharp
await app.Services.SeedAsync();
await app.RunAsync();
```
Placement rule: `MapHealthChecks` goes before `SeedAsync`, because `Map*` calls configure routes, and all routing must be registered before `RunAsync`.

**`public partial class Program` declaration** (line 39):
```csharp
public partial class Program { }
```
This line must remain intact — it is the test host anchor for `WebApplicationFactory<Program>`.

---

### `src/PersonsAPI.Api/PersonsAPI.Api.csproj` — add 2 PackageReferences

**Analog:** `src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj` (lines 1–19)

**Existing `<ItemGroup>` PackageReference block pattern** (Api.csproj lines 16–21):
```xml
<ItemGroup>
  <PackageReference Include="Mediator.SourceGenerator" Version="3.0.2" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  <PackageReference Include="Microsoft.AspNetCore.JsonPatch.SystemTextJson" Version="10.0.8" />
  <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
  <PackageReference Include="Scalar.AspNetCore" Version="2.14.14" />
</ItemGroup>
```
Append two new `<PackageReference>` entries to this same `<ItemGroup>` (alphabetical order by package name is the existing convention):
```xml
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
```
Version note: CONTEXT.md does not pin exact Serilog versions. Use the latest stable compatible with .NET 10 at time of implementation. `Serilog.AspNetCore` 9.x and `Serilog.Formatting.Compact` 3.x are the current stable series.

**Infrastructure csproj analog** (single-package pattern for reference, lines 8–10):
```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
```
One entry per line, no trailing attributes beyond `Include` and `Version` for runtime packages (the `OutputItemType`/`ReferenceOutputAssembly` attributes apply only to source-generator packages like `Mediator.SourceGenerator`).

---

### `src/PersonsAPI.Api/appsettings.json` — simplify or replace Logging section

**Analog:** `src/PersonsAPI.Api/appsettings.Development.json` (lines 1–9, full file)

**Current state** (appsettings.json lines 1–9):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

**Current Development override** (appsettings.Development.json lines 1–9):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

Per decision D-05, Serilog is configured programmatically in `Program.cs` and does not read from the `"Logging"` section. The `"Logging"` block in `appsettings.json` becomes dead configuration — the planner's discretion (noted in CONTEXT.md `## Claude's Discretion`) is whether to remove it or leave it harmlessly in place. Pattern guidance: keep `"AllowedHosts": "*"` intact; removing `"Logging"` is the cleaner outcome since it no longer has effect and could mislead future readers.

Minimal post-change form:
```json
{
  "AllowedHosts": "*"
}
```

---

### `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` — extend in place

**Analog:** Same file — `tests/PersonsAPI.Api.Tests/ResetableApiFactory.cs` (lines 1–43, full file)

**Existing `ConfigureWebHost` override pattern** (lines 23–41):
```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        var toRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<PersonDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(PersonDbContext))
            .ToList();
        foreach (var d in toRemove)
            services.Remove(d);

        services.AddDbContext<PersonDbContext>(opt =>
            opt.UseInMemoryDatabase(_databaseName));
    });
}
```

The Serilog suppression follows the exact same `builder.*` call pattern already established inside `ConfigureWebHost`. Two implementation options the planner can choose between:

**Option A — `builder.UseSerilog()` override (Serilog-native, replaces the logger entirely):**
```csharp
// Add before or after builder.ConfigureServices(...)
builder.UseSerilog((ctx, lc) =>
    lc.MinimumLevel.Fatal()); // nothing below Fatal reaches the sink
```
This requires `using Serilog;` at the top of the file.

**Option B — `builder.ConfigureLogging()` override (framework-native, no extra using):**
```csharp
// Add before or after builder.ConfigureServices(...)
builder.ConfigureLogging(logging =>
{
    logging.ClearProviders();
    logging.SetMinimumLevel(LogLevel.None);
});
```
This uses `Microsoft.Extensions.Logging.LogLevel` already transitively available from `Microsoft.AspNetCore.Mvc.Testing`.

Per CONTEXT.md D-11: either is valid ("suppress or silence Serilog"). Option B has no new `using` requirement. Option A is more targeted — it explicitly reconfigures the same Serilog pipeline added in `Program.cs`. The planner should pick Option A for consistency with the Serilog-native approach used in `Program.cs`.

**Existing `using` block to extend** (lines 1–6):
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Infrastructure.Persistence;
```
If Option A is chosen, add `using Serilog;` here.

---

## Shared Patterns

### `builder.Services.Add*()` Registration Style
**Source:** `src/PersonsAPI.Api/Program.cs` lines 12–23
**Apply to:** The `AddHealthChecks()` insertion in `Program.cs`

One call per line, no wrapping braces unless a lambda options block is needed (as with `AddMediator`). `AddHealthChecks()` takes no lambda for the basic case — single-line form:
```csharp
builder.Services.AddHealthChecks();
```

### `app.Map*()` Middleware Pipeline Style
**Source:** `src/PersonsAPI.Api/Program.cs` lines 29–32
**Apply to:** The `MapHealthChecks("/health")` insertion in `Program.cs`

One call per line, no trailing configuration lambda for the basic case. The order is: exception handler, HTTPS redirect, controllers, OpenAPI, Scalar, health (infrastructure endpoints grouped at the end):
```csharp
app.MapHealthChecks("/health");
```

### `<PackageReference>` csproj Entry Style
**Source:** `src/PersonsAPI.Api/PersonsAPI.Api.csproj` lines 16–21
**Apply to:** The two new Serilog package entries

Runtime packages use only `Include` and `Version` attributes, no extra MSBuild metadata:
```xml
<PackageReference Include="PackageName" Version="x.y.z" />
```

---

## No Analog Found

No files in this phase are entirely new — all four targets are modifications to existing files. No "no analog" entries apply.

---

## Metadata

**Analog search scope:** `src/PersonsAPI.Api/`, `src/PersonsAPI.Infrastructure/`, `tests/PersonsAPI.Api.Tests/`
**Files scanned:** 8 (4 target files + 4 supporting analogs)
**Pattern extraction date:** 2026-06-02
