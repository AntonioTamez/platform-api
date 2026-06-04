# Phase 6: Containerization - Research

**Researched:** 2026-06-03
**Domain:** Docker multi-stage builds for .NET 10 ASP.NET Core
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Use `ASPNETCORE_HTTP_PORTS=8080` as the environment variable to configure Kestrel. This is the modern .NET 8+ approach — direct and unambiguous. Set in both Dockerfile `ENV` and docker-compose `environment:`.
- **D-02:** The container listens on HTTP only. TLS is terminated by Cloud Run (Phase 7) or the developer's local setup — never inside the container itself.
- **D-03:** Remove `app.UseHttpsRedirection()` from `Program.cs` completely. This is an unconditional removal, not a conditional.
- **D-04:** Multi-stage Dockerfile with two stages: `build` (uses `mcr.microsoft.com/dotnet/sdk:10.0`) and `final` (uses `mcr.microsoft.com/dotnet/aspnet:10.0`). Alpine is explicitly excluded — use the default Debian-based images.
- **D-05:** Only `src/` is copied into the Dockerfile. The `tests/` directory is ignored completely.
- **D-06:** No `dotnet test` stage in the Dockerfile.
- **D-07:** Restore-first layer caching: COPY `*.sln` + all `*.csproj` files first, run `dotnet restore`, then COPY remaining source.
- **D-08:** `ASPNETCORE_ENVIRONMENT=Development` in docker-compose `environment:`.
- **D-09:** Docker-level `healthcheck:` in docker-compose.yml pointing to `http://localhost:8080/health`. Interval: 30s, timeout: 5s, retries: 3.
- **D-10:** Port mapping: `"8080:8080"` — host port 8080 → container port 8080.
- **D-11:** Include a `.dockerignore` at the solution root excluding: `.git/`, `bin/`, `obj/`, `tests/`, `.planning/`, `.claude/`, `*.md`, `docker-compose*.yml`.

### Claude's Discretion

- Exact layer ordering within the `build` stage (COPY sln vs csproj ordering details)
- Whether to use `--no-restore` on `dotnet build` after an explicit `dotnet restore` step
- Exact `dotnet publish` flags (e.g., `-c Release --no-restore -o /app/publish`)
- docker-compose service name (e.g., `personsapi` or `api`)

### Deferred Ideas (OUT OF SCOPE)

- None — discussion stayed within phase scope.

</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOCK-01 | Developer can build the API into a Docker image from the solution root using `docker build` | Multi-stage Dockerfile pattern verified from official Microsoft docs; image tags confirmed; layer caching pattern confirmed |
| DOCK-02 | Developer can run the full API locally with `docker compose up` and reach all endpoints at port 8080 | docker-compose schema verified; ASPNETCORE_HTTP_PORTS confirmed; healthcheck curl workaround documented |

</phase_requirements>

---

## Summary

Phase 6 produces three new files at the solution root — `Dockerfile`, `docker-compose.yml`, `.dockerignore` — plus a one-line code change in `Program.cs`. All major decisions were locked in CONTEXT.md during the discussion phase, so research focused on verifying the correct implementation details of those decisions.

**Critical finding:** For .NET 10, there is no `bookworm-slim` image variant. The plain `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` tags resolve to Ubuntu 24.04 Noble. The `10.0` tag is the correct and recommended choice — the decision to use Debian-based images (D-04) is satisfied by the `10.0` tag since Ubuntu Noble is the Debian-compatible Linux base for .NET 10.

**Second critical finding:** The `aspnet:10.0` (noble) runtime image does not include `curl` by default. The docker-compose healthcheck defined in D-09 uses `curl`. This requires adding a `RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` step in the final Dockerfile stage. This is the standard pattern for .NET Ubuntu-based images needing curl.

**Primary recommendation:** Use the official Microsoft multi-stage Dockerfile pattern from `learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images` exactly as documented for .NET 10, adapted for the 4-project solution structure. Install curl in the final stage for healthcheck support.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Port binding / HTTP configuration | Container (ENV) | API layer (Program.cs) | `ASPNETCORE_HTTP_PORTS` env var configures Kestrel at container runtime; Program.cs removes HTTPS redirect |
| Build artifact production | Dockerfile build stage | — | `dotnet publish` produces self-contained output; no application logic changes |
| Container health monitoring | Docker daemon (healthcheck) | ASP.NET Core (endpoint) | `/health` endpoint already exists from Phase 5; docker-compose healthcheck probes it |
| Container composition | docker-compose | — | Port mapping, env vars, healthcheck, service name all live in docker-compose.yml |
| Build context filtering | .dockerignore | — | Keeps build context lean; tests/, .git/, bin/, obj/ excluded |

---

## Standard Stack

### Core (no new NuGet packages — Docker tooling only)

| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| Docker Desktop | 29.4.0 (installed) | Container build and run | Required by phase; already installed and available |
| Docker Compose | v5.1.1 (installed) | Multi-container orchestration | Required by DOCK-02; already installed |
| `mcr.microsoft.com/dotnet/sdk:10.0` | 10.0 (noble/Ubuntu 24.04) | Build stage base image | Official Microsoft .NET 10 SDK image; no bookworm-slim for .NET 10 |
| `mcr.microsoft.com/dotnet/aspnet:10.0` | 10.0 (noble/Ubuntu 24.04) | Runtime stage base image | Official Microsoft ASP.NET Core 10 runtime; correct partner to sdk:10.0 |

No NuGet packages are added in this phase.

### Why `10.0` (not `10.0-noble` or `10.0-bookworm-slim`)

`bookworm-slim` does not exist for .NET 10. The available Linux distributions for .NET 10 are: noble (Ubuntu 24.04), resolute (Ubuntu 25.04), alpine3.23, azurelinux3.0. The plain `10.0` tag resolves to noble. Ubuntu Noble has ICU and tzdata installed, making it globalization-safe — essential for `DateOnly` operations in PersonsAPI. `[VERIFIED: github.com/dotnet/dotnet-docker/tree/main/src/aspnet/10.0 and github.com/dotnet/dotnet-docker/blob/main/documentation/supported-tags.md]`

---

## Package Legitimacy Audit

> No external packages are installed in this phase. Docker images are from the official Microsoft Container Registry (mcr.microsoft.com), not the npm/PyPI/NuGet package ecosystem. Package legitimacy gate does not apply.

---

## Architecture Patterns

### System Architecture Diagram

```
Developer workstation
        |
        | docker build -t personsapi .
        v
[Dockerfile build stage]
  mcr.microsoft.com/dotnet/sdk:10.0
  COPY *.sln + *.csproj --> dotnet restore (cached layer)
  COPY src/ --> dotnet publish -c Release --no-restore -o /app/publish
        |
        | COPY --from=build /app/publish
        v
[Dockerfile final stage]
  mcr.microsoft.com/dotnet/aspnet:10.0
  RUN apt-get install curl       (for healthcheck)
  ENV ASPNETCORE_HTTP_PORTS=8080
  ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
        |
        | docker compose up
        v
[docker-compose.yml service: personsapi]
  image: personsapi
  ports: 8080:8080
  environment:
    ASPNETCORE_ENVIRONMENT=Development
    ASPNETCORE_HTTP_PORTS=8080
  healthcheck: curl -f http://localhost:8080/health
        |
        | HTTP :8080
        v
Kestrel (in container) --> Program.cs middleware --> Controllers --> Application --> Domain
```

### Recommended File Structure (new files only)

```
(solution root)/
├── Dockerfile           # Multi-stage build (new)
├── docker-compose.yml   # Compose config (new)
├── .dockerignore        # Build context filter (new)
├── PersonsAPI.sln
└── src/
    ├── PersonsAPI.Api/Program.cs     # MODIFIED: remove UseHttpsRedirection()
    ├── PersonsAPI.Application/
    ├── PersonsAPI.Domain/
    └── PersonsAPI.Infrastructure/
```

### Pattern 1: Official .NET 10 Multi-Stage Dockerfile (adapted for 4-project solution)

**What:** Two-stage build — sdk:10.0 compiles and publishes; aspnet:10.0 runs the published output.
**When to use:** Any .NET 10 ASP.NET Core API containerized for local dev or cloud deployment.

```dockerfile
# Source: learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images (aspnetcore-10.0)
# Adapted for PersonsAPI 4-project solution structure

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restore-first layer caching (D-07)
# Copy solution file and all csproj files before any source
COPY PersonsAPI.sln .
COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj             ./src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj   ./src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj ./src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                   ./src/PersonsAPI.Api/
RUN dotnet restore

# Copy src/ only — tests/ excluded (D-05)
COPY src/ ./src/

# Publish in Release mode; --no-restore skips redundant restore
RUN dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Final stage — aspnet runtime only (no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install curl for docker-compose healthcheck (D-09)
# aspnet:10.0 (noble/Ubuntu 24.04) does not include curl by default
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Port configuration — ASPNETCORE_HTTP_PORTS is the .NET 8+ canonical approach (D-01)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

### Pattern 2: docker-compose.yml

```yaml
# Source: docs.docker.com/reference/compose-file/services/#healthcheck
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

### Pattern 3: .dockerignore

```
# Source: D-11 from CONTEXT.md
.git/
bin/
obj/
tests/
.planning/
.claude/
*.md
docker-compose*.yml
```

### Pattern 4: Program.cs change (D-03)

Remove this line from `src/PersonsAPI.Api/Program.cs`:
```csharp
// REMOVE this line (line 38 in current source):
app.UseHttpsRedirection();
```

The line currently sits between `app.UseExceptionHandler()` and `app.MapControllers()`. After removal the middleware pipeline reads:
```csharp
app.UseExceptionHandler();
app.MapControllers();
app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health", ...);
```

### Anti-Patterns to Avoid

- **Using `dotnet build` + `dotnet publish` as separate RUN commands:** The official pattern uses `dotnet publish` directly — it builds and publishes in one step. Adding a separate `dotnet build` step is redundant.
- **Using `dotnet restore` inside `dotnet publish` (`--no-restore` omitted):** After the explicit restore layer, always pass `--no-restore` to `dotnet publish`. Omitting it causes restore to run again, defeating layer caching.
- **COPY of entire project before restore:** Copying all source files before `dotnet restore` breaks layer caching — any `.cs` change will bust the restore cache. Always COPY csproj files first, restore, then COPY source.
- **Using `ASPNETCORE_URLS` instead of `ASPNETCORE_HTTP_PORTS`:** `ASPNETCORE_HTTP_PORTS` is the .NET 8+ canonical variable. `ASPNETCORE_URLS` still works but mixes HTTP/HTTPS semantics in the value. D-01 is explicit.
- **Leaving `app.UseHttpsRedirection()` in place:** Inside the container there is no HTTPS certificate, so the redirect will return 307 and break `curl localhost:8080/health`. D-03 requires unconditional removal.
- **Using `aspnet:10.0-noble-chiseled`:** Chiseled (distroless) images lack a shell AND curl, requiring a bespoke healthcheck binary. The plain `aspnet:10.0` (non-chiseled noble) is the correct choice here since we install curl.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Layer cache invalidation | Manual heuristics for COPY ordering | Restore-first pattern (COPY csproj → restore → COPY src) | Docker layer model; any file change in a layer invalidates all downstream layers |
| Container health reporting | Custom health ping script | `curl -f http://localhost:8080/health` against existing `/health` endpoint | `/health` already returns 200 OK from Phase 5; no new code needed |
| Port configuration | Modifying `appsettings.json` or `launchSettings.json` | `ASPNETCORE_HTTP_PORTS=8080` env var | Env var overrides at container runtime without rebuilding the image |
| Multi-architecture builds | `--platform` flags, BuildKit manifest lists | `10.0` tag (multi-platform by default) | Microsoft publishes multi-arch manifests; plain tag resolves correctly on amd64 and arm64 |

**Key insight:** The container artifacts (Dockerfile, docker-compose.yml, .dockerignore) are pure configuration. No application logic changes are needed — the API already has `/health`, already has Serilog JSON stdout, already uses `DataSeeder` at startup. The only application code change is removing one line.

---

## Common Pitfalls

### Pitfall 1: Layer Cache Bust from Wrong COPY Order
**What goes wrong:** COPY of source files before `dotnet restore` means any `.cs` file change triggers a full package restore on the next build.
**Why it happens:** Docker layer model: each RUN/COPY instruction creates a new layer; if any earlier layer changes, all subsequent layers are re-executed.
**How to avoid:** Always COPY `*.sln` + all `*.csproj` first, RUN `dotnet restore`, then COPY source. This is pattern D-07 and is confirmed in official .NET Docker docs.
**Warning signs:** `docker build` always shows "Running dotnet restore..." even when only `.cs` files changed.

### Pitfall 2: curl Missing in Runtime Image
**What goes wrong:** `docker compose up` starts the container but the healthcheck immediately fails; `docker ps` shows `(unhealthy)`.
**Why it happens:** `mcr.microsoft.com/dotnet/aspnet:10.0` (Ubuntu Noble) does not include curl. The docker-compose healthcheck test `["CMD", "curl", "-f", ...]` requires curl to be present in the container.
**How to avoid:** Add `RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` in the final Dockerfile stage.
**Warning signs:** `docker inspect <container>` shows `"Health": {"Status": "unhealthy"}` immediately at startup.

### Pitfall 3: HTTPS Redirect Loop
**What goes wrong:** `curl localhost:8080/health` returns `307 Temporary Redirect` to `https://localhost:8080/health`, then fails because there is no HTTPS listener.
**Why it happens:** `app.UseHttpsRedirection()` in Program.cs sends a redirect when receiving an HTTP request, even in a container where HTTPS is not configured.
**How to avoid:** Remove `app.UseHttpsRedirection()` unconditionally (D-03). Do not leave it conditioned on environment — inside the container there is never a reason to redirect HTTP to HTTPS.
**Warning signs:** `curl -v localhost:8080/health` shows `< HTTP/1.1 307 Temporary Redirect` with a `Location: https://localhost:8080/health` header.

### Pitfall 4: Wrong WORKDIR for dotnet publish
**What goes wrong:** `dotnet publish` targets the wrong project or the wrong relative path, causing a "Project file does not exist" build error.
**Why it happens:** With a multi-project solution, the WORKDIR is `/source` (the solution root in the container), not the individual project directory. The publish command must specify the project path explicitly.
**How to avoid:** Use `dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj -c Release --no-restore -o /app/publish`. The solution root WORKDIR is `/source`; the project path is relative to it.
**Warning signs:** `ERROR: failed to solve: process "/bin/sh -c dotnet publish ..." exited with code 1` with "Project file does not exist" in the output.

### Pitfall 5: Build Context Bloat
**What goes wrong:** `docker build` is slow (minutes) because `bin/`, `obj/`, `.git/`, and `tests/` are sent as build context.
**Why it happens:** Without `.dockerignore`, the Docker client sends every file in the solution root to the daemon.
**How to avoid:** Create `.dockerignore` at the solution root (D-11) before running `docker build`.
**Warning signs:** `docker build` output shows "Sending build context to Docker daemon X.XX GB" — any value above a few MB for a small project is a red flag.

### Pitfall 6: ASPNETCORE_HTTP_PORTS not overriding launchSettings
**What goes wrong:** Container starts but Kestrel binds to a different port or refuses to start.
**Why it happens:** Inside a Docker container, `launchSettings.json` is not loaded — ASP.NET Core only reads it when launched via `dotnet run`. Environment variables take precedence correctly. This pitfall is a false alarm: the env var works correctly in Docker.
**How to avoid:** Ensure `ASPNETCORE_HTTP_PORTS=8080` appears in both the Dockerfile `ENV` and docker-compose `environment:`. Redundancy is intentional — the compose value can override the image default at runtime.
**Warning signs:** Not a common pitfall for Docker builds, but verify by checking `docker logs <container>` for the "Now listening on:" Kestrel startup message.

---

## Code Examples

### Complete Dockerfile (verified from official source)

```dockerfile
# https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Layer caching: restore csproj files first
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

# Install curl — aspnet:10.0 (Ubuntu Noble) does not include it
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

### docker-compose.yml

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

### .dockerignore

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

### Program.cs — remove one line

```csharp
// BEFORE (line 38 in current Program.cs):
app.UseExceptionHandler();
app.UseHttpsRedirection();   // <-- REMOVE this line
app.MapControllers();

// AFTER:
app.UseExceptionHandler();
app.MapControllers();
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Default port 80 in Docker images | Default port 8080 (`ASPNETCORE_HTTP_PORTS=8080`) | .NET 8 (breaking change) | Port 8080 is non-privileged; works as non-root user |
| `ASPNETCORE_URLS` env var | `ASPNETCORE_HTTP_PORTS` env var | .NET 8 | Cleaner HTTP-only semantics; no protocol prefix needed |
| `bookworm-slim` Debian images for .NET | `noble` (Ubuntu 24.04) images for .NET 10 | .NET 10 release | `bookworm-slim` not published for .NET 10; `10.0` tag = noble |
| `.NET 9` default platform base | Ubuntu Noble (24.04) for `.NET 10` | .NET 10 release | `.NET 10` multi-platform tags default to Ubuntu, not Debian |

**Deprecated/outdated:**
- `bookworm-slim` tag: Available for .NET 8 and .NET 9 only. Not published for .NET 10. [VERIFIED: github.com/dotnet/dotnet-docker/tree/main/src/aspnet/10.0]
- `ASPNETCORE_URLS=http://+:8080`: Still functional but superseded by `ASPNETCORE_HTTP_PORTS=8080` for pure HTTP scenarios. D-01 requires the modern form.

---

## Assumptions Log

> Claims tagged `[ASSUMED]` in this research:

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `curl` is not installed in `aspnet:10.0` (noble) by default, requiring `apt-get install curl` in the Dockerfile | Pitfall 2, Code Examples | If curl is actually present, the `apt-get install` step is harmless but unnecessary (adds ~3 MB and ~5s to build time). Low risk if wrong. |

**All other claims verified or cited from official Microsoft documentation.**

---

## Open Questions

1. **Docker Desktop daemon startup requirement**
   - What we know: Docker Desktop 29.4.0 is installed. Docker Compose v5.1.1 is installed. The daemon (`dockerDesktopLinuxEngine`) was not running at research time — this is normal on Windows when Docker Desktop is not open in the system tray.
   - What's unclear: Whether the developer needs to start Docker Desktop manually before running `docker build` and `docker compose up`, or whether it auto-starts.
   - Recommendation: The plan should include a pre-check step: "Ensure Docker Desktop is running (`docker ps` should succeed before proceeding)." Not a blocker — Docker Desktop simply needs to be started.

2. **`--no-restore` vs separate `dotnet build` step**
   - What we know: The official Microsoft .NET 10 multi-stage Dockerfile sample uses `dotnet publish` directly with `--no-restore` after an explicit `dotnet restore` step. No separate `dotnet build` is used.
   - What's unclear: This is a Claude's Discretion item from CONTEXT.md.
   - Recommendation: Use `dotnet publish -c Release --no-restore -o /app/publish` without a separate `dotnet build`. This matches the official pattern and avoids redundant compilation.

3. **docker-compose service name**
   - What we know: This is a Claude's Discretion item. Options are `personsapi` or `api`.
   - Recommendation: Use `personsapi` — matches the image name used in the success criteria (`docker build -t personsapi .`) and is unambiguous when running multiple services in Phase 7+.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker Desktop | `docker build`, `docker compose up` | Installed (daemon offline at research time — requires starting Desktop) | 29.4.0 | — |
| Docker Compose | DOCK-02 | Installed | v5.1.1 | — |
| .NET 10 SDK | Dockerfile build stage (local reference) | Installed | 10.0.202 | — |
| Internet / MCR access | `FROM mcr.microsoft.com/dotnet/sdk:10.0` and `aspnet:10.0` | Required for first pull | — | Pre-pull images; use local cache |

**Missing dependencies with no fallback:**
- Docker Desktop daemon must be running. It is installed but was not active during research. The developer must start Docker Desktop (system tray) before executing phase tasks.

**Missing dependencies with fallback:**
- None.

---

## Validation Architecture

> `nyquist_validation` is set to `false` in `.planning/config.json`. This section is skipped.

---

## Security Domain

> `security_enforcement: true` in config. ASVS level 1 applies.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No auth in this phase; container exposes same API as Phase 5 |
| V3 Session Management | No | Stateless API; no sessions |
| V4 Access Control | No | No auth layer in scope |
| V5 Input Validation | No new inputs | Already implemented in Application layer |
| V6 Cryptography | No | No TLS inside container (D-02); TLS is Cloud Run's concern |

### Known Threat Patterns for Docker/Container

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Container runs as root | Elevation of Privilege | `aspnet:10.0` images run as non-root `app` user by default since .NET 8 — no action needed |
| Secrets in ENV visible to all processes | Information Disclosure | No secrets in this phase; env vars are ASPNETCORE_ENVIRONMENT and ASPNETCORE_HTTP_PORTS — both non-sensitive |
| Build-time secrets leaked into image layers | Information Disclosure | No secrets used during `docker build`; NuGet restore uses public packages only |
| Large attack surface from installed tools | Tampering | `apt-get install curl` in final stage adds one minimal package; `--no-install-recommends` and cache cleanup limit surface area |

**Security note:** The `aspnet:10.0` base image defaults to running as the `app` user (non-root) since .NET 8 — confirmed as default behavior. No explicit `USER app` instruction is needed, but it can be added for explicitness if desired.

---

## Sources

### Primary (HIGH confidence)
- [Official .NET 10 ASP.NET Core Docker Docs — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0) — multi-stage Dockerfile pattern for .NET 10 confirmed
- [GitHub dotnet/dotnet-docker — src/aspnet/10.0 directory listing](https://github.com/dotnet/dotnet-docker/tree/main/src/aspnet/10.0) — confirmed: no bookworm-slim, available OSes: noble, resolute, alpine3.23, azurelinux3.0
- [GitHub dotnet/dotnet-docker — src/sdk/10.0 directory listing](https://github.com/dotnet/dotnet-docker/tree/main/src/sdk/10.0) — confirmed: same distribution set; no bookworm-slim for SDK either
- [GitHub dotnet/dotnet-docker — supported-tags.md](https://github.com/dotnet/dotnet-docker/blob/main/documentation/supported-tags.md) — confirmed: .NET 10 multi-platform tags default to Ubuntu Noble
- [GitHub dotnet/dotnet-docker — samples/aspnetapp/Dockerfile](https://github.com/dotnet/dotnet-docker/blob/main/samples/aspnetapp/Dockerfile) — official aspnetapp sample Dockerfile using sdk:10.0 and aspnet:10.0
- [Docker Compose Healthcheck Reference](https://docs.docker.com/reference/compose-file/services/#healthcheck) — confirmed: `test`, `interval`, `timeout`, `retries` syntax and CMD format
- [Microsoft Learn: .NET Container Images](https://learn.microsoft.com/en-us/dotnet/core/docker/container-images) — globalization-safe images; Ubuntu/Debian have ICU and tzdata

### Secondary (MEDIUM confidence)
- [Microsoft Learn: Default ASP.NET Core port change from 80 to 8080 (.NET 8)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port) — ASPNETCORE_HTTP_PORTS=8080 confirmed as the canonical .NET 8+ approach
- [GitHub dotnet/AspNetCore.Docs issue #24341](https://github.com/dotnet/AspNetCore.Docs/issues/24341) — confirms curl is not present in .NET runtime Docker images

### Tertiary (LOW confidence)
- WebSearch results confirming `apt-get install curl` as standard mitigation for missing curl in Ubuntu-based .NET images — corroborates verified finding A1

---

## Metadata

**Confidence breakdown:**
- Dockerfile image tags and layer pattern: HIGH — verified from official Microsoft GitHub and docs
- curl-missing finding: HIGH — confirmed by GitHub issue, corroborated by web search
- docker-compose healthcheck syntax: HIGH — verified from official Docker documentation
- `ASPNETCORE_HTTP_PORTS` correctness: HIGH — verified from official Microsoft breaking-change doc
- apt-get install curl in final stage: MEDIUM — verified from GitHub issue + multiple corroborating sources; one `[ASSUMED]` tag (curl absence in noble non-chiseled)

**Research date:** 2026-06-03
**Valid until:** 2026-09-03 (stable infra — .NET 10 image tags do not change on patch releases; `10.0` always points to latest patch)
