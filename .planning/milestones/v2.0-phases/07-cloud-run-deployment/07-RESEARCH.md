# Phase 7: Cloud Run Deployment - Research

**Researched:** 2026-06-03
**Domain:** Google Cloud Run + Artifact Registry + gcloud CLI (infrastructure-only, no application code changes)
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** New GCP project creation required (user has no existing GCP project). Plan MUST start with `gcloud projects create`.
- **D-02:** Region: `us-central1` (Iowa). Used for both Artifact Registry and Cloud Run.
- **D-03:** Artifact Registry repository name: `personsapi`. Image path: `us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest`.
- **D-04:** Cloud Run service name: `persons-api`.
- **D-05:** Min instances: `0` (scale to zero). Cold start is acceptable for learning project.
- **D-06:** Memory: `512MiB`. CPU: `1`.
- **D-07:** Access: `--allow-unauthenticated` (fully public).
- **D-08:** Max instances and concurrency: Cloud Run defaults (not specified).
- **D-09:** Service Account with `roles/artifactregistry.writer` + `roles/run.admin`. JSON key auth.
- **D-10:** Service Account setup in Phase 7 runbook so Phase 8 GitHub Actions can reuse the JSON key.
- **D-11:** Authentication method: Service Account JSON key (WIF explicitly out of scope per REQUIREMENTS.md).
- **D-12:** Create `DEPLOYMENT.md` at solution root. Primary deliverable.
- **D-13:** DEPLOYMENT.md covers full sequence: project creation → billing → enable APIs → Artifact Registry → Docker auth → build/tag/push → Cloud Run deploy → verify.
- **D-14:** Use `PROJECT_ID` placeholder throughout DEPLOYMENT.md.
- **D-15:** Plan MUST include explicit verification tasks for all 4 ROADMAP success criteria.

### Claude's Discretion

- Exact gcloud flag ordering and quoting style in DEPLOYMENT.md.
- Whether to include `gcloud config set project PROJECT_ID` as a convenience step.
- Exact Docker tag strategy (`:latest` is fine for Phase 7).
- Whether to add a "Cleanup / Teardown" section at end of DEPLOYMENT.md.
- Exact startup probe configuration (Cloud Run defaults are fine — `/health` already returns HTTP 200).

### Deferred Ideas (OUT OF SCOPE)

- None. Discussion stayed within phase scope.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CLOUD-01 | API is publicly reachable at a Google Cloud Run HTTPS URL after manual deployment | Full gcloud CLI command sequence documented; port/auth/logging compatibility verified |

</phase_requirements>

---

## Summary

Phase 7 is a pure infrastructure and documentation phase — zero application code changes. The PersonsAPI container image (already validated in Phase 6) is pushed to Google Artifact Registry and deployed to Cloud Run using the gcloud CLI. The primary deliverable is `DEPLOYMENT.md` at the solution root, a complete runbook anyone with a fresh GCP account can follow.

The three key technical facts that shape this phase: (1) Cloud Run injects `PORT=8080` by default which aligns exactly with the `ASPNETCORE_HTTP_PORTS=8080` already set in the Dockerfile — no conflict, no code changes needed. (2) The `/health` endpoint already returns HTTP 200 and Cloud Run's default TCP startup probe passes automatically when the container starts listening on port 8080. (3) Serilog CLEF JSON on stdout is already ingested by Cloud Logging; severity mapping to the Cloud Console requires a `severity` field in JSON which CLEF does not emit by default — entries will appear as `DEFAULT` severity, which is acceptable (deferred per REQUIREMENTS.md OBS-03).

The user does not have gcloud CLI installed on their machine. DEPLOYMENT.md must include the gcloud SDK installation step as the prerequisite, before any project commands.

**Primary recommendation:** One plan wave producing DEPLOYMENT.md with the full verified gcloud command sequence. The plan contains no application code changes and no NuGet packages.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Container image storage | Infrastructure (Artifact Registry) | — | GCP-managed Docker registry; no application involvement |
| Public HTTPS termination | Infrastructure (Cloud Run ingress) | — | Cloud Run terminates TLS before reaching the container |
| Container execution | Infrastructure (Cloud Run) | — | Managed serverless runtime; container serves HTTP on 8080 |
| Port binding | Container (Dockerfile ENV) | — | ASPNETCORE_HTTP_PORTS=8080 already set; Cloud Run injects matching PORT=8080 |
| Log ingestion | Infrastructure (Cloud Logging) | — | Automatic from stdout; no sink changes needed |
| Health/liveness probe | Container / API layer | Infrastructure (Cloud Run default TCP probe) | /health at HTTP 200 already implemented; TCP probe passes on port open |
| Service account & IAM | Infrastructure (GCP IAM) | — | JSON key auth for push and deploy; Phase 8 reuses same key |

---

## Standard Stack

### Core (this phase is infrastructure-only — no new NuGet packages)

| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| gcloud CLI (Google Cloud SDK) | Latest stable | All GCP resource management: project, Artifact Registry, Cloud Run deploy, IAM | Official GCP CLI; only supported tool for Cloud Run deployment from command line [CITED: docs.cloud.google.com/sdk/docs/install] |
| Docker Desktop | 29.4.0 (already installed) | Build and tag image before push to Artifact Registry | Already verified present on developer machine |
| Artifact Registry | GCP managed | Docker image registry in `us-central1` | GCP's current-generation registry; Container Registry is deprecated [CITED: docs.cloud.google.com/artifact-registry] |
| Cloud Run | GCP managed | Serverless container execution, HTTPS termination | Correct target per ROADMAP.md; simpler than GKE for this use case |

### No NuGet Packages

This phase introduces zero new application packages. The Dockerfile, application code, and all existing packages are unchanged.

---

## Package Legitimacy Audit

No external packages are installed in this phase. This section is not applicable.

---

## Architecture Patterns

### System Architecture Diagram

```
Developer Machine
  [Dockerfile at solution root]
       |
       | docker build -t IMAGE_URL .
       v
  [Local Docker image]
       |
       | gcloud auth configure-docker us-central1-docker.pkg.dev
       | docker push IMAGE_URL
       v
  [Artifact Registry]
  us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest
       |
       | gcloud run deploy persons-api --image IMAGE_URL ...
       v
  [Cloud Run Service: persons-api]
  https://persons-api-<hash>-uc.a.run.app
       |
       | HTTPS (TLS terminated by Cloud Run)
       v
  [Container: aspnet:10.0, port 8080]
  - ASPNETCORE_HTTP_PORTS=8080
  - PORT=8080 (injected by Cloud Run)
  - GET /health → 200 {"status":"Healthy"}
  - GET /api/persons → 200 [3 seeded persons]
  - Serilog CLEF JSON → stdout
       |
       | auto-ingested
       v
  [Google Cloud Logging]
  Logs Explorer: console.cloud.google.com/logs/query
```

### Recommended DEPLOYMENT.md Structure

```
DEPLOYMENT.md
├── Prerequisites
│   ├── Install gcloud CLI (Windows installer URL)
│   ├── gcloud auth login
│   └── Verify docker is installed
├── Step 1: GCP Project Setup
│   ├── gcloud projects create PROJECT_ID
│   ├── gcloud config set project PROJECT_ID
│   └── Note: link billing account (manual in console or gcloud billing)
├── Step 2: Enable Required APIs
│   └── gcloud services enable run.googleapis.com artifactregistry.googleapis.com
├── Step 3: Create Artifact Registry Repository
│   └── gcloud artifacts repositories create personsapi --repository-format=docker --location=us-central1
├── Step 4: Configure Docker Authentication
│   └── gcloud auth configure-docker us-central1-docker.pkg.dev
├── Step 5: Create Service Account (for Phase 7 push + Phase 8 CI/CD)
│   ├── gcloud iam service-accounts create ...
│   ├── gcloud projects add-iam-policy-binding ... roles/artifactregistry.writer
│   ├── gcloud projects add-iam-policy-binding ... roles/run.admin
│   └── gcloud iam service-accounts keys create key.json ...
├── Step 6: Build, Tag, and Push Docker Image
│   ├── docker build -t us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest .
│   └── docker push us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest
├── Step 7: Deploy to Cloud Run
│   └── gcloud run deploy persons-api --image ... --region us-central1 --memory 512Mi --cpu 1 --min-instances 0 --allow-unauthenticated --port 8080
├── Step 8: Verify
│   ├── Get URL: gcloud run services describe persons-api --region us-central1 --format='value(status.url)'
│   ├── curl https://<url>/health
│   ├── curl https://<url>/api/persons
│   └── View logs in Cloud Console
└── Appendix: Cleanup / Teardown (optional)
```

### Pattern 1: Cloud Run Port Compatibility with ASP.NET Core

**What:** Cloud Run injects `PORT=8080` into every container by default. The Dockerfile already sets `ASPNETCORE_HTTP_PORTS=8080`. Both variables resolve to the same port — no conflict.

**Why it works:** `ASPNETCORE_HTTP_PORTS` is the .NET 8+ canonical port variable. Kestrel binds to port 8080 from the Dockerfile `ENV`. Cloud Run sends traffic to port 8080 (its default). They agree. The `PORT` variable from Cloud Run is injected but not read by ASP.NET Core unless code explicitly reads it — which is fine because `ASPNETCORE_HTTP_PORTS=8080` already achieves the same result.

**Key pitfall to avoid:** Do NOT use `--port 80` in the `gcloud run deploy` command. Cloud Run would inject `PORT=80` but the container still listens on 8080, causing a mismatch and container health failure.

**Example:**
```bash
# Source: Cloud Run container contract docs.cloud.google.com/run/docs/container-contract
# Cloud Run default port = 8080, which matches ASPNETCORE_HTTP_PORTS=8080 in Dockerfile
gcloud run deploy persons-api \
  --image us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest \
  --region us-central1 \
  --port 8080 \
  --memory 512Mi \
  --cpu 1 \
  --min-instances 0 \
  --allow-unauthenticated
```

### Pattern 2: Docker Authentication to Artifact Registry

**What:** Two supported approaches for this phase:
1. gcloud credential helper (simpler, for personal use during Phase 7 manual deploy)
2. Service account JSON key with `docker login` (required for Phase 8 CI/CD)

**Phase 7 approach (interactive):**
```bash
# Source: docs.cloud.google.com/artifact-registry/docs/docker/authentication
gcloud auth configure-docker us-central1-docker.pkg.dev
```

**Phase 8 compatible approach (service account key):**
```powershell
# Windows PowerShell — for GitHub Actions secret later
Get-Content key.json | docker login -u _json_key --password-stdin https://us-central1-docker.pkg.dev
```

### Pattern 3: Extract Service URL After Deploy

```bash
# Source: docs.cloud.google.com/sdk/gcloud/reference/run/services/describe
SERVICE_URL=$(gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)')
echo $SERVICE_URL
# Output: https://persons-api-<hash>-uc.a.run.app
```

Note: `gcloud run deploy` also prints the URL automatically at the end of a successful deploy.

### Pattern 4: Complete Service Account Setup

```bash
# Source: docs.cloud.google.com/artifact-registry/docs/access-control
# Create service account
gcloud iam service-accounts create persons-api-deployer \
  --display-name="PersonsAPI Deployer" \
  --project=PROJECT_ID

# Grant Artifact Registry write access
gcloud projects add-iam-policy-binding PROJECT_ID \
  --member="serviceAccount:persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"

# Grant Cloud Run deploy access
gcloud projects add-iam-policy-binding PROJECT_ID \
  --member="serviceAccount:persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/run.admin"

# Download JSON key (KEEP THIS FILE — needed for Phase 8 GitHub Actions)
gcloud iam service-accounts keys create key.json \
  --iam-account=persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com
```

### Anti-Patterns to Avoid

- **Using `--port 80` in gcloud run deploy:** Causes PORT=80 injection but Dockerfile container listens on 8080 → health check fails, container crash loop.
- **Not setting `--region` flag:** gcloud prompts interactively; non-interactive CI/CD will fail. Always specify `--region us-central1`.
- **Committing `key.json` to git:** Service account JSON key must never be committed. Add `key.json` to `.gitignore` immediately after creation.
- **Forgetting to link billing before enabling APIs:** `gcloud services enable` silently fails or returns error if billing is not linked. Billing must be linked before any API enablement.
- **Enabling only `run.googleapis.com`:** Artifact Registry also requires `artifactregistry.googleapis.com`. Both must be enabled.
- **Using old Container Registry (`gcr.io`):** GCR is deprecated. Always use Artifact Registry (`pkg.dev`).

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Docker auth to Artifact Registry | Manual curl to GCP token endpoint | `gcloud auth configure-docker` | Handles token refresh; manages ~/.docker/config.json credential helpers correctly |
| Service URL extraction | Parsing gcloud text output with grep | `--format='value(status.url)'` | Stable, not fragile to output format changes |
| Health check configuration | Custom probe configuration with HTTP path | Cloud Run default TCP startup probe | Default TCP probe (waits 240s for TCP on port 8080) is sufficient — /health endpoint already works |
| Image naming | Custom tagging scheme | `us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest` | Cloud Run expects full Artifact Registry path; `:latest` is correct for Phase 7 |

**Key insight:** This phase is entirely gcloud CLI commands and a DEPLOYMENT.md document. There is nothing to hand-roll — every operation is a single gcloud or docker command.

---

## Common Pitfalls

### Pitfall 1: Cloud Run container fails to start (port mismatch)
**What goes wrong:** `gcloud run deploy` succeeds but the service returns errors; Cloud Console shows "Container failed to start" or crash loop. The URL returns 503.
**Why it happens:** The container listens on port X but Cloud Run sends traffic to port Y. Common cause: `--port 80` flag used in deploy command while Dockerfile has `ASPNETCORE_HTTP_PORTS=8080`.
**How to avoid:** Use `--port 8080` explicitly (matches Dockerfile `EXPOSE 8080` and `ASPNETCORE_HTTP_PORTS=8080`). Or omit `--port` entirely — Cloud Run defaults to 8080 which already matches.
**Warning signs:** `gcloud run deploy` completes but curl returns 503; Cloud Console status shows error percentage.

### Pitfall 2: Billing not linked — APIs fail to enable
**What goes wrong:** `gcloud services enable run.googleapis.com` returns an error about billing not being enabled.
**Why it happens:** New GCP projects have no billing account linked. Cloud Run is not in the "always free" tier for enablement purposes.
**How to avoid:** DEPLOYMENT.md must include billing link step BEFORE the API enablement step. Billing can be linked via `gcloud billing projects link PROJECT_ID --billing-account=BILLING_ACCOUNT_ID` or via GCP Console.
**Warning signs:** `gcloud services enable` error message mentioning billing.

### Pitfall 3: Docker push authentication failure
**What goes wrong:** `docker push us-central1-docker.pkg.dev/...` returns "unauthorized" or "access denied".
**Why it happens:** `gcloud auth configure-docker us-central1-docker.pkg.dev` was not run before push, or gcloud token has expired.
**How to avoid:** Run `gcloud auth configure-docker us-central1-docker.pkg.dev` immediately before push. Token is short-lived — re-run if session is old.
**Warning signs:** Docker push returns HTTP 401 or 403.

### Pitfall 4: gcloud CLI not installed (user's machine)
**What goes wrong:** All gcloud commands fail with "command not found" / "gcloud is not recognized".
**Why it happens:** gcloud SDK is not installed on this machine (confirmed absent via environment check).
**How to avoid:** DEPLOYMENT.md must open with the gcloud SDK installation step for Windows. Installer: `https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe`. After install, run `gcloud init` to authenticate and set default project.
**Warning signs:** First gcloud command fails.

### Pitfall 5: Cloud Logging shows logs as "DEFAULT" severity, not INFO/WARNING
**What goes wrong:** Logs appear in Cloud Logging but have no colored severity badges.
**Why it happens:** Serilog CLEF format emits a `@l` field (level), but Cloud Logging expects a `severity` field for automatic severity mapping. CLEF does not emit `severity` by default.
**How to avoid:** This is acceptable per REQUIREMENTS.md (OBS-03 deferred to v3). Note this behavior in DEPLOYMENT.md so the user understands why icons are absent. The fix (Serilog.Sinks.GoogleCloudLogging or custom enricher) is explicitly out of scope.
**Warning signs:** Logs visible in Cloud Logging Explorer but all show grey "DEFAULT" badge rather than blue/yellow/red severity icons.

### Pitfall 6: Scale-to-zero cold start fails verification
**What goes wrong:** First `curl https://<url>/health` after idle period returns timeout or 503 instead of 200.
**Why it happens:** With `--min-instances 0`, Cloud Run scales to zero after inactivity. First request triggers container startup. .NET 10 startup + EF InMemory seed takes 2-4 seconds. If curl timeout is very short, it may fail.
**How to avoid:** Use `curl --max-time 30 https://<url>/health` for initial verification. Cloud Run default TCP startup probe allows up to 240 seconds for the container to start — first user request is held until container is ready.
**Warning signs:** First curl after idle returns 503 or times out; second curl works fine.

### Pitfall 7: Committing service account JSON key to git
**What goes wrong:** `key.json` accidentally committed and pushed; GCP detects it and may auto-revoke or send security alert.
**Why it happens:** Developer runs `gcloud iam service-accounts keys create key.json` at solution root and then runs `git add .`.
**How to avoid:** Add `key.json` to `.gitignore` immediately. DEPLOYMENT.md must include this step. Store the key in a secure location (password manager or GitHub Actions secret for Phase 8).
**Warning signs:** `git status` shows `key.json` as untracked.

---

## Code Examples

### Full gcloud run deploy Command (exact flags)
```bash
# Source: docs.cloud.google.com/sdk/gcloud/reference/run/deploy
gcloud run deploy persons-api \
  --image us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest \
  --region us-central1 \
  --port 8080 \
  --memory 512Mi \
  --cpu 1 \
  --min-instances 0 \
  --allow-unauthenticated \
  --set-env-vars "ASPNETCORE_ENVIRONMENT=Production"
```

Note on `ASPNETCORE_ENVIRONMENT`: Cloud Run is a production environment. Set `ASPNETCORE_ENVIRONMENT=Production` explicitly. This disables the Scalar UI (`/scalar`) in production — acceptable for a deployed service. If keeping Scalar in Cloud Run is desired, set `Development` instead and note the trade-off.

### Enable Required APIs (single command)
```bash
# Source: docs.cloud.google.com/sdk/docs (gcloud services enable)
gcloud services enable run.googleapis.com artifactregistry.googleapis.com
```

### Create Artifact Registry Repository
```bash
# Source: docs.cloud.google.com/artifact-registry/docs/repositories/create-repos
gcloud artifacts repositories create personsapi \
  --repository-format=docker \
  --location=us-central1 \
  --description="PersonsAPI Docker images"
```

### Verify Deployment (all 4 success criteria)
```bash
# Get URL
SERVICE_URL=$(gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)')

# SC-1: Health check from public internet
curl --max-time 30 "$SERVICE_URL/health"
# Expected: HTTP 200, body: {"status":"Healthy"}

# SC-2: Seeded persons
curl "$SERVICE_URL/api/persons"
# Expected: HTTP 200, JSON array with 3 persons

# SC-3: No crash loop — check service status
gcloud run services describe persons-api --region us-central1 --format='value(status.conditions)'

# SC-4: Logs in Cloud Logging (view in browser)
# https://console.cloud.google.com/logs/query?project=PROJECT_ID
# Filter: resource.type="cloud_run_revision" AND resource.labels.service_name="persons-api"
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Google Container Registry (`gcr.io`) | Artifact Registry (`pkg.dev`) | 2023 | GCR deprecated; all new projects should use Artifact Registry |
| Swashbuckle OpenAPI | Microsoft.AspNetCore.OpenApi + Scalar | .NET 9 | Already handled in Phase 4 — no impact on this phase |
| `ASPNETCORE_URLS` env var | `ASPNETCORE_HTTP_PORTS` env var | .NET 8 | Already set correctly in Dockerfile — no action needed |

**Deprecated/outdated:**
- `gcr.io` (Google Container Registry): Use `pkg.dev` (Artifact Registry) for all new image storage. [CITED: docs.cloud.google.com/artifact-registry/docs/transition]
- `gcloud container` commands: Use `gcloud run` commands for Cloud Run deployment.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `gcloud run deploy` prints the service URL automatically at the end of a successful deploy | Architecture Patterns / Standard Patterns | User doesn't know the URL; workaround: use `gcloud run services describe` to get it |
| A2 | `ASPNETCORE_ENVIRONMENT=Production` disables the Scalar UI endpoint in Cloud Run | Code Examples | Scalar UI accessible from public internet if wrong — not a security risk per D-07 (fully public), just unexpected behavior |
| A3 | Cold start time for this container is 2-4 seconds | Common Pitfalls (Pitfall 6) | Could be longer on first cold start with very stale image cache; 30s curl timeout is conservative safety margin |

**All other claims in this research were verified against official Google Cloud documentation or confirmed via environment checks.**

---

## Open Questions

1. **ASPNETCORE_ENVIRONMENT in Cloud Run**
   - What we know: gcloud `--set-env-vars` supports it; default from Dockerfile will be whatever ASPNETCORE_ENVIRONMENT was at build time (likely `Development` since it's set in docker-compose but not in Dockerfile)
   - What's unclear: Should DEPLOYMENT.md explicitly set `Production` via `--set-env-vars`, or leave as Development? Production disables Scalar UI; Development keeps it accessible.
   - Recommendation: Set `ASPNETCORE_ENVIRONMENT=Production` in the `gcloud run deploy` command. For a public learning demo, keeping Scalar accessible is fine — but Production is the correct setting for Cloud Run. Note the Scalar impact in DEPLOYMENT.md.

2. **Billing account linking via CLI vs. console**
   - What we know: `gcloud billing projects link PROJECT_ID --billing-account=BILLING_ACCOUNT_ID` works if a billing account already exists. A billing account itself cannot be created via CLI — only via GCP Console.
   - What's unclear: Whether the user has an existing billing account or needs to create one (requires credit card in GCP Console).
   - Recommendation: DEPLOYMENT.md should note that billing account creation (credit card entry) must be done in the GCP Console first, then `gcloud billing accounts list` to get the BILLING_ACCOUNT_ID for the link command. The $300 free credit covers this project entirely.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Docker Desktop | Build and push image | Yes | 29.4.0 | None needed |
| gcloud CLI | All GCP operations | No | — | Must install before executing any plan task |
| GCP Account | Project creation | Unknown | — | User must create free GCP account at console.cloud.google.com |
| Billing Account | API enablement | Unknown | — | User must enter credit card in GCP Console ($300 free credit available) |

**Missing dependencies with no fallback:**
- `gcloud CLI` — not installed on developer machine. Every plan task requires it. DEPLOYMENT.md must open with installation instructions: `https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe`

**Missing dependencies with fallback:**
- None — Docker is already present.

---

## Security Domain

Security enforcement is enabled (ASVS level 1). This phase is infrastructure-only.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | Partial | Service account JSON key (not WIF — explicitly out of scope per REQUIREMENTS.md) |
| V3 Session Management | No | No session state; stateless API |
| V4 Access Control | Yes | `--allow-unauthenticated` is intentional per D-07; all operations are read-only GET for verification |
| V5 Input Validation | No | No new application code in this phase |
| V6 Cryptography | No | TLS handled by Cloud Run infrastructure; no application-level crypto |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Service account key exposure | Information Disclosure | Never commit `key.json` to git; add to `.gitignore` immediately; store in GitHub Actions secret for Phase 8 |
| Public unauthenticated API | Elevation of Privilege | Acceptable per D-07; API is read/write but learning project; no sensitive data |
| Over-permissioned service account | Elevation of Privilege | Principle of least privilege: `artifactregistry.writer` + `run.admin` only — not `Owner` or `Editor` |

**Security note on `--allow-unauthenticated`:** This flag grants the Cloud Run Invoker IAM role to `allUsers`. Any person with the URL can call all 6 API endpoints including POST/PUT/DELETE. This is correct per D-07 for a learning project with no real data. DEPLOYMENT.md should note this explicitly.

---

## Sources

### Primary (HIGH confidence)
- [Cloud Run container contract](https://docs.cloud.google.com/run/docs/container-contract) — PORT=8080 default, container must listen on 0.0.0.0, startup probe behavior
- [gcloud run deploy reference](https://docs.cloud.google.com/sdk/gcloud/reference/run/deploy) — all flags: --image, --region, --memory, --cpu, --min-instances, --allow-unauthenticated, --port
- [Artifact Registry Docker authentication](https://docs.cloud.google.com/artifact-registry/docs/docker/authentication) — gcloud auth configure-docker command, Windows compatibility
- [gcloud SDK install (Windows)](https://docs.cloud.google.com/sdk/docs/install) — `https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe`
- [Cloud Run logging](https://docs.cloud.google.com/run/docs/logging) — stdout ingestion, Cloud Logging Explorer URL
- [Cloud Run health checks](https://docs.cloud.google.com/run/docs/configuring/healthchecks) — default TCP probe (240s timeout), custom HTTP probe config
- [gcloud billing projects link](https://docs.cloud.google.com/sdk/gcloud/reference/billing/projects/link) — billing account linking syntax

### Secondary (MEDIUM confidence)
- [ASP.NET Core port change (.NET 8+)](https://learn.microsoft.com/en-us/dotnet/core/compatibility/containers/8.0/aspnet-port) — ASPNETCORE_HTTP_PORTS behavior verified against official Microsoft Learn docs
- [Artifact Registry create repos](https://docs.cloud.google.com/artifact-registry/docs/repositories/create-repos) — `gcloud artifacts repositories create` command syntax

### Tertiary (LOW confidence)
- None. All claims verified against official documentation.

---

## Metadata

**Confidence breakdown:**
- gcloud command syntax: HIGH — verified against official reference docs
- Port compatibility (ASPNETCORE_HTTP_PORTS + Cloud Run PORT): HIGH — both set to 8080; verified against official container contract and .NET 8 breaking change docs
- Cloud Logging JSON ingestion: HIGH — verified against official Cloud Run logging docs
- Environment availability (gcloud missing): HIGH — confirmed via `command -v gcloud` on developer machine
- Cold start timing estimate: LOW (A3) — .NET startup times are approximate; not benchmarked for this specific container

**Research date:** 2026-06-03
**Valid until:** 2026-09-03 (stable GCP APIs; gcloud command flags rarely change)
