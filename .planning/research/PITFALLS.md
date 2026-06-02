# Pitfalls Research

**Domain:** Containerizing and deploying a multi-project .NET 10 Web API to Google Cloud Run with GitHub Actions CI/CD
**Researched:** 2026-06-01
**Confidence:** HIGH — pitfalls verified against official Google Cloud Run container contract docs, Microsoft .NET Docker docs, Serilog official docs, and GitHub Actions OIDC documentation

---

## Critical Pitfalls

Mistakes that cause the container to not start, the deployment to fail silently, or the CI/CD pipeline to break on first run.

---

### Pitfall 1: Wrong COPY Paths in a Multi-Project Solution Dockerfile

**What goes wrong:** The Dockerfile COPY instructions reference paths that only exist in a single-project layout. The build stage fails with `COPY failed: file not found in build context` because the `.csproj` files are nested under `src/` and the context must be the solution root.

A common broken pattern:

```dockerfile
# WRONG — assumes Dockerfile sits next to the .csproj
COPY *.csproj ./
RUN dotnet restore
COPY . ./
```

When the Docker build context is the solution root (which it must be, to access all four projects), this copies nothing useful from `src/` because none of the `.csproj` files are at the root.

**Why it happens:** Most .NET Docker examples show a single-project layout where the Dockerfile sits next to the `.csproj`. The PersonsAPI solution has a `src/` subfolder with four separate `.csproj` files and a `tests/` subfolder — the Dockerfile must live at the solution root and COPY must mirror the directory tree.

**How to avoid:** Place the Dockerfile at the solution root. COPY each `.csproj` file individually before running `dotnet restore` so Docker layer caching works. Then COPY the full source.

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy solution file and each .csproj individually for layer-cached restore
COPY PersonsAPI.sln .
COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj          src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                src/PersonsAPI.Api/

# Restore uses the solution — resolves all project references
RUN dotnet restore PersonsAPI.sln

# Copy everything else (source files)
COPY src/ src/

# Publish the API project
RUN dotnet publish src/PersonsAPI.Api/PersonsAPI.Api.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

Build always with context at solution root:

```
docker build -t personsapi .
```

**Warning signs:**
- `COPY failed: file not found in build context` during `docker build`
- Dockerfile is placed inside `src/PersonsAPI.Api/` instead of the solution root
- Single `COPY *.csproj ./` instruction that grabs nothing when context is the solution root
- `dotnet restore` succeeds locally but fails in CI because context differs

**Phase to address:** Phase 1 (Dockerfile creation — DOCK-01). Get this right before any CI/CD integration.

---

### Pitfall 2: Cloud Run Port Mismatch — Container Listens on 5000, Cloud Run Expects 8080

**What goes wrong:** Cloud Run routes traffic to port 8080 by default (configurable via the `PORT` environment variable it injects). The ASP.NET Core development server defaults to `http://localhost:5000` (and `https://localhost:5001`). A container that only listens on 5000 will appear to deploy successfully — Cloud Run reports "Deployed" — but every request returns a 502 or the service never passes its health check, causing deployment to roll back immediately.

**Why it happens:** ASP.NET Core's Kestrel defaults are set for local development convenience. In a container, these defaults persist unless explicitly overridden. Cloud Run injects a `PORT` environment variable at runtime and expects the container to honor it. Most .NET documentation shows Kestrel config for development, not for Cloud Run deployment.

**How to avoid:** Configure the container to listen on `0.0.0.0:8080` (all interfaces, port 8080). The canonical approach for Cloud Run is to read the `PORT` env var:

```dockerfile
# In the runtime stage — set the URL Kestrel binds to
ENV ASPNETCORE_URLS=http://+:8080
```

Or, more correctly, read Cloud Run's injected `PORT` variable at startup. Add to `Program.cs` before `builder.Build()`:

```csharp
// Respect Cloud Run's PORT environment variable (defaults to 8080)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://+:{port}");
```

The `ENV ASPNETCORE_URLS=http://+:8080` approach in the Dockerfile is simpler and sufficient for this project. Do not set HTTPS in the container — Cloud Run terminates TLS externally.

**Warning signs:**
- `ASPNETCORE_URLS` is not set in the Dockerfile or as a Cloud Run environment variable
- The service deploys but returns 502 on every request
- Cloud Run logs show the container started but health check never succeeds
- `docker run -p 8080:5000` is used locally as a workaround — this masks the real issue (the container still only binds to 5000 internally)
- `EXPOSE 5000` is in the Dockerfile

**Phase to address:** Phase 1 (Dockerfile — DOCK-01). Test locally with `docker run -e PORT=8080 -p 8080:8080 personsapi` before deploying. Confirm `curl http://localhost:8080/health` returns 200.

---

### Pitfall 3: EF Core InMemory Data Does Not Survive Container Restarts

**What goes wrong:** Every time Cloud Run scales to zero and back (or restarts the container after a new deployment), all seeded data is gone. The 3 persons seeded by `DataSeeder` disappear. This is expected behavior for an in-memory store, but it surprises developers who confuse "the container is running" with "the data persists."

This is not a bug — it is the correct and expected behavior of the EF Core InMemory provider. It must be explicitly documented so the team does not spend time debugging "missing data" as if it were a deployment fault.

**Why it happens:** EF Core InMemory stores data in the process's heap. When the process exits (container stop/restart/scale-down), all data is lost. Cloud Run's stateless execution model makes this highly visible: Cloud Run scales to zero after inactivity by default, so the container restarts frequently.

**How to avoid (for this project's scope):** This is the known and accepted tradeoff of using EF Core InMemory for a learning project. The `DataSeeder` runs on every startup and restores the 3 baseline persons — this is intentional. No fix is needed for v2.0.

Document it explicitly in the Cloud Run deployment configuration so no one tries to "fix" it:

```yaml
# cloud-run-service.yaml comment
# NOTE: This service uses EF Core InMemory persistence.
# All data resets on every container restart / scale-to-zero event.
# This is expected behavior for the learning scope of this project.
# Real persistence (SQLite / PostgreSQL) is deferred to v2.1+.
```

**What would be wrong to do:** Mounting a Cloud Run volume (Cloud Run gen2 supports volume mounts) and trying to persist the InMemory database through a file — this is architecturally incorrect and signals a misunderstanding of EF Core InMemory. The correct path to persistence is switching the provider to SQLite or Cloud SQL (v2.1+ scope).

**Warning signs (misdiagnosis):**
- "Persons are missing after redeployment" treated as a bug to fix rather than expected behavior
- Attempts to add Cloud Run volume mounts to preserve in-memory state
- `DataSeeder` is removed because "data should already be there from the last run"

**Phase to address:** Phase 2 (Cloud Run deployment — CLOUD-01). Document in the Cloud Run service YAML comment. Verify `DataSeeder` runs on each container boot.

---

### Pitfall 4: Health Check Path Misconfiguration

**What goes wrong:** The health check endpoint is registered correctly in ASP.NET Core but the Cloud Run liveness/readiness probe targets the wrong path, or the endpoint is registered but the middleware pipeline order places it after authentication/authorization middleware that rejects the unauthenticated probe request.

Three distinct failure modes:

**Failure Mode A — Path mismatch:**
Cloud Run probe configured to `GET /healthz` but the endpoint is registered at `/health`. The probe returns 404, Cloud Run marks the service unhealthy, and the deployment fails or traffic does not route to the new revision.

**Failure Mode B — MapHealthChecks missing or ordered wrong in middleware pipeline:**
```csharp
// WRONG ORDER — health check endpoint unreachable if authorization runs first
app.UseAuthorization();
app.MapControllers();
// health check never mapped, or placed after auth rejects the probe
```

If this project adds authentication in a future milestone and the health endpoint is not explicitly excluded from auth, Cloud Run probes will get 401 responses and the service will appear unhealthy.

**Failure Mode C — Health check endpoint returns the wrong content type or status code:**
Cloud Run only requires an HTTP 200 response. It does not require a specific body. However, some health check configurations return 503 when a dependency (like a database) is unhealthy — with EF Core InMemory this is unlikely, but a misconfigured `AddDbContextCheck` or a typo in a custom check can return 503 consistently, causing Cloud Run to fail the deployment.

**How to avoid:**

Register health checks in `Program.cs` correctly:

```csharp
// Add health checks — no DB check needed for InMemory (it never goes down)
builder.Services.AddHealthChecks();

// Map before auth middleware so probes are never blocked
app.MapHealthChecks("/health");
// This must appear before app.UseAuthorization() if auth is added later
```

In Cloud Run service configuration, match the path exactly:

```yaml
livenessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
readinessProbe:
  httpGet:
    path: /health
    port: 8080
```

**Warning signs:**
- Health check endpoint returns 404 (path mismatch or `MapHealthChecks` not called)
- Service deploys but Cloud Run shows "container failed to start" or "health check failed"
- `curl http://localhost:8080/health` returns a non-200 response locally
- Health endpoint is behind a `[Authorize]` attribute or protected by middleware order

**Phase to address:** Phase 3 (Health check endpoint — OBS-02). Test with `curl` locally in Docker before deploying to Cloud Run. Verify with `docker run -e PORT=8080 -p 8080:8080 personsapi` that `http://localhost:8080/health` returns `200 Healthy`.

---

### Pitfall 5: GitHub Actions GCP Authentication — Service Account Key JSON vs Workload Identity Federation

**What goes wrong:** Developers store a long-lived service account key JSON file as a GitHub Actions secret (`GCP_SA_KEY`) and use it directly for authentication. This works but creates a persistent security risk: the key never expires, anyone with access to the secret can impersonate the service account indefinitely, and rotating the key requires manual action.

The more critical practical pitfall: developers generate the key, paste it incorrectly into the GitHub secret (truncated, with extra whitespace, or without base64-encoding when required), and the `google-github-actions/auth` action fails with a cryptic JSON parse error or `invalid_grant` — wasting hours of debugging.

**Why it happens:** Service account key JSON is the simplest path shown in most tutorials. Workload Identity Federation requires additional GCP setup (creating a Workload Identity Pool, configuring the provider, binding the service account) that seems complex on first encounter.

**Recommendation: Use Workload Identity Federation (WIF) for v2.0.** It is Google's officially recommended approach and GitHub Actions is explicitly supported. The one-time GCP setup takes ~10 minutes but eliminates secret rotation concerns permanently.

**How to avoid (WIF setup):**

GCP setup (one-time, via gcloud CLI):

```bash
# Create Workload Identity Pool
gcloud iam workload-identity-pools create "github-pool" \
  --location="global" --display-name="GitHub Actions Pool"

# Create provider for GitHub Actions
gcloud iam workload-identity-pools providers create-oidc "github-provider" \
  --location="global" \
  --workload-identity-pool="github-pool" \
  --display-name="GitHub provider" \
  --attribute-mapping="google.subject=assertion.sub,attribute.repository=assertion.repository" \
  --issuer-uri="https://token.actions.githubusercontent.com"

# Allow the pool to impersonate the service account
gcloud iam service-accounts add-iam-policy-binding \
  "deploy-sa@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/iam.workloadIdentityUser" \
  --member="principalSet://iam.googleapis.com/projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/github-pool/attribute.repository/GITHUB_ORG/REPO_NAME"
```

GitHub Actions workflow step:

```yaml
- name: Authenticate to GCP
  uses: google-github-actions/auth@v2
  with:
    workload_identity_provider: 'projects/PROJECT_NUMBER/locations/global/workloadIdentityPools/github-pool/providers/github-provider'
    service_account: 'deploy-sa@PROJECT_ID.iam.gserviceaccount.com'
```

**If the team uses service account key JSON as a fallback** (acceptable for a learning project with low security requirements), ensure the key is stored as the raw JSON string in the GitHub secret, not base64-encoded, unless the action version requires it. The `google-github-actions/auth@v2` action accepts raw JSON in `credentials_json`.

**Warning signs:**
- `invalid_grant` or `Could not load the default credentials` errors in the auth step
- The service account key JSON secret is truncated or has been modified (copy-paste truncation at 65KB GitHub secret limit — this is real for large JSON keys)
- Workflow fails at auth step with a JSON parse error
- Service account key is committed to the repository (critical security incident — rotate immediately)

**Phase to address:** Phase 4 (GitHub Actions CI/CD — CICD-01). Set up GCP auth in the first pipeline iteration. Do not defer — auth is the prerequisite for every subsequent pipeline step.

---

### Pitfall 6: Serilog Not Outputting JSON in the Container (Plain Text Logs in Cloud Logging)

**What goes wrong:** Serilog is added to the project and logs appear in the console during `dotnet run` locally — but in Cloud Run, Cloud Logging shows plain text like `[16:22:01 INF] GET /health 200` instead of structured JSON. Google Cloud Logging cannot parse severity, correlate traces, or query structured fields from plain text output.

Three failure modes:

**Failure Mode A — Console sink uses the default plain text formatter:**
```csharp
// This installs Serilog with a plain text console sink — NOT JSON
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();
```
The `WriteTo.Console()` default uses a human-readable template. Cloud Logging receives raw text, not JSON.

**Failure Mode B — JSON formatter configured but environment check prevents it in production:**
```csharp
// This only uses JSON in "Production" ASPNETCORE_ENVIRONMENT
// Cloud Run's default environment is not always "Production" — check this
if (app.Environment.IsProduction())
    Log.Logger = new LoggerConfiguration().WriteTo.Console(new JsonFormatter()).CreateLogger();
```
If `ASPNETCORE_ENVIRONMENT` is not explicitly set to `Production` in the Cloud Run service, the JSON branch never runs.

**Failure Mode C — `Serilog.Sinks.Console` package version does not include `CompactJsonFormatter`:**
The `CompactJsonFormatter` lives in `Serilog.Formatting.Compact` (a separate NuGet package). If only `Serilog.Sinks.Console` is installed, the formatter is not available and developers fall back to the default text template.

**How to avoid:**

Install both packages in the API project:

```xml
<PackageReference Include="Serilog.AspNetCore" Version="9.0.0" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
```

Configure JSON output unconditionally (not environment-gated — Cloud Run always wants JSON):

```csharp
// Program.cs — configure Serilog before builder.Build()
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));  // Always JSON — Cloud Logging parses this
```

Do not use `WriteTo.Console()` without the formatter. The `CompactJsonFormatter` from `Serilog.Formatting.Compact` produces CLEF (Compact Log Event Format) JSON that Cloud Logging understands.

For the best Cloud Logging integration, each log line should be a single-line JSON object on stdout — Cloud Run captures stdout and forwards it to Cloud Logging automatically when the format is recognized.

**Warning signs:**
- Cloud Logging shows log entries with no severity or with severity "DEFAULT" (plain text was not parsed)
- Local `dotnet run` shows colored output — this is the plain text template, not JSON
- `CompactJsonFormatter` gives a compile error — `Serilog.Formatting.Compact` package is missing
- `ASPNETCORE_ENVIRONMENT` is not set in Cloud Run service configuration

**Phase to address:** Phase 3 (Serilog logging — OBS-01). Verify by running the container locally with `docker run` and confirming stdout lines are valid JSON (`docker logs <container> | python -m json.tool` or pipe to `jq`).

---

## Technical Debt Patterns

Shortcuts that seem reasonable but create long-term problems.

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Service account key JSON in GitHub secret | Simpler GCP setup (no Workload Identity Pool) | Key never expires; must be rotated manually; leakage is permanent compromise | Acceptable for a personal learning project; never for production team repos |
| `ASPNETCORE_ENVIRONMENT=Development` in Cloud Run | Detailed error pages, Scalar UI accessible in cloud | Exposes stack traces publicly; disables production optimizations | Never — always set `Production` in Cloud Run |
| Pinning to `latest` Docker tag for the base image | Always gets newest SDK/runtime | Non-reproducible builds; future .NET releases may break the container | Never — pin to `10.0` or a specific digest |
| Skipping `.dockerignore` | No extra work | `.git/`, `tests/`, `**/obj/`, `**/bin/` are copied into the build context — slows build and risks leaking secrets in git history into the image | Never — always include `.dockerignore` |
| Hardcoding Cloud Run region in workflow | Fewer variables to configure | Region change requires editing the workflow file | Acceptable for a learning project; use a variable/secret in production |
| `dotnet publish` without `--no-restore` in CI | Simpler command | Doubles restore time; causes subtle differences between CI restore and local restore | Never in CI — always separate restore and publish steps |

---

## Integration Gotchas

Common mistakes when connecting to external services.

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| Artifact Registry (GCP) | Pushing to the wrong registry hostname — using `gcr.io` instead of the regional Artifact Registry hostname | Use `REGION-docker.pkg.dev/PROJECT_ID/REPO/IMAGE:TAG` format. `gcr.io` is legacy Container Registry, not Artifact Registry. |
| Cloud Run `gcloud run deploy` | Missing `--allow-unauthenticated` flag — service deploys but all requests return 403 | Add `--allow-unauthenticated` for a public API, or configure IAM invoker role explicitly |
| GitHub Actions `docker/build-push-action` | Build context defaults to `.` but the workflow file is in `.github/workflows/` — context is correct if the workflow's working directory is the repo root | Always set `context: .` explicitly in the build-push step |
| Cloud Run environment variables | `ASPNETCORE_ENVIRONMENT` not set — defaults to empty string, which ASP.NET Core treats as neither Development nor Production | Set `ASPNETCORE_ENVIRONMENT=Production` in Cloud Run service environment variables |
| Serilog + Cloud Logging | Two-level log aggregation: Cloud Run captures stdout, Cloud Logging ingests it. If Serilog also writes to a file sink, logs duplicate and the file is lost on container restart | Use only `WriteTo.Console()` in a containerized environment — never file sinks |
| `docker-compose` local vs Cloud Run | `docker-compose` `ports` mapping hides the Cloud Run port mismatch — `ports: "8080:5000"` makes local tests pass even when the container binds to the wrong internal port | Map `8080:8080` in compose and set `ASPNETCORE_URLS=http://+:8080` in the container so local and Cloud Run behave identically |

---

## Performance Traps

Patterns that work at small scale but fail as usage grows. (Scope note: Cloud Run autoscaling and EF InMemory make classic performance traps less relevant for this learning project — entries focus on container startup and CI/CD throughput.)

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| No `.dockerignore` — entire repo context sent to daemon | `docker build` takes 30+ seconds even for small code changes | Add `.dockerignore` excluding `**/obj`, `**/bin`, `.git`, `tests/` | Every build; gets worse as git history grows |
| COPY entire source before restore — no layer caching | Every code change rebuilds NuGet packages from scratch (minutes per build) | COPY `.csproj` files first, run `dotnet restore`, then COPY source | Every CI run; 3-5 minute penalty per commit |
| Cloud Run min-instances = 0 with EF InMemory + DataSeeder | First request after scale-to-zero takes 5-10 seconds (cold start + seed) | Acceptable for this project; set `--min-instances=1` if cold starts are unacceptable | Every scale-to-zero event |
| Running `dotnet test` in the same stage as `dotnet publish` | Tests add 30-60 seconds to every deploy pipeline | Separate test job from build/push job in GitHub Actions; use job dependencies | Every deployment |

---

## Security Mistakes

Domain-specific security issues for container deployment.

| Mistake | Risk | Prevention |
|---------|------|------------|
| Running the container as root (default) | If the process is compromised, attacker has root in the container | Add `USER app` to the Dockerfile runtime stage; use the non-root user from `mcr.microsoft.com/dotnet/aspnet` |
| Storing GCP credentials in a Dockerfile `ENV` | Credentials baked into the image layer — visible in `docker history` | Never put credentials in Dockerfile; use GitHub Actions secrets + WIF or runtime env vars injected by Cloud Run |
| `ASPNETCORE_ENVIRONMENT=Development` in Cloud Run | Scalar UI and detailed error pages are publicly accessible | Always set `Production` in Cloud Run; gate Scalar UI to `Development` only |
| Service account with `Editor` or `Owner` role | Blast radius if credentials leak: attacker can modify entire GCP project | Grant minimum roles: `roles/run.developer` + `roles/artifactregistry.writer` for the deploy SA |
| No `EXPOSE` in Dockerfile | Not a security issue — purely cosmetic — but misleads developers about what port the container uses | Add `EXPOSE 8080` to document the port; Cloud Run does not require it but it is correct self-documentation |

---

## "Looks Done But Isn't" Checklist

Things that appear complete but are missing critical pieces.

- [ ] **Dockerfile builds locally:** Run `docker build -t personsapi .` from the solution root — confirm zero errors before pushing to CI.
- [ ] **Container runs on correct port:** Run `docker run -e PORT=8080 -p 8080:8080 personsapi` and `curl http://localhost:8080/health` — confirm `200 Healthy`.
- [ ] **Health endpoint registered before auth middleware:** In `Program.cs`, `app.MapHealthChecks("/health")` must appear before any `app.UseAuthorization()` call.
- [ ] **JSON logs in container stdout:** Run the container and pipe stdout through `jq` — every line must be valid JSON. If any line is plain text, the Serilog formatter is wrong.
- [ ] **DataSeeder runs on container startup:** After `docker run`, call `GET /api/persons` — confirm 3 persons are returned (María, Carlos, Ana).
- [ ] **Cloud Run service listens on 8080:** After `gcloud run deploy`, call the Cloud Run URL — confirm `200` response. A `502` means port mismatch.
- [ ] **GitHub Actions auth step passes:** The `google-github-actions/auth` step must complete green before any `gcloud` or `docker push` step. A red auth step means every subsequent step will silently fail or use the wrong identity.
- [ ] **Artifact Registry push tag format correct:** Image tag must be `REGION-docker.pkg.dev/PROJECT_ID/REPO/image:SHA` — not `gcr.io/...` and not `latest`.
- [ ] **`ASPNETCORE_ENVIRONMENT=Production` set in Cloud Run:** Verify with `gcloud run services describe personsapi --format="value(spec.template.spec.containers[0].env)"`.
- [ ] **`.dockerignore` present:** Confirm `**/bin`, `**/obj`, `.git`, `tests/` are excluded. Build context size should be under 1 MB for this project.

---

## Recovery Strategies

When pitfalls occur despite prevention, how to recover.

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Wrong COPY paths in Dockerfile | LOW | Fix COPY instructions; rebuild locally first; push corrected Dockerfile |
| Cloud Run 502 (port mismatch) | LOW | Add `ENV ASPNETCORE_URLS=http://+:8080` to Dockerfile; rebuild and redeploy |
| EF InMemory data loss after restart | NONE | Expected behavior — no recovery needed. DataSeeder restores on boot. Document and move on. |
| Health check 404 in Cloud Run | LOW | Verify `MapHealthChecks("/health")` is in `Program.cs`; confirm path matches probe config; redeploy |
| GitHub Actions auth failure (SA key) | LOW-MEDIUM | Regenerate and re-upload the service account key JSON; switch to WIF to prevent recurrence |
| Plain text logs in Cloud Logging | LOW | Install `Serilog.Formatting.Compact`; change `WriteTo.Console()` to `WriteTo.Console(new CompactJsonFormatter())`; redeploy |
| Service account key committed to git | HIGH | Revoke key immediately in GCP Console; rotate; git history scrub with `git filter-repo`; switch to WIF |

---

## Pitfall-to-Phase Mapping

How roadmap phases should address these pitfalls.

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Wrong COPY paths in multi-project Dockerfile | Phase 1: Dockerfile (DOCK-01) | `docker build` succeeds from solution root; all 4 project layers resolve |
| Cloud Run port mismatch (5000 vs 8080) | Phase 1: Dockerfile (DOCK-01) | `docker run -e PORT=8080 -p 8080:8080 personsapi` + `curl /health` returns 200 |
| EF InMemory data loss on restart | Phase 2: Cloud Run deploy (CLOUD-01) | Documented expectation; DataSeeder verified on startup; no spurious "fix" attempted |
| Health check path misconfiguration | Phase 3: Health endpoint (OBS-02) | `curl http://localhost:8080/health` returns `200 Healthy` in Docker; Cloud Run probe green |
| GitHub Actions GCP auth (key vs WIF) | Phase 4: CI/CD pipeline (CICD-01) | Auth step completes green; no credentials in repository or Dockerfile |
| Serilog not outputting JSON | Phase 3: Serilog logging (OBS-01) | Container stdout lines are valid JSON; Cloud Logging shows severity field populated |
| Container running as root | Phase 1: Dockerfile (DOCK-01) | `docker inspect` shows non-root USER; `whoami` in container returns `app` |
| Missing `.dockerignore` | Phase 1: Dockerfile (DOCK-01) | Build context size < 1 MB; `docker build` output shows correct context size |

---

## Sources

- [Google Cloud Run container contract — Port and startup requirements](https://cloud.google.com/run/docs/container-contract) — HIGH confidence (official Google documentation; PORT env var injection and 8080 default are explicitly specified)
- [Containerize a .NET app — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/docker/build-container) — HIGH confidence (official Microsoft Docker guidance for multi-project .NET solutions)
- [.NET Docker samples — GitHub (dotnet/dotnet-docker)](https://github.com/dotnet/dotnet-docker/tree/main/samples/aspnetapp) — HIGH confidence (official multi-project Dockerfile pattern with solution-root context and per-csproj COPY)
- [Serilog.Formatting.Compact — NuGet + GitHub](https://github.com/serilog/serilog-formatting-compact) — HIGH confidence (official Serilog org; CompactJsonFormatter is the canonical JSON formatter for console sinks)
- [Serilog.AspNetCore — GitHub](https://github.com/serilog/serilog-aspnetcore) — HIGH confidence (official Serilog ASP.NET Core integration; UseSerilog() API)
- [GitHub Actions — Configuring OpenID Connect in Google Cloud Platform](https://docs.github.com/en/actions/security-for-github-actions/security-hardening-your-deployments/configuring-openid-connect-in-google-cloud-platform) — HIGH confidence (official GitHub documentation for WIF with GCP)
- [google-github-actions/auth — GitHub](https://github.com/google-github-actions/auth) — HIGH confidence (official GCP-maintained GitHub Action; WIF and SA key JSON both documented)
- [ASP.NET Core health checks — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) — HIGH confidence (official Microsoft docs; MapHealthChecks API and middleware ordering)
- [Cloud Run — Configure health checks](https://cloud.google.com/run/docs/configuring/healthchecks) — HIGH confidence (official Google documentation; liveness and readiness probe HTTP path configuration)
- [Deploying to Cloud Run with GitHub Actions — Google Cloud Blog](https://cloud.google.com/blog/products/devops-sre/deploy-to-cloud-run-with-github-actions) — MEDIUM confidence (official blog but may lag latest action versions; verify action versions against google-github-actions/deploy-cloudrun)

---
*Pitfalls research for: Containerizing a multi-project .NET 10 solution and deploying to Google Cloud Run with GitHub Actions CI/CD*
*Researched: 2026-06-01*
