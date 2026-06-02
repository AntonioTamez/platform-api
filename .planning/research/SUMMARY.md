# Project Research Summary

**Project:** PersonsAPI v2.0 — Cloud Deployment
**Domain:** .NET API containerization + Google Cloud Run CI/CD
**Researched:** 2026-06-01
**Confidence:** HIGH

## Executive Summary

PersonsAPI v2.0 takes a complete, working .NET 10 Clean Architecture API and makes it deployable to Google Cloud Run via a GitHub Actions CI/CD pipeline. The work is purely infrastructure — none of the Domain, Application, or Infrastructure layers change. All changes land at the composition root (`Program.cs`, `appsettings.json`) and in new files at the repository root (`Dockerfile`, `docker-compose.yml`, `.dockerignore`, `.github/workflows/deploy.yml`). The Clean Architecture boundary holds exactly as designed.

The recommended build sequence is driven by feature dependencies: Serilog + health check first (local only, zero containerization complexity), Dockerfile + docker-compose second (local container verification), manual Cloud Run deployment third, GitHub Actions pipeline last. This inside-out order keeps every step independently testable and avoids debugging three systems simultaneously.

The primary risk is configuration correctness, not code complexity. Six critical pitfalls are documented: wrong COPY paths in the Dockerfile for a multi-project solution, Cloud Run port mismatch (5000 vs 8080), Serilog emitting plain text instead of JSON, health check middleware ordering, GCP authentication setup, and EF InMemory data loss being mistaken for a bug. All six are preventable with explicit configuration choices made upfront.

## Key Findings

### Recommended Stack

Only two new NuGet packages are needed for the entire milestone. Health checks are built into ASP.NET Core 10 SDK — no package required. Docker uses the official Microsoft multi-stage image pattern. GitHub Actions uses the official Google `google-github-actions/*` action suite.

**Core additions:**
- `Serilog.AspNetCore 8.0.3`: Structured logging integration — replaces the default Microsoft logging
- `Serilog.Formatting.Compact 3.0.0`: CompactJsonFormatter for stdout — Cloud Logging ingests JSON automatically
- `mcr.microsoft.com/dotnet/sdk:10.0`: Docker build stage
- `mcr.microsoft.com/dotnet/aspnet:10.0`: Docker runtime stage (~200 MB final image)
- `google-github-actions/auth@v2` + `google-github-actions/deploy-cloudrun@v2`: GCP deployment from CI

### Expected Features

**Must have (required by Cloud Run):**
- Container listens on `$PORT` env var (default 8080) — Cloud Run injects this
- `/health` returns 200 OK — Cloud Run liveness probe requirement
- JSON-formatted logs on stdout — Cloud Logging ingestion requirement
- `ASPNETCORE_ENVIRONMENT=Production` — disables dev exception pages

**Should have (complete pipeline):**
- Multi-stage Dockerfile with layer-caching COPY pattern
- docker-compose for local cloud parity
- GitHub Actions: build → test → push to Artifact Registry → deploy
- `appsettings.Production.json` for environment-specific config

**Defer (v2.1+):**
- GCP-native Serilog sink (`Serilog.Sinks.GoogleCloudLogging`) for severity icons
- Separate liveness vs. readiness endpoints
- Real database (Cloud SQL) — EF InMemory stays for v2.0

### Architecture Approach

All v2.0 changes are confined to the composition root and new infra files. Zero changes to `PersonsAPI.Domain`, `PersonsAPI.Application`, or `PersonsAPI.Infrastructure`. The Dockerfile lives at the solution root (required for multi-project COPY).

**New files (5):**
1. `Dockerfile` (solution root) — multi-stage build for all 4 projects
2. `.dockerignore` (solution root) — excludes `obj/`, `bin/`, `.git/`, test projects
3. `docker-compose.yml` (solution root) — local development parity
4. `appsettings.Production.json` (src/PersonsAPI.Api/) — JSON Serilog sink
5. `.github/workflows/deploy.yml` — CI/CD pipeline

**Modified files (3):**
- `Program.cs` — PORT binding + UseSerilog + health checks + conditional HTTPS redirect
- `appsettings.json` — replace Logging section with Serilog configuration
- `PersonsAPI.Api.csproj` — add 2 Serilog package references

### Critical Pitfalls

1. **Wrong Dockerfile COPY paths** — `COPY *.csproj ./` finds nothing at solution root. Must copy each `.csproj` by its explicit relative path. Phase plan must list all 4 paths.
2. **Cloud Run port mismatch** — .NET defaults to 5000; Cloud Run sends traffic to 8080. Fix: read `PORT` env var in `Program.cs`. Returns 502 on every request without this.
3. **Serilog plain text output** — Default `WriteTo.Console()` produces unstructured text. Must use `WriteTo.Console(new CompactJsonFormatter())`.
4. **Health check middleware order** — `MapHealthChecks("/health")` must be before `UseAuthorization()`. Wrong order returns 401 on probes, blocking Cloud Run startup.
5. **UseHttpsRedirection in container** — Cloud Run terminates TLS at load balancer. Must guard: `if (!app.Environment.IsProduction()) app.UseHttpsRedirection()`.
6. **EF InMemory data loss on restart** — Container restart wipes InMemory state. `DataSeeder` re-seeds on boot. Expected behavior — document it, don't fix it.

## Implications for Roadmap

Based on research, suggested 4-phase structure (continuing from v1.0's Phase 4, so starting at Phase 5):

### Phase 5: Observability
**Rationale:** Local-only changes; zero containerization complexity; confirms 64 existing tests still pass before touching Docker.
**Delivers:** Serilog JSON logging + `/health` endpoint — both required by Cloud Run.
**Addresses:** OBS-01, OBS-02
**Avoids:** Debugging Serilog/health inside a container before proving they work locally.

### Phase 6: Containerization
**Rationale:** Prove the container works locally before CI. Docker issues (COPY paths, port binding, HTTPS redirect) are easier to diagnose with `docker logs` than through GitHub Actions logs.
**Delivers:** Dockerfile + docker-compose; `docker compose up` + `curl localhost:8080/health` = integration test.
**Addresses:** DOCK-01, DOCK-02
**Avoids:** The multi-project COPY pitfall and the port mismatch pitfall.

### Phase 7: Cloud Run Deployment
**Rationale:** First manual deploy proves GCP config (Artifact Registry, Cloud Run service, IAM roles) before automation. Avoids debugging infra + code simultaneously.
**Delivers:** Live public URL. Manual `gcloud run deploy` confirms everything before automating.
**Addresses:** CLOUD-01
**Avoids:** Blind CI debugging when GCP config is wrong.

### Phase 8: CI/CD Pipeline
**Rationale:** Automates steps already proven to work manually. Three-job sequence: build-and-test → push-image → deploy.
**Delivers:** Automatic deployment on every push to `main`.
**Addresses:** CICD-01
**Avoids:** GCP auth failures by using established service account or WIF pattern.

### Phase Ordering Rationale

- Observability before containers: Prove Serilog + health work with `dotnet run` before adding container complexity.
- Local containers before remote cloud: `docker compose up` is one command; GCP has 10+ IAM/config steps. Isolate Docker bugs first.
- Manual deploy before automated CI: Proves GCP config works; makes GitHub Actions debugging fast.
- CI/CD last: Automates known-working steps.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 7 (Cloud Run):** GCP first-time setup (Artifact Registry, IAM role grants, Cloud Run service) has multiple interdependent steps. Recommend a research pass at planning time — exact commands depend on user's GCP project ID and region.

Phases with standard patterns (skip research-phase):
- **Phase 5 (Observability):** Official ASP.NET Core health checks + Serilog docs. HIGH confidence.
- **Phase 6 (Containerization):** Official Microsoft multi-stage Dockerfile pattern. HIGH confidence.
- **Phase 8 (CI/CD):** `google-github-actions/*` action APIs fully documented. HIGH confidence once GCP config proven in Phase 7.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Verified against official sources. Serilog.AspNetCore 8.0.3 — verify on NuGet at implementation time. |
| Features | HIGH | Cloud Run contract from GCP official docs; health checks from Microsoft Learn. |
| Architecture | HIGH | All insertion points in Program.cs mapped explicitly. Zero ambiguity on file placement. |
| Pitfalls | HIGH | All 6 verified against official documentation. |

**Overall confidence:** HIGH

### Gaps to Address

- **GCP project ID and region** — User must decide before Phase 7. Affects Artifact Registry URL format: `REGION-docker.pkg.dev/PROJECT_ID/REPO/IMAGE`.
- **GitHub auth approach** — Workload Identity Federation (secure) vs. Service Account JSON key (simpler). User decision at Phase 8 planning.
- **Serilog.AspNetCore version** — 8.0.3 from training data; verify on NuGet for .NET 10 compatibility at implementation time.

## Sources

### Primary (HIGH confidence)
- Microsoft Learn — ASP.NET Core health checks (`AddHealthChecks`, `MapHealthChecks`)
- Microsoft Learn — Containerize a .NET app (multi-stage Dockerfile pattern, aspnet:10.0 image)
- Microsoft Learn — Web Host configuration (`UseUrls`, `ASPNETCORE_HTTP_PORTS`)
- Google Cloud Run docs — Container contract (PORT env var, startup probe, stdout logging)
- `serilog/serilog-aspnetcore` README — `UseSerilog()` bootstrap pattern

### Secondary (MEDIUM confidence)
- GitHub Actions `google-github-actions/*` README — action versions and workflow structure
- NuGet training data — Serilog package versions (verify at implementation time)

---
*Research completed: 2026-06-01*
*Ready for roadmap: yes*
