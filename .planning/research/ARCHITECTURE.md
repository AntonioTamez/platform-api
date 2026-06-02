# Architecture: Cloud Deployment Integration

**Domain:** .NET 10 Web API — Docker + GitHub Actions + Google Cloud Run
**Researched:** 2026-06-01
**Confidence:** HIGH

---

## System Overview

The v2.0 milestone adds five new infrastructure concerns on top of the existing four-project Clean
Architecture solution. None of these concerns touch Domain, Application, or Infrastructure layers.
They all attach at the outermost ring (Api layer and repository root).

```
┌─────────────────────────────────────────────────────────────────────┐
│                      REPOSITORY ROOT (new files)                    │
│  Dockerfile  docker-compose.yml  .dockerignore  .github/workflows/  │
├─────────────────────────────────────────────────────────────────────┤
│                   PersonsAPI.Api  (modified files)                  │
│  Program.cs    appsettings.json    appsettings.Production.json      │
│  + AddHealthChecks / MapHealthChecks                                │
│  + UseSerilog + builder.Host.UseSerilog(...)                        │
├─────────────────────────────────────────────────────────────────────┤
│          PersonsAPI.Api.csproj  (new package references)            │
│  Serilog.AspNetCore   Serilog.Sinks.Console                        │
├─────────────────────────────────────────────────────────────────────┤
│  PersonsAPI.Application  │  PersonsAPI.Infrastructure               │
│  PersonsAPI.Domain       │  (UNCHANGED in v2.0)                     │
└─────────────────────────────────────────────────────────────────────┘
```

Key architectural principle: all cloud-readiness changes are confined to the composition root
(`Program.cs`), configuration files (`appsettings.json`), and new files at the repository root.
No domain, application, or infrastructure code changes.

---

## New Files vs. Modified Files

### New Files (do not exist yet)

| File | Location | Purpose |
|------|----------|---------|
| `Dockerfile` | repository root (`/`) | Multi-stage build: SDK build stage + aspnet runtime stage |
| `docker-compose.yml` | repository root (`/`) | Local development with container-equivalent env |
| `.dockerignore` | repository root (`/`) | Exclude `bin/`, `obj/`, `.planning/`, `tests/`, `.git/` from build context |
| `.github/workflows/deploy.yml` | `.github/workflows/` | CI/CD pipeline: build → test → push → deploy |
| `src/PersonsAPI.Api/appsettings.Production.json` | `src/PersonsAPI.Api/` | Production-specific config (Serilog JSON sink, no dev middleware) |

### Modified Files (already exist)

| File | What Changes |
|------|-------------|
| `src/PersonsAPI.Api/Program.cs` | Add `AddHealthChecks()`, `MapHealthChecks("/health")`, `UseSerilog()` |
| `src/PersonsAPI.Api/appsettings.json` | Add `Serilog` section with console sink (human-readable for dev) |
| `src/PersonsAPI.Api/PersonsAPI.Api.csproj` | Add `Serilog.AspNetCore` and `Serilog.Sinks.Console` PackageReferences |

### Files That Do NOT Change

All files in `PersonsAPI.Domain/`, `PersonsAPI.Application/`, `PersonsAPI.Infrastructure/`,
and all test projects remain untouched. v2.0 is purely an infrastructure/deployment concern.

---

## Dockerfile Integration

### Location and Build Context

The `Dockerfile` lives at the repository root. The `docker build` command is run from the
repository root with `.` as the build context. This is the correct placement for a solution with
`PersonsAPI.sln` at the root — it lets `COPY` commands reach all four `src/` projects and the
solution file in a single build context.

**Do not place the Dockerfile inside `src/PersonsAPI.Api/`** — that directory does not contain the
solution file or the other three project directories, so `dotnet restore PersonsAPI.sln` would fail.

### Multi-Stage Build: COPY Order for Four Projects

The key insight for multi-project solutions is: copy all `.csproj` files first (one COPY per
project), then run `dotnet restore`. This makes dependency restore a cacheable Docker layer — if
no `.csproj` files change, the restore layer is reused on every subsequent build. Only then copy
full source.

The four source projects and their relative paths from the repository root:

```
src/PersonsAPI.Domain/PersonsAPI.Domain.csproj
src/PersonsAPI.Application/PersonsAPI.Application.csproj
src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj
src/PersonsAPI.Api/PersonsAPI.Api.csproj
```

Test projects are excluded from the production image — only `src/` is published, not `tests/`.

### Recommended Dockerfile

```dockerfile
# syntax=docker/dockerfile:1

# Stage 1: build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution file
COPY PersonsAPI.sln .

# Copy each project file individually for layer-cached restore
COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj             src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj   src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                   src/PersonsAPI.Api/

# Restore using the solution — resolves all inter-project references
RUN dotnet restore PersonsAPI.sln

# Copy full source (only src/ — not tests/)
COPY src/ src/

# Publish the Api project
RUN dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj \
    -c Release -o /app/publish --no-restore

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cloud Run injects PORT; ASP.NET Core reads ASPNETCORE_HTTP_PORTS or ASPNETCORE_URLS
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}

EXPOSE 8080

ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

**Why `dotnet restore PersonsAPI.sln` (not `dotnet restore src/PersonsAPI.Api/...`):**
The solution-level restore resolves all ProjectReference dependencies in one pass. Restoring only
the Api `.csproj` would also work (it transitively pulls all dependencies), but using the solution
file is more explicit and matches what CI tooling runs.

**Why copy test projects are excluded:**
Test assemblies (xUnit, test adapters) would inflate the image by ~50 MB and have no function at
runtime. The build stage produces a self-contained publish output from `src/` only.

### .dockerignore

Prevents unnecessary files from entering the build context, which speeds up `docker build` and
avoids accidentally embedding secrets or planning artifacts in the image:

```
**/bin/
**/obj/
**/.git/
.planning/
tests/
*.md
.github/
```

---

## Cloud Run Port Binding

### How Cloud Run Injects PORT

Google Cloud Run injects a `PORT` environment variable into every container at startup. The
container must listen on that port — Cloud Run's load balancer routes to it. The default value
Cloud Run injects is `8080`, but containers must not hardcode this; they must read `PORT`.

If the container does not accept connections on the PORT Cloud Run specified, health checks fail
and the revision is marked unhealthy. The deployment rolls back.

### How ASP.NET Core 10 Reads PORT

ASP.NET Core resolves its listening URL from (in priority order):
1. `ASPNETCORE_URLS` environment variable — full URL format (`http://+:8080`)
2. `ASPNETCORE_HTTP_PORTS` environment variable — port number only (`8080`)
3. Default: `http://localhost:5000;https://localhost:5001` (development only)

In a container, `ASPNETCORE_URLS` is the correct variable to set. The value must include the
protocol prefix:

```
ASPNETCORE_URLS=http://+:8080
```

The `+` wildcard binds to all interfaces (required in a container — `localhost` would refuse
external connections).

**Cloud Run pattern — read PORT dynamically:**

Option A (Dockerfile ENV with shell-form default):
```dockerfile
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
```
This sets a default at image build time. At runtime, Cloud Run overrides `PORT`, and the container
startup script expands it. However, Docker `ENV` does not support variable expansion with a
`${VAR:-default}` syntax in all runtimes — this depends on the shell.

Option B (Program.cs — recommended, most explicit):
```csharp
// In Program.cs, before WebApplication.CreateBuilder
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");
```
This is the most reliable pattern for Cloud Run + ASP.NET Core. It reads `PORT` directly from the
environment at startup and configures Kestrel programmatically. No shell expansion required.

**Recommended approach: Option B** — add `UseUrls` from PORT in `Program.cs`. This is explicit,
testable, and works identically in docker-compose, Cloud Run, and local runs with `PORT=5000`.

**HTTPS redirection:** Remove or skip `app.UseHttpsRedirection()` in the container. Cloud Run
handles TLS termination at the load balancer. The container only needs HTTP. In `Program.cs`,
guard it:

```csharp
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();
```

### Environment Variables Cloud Run Sets

| Variable | Value | Notes |
|----------|-------|-------|
| `PORT` | `8080` (default, configurable) | Container must listen here |
| `K_SERVICE` | Cloud Run service name | Can detect Cloud Run environment |
| `K_REVISION` | Revision name | Useful for log correlation |
| `K_CONFIGURATION` | Configuration name | |
| `ASPNETCORE_ENVIRONMENT` | Set by deployment workflow | Set to `Production` in gcloud deploy |

The `ASPNETCORE_ENVIRONMENT=Production` variable must be set explicitly in the deployment
command or Cloud Run service configuration — Cloud Run does not set it automatically.

---

## Health Check Integration in Program.cs

### Registration Pattern

ASP.NET Core's built-in health check middleware requires no NuGet package — it ships with the
framework. The registration is two lines in `Program.cs`:

```csharp
// In the services section (before builder.Build())
builder.Services.AddHealthChecks();

// In the middleware section (after builder.Build())
app.MapHealthChecks("/health");
```

### Insertion Point in Existing Program.cs

Current `Program.cs` structure with insertion points marked:

```csharp
var builder = WebApplication.CreateBuilder(args);

// === PORT BINDING (new) ===
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");

// === SERILOG (new — before other services) ===
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddMediator(options => { ... });
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// === HEALTH CHECKS (new) ===
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

// === HTTPS GUARD (modified) ===
if (!app.Environment.IsProduction())
    app.UseHttpsRedirection();

app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();

// === HEALTH ENDPOINT (new) ===
app.MapHealthChecks("/health");

await app.Services.SeedAsync();
await app.RunAsync();
```

### What `/health` Returns

- **Response body:** `Healthy` (plain text string)
- **Content-Type:** `text/plain`
- **Status 200 OK** when healthy
- **Status 503 Service Unavailable** when unhealthy

Cloud Run's liveness and startup probes accept HTTP 200 as healthy. The `/health` endpoint
satisfies this out of the box with no custom response writer needed.

The EF Core InMemory provider has no real connection to check, so the default no-checks
`AddHealthChecks()` is correct for v2.0. If a real database is added in v2.1+, register
`AddDbContextCheck<AppDbContext>()` to probe it.

---

## Serilog Integration

### Packages Required (in PersonsAPI.Api.csproj)

```xml
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
```

`Serilog.AspNetCore` is the integration package. It includes `Serilog.Extensions.Hosting` and
the `UseSerilog` extension on `IHostBuilder`. `Serilog.Sinks.Console` provides the console
output sink. No additional sink package is needed for Google Cloud Logging — GCP reads stdout/stderr
from the container and routes it automatically. JSON format on stdout is all that is required.

### Program.cs Changes

Replace the default Microsoft logging with Serilog's bootstrap-then-configure pattern:

```csharp
// At the very top of Program.cs — bootstrap logger captures startup failures
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
    builder.WebHost.UseUrls($"http://+:{port}");

    // Replace default logging with Serilog, configured from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration)
                     .ReadFrom.Services(services));

    // ... rest of service registration ...

    var app = builder.Build();

    // ... rest of middleware ...

    await app.Services.SeedAsync();
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program { }
```

The bootstrap logger (`Log.Logger = new LoggerConfiguration()...CreateBootstrapLogger()`) captures
any exception that occurs before the host is built. Once the host is built, `UseSerilog` replaces
it with the fully configured Serilog logger that reads from `appsettings.json`.

**The `public partial class Program { }` declaration must remain** — it is required by
`WebApplicationFactory<Program>` in the integration tests. Place it after the top-level statements.

### appsettings.json Changes

Add a `Serilog` section. The console sink uses plain text format in development:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName"]
  },
  "AllowedHosts": "*"
}
```

Remove the existing `Logging` section — Serilog replaces it entirely. The `Microsoft.Extensions.Logging`
abstraction still works through Serilog's `ILogger<T>` bridge; nothing in existing code changes.

### appsettings.Production.json (new file)

In production (Cloud Run), override the console sink to emit JSON format — the format Google Cloud
Logging expects for structured log parsing:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Json.JsonFormatter, Serilog"
        }
      }
    ]
  }
}
```

When `ASPNETCORE_ENVIRONMENT=Production`, ASP.NET Core loads `appsettings.json` then merges
`appsettings.Production.json` on top. The Production override switches the console sink from
human-readable to JSON without any code change.

**Why not a Google-specific sink?** Google Cloud Logging automatically ingests stdout/stderr from
Cloud Run containers. JSON-formatted lines on stdout are parsed into structured log entries with
severity mapping. No `Serilog.Sinks.GoogleCloudLogging` package is needed — that sink is for
direct API ingestion, which adds latency and a service account dependency. Stdout JSON is the
recommended pattern for Cloud Run.

---

## docker-compose.yml Integration

docker-compose provides local development parity with the Cloud Run environment. It builds from the
same Dockerfile and injects the same environment variables Cloud Run sets:

```yaml
version: "3.9"

services:
  personsapi:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - PORT=8080
      - ASPNETCORE_ENVIRONMENT=Production
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 5s
```

Running `docker-compose up` locally builds the production image and starts the API on port 8080
with JSON logging — exactly what Cloud Run runs. The `healthcheck` mirrors Cloud Run's probe.

---

## GitHub Actions Workflow Integration

### File Location

```
.github/
  workflows/
    deploy.yml
```

The `.github/` directory does not yet exist in the repository. It must be created at the repository
root alongside `PersonsAPI.sln`.

### Pipeline Structure

The workflow has three jobs in sequence:

```
build-and-test → push-image → deploy-to-cloud-run
```

**Job 1 — build-and-test:**
- Checks out code
- Sets up .NET 10 SDK
- Runs `dotnet restore PersonsAPI.sln`
- Runs `dotnet build PersonsAPI.sln -c Release --no-restore`
- Runs `dotnet test PersonsAPI.sln --no-build` (all 4 test projects)

**Job 2 — push-image (depends on build-and-test):**
- Authenticates to GCP via Workload Identity Federation (preferred) or service account key
- Configures Docker for `REGION-docker.pkg.dev`
- Builds Docker image with `docker/build-push-action`
- Tags image: `REGION-docker.pkg.dev/PROJECT/REPOSITORY/persons-api:${{ github.sha }}`
- Pushes to Google Artifact Registry

**Job 3 — deploy-to-cloud-run (depends on push-image):**
- Uses `google-github-actions/deploy-cloudrun@v2`
- Sets `ASPNETCORE_ENVIRONMENT=Production`
- Sets Cloud Run service name, region, image URL
- Configures port (matches `PORT` default)

### Workflow YAML Skeleton

```yaml
name: Deploy to Cloud Run

on:
  push:
    branches: [master]

env:
  PROJECT_ID: ${{ secrets.GCP_PROJECT_ID }}
  REGION: us-central1
  REPOSITORY: persons-api
  SERVICE: persons-api
  IMAGE: us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/persons-api/persons-api

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'
      - run: dotnet restore PersonsAPI.sln
      - run: dotnet build PersonsAPI.sln -c Release --no-restore
      - run: dotnet test PersonsAPI.sln --no-build -c Release

  push-image:
    needs: build-and-test
    runs-on: ubuntu-latest
    permissions:
      contents: read
      id-token: write   # Required for Workload Identity Federation
    steps:
      - uses: actions/checkout@v4
      - uses: google-github-actions/auth@v2
        with:
          workload_identity_provider: ${{ secrets.WIF_PROVIDER }}
          service_account: ${{ secrets.WIF_SERVICE_ACCOUNT }}
      - uses: google-github-actions/setup-gcloud@v2
      - run: gcloud auth configure-docker ${{ env.REGION }}-docker.pkg.dev
      - uses: docker/build-push-action@v6
        with:
          context: .
          push: true
          tags: ${{ env.IMAGE }}:${{ github.sha }},${{ env.IMAGE }}:latest

  deploy-to-cloud-run:
    needs: push-image
    runs-on: ubuntu-latest
    permissions:
      id-token: write
    steps:
      - uses: google-github-actions/auth@v2
        with:
          workload_identity_provider: ${{ secrets.WIF_PROVIDER }}
          service_account: ${{ secrets.WIF_SERVICE_ACCOUNT }}
      - uses: google-github-actions/deploy-cloudrun@v2
        with:
          service: ${{ env.SERVICE }}
          region: ${{ env.REGION }}
          image: ${{ env.IMAGE }}:${{ github.sha }}
          env_vars: |
            ASPNETCORE_ENVIRONMENT=Production
```

### GitHub Secrets Required

| Secret | Value |
|--------|-------|
| `GCP_PROJECT_ID` | GCP project ID |
| `WIF_PROVIDER` | Workload Identity Federation provider resource name |
| `WIF_SERVICE_ACCOUNT` | Service account email for Workload Identity |

Workload Identity Federation is preferred over a service account JSON key stored in secrets —
it is keyless, does not expire, and follows GCP security best practices.

---

## Integration Points: Exact Changes to Existing Files

### Program.cs — delta summary

| Location | Change |
|----------|--------|
| Before `var builder = ...` | Add bootstrap `Log.Logger` |
| After `var builder = ...` | Add `UseUrls` from `PORT` env var |
| After `builder = ...` | Add `builder.Host.UseSerilog(...)` |
| Services section | Add `builder.Services.AddHealthChecks()` |
| Middleware section | Guard `UseHttpsRedirection` with `!IsProduction()` |
| Endpoint registration | Add `app.MapHealthChecks("/health")` |
| Wrap all in try/catch/finally | Flush Serilog on exit |

### PersonsAPI.Api.csproj — delta summary

```xml
<!-- Add to existing ItemGroup -->
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
```

### appsettings.json — delta summary

- Remove `Logging` section (replaced by Serilog)
- Add `Serilog` section with console sink (human-readable format)
- Keep `AllowedHosts`

### appsettings.Development.json — delta summary

- Remove `Logging` section (was overriding EF Core log level)
- Optionally add `Serilog.MinimumLevel.Override.Microsoft.EntityFrameworkCore: Information`
  inside the `Serilog` section instead

---

## Component Boundaries: What the New Files Touch

| New File | Touches | Does Not Touch |
|----------|---------|----------------|
| `Dockerfile` | Builds all 4 src/ projects, publishes Api | No runtime code changes |
| `docker-compose.yml` | Runs the container image | No source code |
| `.dockerignore` | Controls build context | No source code |
| `.github/workflows/deploy.yml` | Runs dotnet test (reads all 4 test projects) | No source code |
| `appsettings.Production.json` | Overrides Serilog sink format | No code logic |
| `Program.cs` changes | Api layer only (composition root) | Domain, Application, Infrastructure |
| `PersonsAPI.Api.csproj` changes | Api layer NuGet refs | Other project files |

Domain, Application, and Infrastructure projects have zero changes in v2.0.

---

## Data Flow: Request Through Cloud Run

```
HTTPS client
  → Cloud Run load balancer (TLS termination)
  → Container HTTP :8080
  → Kestrel (reads PORT env var)
  → PersonsAPI.Api middleware pipeline
      UseExceptionHandler
      MapControllers → PersonsController → ISender
      MapHealthChecks("/health") → 200 Healthy
  → Serilog writes JSON to stdout
  → Cloud Run captures stdout
  → Google Cloud Logging ingests structured log entry
```

---

## Anti-Patterns

### Anti-Pattern 1: Dockerfile inside src/PersonsAPI.Api/

**What goes wrong:** The Api project directory contains only one project. `COPY PersonsAPI.sln .`
fails because the solution file is at the repository root. `dotnet restore` cannot resolve
ProjectReferences to the other three projects.

**Do this instead:** Dockerfile at the repository root. Build context is `.` (repo root).

### Anti-Pattern 2: Single COPY for all source, then restore

**What goes wrong:** `COPY . .` then `dotnet restore` defeats Docker layer caching. Every change
to any `.cs` file invalidates the restore layer, causing full NuGet restore on every build (slow,
bandwidth-expensive in CI).

**Do this instead:** Copy `.csproj` files individually first, run `dotnet restore`, then copy
full source. The restore layer is only invalidated when a `.csproj` changes.

### Anti-Pattern 3: Hardcoding port 8080 in ASPNETCORE_URLS

**What goes wrong:** Cloud Run can be configured to use a different port. Hardcoding breaks
portability.

**Do this instead:** `var port = Environment.GetEnvironmentVariable("PORT") ?? "8080"` in
`Program.cs`. The fallback `8080` matches Cloud Run's default, so local docker-compose also works
without setting `PORT`.

### Anti-Pattern 4: UseHttpsRedirection in a Cloud Run container

**What goes wrong:** Cloud Run terminates TLS at the load balancer. The container receives plain
HTTP. `UseHttpsRedirection` sends a redirect from HTTP to HTTPS — but the container has no HTTPS
listener. Requests loop or fail.

**Do this instead:** Guard with `if (!app.Environment.IsProduction()) app.UseHttpsRedirection()`.

### Anti-Pattern 5: ASPNETCORE_ENVIRONMENT not set in Cloud Run deployment

**What goes wrong:** Without `ASPNETCORE_ENVIRONMENT=Production`, the app defaults to the
`appsettings.Development.json` overrides and may load developer exception pages, verbose EF logs,
or human-readable Serilog output instead of JSON.

**Do this instead:** Set `ASPNETCORE_ENVIRONMENT=Production` as an environment variable in the
`gcloud run deploy` command or the GitHub Actions deployment step.

### Anti-Pattern 6: Storing service account JSON in GitHub Secrets

**What goes wrong:** JSON key files expire, can be leaked, and require manual rotation.

**Do this instead:** Use Workload Identity Federation — keyless, bound to the GitHub Actions
OIDC token, no secrets to manage or rotate.

---

## Sources

- [Microsoft Learn: Run an ASP.NET Core app in Docker containers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0) — official multi-stage Dockerfile pattern for ASP.NET Core 10 (verified 2025-04-22, updated to aspnetcore-10.0 moniker)
- [Microsoft Learn: Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) — `AddHealthChecks()` + `MapHealthChecks()` registration, default response format
- [Microsoft Learn: ASP.NET Core Web Host — Server URLs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-10.0#server-urls) — `ASPNETCORE_URLS` environment variable behavior
- [GitHub: serilog/serilog-aspnetcore](https://github.com/serilog/serilog-aspnetcore) — `UseSerilog` pattern, bootstrap logger, appsettings.json integration
- Google Cloud Run container contract (PORT env var, 8080 default) — HIGH confidence from community consensus and GCP docs (WebFetch blocked by permissions; based on well-established Cloud Run behavior)
