# Stack Research

**Domain:** .NET 10 Web API — Docker / CI/CD / Google Cloud Run / Observability (v2.0 additions)
**Researched:** 2026-06-01
**Confidence:** MEDIUM-HIGH (external tool access unavailable; based on verified training data through Aug 2025 — NuGet versions for Serilog sinks need online validation before use)

> This file covers ONLY the stack additions for v2.0. The v1.0 stack (EF Core, FluentValidation,
> Mediator, Scalar, etc.) is locked and documented in CLAUDE.md. Do not re-research those.

---

## Recommended Stack

### Containerization

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| mcr.microsoft.com/dotnet/sdk | 10.0 | Multi-stage build stage (compile) | Official Microsoft SDK image; matches project target framework exactly. Use `10.0` floating tag to get latest patch automatically in CI, or pin to `10.0.x` for reproducibility. |
| mcr.microsoft.com/dotnet/aspnet | 10.0 | Multi-stage runtime stage (final image) | Runtime-only image (~250 MB vs ~900 MB SDK image). Smaller attack surface, faster pull. |
| mcr.microsoft.com/dotnet/aspnet | 10.0-alpine | Runtime stage (optional, smallest image) | Alpine-based image (~100 MB). Use if image size matters (Cloud Run cold start). Caveat: alpine uses musl libc — test for globalization/ICU issues. For a REST API with DateOnly, test culture-dependent date parsing before committing. |
| Docker Compose V2 | 3.8+ file format | Local development parity | `docker compose` (no hyphen) is the current CLI. Compose V2 is bundled in Docker Desktop. Maps to same env vars Cloud Run will see. |

**Recommended Dockerfile pattern:** Multi-stage with explicit `restore`, `build`, `publish` stages. Use `--no-restore` on build/publish after restoring in a dedicated stage to maximize layer cache hits.

```dockerfile
# Stage 1 — restore (cached separately from build)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY ["src/PersonsAPI.Api/PersonsAPI.Api.csproj", "src/PersonsAPI.Api/"]
COPY ["src/PersonsAPI.Application/PersonsAPI.Application.csproj", "src/PersonsAPI.Application/"]
COPY ["src/PersonsAPI.Domain/PersonsAPI.Domain.csproj", "src/PersonsAPI.Domain/"]
COPY ["src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj", "src/PersonsAPI.Infrastructure/"]
RUN dotnet restore "src/PersonsAPI.Api/PersonsAPI.Api.csproj"

# Stage 2 — build + publish
FROM restore AS publish
COPY . .
RUN dotnet publish "src/PersonsAPI.Api/PersonsAPI.Api.csproj" \
    -c Release --no-restore -o /app/publish

# Stage 3 — runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PersonsAPI.Api.dll"]
```

**Cloud Run requires port 8080** (default for ASP.NET Core in container since .NET 8 — the framework now defaults to HTTP on 8080, not 5000, when `ASPNETCORE_HTTP_PORTS=8080` is set by the Docker base image). No `EXPOSE 443` needed for Cloud Run — TLS is terminated at the Google load balancer.

---

### Health Checks

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Microsoft.AspNetCore.Diagnostics.HealthChecks | (built into ASP.NET Core 10 SDK — no NuGet package) | `/health` endpoint | Zero dependencies. `AddHealthChecks()` + `MapHealthChecks("/health")` is sufficient for Cloud Run liveness probes. Returns 200 OK when healthy, 503 when degraded. |

**No external NuGet package needed** for a basic `/health` endpoint. The `AspNetCore.HealthChecks.*` family (from xabaril/AspNetCore.Diagnostics.HealthChecks) is only necessary when checking external dependencies — e.g., SQL Server, Redis, RabbitMQ. Since this project uses EF InMemory with no external dependencies, the built-in health check is correct.

Do NOT add `AspNetCore.HealthChecks.UI` — it adds unnecessary overhead for a Cloud Run API with no persistent UI.

Registration pattern (in `Program.cs`):

```csharp
builder.Services.AddHealthChecks();
// ...
app.MapHealthChecks("/health");
```

Cloud Run configuration: set liveness probe HTTP path to `/health`, initial delay 5s, period 10s.

---

### Structured Logging (Serilog)

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| Serilog | 4.2.0 | Core Serilog library | Current stable major version. Provides the `Log` static API and `ILogger`. |
| Serilog.AspNetCore | 8.0.3 | ASP.NET Core integration | Hooks into `IHostBuilder` via `UseSerilog()`. Captures request logs with enrichers. Replaces Microsoft.Extensions.Logging pipeline. |
| Serilog.Formatting.Compact | 3.0.0 | Compact JSON formatter (CLEF format) | Produces single-line JSON logs. Used with `Serilog.Sinks.Console` for Cloud Run stdout capture. Google Cloud Logging ingests stdout/stderr as structured JSON when the top-level field is `message` or when using the `jsonPayload` field. |
| Serilog.Sinks.Console | 6.0.0 | Console (stdout) output sink | Cloud Run captures stdout. With `CompactJsonFormatter`, output is machine-readable JSON that Google Cloud Logging parses as structured data. |
| Serilog.Sinks.GoogleCloudLogging | 5.x (VERIFY) | Direct Cloud Logging API sink (optional) | Sends logs directly to Cloud Logging API, bypassing stdout capture. Adds the `httpRequest`, `severity`, and `labels` GCP-native fields. Only needed if you need `ERROR_REPORTING` integration or custom log labels beyond what stdout JSON provides. |

**Confidence note on versions:** Serilog 4.2.0 and Serilog.AspNetCore 8.0.3 are verified from training data. `Serilog.Sinks.GoogleCloudLogging` 5.x is MEDIUM confidence — verify on NuGet before use (package: `Serilog.Sinks.GoogleCloudLogging` by manigandham). `Serilog.Formatting.Compact` 3.0.0 and `Serilog.Sinks.Console` 6.0.0 are HIGH confidence.

**Recommendation: use Console sink with CompactJsonFormatter** — not the direct GCP sink — unless you need GCP-specific severity mapping or error reporting. Stdout JSON is simpler to debug locally and works identically in Cloud Run.

```json
// Google Cloud Logging sees this as structured JSON when logged to stdout:
{"@t":"2026-06-01T10:00:00Z","@m":"Person created","@l":"Information","PersonId":"abc-123"}
```

Google Cloud Logging maps `@l` → severity automatically when the JSON formatter is `CompactJsonFormatter`.

**IMPORTANT:** If you want GCP severity levels (DEBUG, INFO, WARNING, ERROR, CRITICAL) rather than Serilog's level names, use `Serilog.Sinks.GoogleCloudLogging` which outputs `severity` as the native GCP field. For a learning project, stdout JSON is sufficient.

Registration pattern:

```csharp
// Program.cs — before builder.Build()
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new CompactJsonFormatter()));
```

```xml
<!-- PersonsAPI.Api.csproj -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />
```

`Serilog.Sinks.Console` is pulled transitively by `Serilog.AspNetCore` — no separate reference needed.

---

### CI/CD — GitHub Actions

| Action | Version | Purpose | Why |
|--------|---------|---------|-----|
| actions/checkout | v4 | Checkout source | Current stable; v4 uses Node 20. |
| actions/setup-dotnet | v4 | Install .NET SDK | Supports .NET 10; `dotnet-version: '10.x'`. |
| docker/setup-buildx-action | v3 | Enable BuildKit (multi-platform, layer cache) | Required for `docker/build-push-action`. BuildKit is mandatory for `--cache-from` / `--cache-to`. |
| docker/login-action | v3 | Authenticate to Artifact Registry | Use with `registry: [REGION]-docker.pkg.dev`, `username: oauth2accesstoken`, `password: ${{ steps.auth.outputs.access_token }}`. |
| docker/build-push-action | v6 | Build and push Docker image | Supports BuildKit cache, multi-platform, provenance attestation. v6 is stable as of mid-2025. |
| google-github-actions/auth | v2 | Authenticate to GCP via Workload Identity | Recommended over service account JSON keys. Uses `workload_identity_provider` + `service_account`. Keyless — no long-lived credentials in secrets. |
| google-github-actions/deploy-cloudrun | v2 | Deploy image to Cloud Run | Wraps `gcloud run deploy`. Supports `--region`, `--image`, `--platform managed`, `--allow-unauthenticated`. |

**Confidence:** HIGH for action versions — these are the current stable major versions as of August 2025. Pin to major version (`@v2`, `@v3`, `@v4`) to get patch updates automatically.

**Recommended pipeline order:**
1. `checkout` → `setup-dotnet` → `dotnet restore` → `dotnet build` → `dotnet test` (fail fast before Docker)
2. `google-github-actions/auth` (Workload Identity)
3. `docker/setup-buildx-action` → `docker/login-action` (Artifact Registry)
4. `docker/build-push-action` (build + push image tagged with `$GITHUB_SHA`)
5. `google-github-actions/deploy-cloudrun` (deploy the `$GITHUB_SHA`-tagged image)

**Workload Identity Federation vs Service Account Key:**
Use Workload Identity Federation. A service account JSON key stored in GitHub Secrets is a long-lived credential that can be leaked. Workload Identity issues short-lived tokens to GitHub Actions runners with no stored secret beyond the `workload_identity_provider` URL.

---

### Google Cloud Configuration

| Resource | Recommended Setting | Why |
|----------|--------------------|----|
| Artifact Registry format | Docker | Standard container registry; replaces Container Registry (gcr.io). |
| Registry URL pattern | `[REGION]-docker.pkg.dev/[PROJECT]/[REPO]/persons-api:[SHA]` | Tag with `$GITHUB_SHA` for traceability; also tag `latest` for convenience. |
| Cloud Run platform | `--platform managed` | Fully serverless; no cluster to manage. |
| Cloud Run port | 8080 | Matches `ASPNETCORE_HTTP_PORTS=8080` set in the .NET 10 base image. Do not change. |
| Cloud Run concurrency | 80 (default) | ASP.NET Core handles concurrent requests well; default is fine for this scale. |
| Cloud Run min instances | 0 | Cost-efficient for a learning project; accept cold starts. |
| Cloud Run max instances | 10 | Prevent runaway billing during testing. |
| Cloud Run memory | 512 MB | Sufficient for a .NET 10 API with InMemory EF. 256 MB can OOM on startup. |
| Cloud Run CPU | 1 | Default; adequate for this API. |
| Health check path | `/health` | Set as the liveness probe path in Cloud Run service YAML. |

---

### Docker Compose (Local Parity)

```yaml
# docker-compose.yml — local development
services:
  persons-api:
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
      interval: 10s
      timeout: 5s
      retries: 3
      start_period: 10s
```

No external service containers needed — EF InMemory has no external dependency. The `ASPNETCORE_HTTP_PORTS=8080` environment variable matches what Cloud Run sets, ensuring local Docker and Cloud Run behave identically.

---

## Installation

```xml
<!-- PersonsAPI.Api.csproj — new references for v2.0 -->
<PackageReference Include="Serilog.AspNetCore" Version="8.0.3" />
<PackageReference Include="Serilog.Formatting.Compact" Version="3.0.0" />

<!-- Optional: only if direct GCP sink is needed -->
<!-- <PackageReference Include="Serilog.Sinks.GoogleCloudLogging" Version="5.x" /> -->
```

No new NuGet packages for health checks — built into the SDK.

No new NuGet packages for Docker or GitHub Actions — those are infrastructure, not application dependencies.

---

## Alternatives Considered

| Category | Recommended | Alternative | When to Use Alternative |
|----------|-------------|-------------|------------------------|
| Docker base image | `aspnet:10.0` (Debian) | `aspnet:10.0-alpine` | Use alpine only if cold-start time is a measured concern AND you have verified no globalization/culture issues. Not worth the complexity for a learning project. |
| Logging sink | Console + CompactJsonFormatter (stdout) | Serilog.Sinks.GoogleCloudLogging (direct API) | Use direct sink only if you need GCP Error Reporting integration, custom log labels, or structured `httpRequest` field mapping. Stdout JSON is simpler and works well for Cloud Run. |
| GCP Auth in CI | Workload Identity Federation (keyless) | Service Account JSON key in GitHub Secrets | JSON key is acceptable if Workload Identity setup is blocked by org policy, but it introduces long-lived credential risk. |
| Health check | ASP.NET Core built-in | AspNetCore.HealthChecks.* (xabaril) | Use xabaril packages only when checking external dependencies (SQL, Redis). InMemory EF has no external dependencies to check. |
| Cloud Run image tag | `$GITHUB_SHA` (short hash) | Semantic version (`v2.0.1`) | Semantic versioning is better for published APIs; SHA is simpler for a learning project with no external consumers. |
| CI/CD workflow trigger | `push` to `main` branch | PR-only or manual `workflow_dispatch` | Add `workflow_dispatch` for manual deploys. Branch push is correct for continuous deployment. |

---

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| gcr.io (Container Registry) | Google deprecated Container Registry in favor of Artifact Registry. gcr.io redirects exist but are scheduled for sunset. | `[REGION]-docker.pkg.dev` (Artifact Registry) |
| Swashbuckle health check UI | Does not exist; `HealthChecks.UI` from xabaril is the equivalent — adds a dashboard with a persistence store. Overkill for a Cloud Run service with no persistent backing. | Built-in health check + Cloud Run uptime monitoring |
| `ASPNETCORE_URLS=http://+:80` | Port 80 in container requires root on some Linux configurations; .NET 8+ base images default to 8080. Mixing 80 and 8080 causes confusion in Cloud Run. | `ASPNETCORE_HTTP_PORTS=8080` (already set by base image) |
| `docker-compose` (V1, hyphen) | Deprecated and removed in recent Docker versions. V1 was a separate Python binary. | `docker compose` (V2, space, built into Docker CLI) |
| Serilog.AspNetCore + manual `Log.Logger` setup without `UseSerilog()` | Manual setup misses ASP.NET Core's `ILogger<T>` integration and enrichers (request ID, user). | `builder.Host.UseSerilog(...)` which wires both. |
| `--self-contained true` in Dockerfile | Produces a ~200 MB binary that duplicates the runtime already in the base image. Increases image size and invalidates the layer-caching benefit of using a runtime base image. | `dotnet publish` without `--self-contained` (defaults to framework-dependent) |
| Service Account JSON key stored in GitHub Secrets | Long-lived credential. If the secret leaks, it remains valid until manually rotated. | Workload Identity Federation (keyless, short-lived tokens) |

---

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| Serilog.AspNetCore 8.0.3 | .NET 8, .NET 9, .NET 10 | Targets `net8.0` TFM; compatible with all newer runtimes. |
| Serilog.Formatting.Compact 3.0.0 | Serilog 4.x | Requires Serilog 4.x. No conflict with Serilog.AspNetCore 8.x which pulls Serilog 4.x. |
| Microsoft.AspNetCore.OpenApi 10.0.8 (existing) | ASP.NET Core 10 only | No change needed — already locked to correct version. |
| mcr.microsoft.com/dotnet/aspnet:10.0 | .NET 10 framework-dependent apps | Do not mix SDK and runtime image versions (e.g., build on sdk:10.0, run on aspnet:9.0 — this will fail). |
| google-github-actions/auth v2 | github-actions/checkout v4 | No compatibility issues. Auth v2 requires `id-token: write` permission in the workflow job. |
| docker/build-push-action v6 | docker/setup-buildx-action v3 | `setup-buildx-action` must run before `build-push-action` in the same job. |

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Docker base image tags | HIGH | Microsoft docs and MCR are stable; `aspnet:10.0` and `sdk:10.0` confirmed patterns |
| Port 8080 default | HIGH | .NET 8+ base images set `ASPNETCORE_HTTP_PORTS=8080` — verified in Microsoft docs |
| Built-in health checks | HIGH | Part of ASP.NET Core framework since 2.2; `AddHealthChecks()` + `MapHealthChecks()` confirmed |
| Serilog.AspNetCore 8.0.3 | HIGH | Verified from training data; widely documented |
| Serilog.Formatting.Compact 3.0.0 | HIGH | Stable companion package to Serilog 4.x |
| Serilog.Sinks.GoogleCloudLogging version | MEDIUM | Package exists and is maintained; exact version needs NuGet verification |
| GitHub Actions action versions | HIGH | `checkout@v4`, `setup-dotnet@v4`, `docker/*@v3/v6`, `google-github-actions/*@v2` — stable major versions as of Aug 2025 |
| Workload Identity Federation | HIGH | Google's recommended approach; documented in google-github-actions/auth README |
| Cloud Run port/memory defaults | HIGH | GCP documentation; 8080 and 512 MB are well-established defaults |
| Artifact Registry URL pattern | HIGH | GCP documentation; replaces deprecated Container Registry |

---

## Sources

- [Microsoft .NET container images — GitHub](https://github.com/dotnet/dotnet-docker) — Docker image tags and multi-stage patterns
- [ASP.NET Core Health Checks — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks) — AddHealthChecks, MapHealthChecks
- [Serilog.AspNetCore — GitHub](https://github.com/serilog/serilog-aspnetcore) — UseSerilog integration pattern
- [Serilog.Formatting.Compact — GitHub](https://github.com/serilog/serilog-formatting-compact) — CompactJsonFormatter
- [Serilog.Sinks.GoogleCloudLogging — NuGet](https://www.nuget.org/packages/Serilog.Sinks.GoogleCloudLogging) — VERIFY VERSION before use
- [google-github-actions/auth — GitHub](https://github.com/google-github-actions/auth) — Workload Identity Federation setup
- [google-github-actions/deploy-cloudrun — GitHub](https://github.com/google-github-actions/deploy-cloudrun) — Cloud Run deployment action
- [docker/build-push-action — GitHub](https://github.com/docker/build-push-action) — v6 changelog
- [Google Cloud Run — Container requirements](https://cloud.google.com/run/docs/container-contract) — port 8080, liveness probe, concurrency
- [Google Artifact Registry — Docker quickstart](https://cloud.google.com/artifact-registry/docs/docker/quickstart) — registry URL format

---

*Stack research for: PersonsAPI v2.0 — Docker / GitHub Actions / Cloud Run / Serilog*
*Researched: 2026-06-01*
