# Phase 6: Containerization - Pattern Map

**Mapped:** 2026-06-03
**Files analyzed:** 4 (3 new, 1 modified)
**Analogs found:** 1 / 4 (3 new files have no codebase analog — Docker artifacts are new to this project)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Dockerfile` | config | build-pipeline | none in codebase | no analog — use RESEARCH.md pattern |
| `docker-compose.yml` | config | request-response (local dev) | none in codebase | no analog — use RESEARCH.md pattern |
| `.dockerignore` | config | n/a (build context filter) | none in codebase | no analog — use RESEARCH.md pattern |
| `src/PersonsAPI.Api/Program.cs` | config | request-response | `src/PersonsAPI.Api/Program.cs` (itself) | exact — single-line deletion |

---

## Pattern Assignments

### `Dockerfile` (config, build-pipeline)

**Analog:** none — first Docker artifact in this project.
**Source:** RESEARCH.md Pattern 1 (verified from `learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0`).

**Solution structure required by Dockerfile COPY commands** (derived from `PersonsAPI.sln` lines 8–24):

The solution contains exactly 4 `src/` projects. The Dockerfile must COPY each `.csproj` individually before `dotnet restore` to enable layer caching (D-07):

```
src/PersonsAPI.Domain/PersonsAPI.Domain.csproj
src/PersonsAPI.Application/PersonsAPI.Application.csproj
src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj
src/PersonsAPI.Api/PersonsAPI.Api.csproj
```

Note: the `tests/` projects (`PersonsAPI.Domain.Tests`, `PersonsAPI.Application.Tests`, `PersonsAPI.Infrastructure.Tests`, `PersonsAPI.Api.Tests`) are intentionally excluded per D-05.

**Complete Dockerfile pattern** (from RESEARCH.md Code Examples, lines 322–354):

```dockerfile
# https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Layer caching: restore csproj files first (D-07)
COPY PersonsAPI.sln .
COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj             ./src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj   ./src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj ./src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                   ./src/PersonsAPI.Api/
RUN dotnet restore

# Copy src/ only — tests/ excluded (D-05)
COPY src/ ./src/
RUN dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl — aspnet:10.0 (Ubuntu Noble) does not include it by default
# Required for docker-compose healthcheck CMD (D-09)
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

**Key constraints:**
- Base images: `mcr.microsoft.com/dotnet/sdk:10.0` (build) and `mcr.microsoft.com/dotnet/aspnet:10.0` (final). No Alpine, no `-chiseled`, no `bookworm-slim` — those tags do not exist for .NET 10 (D-04).
- `--no-restore` on `dotnet publish` is mandatory after the explicit `dotnet restore` step; omitting it defeats layer caching.
- `dotnet publish` targets `src/PersonsAPI.Api/PersonsAPI.Api.csproj` explicitly from WORKDIR `/source`.
- The entrypoint DLL name is `PersonsAPI.Api.dll` — matches the project name from `PersonsAPI.sln` line 22.

---

### `docker-compose.yml` (config, request-response)

**Analog:** none — first compose file in this project.
**Source:** RESEARCH.md Pattern 2 (verified from `docs.docker.com/reference/compose-file/services/#healthcheck`).

**Complete docker-compose pattern** (from RESEARCH.md Code Examples, lines 359–376):

```yaml
# https://docs.docker.com/reference/compose-file/services/#healthcheck
services:
  personsapi:
    image: personsapi
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ASPNETCORE_HTTP_PORTS=8080
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 5s
      retries: 3
```

**Key constraints:**
- Service name `personsapi` (Claude's Discretion — matches the `docker build -t personsapi .` image name from success criteria).
- Both `ASPNETCORE_ENVIRONMENT=Development` (D-08) and `ASPNETCORE_HTTP_PORTS=8080` (D-01) in `environment:`. The compose value can override the Dockerfile `ENV` at runtime — redundancy is intentional.
- Healthcheck probes `/health` — this endpoint already exists in `src/PersonsAPI.Api/Program.cs` lines 42–49.
- `curl` must be present in the container final stage (Dockerfile) for the `CMD` test to work.

---

### `.dockerignore` (config, n/a)

**Analog:** none — first `.dockerignore` in this project.
**Source:** RESEARCH.md Pattern 3 (from D-11, CONTEXT.md).

**Complete .dockerignore pattern** (from RESEARCH.md Code Examples, lines 381–389):

```
.git/
bin/
obj/
tests/
.planning/
.claude/
*.md
docker-compose*.yml
```

**Key constraints:**
- Place at solution root (`C:\ATS\Git\platform\.dockerignore`), same directory as `Dockerfile`.
- `tests/` exclusion aligns with D-05 (no test projects in image).
- `docker-compose*.yml` exclusion prevents the compose file itself from entering the build context.

---

### `src/PersonsAPI.Api/Program.cs` (config, request-response)

**Analog:** `src/PersonsAPI.Api/Program.cs` itself (modification, not new file).

**Current middleware pipeline** (`src/PersonsAPI.Api/Program.cs` lines 37–49):

```csharp
app.UseExceptionHandler();      // NO route argument — activates IExceptionHandler chain (Pitfall 2)
app.UseHttpsRedirection();
app.MapControllers();
app.MapOpenApi();               // /openapi/v1.json (DOC-01)
app.MapScalarApiReference();    // /scalar — MapScalar not UseScalar (Pitfall 8)
app.MapHealthChecks("/health", new HealthCheckOptions             // D-02: JSON body {"status":"Healthy"}
{
    ResponseWriter = (ctx, _) =>
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        return ctx.Response.WriteAsync("{\"status\":\"Healthy\"}");
    }
}); // D-03: anonymous — Cloud Run liveness probe calls /health without credentials
```

**Required change — delete line 38 only** (D-03):

```csharp
// REMOVE this exact line (line 38):
app.UseHttpsRedirection();
```

**After removal, the pipeline reads:**

```csharp
app.UseExceptionHandler();
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = (ctx, _) =>
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        return ctx.Response.WriteAsync("{\"status\":\"Healthy\"}");
    }
});
```

**Key constraint:** This is an unconditional removal (D-03) — do not replace with an environment-conditional guard. Inside the container there is no HTTPS certificate and no scenario where redirecting HTTP to HTTPS is correct.

**Nothing else changes in Program.cs.** The existing Serilog CLEF JSON stdout (`WriteTo.Console(new CompactJsonFormatter())` at lines 15–19) already satisfies the "JSON logs to stdout" success criterion without any Dockerfile-level changes.

---

## Shared Patterns

### Port Configuration Pattern
**Source:** D-01 (CONTEXT.md) + RESEARCH.md Standard Stack section
**Apply to:** Dockerfile (`ENV` instruction) and `docker-compose.yml` (`environment:` section)

```
ASPNETCORE_HTTP_PORTS=8080
```

This env var is the .NET 8+ canonical approach. It configures Kestrel at container runtime without modifying `appsettings.json` or `launchSettings.json`. Set in both locations so the compose value can override the image default at runtime.

### Health Endpoint Pattern
**Source:** `src/PersonsAPI.Api/Program.cs` lines 42–49
**Apply to:** `docker-compose.yml` healthcheck `test:` command

The `/health` endpoint already returns `200 OK` with body `{"status":"Healthy"}`. No new code is needed. The docker-compose healthcheck probes it via:

```
curl -f http://localhost:8080/health
```

The `-f` flag causes curl to return a non-zero exit code on HTTP error responses, which is what the Docker health daemon expects.

### Layer Cache Pattern
**Source:** RESEARCH.md Pattern 1 (D-07)
**Apply to:** Dockerfile `build` stage

Always: COPY `*.sln` + all `*.csproj` → `RUN dotnet restore` → COPY source → `RUN dotnet publish --no-restore`. This ensures `dotnet restore` is skipped (cached layer) when only `.cs` files change.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `Dockerfile` | config | build-pipeline | No Docker artifacts exist in this project yet; use RESEARCH.md Pattern 1 verbatim |
| `docker-compose.yml` | config | request-response | No compose files exist in this project yet; use RESEARCH.md Pattern 2 verbatim |
| `.dockerignore` | config | n/a | No `.dockerignore` exists in this project yet; use RESEARCH.md Pattern 3 verbatim |

---

## Metadata

**Analog search scope:** `C:\ATS\Git\platform\` (solution root and all subdirectories)
**Files scanned:** `Program.cs` (58 lines), `PersonsAPI.sln` (146 lines), Dockerfile glob (0 results), docker-compose glob (0 results), .dockerignore glob (0 results)
**Pattern extraction date:** 2026-06-03
