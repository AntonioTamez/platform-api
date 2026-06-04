---
phase: 06-containerization
reviewed: 2026-06-03T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - .dockerignore
  - Dockerfile
  - docker-compose.yml
  - src/PersonsAPI.Api/Program.cs
findings:
  critical: 1
  warning: 4
  info: 2
  total: 7
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-06-03
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

This phase containerizes PersonsAPI using a multi-stage Docker build targeting HTTP-only on port 8080, with TLS termination deferred upstream (Cloud Run). The overall approach is sound: layer-caching strategy is correct, the runtime image is trimmed to the `aspnet` stage, and the health-check endpoint is properly wired in both compose and Program.cs.

One critical finding requires a fix before this ships: the container runs as root because no non-root user is established in the Dockerfile. Four warnings cover a missing healthcheck `start_period`, an overly broad `*.md` exclusion that could silently hide a future `README.md` from context, the ASPNETCORE_ENVIRONMENT=Development leak into production-profile images, and the absence of a `USER` instruction check. Two info items note the redundant env-var duplication between the Dockerfile and compose, and the absence of `HEALTHCHECK` instruction in the Dockerfile itself.

---

## Critical Issues

### CR-01: Container process runs as root

**File:** `Dockerfile:25-40`
**Issue:** The final stage never calls `USER` to drop to a non-root identity. The `mcr.microsoft.com/dotnet/aspnet:10.0` base image defaults to root (UID 0). Every process inside the container — including the .NET runtime and all application code — therefore runs with root privileges. If the container is compromised (e.g., via a path-traversal or deserialization exploit in a dependency), the attacker gains root inside the container, making container-escape and host-filesystem manipulation significantly easier. This is a CIS Docker Benchmark Level 1 violation and a blocker for any deployment target beyond a local learning machine.

**Fix:**
```dockerfile
# Final stage — ASP.NET Core runtime only (no SDK)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Drop to non-root before the process starts
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

The `appuser` account has no login shell and no home directory by default, which is the correct minimal-privilege posture.

---

## Warnings

### WR-01: No `start_period` in docker-compose healthcheck — container may be killed during cold startup

**File:** `docker-compose.yml:13-18`
**Issue:** The healthcheck starts immediately at container launch with `interval: 30s`, `timeout: 5s`, and `retries: 3`. The .NET 10 runtime cold-start (restore InMemory DB seed via `SeedAsync`, JIT compilation) can exceed the 30-second window on a resource-constrained dev machine, causing the container to be marked `unhealthy` — and any orchestrator configured to act on that status will restart it in a loop. The `start_period` grace field exists precisely for this scenario and is absent here.

**Fix:**
```yaml
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
  interval: 30s
  timeout: 5s
  retries: 3
  start_period: 30s   # allow cold-start before health failures count
```

---

### WR-02: `ASPNETCORE_ENVIRONMENT=Development` hardcoded in docker-compose.yml — development secrets exposed if image is promoted

**File:** `docker-compose.yml:11`
**Issue:** The compose file sets `ASPNETCORE_ENVIRONMENT=Development` unconditionally. In Development mode ASP.NET Core enables the developer exception page (full stack traces in HTTP responses), detailed error messages, and potentially development-only middleware. If this compose file is ever used as a base for a staging or production deployment (a common shortcut in small teams), sensitive internal details will be exposed to callers. Even for a learning project, establishing the correct pattern matters.

**Fix:** Use an environment variable substitution with a safe default, or add a comment that this file is local-only and must never be used for staging/production:

```yaml
environment:
  # LOCAL DEVELOPMENT ONLY — never promote this file to staging/production
  - ASPNETCORE_ENVIRONMENT=Development
  - ASPNETCORE_HTTP_PORTS=8080
```

Alternatively, use compose override files (`docker-compose.override.yml` for dev, `docker-compose.prod.yml` for production) and leave the base file environment-neutral.

---

### WR-03: `*.md` glob in `.dockerignore` excludes all Markdown files, including any future `README.md` needed at build time

**File:** `.dockerignore:9`
**Issue:** The rule `*.md` applies at the root level and, because Docker evaluates `.dockerignore` against the build context root, it will silently exclude any Markdown file added in future directly under the repo root (e.g., a `README.md` that a `COPY . .` might need, or a `LICENSE.md`). More importantly, the current intent appears to be "exclude planning docs", but the glob is broader than necessary and could cause surprising build failures if the project structure evolves to include generated Markdown (e.g., from a `dotnet tool run` step in a multi-stage future build). The exclusion should be scoped to the directories it intends to cover.

**Fix:** Replace the catch-all glob with the specific directories already listed:

```
# Already excluded:
.planning/
.claude/
# Remove the *.md line — planning MD files are already covered by the directory exclusions above.
# If root-level .md files should also be excluded, be explicit:
# README.md
# CLAUDE.md
```

---

### WR-04: `ASPNETCORE_HTTP_PORTS` duplicated between Dockerfile `ENV` and docker-compose `environment` — can diverge silently

**File:** `Dockerfile:36` and `docker-compose.yml:12`
**Issue:** `ASPNETCORE_HTTP_PORTS=8080` is set in two places: baked into the image via `ENV` in the Dockerfile, and overridden again in the compose `environment` block. This is redundant today but creates a maintenance trap: if someone changes the port in one place but not the other, the effective value depends on which layer wins (compose environment overrides image ENV, so compose always wins — but this ordering is non-obvious and has surprised teams before). The correct pattern is to set the default once in the Dockerfile and only override in compose when a different port is needed.

**Fix:** Remove the duplication from compose and rely on the image default:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  # ASPNETCORE_HTTP_PORTS is already set to 8080 in the image ENV; no override needed
```

If overriding is intentional (to document the value explicitly), add a comment explaining that.

---

## Info

### IN-01: No `HEALTHCHECK` instruction in Dockerfile — health state invisible outside docker-compose

**File:** `Dockerfile:25-40`
**Issue:** The `HEALTHCHECK` instruction baked into the image would make the container's health status visible to any orchestrator (Docker Swarm, Kubernetes liveness probe via `docker inspect`, `docker ps` HEALTH column). Currently the health logic lives only in `docker-compose.yml`, meaning a container started without compose (e.g., `docker run personsapi`) shows `health: none` and no liveness probe runs. For a learning/reference project this is a minor gap; for production parity it matters.

**Suggestion:** Add to the Dockerfile after the `EXPOSE` line:

```dockerfile
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

---

### IN-02: `/health` endpoint returns a static `{"status":"Healthy"}` string regardless of actual dependency state

**File:** `src/PersonsAPI.Api/Program.cs:41-48`
**Issue:** The `HealthCheckOptions` response writer hard-codes `{"status":"Healthy"}` as the response body, bypassing the result of the registered `IHealthCheck` implementations entirely. The `AddHealthChecks()` call at line 33 only registers the ASP.NET Core health check infrastructure with no checks added (no DB check, no custom check). The result is that `/health` always returns 200 OK with a "Healthy" body even if the InMemory store seed failed or the application is in a degraded state. For Cloud Run liveness probes this means the probe can never detect an unhealthy application, defeating its purpose.

For this learning-scope project the InMemory DB has no real failure mode, so this is informational rather than a blocker. However, the pattern of overriding `ResponseWriter` with a static string should not be cargo-culted into a real service.

**Suggestion:** Either remove the custom `ResponseWriter` entirely (the default ASP.NET Core health check response is already JSON) or wire a real check:

```csharp
// Minimal: remove custom writer and rely on the built-in formatter
app.MapHealthChecks("/health");

// Or add a real check in registration:
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();  // requires Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
```

---

_Reviewed: 2026-06-03_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
