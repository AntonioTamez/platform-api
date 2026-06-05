# Phase 7: Cloud Run Deployment - Context

**Gathered:** 2026-06-03
**Status:** Ready for planning

<domain>
## Phase Boundary

Deploy the containerized PersonsAPI to Google Cloud Run manually using the gcloud CLI. Starting from zero GCP setup (no existing project), the phase covers: GCP project creation, enabling APIs, Artifact Registry repository, pushing the Docker image, creating the Cloud Run service, and verifying the API is publicly reachable at the generated HTTPS URL. The deliverable is a working public URL plus a `DEPLOYMENT.md` runbook documenting every step.

No application code changes in this phase — all work is GCP infrastructure and documentation.

</domain>

<decisions>
## Implementation Decisions

### GCP Project & Resources
- **D-01:** The user has no existing GCP project. The plan MUST include creating a new GCP project as its first task. Use a placeholder `PROJECT_ID` throughout all commands — the user fills in the actual value when executing.
- **D-02:** Region: `us-central1` (Iowa). Used for both Artifact Registry and Cloud Run service.
- **D-03:** Artifact Registry repository name: `personsapi`. Docker image path: `us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest`.
- **D-04:** Cloud Run service name: `persons-api`.

### Cloud Run Service Configuration
- **D-05:** Min instances: `0` (scale to zero). The service scales to zero when idle — cost is $0 at rest. Cold start of ~3-5s is acceptable for a learning project.
- **D-06:** Memory: `512MiB`. CPU: `1`. This gives .NET 10 + ASP.NET Core + EF InMemory ample headroom beyond the ~180-250 MiB baseline usage.
- **D-07:** Access: `--allow-unauthenticated` (fully public). Anyone with the Cloud Run URL can call the API. This is correct for a learning/demo project and is required for verifying the success criteria with plain `curl` without IAM tokens.
- **D-08:** Max instances: Cloud Run default (not specified). Concurrency: Cloud Run default.

### Service Account Setup
- **D-09:** The plan MUST include creating a dedicated Service Account with two IAM roles:
  - `roles/artifactregistry.writer` — push Docker images
  - `roles/run.admin` — deploy Cloud Run services
- **D-10:** The Service Account setup is included in the runbook because Phase 8 (CI/CD via GitHub Actions) will need the same Service Account's JSON key for the `google-github-actions/auth` action. Setting it up now avoids rework.
- **D-11:** Authentication method: Service Account JSON key (not Workload Identity Federation). WIF is explicitly out of scope per REQUIREMENTS.md — service account key is acceptable for this learning milestone.

### Deployment Runbook
- **D-12:** Create `DEPLOYMENT.md` at the solution root. This is the primary deliverable of the phase. It contains all gcloud commands from project creation to public URL verification — a complete runbook someone with a fresh GCP account can follow.
- **D-13:** The DEPLOYMENT.md covers the full sequence in order:
  1. GCP project creation and billing link
  2. Enable required APIs (Cloud Run, Artifact Registry, Cloud Build)
  3. Create Artifact Registry repository
  4. Authenticate Docker to Artifact Registry
  5. Build, tag, and push the Docker image
  6. Deploy to Cloud Run (`gcloud run deploy`)
  7. Verify the public URL
- **D-14:** `DEPLOYMENT.md` uses `PROJECT_ID` as a placeholder throughout so the user can do a find-replace with their actual project ID.

### Verification Tasks
- **D-15:** The plan MUST include explicit verification tasks for each of the 4 ROADMAP success criteria:
  1. `curl https://<cloud-run-url>/health` → HTTP 200 OK from the public internet
  2. `curl https://<cloud-run-url>/api/persons` → 3 seeded persons in JSON
  3. Cloud Run startup probe passes (no container crash loop — check service status in console or via gcloud)
  4. Google Cloud Logging shows JSON log entries from the running service

### Claude's Discretion
- Exact gcloud flag ordering and quoting style in DEPLOYMENT.md
- Whether to include `gcloud config set project PROJECT_ID` as a convenience step
- Exact Docker tag strategy (`:latest` is fine for Phase 7; semantic versioning deferred to Phase 8)
- Whether to add a "Cleanup / Teardown" section at the end of DEPLOYMENT.md
- Exact startup probe configuration (Cloud Run defaults are fine — the `/health` endpoint already returns HTTP 200)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope and Requirements
- `.planning/ROADMAP.md` — Phase 7 goal, 4 success criteria, dependency on Phase 6
- `.planning/REQUIREMENTS.md` — CLOUD-01 definition; Out of Scope section (WIF excluded, Alpine excluded, Serilog GCP sink excluded)

### Project Constraints
- `.planning/PROJECT.md` — Current state summary (Phase 6 complete, Docker artifacts ready), Key Decisions table
- `CLAUDE.md` — Technology stack, constraints (.NET 10, C# 14, controllers only)

### Prior Phase Context
- `.planning/phases/06-containerization/06-CONTEXT.md` — D-01 (ASPNETCORE_HTTP_PORTS=8080), D-02 (HTTP only, TLS by Cloud Run), D-04 (Debian aspnet:10.0, not Alpine) — all carry forward to Cloud Run config
- `.planning/phases/05-observability/05-CONTEXT.md` — D-01 (/health endpoint), D-03 (anonymous health check), D-06 (CLEF JSON logs to stdout for Cloud Logging)

### Existing Docker Artifacts (read before planning push/deploy steps)
- `Dockerfile` — Multi-stage build, ASPNETCORE_HTTP_PORTS=8080, aspnet:10.0, EXPOSE 8080. Image name used in `docker build` command.
- `docker-compose.yml` — Reference for port 8080 mapping and ASPNETCORE_HTTP_PORTS environment variable pattern already established.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Dockerfile` at solution root: Production-ready multi-stage image, already tested via `docker compose up`. This is the image that gets pushed to Artifact Registry — no Dockerfile changes needed.
- `src/PersonsAPI.Api/Program.cs`: `/health` endpoint already configured anonymous (D-03 from Phase 5) — Cloud Run startup probe works without additional configuration.
- Serilog CLEF JSON stdout logging: Already operational. Cloud Logging will ingest it automatically when the container runs on Cloud Run — no sink changes needed.

### Established Patterns
- Port configuration pattern: `ASPNETCORE_HTTP_PORTS=8080` set via `ENV` in Dockerfile + `environment:` in docker-compose. Cloud Run sets its own `PORT` env variable but our explicit `ASPNETCORE_HTTP_PORTS=8080` is compatible.
- HTTP-only inside container, TLS at proxy: Established in Phase 6 (D-02). Cloud Run's HTTPS URL terminates TLS before the container — the container continues serving HTTP on port 8080.

### Integration Points
- `Dockerfile` is the build source for `docker build -t IMAGE_URL .` before push to Artifact Registry
- `.github/workflows/` directory exists (empty) — Phase 8 will add the CI/CD workflow there; Phase 7 has no workflow files

</code_context>

<specifics>
## Specific Ideas

- The Service Account created in this phase (D-09, D-10) becomes the credential for Phase 8 GitHub Actions workflow. DEPLOYMENT.md should note this forward-compatibility explicitly so the user keeps the JSON key for Phase 8.
- `PROJECT_ID` placeholder pattern in DEPLOYMENT.md: consistent with how Cloud Run and Artifact Registry URLs are formed (`us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest` and `https://persons-api-HASH-uc.a.run.app`).
- Cold start context: With 0 min instances, the first request after scale-to-zero wakes the container. .NET 10 startup is fast (~1-2s) but the EF InMemory seed (`DataSeeder.SeedAsync()`) runs on startup — acceptable for a demo, worth noting in DEPLOYMENT.md.

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope.

</deferred>

---

*Phase: 7-Cloud Run Deployment*
*Context gathered: 2026-06-03*
