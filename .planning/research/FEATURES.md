# Feature Landscape

**Domain:** .NET 10 API — Docker containerization + GitHub Actions CI/CD + Google Cloud Run deployment
**Researched:** 2026-06-01
**Confidence:** HIGH — Docker and ASP.NET Core health check claims verified against official Microsoft docs (aspnetcore-10.0 moniker). Cloud Run behavior verified against Google Cloud docs redirect target. GitHub Actions workflow structure from well-established google-github-actions action suite. Serilog package behavior from training data (HIGH confidence — stable, multi-year API).

---

## Scope Note

This FEATURES.md covers **v2.0 additions only**. v1.0 features (CRUD, FluentValidation, Problem Details, OpenAPI/Scalar) are already built and in `.planning/research/FEATURES.md` from the prior milestone. The features below are additive — none replace existing v1.0 functionality.

---

## What Cloud Run Requires vs. What is Nice-to-Have

This is the most important distinction for v2.0 planning. Cloud Run has hard requirements; everything else is optional.

### Cloud Run Hard Requirements

| Requirement | Detail | Source |
|-------------|--------|--------|
| Container listens on `$PORT` (default 8080) | Cloud Run injects `PORT` env var; container must bind to it | Cloud Run container contract |
| HTTP response to health probe | Must return 2xx on startup probe path (default: any path on `$PORT`) | Cloud Run health check docs |
| Container starts within 4 minutes (default) | Startup timeout configurable up to 3600s, but 4 min is default | Cloud Run container contract |
| Linux container image (amd64) | Cloud Run runs on Linux; Windows containers not supported | Cloud Run platform constraint |
| JSON logs on stdout/stderr for Cloud Logging integration | Plaintext logs work but lose structure; JSON enables filtering/searching | Google Cloud Logging best practice |

### Nice-to-Have (Operational, Not Required to Deploy)

- Separate liveness vs readiness endpoints (Cloud Run supports both but does not require them)
- `/health` as the probe path (any path that returns 200 works; `/health` is convention)
- docker-compose for local parity (useful locally, irrelevant to Cloud Run)
- Image scanning in CI (security hygiene, not deployment blocker)

---

## Feature Areas

### Area 1: Dockerfile (DOCK-01)

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Multi-stage build | Build stage must not ship the SDK into runtime image; standard for .NET | LOW | Stage 1: `mcr.microsoft.com/dotnet/sdk:10.0` for restore + publish. Stage 2: `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime. |
| `dotnet restore` as a separate layer | NuGet restore is the slowest step; caching it avoids re-downloading packages on every push | LOW | Copy `*.sln` and all `*.csproj` files first, run `dotnet restore`, then copy source. This is the official Microsoft recommended pattern. |
| `dotnet publish -c Release` | Release build for production | LOW | Use `--no-restore` after the cached restore layer. Output to `/app`. |
| EXPOSE 8080 | Documents the port; Cloud Run defaults to 8080 | LOW | `EXPOSE 8080` is documentation only — Cloud Run uses the `PORT` env var, not EXPOSE. Set `ASPNETCORE_HTTP_PORTS=8080` or `ASPNETCORE_URLS=http://+:8080` to tell Kestrel where to listen. |
| ENTRYPOINT as exec form | Required for signal handling (SIGTERM) to work correctly; Cloud Run sends SIGTERM before killing a container | LOW | `ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]` — exec form, not shell form. Shell form wraps in `/bin/sh -c` which prevents signal propagation. |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Non-root user (`USER $APP_UID`) | Security hardening; .NET 8+ images ship a built-in `app` user (UID 1654); Cloud Run supports non-root | LOW | `USER $APP_UID` in the runtime stage. `$APP_UID` is pre-defined in `mcr.microsoft.com/dotnet/aspnet:10.0`. No custom useradd needed. |
| `.dockerignore` | Excludes `bin/`, `obj/`, `.git/`, test results from the build context; speeds up `docker build` | LOW | Essential for multi-project solution; without it the SDK restores against stale local artifacts. |
| SHA pinning on base images | Prevents supply-chain drift; ensures reproducible builds | MEDIUM | `FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:<digest>`. Adds maintenance burden of updating digests. Optional for a learning project; document the practice. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Single-stage Dockerfile | Ships the full .NET SDK (700+ MB) in the runtime image; final image is 3-4x larger than needed | Use multi-stage: SDK for build, aspnet runtime for final image (final size ~200 MB) |
| COPY . . before restore | Invalidates the NuGet restore cache on every source change; rebuild takes minutes instead of seconds | Copy only `*.csproj`/`*.sln` first, restore, then copy source |
| Running as root | Violates least-privilege; Cloud Run runs containers as root by default but the .NET aspnet image ships `$APP_UID` for a reason | Add `USER $APP_UID` before ENTRYPOINT |
| Hardcoding port 5000 or 80 | Cloud Run injects `PORT=8080`; hardcoded port breaks deployment | Use `ASPNETCORE_HTTP_PORTS=8080` env var in Dockerfile (or configure via Cloud Run service YAML) |

---

### Area 2: docker-compose (DOCK-02)

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `docker-compose.yml` that runs the API locally | Local parity — developers can `docker compose up` and hit the same image that ships to Cloud Run | LOW | Service: build from Dockerfile, port mapping `8080:8080`, env var `ASPNETCORE_ENVIRONMENT=Development` |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `docker-compose.override.yml` for dev vs. prod settings | Keeps production config clean; dev overrides mount local files or set dev environment variables | LOW | Override file sets `ASPNETCORE_ENVIRONMENT=Development`; base file is production-safe |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Committing secrets in docker-compose.yml | Exposes API keys/connection strings in version control | Use `.env` file (gitignored) or environment variable injection; docker-compose supports `env_file:` |

---

### Area 3: GitHub Actions CI/CD Pipeline (CICD-01)

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Trigger on `push` to `main` | Standard CD trigger; every merge to main deploys | LOW | `on: push: branches: [main]` |
| `dotnet restore` + `dotnet build` step | Verifies the code compiles in CI before building the image | LOW | Use `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` |
| `dotnet test` step | Runs all 64 existing tests; gates the deploy on green tests | LOW | `dotnet test --no-build` after build step; fail-fast |
| Authenticate to Google Cloud | Required before pushing to Artifact Registry or deploying to Cloud Run | MEDIUM | Use `google-github-actions/auth@v2` with Workload Identity Federation (preferred) or Service Account JSON key (simpler for learning) |
| `docker build` + `docker push` to Artifact Registry | Produces the container image that Cloud Run pulls | MEDIUM | Use `docker/build-push-action@v5` after auth; tag with `${{ github.sha }}` for traceability |
| Deploy to Cloud Run | The deployment step itself | MEDIUM | Use `google-github-actions/deploy-cloudrun@v2`; specify service name, region, image tag |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Workload Identity Federation (WIF) | Eliminates long-lived service account JSON key; uses short-lived tokens; Google's recommended auth pattern | MEDIUM | Requires one-time GCP setup (Workload Identity Pool + Provider); stored as GitHub secret `WORKLOAD_IDENTITY_PROVIDER` and `SERVICE_ACCOUNT`. More secure than JSON key. |
| Tag image with `git sha` | Makes every deployed image traceable to the exact commit; enables rollback | LOW | `IMAGE_TAG=${{ github.sha }}` — append to Artifact Registry path |
| Separate `build` and `deploy` jobs | Build can succeed but deploy can be gated on approval; clear separation of concerns | MEDIUM | Not required for a learning project; single job is acceptable |
| Cache Docker build layers in CI | Speeds up image builds significantly on repeated pushes | MEDIUM | `docker/setup-buildx-action` + `cache-from: type=gha` — GitHub Actions cache for BuildKit layers |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Service account JSON key committed to repo | Critical security violation — rotatable secrets must never be in source | Store as GitHub Actions secret; prefer WIF over JSON keys |
| Deploying latest tag | `latest` tag makes rollbacks ambiguous; you can't tell which commit a `latest` image corresponds to | Tag with `github.sha`; optionally also tag `latest` for human convenience but deploy by SHA tag |
| Building image without running tests first | Shipping broken code to Cloud Run; wastes push/deploy time | Always run `dotnet test` before `docker build` in the workflow |
| Using `actions/checkout@v2` or older | v2 is outdated; security improvements and performance fixes are in v4 | Use `actions/checkout@v4` |

---

### Area 4: Health Check Endpoint (OBS-02)

#### Cloud Run Probe Behavior (Verified Against Official Docs)

Cloud Run supports two probe types:
- **Startup probe**: checks that the container successfully started. Default: Cloud Run polls any path on the container port until it gets a 2xx response. Configured via service YAML `startupProbe`.
- **Liveness probe**: periodically checks that the container is still healthy. If it fails, Cloud Run restarts the container. Configured via service YAML `livenessProbe`.

Cloud Run does NOT have a readiness probe concept (that is Kubernetes-specific). Cloud Run withholds traffic until the startup probe passes.

Default startup probe: Cloud Run polls `GET /` on `$PORT` every 10 seconds for up to 240 seconds. A `200 OK` response passes. This means the API works without any `/health` endpoint — but `/health` is the conventional, explicit path.

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| `/health` endpoint returning `200 OK` | Cloud Run needs proof the container started; the conventional path is `/health` | LOW | `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")` in Program.cs. No custom checks needed for InMemory app. |
| Responds within startup timeout | Cloud Run default: 240s; Kestrel cold start on .NET 10 is under 2s | LOW | No action needed beyond ensuring app boots; document the default timeout |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Separate `/health/live` and `/health/ready` endpoints | Follows Kubernetes naming convention; Cloud Run ignores readiness but the split is forward-compatible | LOW | `app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })` — liveness excludes all checks (just proves the process is alive). `app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = hc => hc.Tags.Contains("ready") })` — readiness runs tagged checks. |
| JSON response body on health endpoint | Returns `{ "status": "Healthy", "totalDuration": "...", "entries": {} }` — useful for debugging | LOW | Set `ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse` if using AspNetCore.HealthChecks.UI.Client, or write a custom JSON writer using `JsonSerializer`. Default writer returns plaintext "Healthy". |
| Tag health checks for selective probing | Allows future database checks, downstream checks to be selectively included in ready vs live probes | LOW | `AddHealthChecks().AddCheck<MyCheck>("my-check", tags: new[] { "ready" })` |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Custom `/health` controller action | Adds unnecessary MVC routing overhead; `MapHealthChecks` is purpose-built middleware | Use `app.MapHealthChecks("/health")` directly in Program.cs |
| Health check that calls the InMemory database | InMemory provider never fails; the check would always return Healthy and adds zero signal | For InMemory: register no custom checks. If DB is swapped to real in v2.1, add `AddDbContextCheck<AppDbContext>()` then. |
| Protecting `/health` with authorization | Cloud Run's probe cannot authenticate; a protected `/health` endpoint will fail the startup probe | Map health checks without `RequireAuthorization()`. They should be publicly accessible on the container port. |

---

### Area 5: Serilog Structured Logging (OBS-01)

#### Google Cloud Logging Integration

Google Cloud Logging reads stdout/stderr from Cloud Run containers. If the output is a single JSON object per line, Cloud Logging automatically parses fields like `severity`, `message`, `timestamp`, `labels`, etc. If the output is plaintext, Cloud Logging stores it as an unstructured string — no filtering, no severity levels.

The key field for Cloud Logging severity mapping is `"severity"` (not Serilog's default `"Level"`). Serilog's `CompactJsonFormatter` writes `"@l"` for level. The `Serilog.Sinks.GoogleCloudLogging` sink or a custom formatter that maps to `"severity"` is needed for first-class integration.

**Minimum viable approach**: `Serilog.Formatting.Compact` with `CompactJsonFormatter` outputs one JSON line per log entry. Cloud Logging ingests it and stores it as a JSON payload. The `@l` field does not auto-map to severity labels — logs appear at the default severity. This is acceptable for a learning project.

**Production approach**: Use `Serilog.Sinks.GoogleCloudLogging` which writes the `severity` field in Google's format and maps Serilog log levels to Cloud Logging severity levels (DEBUG, INFO, WARNING, ERROR, CRITICAL).

#### Table Stakes

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Replace default Microsoft console logger with Serilog | Default ASP.NET Core logger outputs plaintext; Serilog with a JSON formatter outputs structured JSON | LOW | `Serilog.AspNetCore` package. `UseSerilog()` on `WebApplicationBuilder.Host`. |
| JSON output to stdout | Cloud Run captures stdout; JSON format enables Cloud Logging parsing | LOW | `Serilog.Formatting.Compact` package. `WriteTo.Console(new CompactJsonFormatter())` in Serilog config. |
| Request logging middleware | Logs every HTTP request with method, path, status code, elapsed time as structured fields | LOW | `.UseSerilogRequestLogging()` in the middleware pipeline — single call, replaces verbose Microsoft request logging with one structured log per request. |
| `appsettings.json` configuration | Log levels configurable without recompile | LOW | Serilog reads from `Serilog` section in appsettings.json; `MinimumLevel`, `Override` per namespace |

#### Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| `Serilog.Sinks.GoogleCloudLogging` sink | Maps Serilog levels to Cloud Logging severity labels; logs appear with correct INFO/WARNING/ERROR icons in Cloud Console | MEDIUM | Adds `Serilog.Sinks.GoogleCloudLogging` NuGet package. Requires GCP credentials in container (ADC or service account). For Cloud Run, ADC is available automatically via the instance service account. |
| Enrichers: `FromLogContext`, `WithMachineName`, `WithEnvironmentName` | Adds context fields to every log entry — useful for correlating logs across instances | LOW | `Serilog.Enrichers.Environment` + `.Enrich.FromLogContext()` + `.Enrich.WithMachineName()`. Cloud Run sets `K_SERVICE`, `K_REVISION`, `K_CONFIGURATION` env vars automatically — can enrich with these. |
| Destructuring request bodies (selectively) | Logs structured command/query data instead of raw strings — enables log-driven debugging | MEDIUM | `.Destructure.ByTransforming<CreatePersonCommand>(c => new { c.FirstName, ... })`. Avoid logging PII; for PersonsAPI with names + DOB this is a sensitivity decision. |

#### Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Serilog.Sinks.File in Cloud Run | Cloud Run containers are ephemeral; files written inside the container are lost on restart; disk in Cloud Run is not persistent | Write to stdout only (Console sink). Use Cloud Logging for persistence and querying. |
| `new LoggerConfiguration().CreateLogger()` before builder.Build() (two-phase bootstrap) | Two-phase Serilog setup (bootstrap logger then full logger) adds complexity; only needed if you want to log startup errors before the host is built | For a learning project: configure Serilog fully inside `UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration))` — single phase, simpler, reads appsettings. |
| Using both Serilog and the default Microsoft logging simultaneously | Causes duplicate log entries; wastes CPU on two log pipelines | Call `builder.Logging.ClearProviders()` before or rely on `UseSerilog(clearProviders: true)` to replace the default providers. |
| Logging sensitive PII fields | DateOfBirth and full names are PII; logging them creates compliance issues | Log operation context (person ID, command type) not field values. Serilog destructuring can exclude sensitive properties. |

---

## Feature Dependencies

```
Cloud Run deployment (CLOUD-01)
    requires: Dockerfile with port 8080 (DOCK-01)
    requires: Health check endpoint at /health (OBS-02)
                 requires: AddHealthChecks() + MapHealthChecks() in Program.cs
    requires: JSON-format logs on stdout (OBS-01)
                 requires: Serilog + CompactJsonFormatter

GitHub Actions CI/CD (CICD-01)
    requires: Dockerfile (DOCK-01)
    requires: GCP Artifact Registry (manual one-time setup in GCP console)
    requires: Cloud Run service (first deploy creates it; subsequent deploys update it)
    requires: GCP IAM permissions for the deploying service account
    enhances: docker-compose (DOCK-02) — local build validation matches CI build

docker-compose (DOCK-02)
    requires: Dockerfile (DOCK-01)
    enhances: local development — run the exact runtime image before pushing

Health check (OBS-02)
    requires: ASP.NET Core health checks middleware (built-in, no extra NuGet)
    independent: can be added before or after Serilog/Docker work

Serilog (OBS-01)
    requires: Serilog.AspNetCore NuGet package
    requires: Serilog.Formatting.Compact NuGet package (for JSON output)
    optional-enhancer: Serilog.Sinks.GoogleCloudLogging (for severity-mapped Cloud Logging)
    independent: can be added before Docker/CI work; works locally too
```

### Dependency Notes

- **Health check requires no DB check for this project:** EF InMemory never fails, so `AddHealthChecks()` with no registered checks returns `Healthy` by default. This is correct behavior — the check proves the process is alive, not that data is valid.
- **Port 8080 is the Cloud Run default but must be explicitly configured in Kestrel:** ASP.NET Core 10 defaults to port 5000 (HTTP) and 5001 (HTTPS) in development. In the Dockerfile (or via Cloud Run service env vars), set `ASPNETCORE_HTTP_PORTS=8080`. Do not set `ASPNETCORE_URLS=https://+:8080` — Cloud Run terminates TLS at the load balancer; the container only needs HTTP.
- **HTTPS is not needed in the container:** Cloud Run handles TLS termination. The container talks plain HTTP on port 8080. Remove HTTPS redirection middleware (`app.UseHttpsRedirection()`) when running in Cloud Run, or make it conditional on environment.

---

## MVP Definition (v2.0 Launch Sequence)

Build in this order — each step is independently testable:

### Launch With (v2.0)

- [ ] **Serilog with JSON output (OBS-01)** — lowest risk, fully local, improves all subsequent debugging. Add before any Docker work.
- [ ] **Health check at `/health` (OBS-02)** — trivial to add, required by Cloud Run. Add while still running locally.
- [ ] **Dockerfile multi-stage (DOCK-01)** — verify `docker build` + `docker run -p 8080:8080` works locally before writing CI.
- [ ] **docker-compose (DOCK-02)** — `docker compose up` as local integration test of the image.
- [ ] **GitHub Actions CI/CD (CICD-01)** — build → test → push to Artifact Registry → deploy to Cloud Run.

### Add After Validation (v2.x)

- [ ] Workload Identity Federation — replace JSON key auth with WIF after baseline deployment is confirmed.
- [ ] `Serilog.Sinks.GoogleCloudLogging` — add after confirming basic JSON logs are visible in Cloud Console.
- [ ] Separate `/health/live` and `/health/ready` endpoints — add after basic `/health` is confirmed working.
- [ ] Docker BuildKit layer caching in CI — add after basic CI pipeline is stable.

### Future Consideration (v3+)

- [ ] Image vulnerability scanning (Trivy or Google Artifact Analysis) in CI pipeline.
- [ ] Cloud Run traffic splitting (blue/green) for zero-downtime deploys.
- [ ] Cloud Run min-instances to avoid cold starts.

---

## Feature Prioritization Matrix

| Feature | Value to v2 Goal | Implementation Cost | Priority |
|---------|-----------------|---------------------|----------|
| Dockerfile (multi-stage, port 8080) | HIGH — nothing deploys without it | LOW | P1 |
| Health check `/health` | HIGH — Cloud Run requires 200 response | LOW | P1 |
| Serilog JSON logging | HIGH — Cloud Logging usability | LOW | P1 |
| GitHub Actions CI/CD | HIGH — the whole point of v2 | MEDIUM | P1 |
| docker-compose local parity | MEDIUM — local dev quality | LOW | P2 |
| WIF auth vs. JSON key | MEDIUM — security improvement | MEDIUM | P2 |
| Separate live/ready endpoints | LOW — Cloud Run doesn't require it | LOW | P3 |
| GCL sink for severity mapping | LOW — logs still work without it | MEDIUM | P3 |
| BuildKit layer cache in CI | LOW — nice speedup | MEDIUM | P3 |

**Priority key:**
- P1: Must have for v2.0 launch
- P2: Should have, add when P1 is stable
- P3: Nice to have, defer to v2.1

---

## Packages Required for v2.0

| Package | Purpose | NuGet |
|---------|---------|-------|
| `Serilog.AspNetCore` | Serilog integration with ASP.NET Core host; `UseSerilog()`, `UseSerilogRequestLogging()` | serilog/serilog-aspnetcore |
| `Serilog.Formatting.Compact` | `CompactJsonFormatter` — one JSON line per log entry; Cloud Logging compatible | serilog/serilog-formatting-compact |
| No new package for health checks | `Microsoft.AspNetCore.Diagnostics.HealthChecks` is built into ASP.NET Core 10 SDK | — |

Optional (P3):

| Package | Purpose |
|---------|---------|
| `Serilog.Sinks.GoogleCloudLogging` | Maps Serilog levels to Cloud Logging severity; structured metadata |
| `AspNetCore.HealthChecks.UI.Client` | JSON response writer for health check endpoint; returns detailed check results |

---

## Sources

- Microsoft Docs — Run ASP.NET Core in Docker (aspnetcore-10.0): https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-10.0
- Microsoft Docs — Health checks in ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
- Microsoft Docs — .NET container images (sdk vs aspnet, non-root user): https://learn.microsoft.com/en-us/dotnet/core/docker/container-images
- Microsoft Docs — Logging in ASP.NET Core 10: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/
- Microsoft Docs — Containerize a .NET app with Docker: https://learn.microsoft.com/en-us/dotnet/core/docker/build-container
- Google Cloud Run container contract: https://cloud.google.com/run/docs/container-contract
- Google Cloud Run health checks: https://cloud.google.com/run/docs/configuring/healthchecks
- google-github-actions/deploy-cloudrun action: https://github.com/google-github-actions/deploy-cloudrun
- google-github-actions/auth (WIF): https://github.com/google-github-actions/auth
- Serilog.AspNetCore GitHub: https://github.com/serilog/serilog-aspnetcore
- Serilog.Formatting.Compact GitHub: https://github.com/serilog/serilog-formatting-compact

---

*Feature landscape for: v2.0 Cloud Deployment (Docker + GitHub Actions CI/CD + Cloud Run + Health Checks + Serilog)*
*Researched: 2026-06-01*
