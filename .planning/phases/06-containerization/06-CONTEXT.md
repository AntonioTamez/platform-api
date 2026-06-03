# Phase 6: Containerization - Context

**Gathered:** 2026-06-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Build a multi-stage Dockerfile and docker-compose.yml at the solution root so a developer can `docker build -t personsapi .` and `docker compose up` to run the full PersonsAPI locally on port 8080. All application code changes are minimal and contained to `Program.cs` (remove `UseHttpsRedirection()`) and the new Docker artifacts. No changes to Domain, Application, or Infrastructure layers.

</domain>

<decisions>
## Implementation Decisions

### Port Configuration
- **D-01:** Use `ASPNETCORE_HTTP_PORTS=8080` as the environment variable to configure Kestrel. This is the modern .NET 8+ approach — direct and unambiguous. Set in both Dockerfile `ENV` and docker-compose `environment:`.
- **D-02:** The container listens on HTTP only. TLS is terminated by Cloud Run (Phase 7) or the developer's local setup — never inside the container itself.

### HTTPS Handling
- **D-03:** Remove `app.UseHttpsRedirection()` from `Program.cs` completely. The API is designed for Cloud Run where TLS is proxy-terminated. There is no scenario where HTTPS redirect inside the container is correct. This is an unconditional removal, not a conditional.

### Dockerfile Structure
- **D-04:** Multi-stage Dockerfile with two stages: `build` (uses `mcr.microsoft.com/dotnet/sdk:10.0`) and `final` (uses `mcr.microsoft.com/dotnet/aspnet:10.0`). Alpine is explicitly excluded — use the default Debian-based images (documented out of scope in REQUIREMENTS.md: culture/globalization issues with DateOnly).
- **D-05:** Only `src/` is copied into the Dockerfile. The `tests/` directory is ignored completely — the final image contains only production code.
- **D-06:** No `dotnet test` stage in the Dockerfile. Tests are a CI/CD concern (Phase 8). Running integration tests with `WebApplicationFactory` inside the Docker build context creates unnecessary complexity.
- **D-07:** Use restore-first layer caching: COPY `*.sln` + all `*.csproj` files first, run `dotnet restore`, then COPY remaining source. This ensures `dotnet restore` is cached when only `.cs` files change.

### docker-compose Configuration
- **D-08:** `ASPNETCORE_ENVIRONMENT=Development` — maintains local dev parity with `dotnet run`. Scalar UI remains accessible at `/scalar`, detailed error responses visible. docker-compose is for local development, not production simulation.
- **D-09:** Include a Docker-level `healthcheck:` in docker-compose.yml pointing to `http://localhost:8080/health`. This lets `docker ps` show container health status. Interval: 30s, timeout: 5s, retries: 3.
- **D-10:** Port mapping: `"8080:8080"` — host port 8080 → container port 8080. Consistent with success criteria (`curl localhost:8080/health`).

### .dockerignore
- **D-11:** Include a `.dockerignore` at the solution root. Exclude: `.git/`, `bin/`, `obj/`, `tests/`, `.planning/`, `.claude/`, `*.md`, `docker-compose*.yml`. Keeps the build context lean.

### Claude's Discretion
- Exact layer ordering within the `build` stage (COPY sln vs csproj ordering details)
- Whether to use `--no-restore` on `dotnet build` after an explicit `dotnet restore` step
- Exact `dotnet publish` flags (e.g., `-c Release --no-restore -o /app/publish`)
- docker-compose service name (e.g., `personsapi` or `api`)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope and Requirements
- `.planning/ROADMAP.md` — Phase 6 goal, success criteria (4 items), and dependency on Phase 5
- `.planning/REQUIREMENTS.md` — DOCK-01 (`docker build`) and DOCK-02 (`docker compose up`) definitions; Out of Scope section (Alpine base image exclusion, `Serilog.Sinks.GoogleCloudLogging` exclusion)

### Project Constraints
- `.planning/PROJECT.md` — Framework constraints (.NET 10, C# 14), current state summary, Key Decisions table
- `CLAUDE.md` — Technology stack table with exact package versions

### Existing Code to Modify
- `src/PersonsAPI.Api/Program.cs` — Remove `app.UseHttpsRedirection()` (D-03); verify `app.MapHealthChecks("/health")` is in place (already done in Phase 5)
- `src/PersonsAPI.Api/PersonsAPI.Api.csproj` — Reference file for project structure (4 layers: Domain, Application, Infrastructure, Api)
- `PersonsAPI.sln` — Solution file at root; used to understand the `src/` layout for Dockerfile COPY commands

### New Files to Create
- `Dockerfile` — at solution root (`./Dockerfile`)
- `docker-compose.yml` — at solution root (`./docker-compose.yml`)
- `.dockerignore` — at solution root (`./.dockerignore`)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `src/PersonsAPI.Api/Program.cs`: Already has `app.MapHealthChecks("/health")` with JSON response from Phase 5 — the `/health` endpoint that docker-compose healthcheck will probe is already implemented
- `PersonsAPI.sln` at root: 4 project references under `src/` — Dockerfile must COPY and restore all 4 `.csproj` files for layer caching to work

### Established Patterns
- Serilog CLEF JSON to stdout (`WriteTo.Console(new CompactJsonFormatter())`): Already configured in Program.cs — container logs will automatically emit JSON without any Dockerfile-level changes (success criterion 4 already satisfied)
- `ASPNETCORE_HTTP_PORTS` as the canonical port configuration: Modern .NET 8+ pattern, consistent with how Cloud Run will set the port in Phase 7

### Integration Points
- `app.UseHttpsRedirection()` in `Program.cs` (line 38): Must be removed — this is the only application code change in this phase
- Solution structure: `src/PersonsAPI.Api/`, `src/PersonsAPI.Application/`, `src/PersonsAPI.Domain/`, `src/PersonsAPI.Infrastructure/` — Dockerfile COPY commands must reference these paths from the solution root context
- `DataSeeder` runs on `app.Services.SeedAsync()` before `RunAsync()` — in-memory seeding works identically in the container; no DB connection string needed

</code_context>

<specifics>
## Specific Ideas

- Success criteria explicitly use `curl localhost:8080/health` and `curl localhost:8080/api/persons` — HTTP on port 8080, no HTTPS. D-01 and D-03 directly address this.
- Google Cloud Run in Phase 7 will also use HTTP internally (Cloud Run handles TLS). The `ASPNETCORE_HTTP_PORTS=8080` env var set here will carry forward or be overridden by Cloud Run's `PORT` env var. Phase 7 planner should note this compatibility.
- The 3 seeded persons (María García López, Carlos Ramírez Martínez, Ana Flores Mendoza) are seeded by `DataSeeder` on startup — no volume mount or external data source needed.

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope.

</deferred>

---

*Phase: 6-Containerization*
*Context gathered: 2026-06-02*
