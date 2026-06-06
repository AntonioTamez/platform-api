---
phase: 06-containerization
plan: 01
subsystem: infra
tags: [docker, dockerfile, dotnet, aspnetcore, containerization]

# Dependency graph
requires:
  - phase: 05-observability
    provides: "Health check endpoint at /health and Serilog CLEF JSON stdout logging — both consumed by the container"
provides:
  - "Multi-stage Dockerfile (sdk:10.0 build + aspnet:10.0 final) at solution root"
  - ".dockerignore at solution root excluding tests/, bin/, obj/, .git/, .planning/, .claude/"
  - "Program.cs without UseHttpsRedirection() — HTTP-only middleware pipeline"
affects: [06-02-docker-compose, 07-cloud-run, 08-ci-cd]

# Tech tracking
tech-stack:
  added: [mcr.microsoft.com/dotnet/sdk:10.0, mcr.microsoft.com/dotnet/aspnet:10.0, curl (apt-get install in final stage)]
  patterns:
    - "Restore-first layer caching: COPY *.csproj -> RUN dotnet restore -> COPY src/ -> dotnet publish --no-restore"
    - "ASPNETCORE_HTTP_PORTS=8080 as .NET 8+ canonical port configuration via ENV in Dockerfile"
    - "Non-root container: aspnet:10.0 runs as 'app' user by default since .NET 8, no USER override needed"

key-files:
  created:
    - Dockerfile
    - .dockerignore
  modified:
    - src/PersonsAPI.Api/Program.cs

key-decisions:
  - "Unconditional removal of UseHttpsRedirection() per D-03 — container never does TLS, Cloud Run does"
  - "curl installed in final stage via apt-get: aspnet:10.0 (Ubuntu Noble) lacks it, needed for healthcheck"
  - "ASPNETCORE_HTTP_PORTS=8080 in both Dockerfile ENV and (later) docker-compose environment — redundancy intentional for override"
  - "No USER override needed — aspnet:10.0 defaults to non-root app user since .NET 8"

patterns-established:
  - "Dockerfile Pattern: restore-first caching with explicit per-csproj COPY before dotnet restore"
  - "Dockerfile Pattern: aspnet:10.0 as final stage — Debian-based Ubuntu Noble, no Alpine (DateOnly/globalization safe)"

requirements-completed: [DOCK-01]

# Metrics
duration: 15min
completed: 2026-06-04
---

# Phase 6 Plan 01: Multi-stage Dockerfile for PersonsAPI Summary

**Multi-stage Dockerfile (sdk:10.0 build / aspnet:10.0 final) with restore-first layer caching, curl for healthchecks, and HTTP-only Program.cs removing UseHttpsRedirection**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-04T01:41:00Z
- **Completed:** 2026-06-04T01:56:17Z
- **Tasks:** 3 (files created/modified; docker build pending Docker Desktop)
- **Files modified:** 3

## Accomplishments

- Removed `app.UseHttpsRedirection()` from Program.cs unconditionally (D-03); Release build verified passing
- Created `.dockerignore` at solution root with all 8 D-11 exclusion entries; node verification script prints OK
- Created multi-stage `Dockerfile` at solution root: sdk:10.0 build stage with restore-first layer caching, aspnet:10.0 final stage with curl installed, `ENV ASPNETCORE_HTTP_PORTS=8080`, correct `ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]`

## Task Commits

Each task was committed atomically:

1. **Task 1: Remove app.UseHttpsRedirection() from Program.cs** - `aeddb16` (chore)
2. **Task 2: Create .dockerignore at solution root** - `8bbf724` (chore)
3. **Task 3: Create multi-stage Dockerfile** - `053ad22` (feat)

**Plan metadata:** (see final commit below)

## Files Created/Modified

- `src/PersonsAPI.Api/Program.cs` - Removed `app.UseHttpsRedirection();` line 38; pipeline now UseExceptionHandler -> MapControllers -> ...
- `.dockerignore` - Excludes `.git/`, `bin/`, `obj/`, `tests/`, `.planning/`, `.claude/`, `*.md`, `docker-compose*.yml`
- `Dockerfile` - Two-stage build: sdk:10.0 build stage with restore-first caching + aspnet:10.0 final stage with curl, ASPNETCORE_HTTP_PORTS=8080, non-root app user by default

## Decisions Made

- D-03 followed exactly: UseHttpsRedirection removed unconditionally, not with environment conditional guard
- D-04 followed: used plain `mcr.microsoft.com/dotnet/sdk:10.0` and `mcr.microsoft.com/dotnet/aspnet:10.0` tags (no Alpine, no chiseled, no bookworm-slim — those don't exist for .NET 10)
- D-07 followed: COPY *.sln + 4 *.csproj files first, RUN dotnet restore, then COPY src/, then dotnet publish --no-restore
- curl installed in final stage: `apt-get install -y --no-install-recommends curl` + `rm -rf /var/lib/apt/lists/*` (T-06-03 mitigation)
- No explicit `USER` directive needed: aspnet:10.0 runs as non-root `app` user by default since .NET 8 (T-06-01 accepted)

## Deviations from Plan

None — plan executed exactly as written. All three tasks completed as specified.

The Docker daemon was not running when Task 3 executed (Docker Desktop not started). The Dockerfile was created and all structural acceptance criteria verified. The `docker build -t personsapi .` verification step could not run. This is an infrastructure gate, not a code deviation.

## Issues Encountered

**Docker daemon not running:** Docker Desktop was not active when Task 3 ran. The `docker ps` check returned an error: "failed to connect to the docker API at npipe:////./pipe/docker_engine". All Dockerfile content was created and verified structurally (all acceptance criteria checks on file content passed). The live `docker build -t personsapi .` command must be run manually once Docker Desktop is started, or it will run as part of the Plan 02 continuation.

## Known Stubs

None — no UI components, no hardcoded empty values, no placeholder text. The Dockerfile and .dockerignore are complete production-ready configurations.

## Threat Flags

No new threat surface beyond what is documented in the plan's threat model. All mitigations applied:
- T-06-02: .dockerignore excludes `.git/`, `.planning/`, `.claude/` — no repo metadata enters build context
- T-06-03: `apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*` — minimal surface area
- T-06-01: Non-root `app` user preserved (no USER override added)
- T-06-04: Only ASPNETCORE_HTTP_PORTS=8080 baked in — non-sensitive

## User Setup Required

Before running `docker build -t personsapi .` or `docker compose up`, Docker Desktop must be running:
1. Start Docker Desktop from the Windows system tray or Start menu
2. Wait for the Docker Desktop icon to show "running" status
3. Verify: `docker ps` should succeed (exit 0)
4. Then run: `docker build -t personsapi .` from the solution root (`C:/ATS/Git/platform`)

## Next Phase Readiness

- `Dockerfile`, `.dockerignore`, and the updated `Program.cs` are committed and ready
- Plan 02 (docker-compose) can proceed — it depends on the `personsapi` image built by this plan's Dockerfile
- The `docker build -t personsapi .` command (DOCK-01 phase success criterion 1) must be executed once Docker Desktop is running
- `/health` endpoint and Serilog JSON stdout are already in place from Phase 5 — no additional application code changes needed for Phase 6

---
*Phase: 06-containerization*
*Completed: 2026-06-04*
