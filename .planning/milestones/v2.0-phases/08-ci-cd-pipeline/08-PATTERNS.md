# Phase 8: CI/CD Pipeline - Pattern Map

**Mapped:** 2026-06-04
**Files analyzed:** 1 (1 new)
**Analogs found:** 1 / 1 (no exact GitHub Actions YAML exists; closest analog is `docker-compose.yml` for YAML structure + `DEPLOYMENT.md` for command content)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `.github/workflows/cicd.yml` | config (CI/CD workflow) | event-driven (push/dispatch → sequential jobs) | `docker-compose.yml` (YAML structure) + `DEPLOYMENT.md` (gcloud/docker commands) | partial — YAML conventions from docker-compose; command content verbatim from DEPLOYMENT.md Steps 4–8 |

---

## Pattern Assignments

### `.github/workflows/cicd.yml` (config, event-driven CI/CD)

**Primary analog for YAML conventions:** `docker-compose.yml` (solution root, 19 lines)
**Primary analog for command content:** `DEPLOYMENT.md` (solution root, Steps 4–8)

There are no existing GitHub Actions workflows in this repository. The pattern is assembled from two orthogonal sources: the project's only other YAML file (`docker-compose.yml`) provides formatting and environment variable conventions; `DEPLOYMENT.md` provides the exact validated gcloud and docker commands that the workflow must automate.

---

#### YAML structure pattern

**Source:** `docker-compose.yml` lines 1–19

```yaml
# docker-compose.yml — establishes YAML formatting conventions for this project
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
      start_period: 30s
```

**Formatting rules to carry forward:**
- Two-space indentation (consistent throughout docker-compose.yml)
- Double-quoted strings for values that contain special characters or could be misread as non-strings
- No trailing whitespace
- Top-level comment block referencing the authoritative doc URL (line 1: `# https://docs.docker.com/...`)
- Inline environment variable references use `${{ ... }}` in GitHub Actions (analogous to `${VAR}` shell syntax)

---

#### Dockerfile context: build source and port binding

**Source:** `Dockerfile` lines 1–41

The `push-image` job runs `docker build . ` from the solution root — the same directory and same `Dockerfile` used in Phase 7. Key constraints the workflow must respect:

```dockerfile
# Dockerfile lines 2–13 — multi-stage build; restore against .csproj before copying source
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/PersonsAPI.Domain/PersonsAPI.Domain.csproj             ./src/PersonsAPI.Domain/
COPY src/PersonsAPI.Application/PersonsAPI.Application.csproj   ./src/PersonsAPI.Application/
COPY src/PersonsAPI.Infrastructure/PersonsAPI.Infrastructure.csproj ./src/PersonsAPI.Infrastructure/
COPY src/PersonsAPI.Api/PersonsAPI.Api.csproj                   ./src/PersonsAPI.Api/
RUN dotnet restore src/PersonsAPI.Api/PersonsAPI.Api.csproj
```

```dockerfile
# Dockerfile lines 36–37 — port binding that constrains gcloud run deploy --port flag
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
```

The `docker build` step in `push-image` must run from the solution root (`.`) — the same working directory where `Dockerfile` lives. The `deploy` job must use `--port 8080` (not 80) to match `ASPNETCORE_HTTP_PORTS=8080`.

---

#### .dockerignore: clean build context in CI

**Source:** `.dockerignore` lines 1–11

```
.git/
bin/
obj/
**/bin/
**/obj/
tests/
.planning/
.claude/
*.md
docker-compose*.yml
```

`tests/` is excluded from the Docker build context. This means the `build-and-test` job (which needs test projects) must use `dotnet test src/PersonsAPI.sln` — the `.sln` file references all projects including test projects. The Docker build step in `push-image` does NOT need tests; the exclusion is already correct and requires no workflow workaround.

---

#### Command content: DEPLOYMENT.md Steps 4–8 (the manual runbook being automated)

**Source:** `DEPLOYMENT.md`

The workflow automates exactly five steps from the manual runbook. Each step maps directly to a workflow step.

**Step 4 — Docker authentication** (`DEPLOYMENT.md` lines 134–140):

```bash
# DEPLOYMENT.md Step 4 — validated in Phase 7
gcloud auth configure-docker us-central1-docker.pkg.dev
```

In the workflow, append `--quiet` to suppress interactive prompts:

```yaml
- name: Configure Docker for Artifact Registry
  run: gcloud auth configure-docker us-central1-docker.pkg.dev --quiet
```

**Step 6 — Build, tag, and push** (`DEPLOYMENT.md` lines 194–207):

```bash
# DEPLOYMENT.md Step 6 — validated in Phase 7
docker build -t us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest .
docker push us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest
```

In the workflow, `PROJECT_ID` becomes `${{ secrets.GCP_PROJECT_ID }}`. The image URL is constant per run and defined once as a job-level `env` variable:

```yaml
env:
  IMAGE_URL: us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest

steps:
  - name: Build Docker image
    run: docker build -t ${{ env.IMAGE_URL }} .

  - name: Push Docker image
    run: docker push ${{ env.IMAGE_URL }}
```

**Step 7 — Deploy to Cloud Run** (`DEPLOYMENT.md` lines 221–231):

```bash
# DEPLOYMENT.md Step 7 — validated in Phase 7
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

In the workflow, the `deploy` job uses a reduced flag set: `--image`, `--region`, `--platform managed`, `--quiet`. The Cloud Run service configuration (port, memory, CPU, min-instances, allow-unauthenticated, env vars) is already set on the existing service from Phase 7 — `gcloud run deploy` on an existing service only updates the image unless flags override existing settings. Do not repeat the full flag set or it overwrites manually tuned settings.

```yaml
- name: Deploy to Cloud Run
  run: |
    gcloud run deploy persons-api \
      --image ${{ env.IMAGE_URL }} \
      --region us-central1 \
      --platform managed \
      --quiet
```

**Step 8 — Verify: print service URL** (`DEPLOYMENT.md` lines 265–270):

```bash
# DEPLOYMENT.md Step 8 — URL extraction command (bash syntax)
SERVICE_URL=$(gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)')
echo $SERVICE_URL
```

In the workflow (D-06), print the URL as the last step of the `deploy` job:

```yaml
- name: Print service URL
  run: |
    gcloud run services describe persons-api \
      --region us-central1 \
      --format='value(status.url)'
```

---

#### GCP authentication pattern (new — no codebase analog)

**Source:** `08-RESEARCH.md` lines 204–215, Pattern 2

No codebase analog exists. Use the canonical pattern from `google-github-actions/auth` EXAMPLES.md as documented in RESEARCH.md:

```yaml
# Pattern 2 from RESEARCH.md — SA key auth via credentials_json
- uses: google-github-actions/auth@v2
  with:
    credentials_json: '${{ secrets.GCP_SA_KEY }}'
```

This step must appear in BOTH `push-image` and `deploy` jobs (job isolation — GCP credentials do not carry over between jobs). It must NOT appear in `build-and-test` (that job only needs dotnet; no GCP access required).

---

#### .NET build + test pattern (new — no codebase analog)

**Source:** `08-RESEARCH.md` lines 232–246, Pattern 4

No codebase analog exists (no existing CI workflow). Use the official Microsoft pattern:

```yaml
- name: Restore
  run: dotnet restore src/PersonsAPI.sln

- name: Build
  run: dotnet build src/PersonsAPI.sln --configuration Release --no-restore

- name: Test
  run: dotnet test src/PersonsAPI.sln --no-build --configuration Release --verbosity normal
```

Critical: `--configuration Release` must appear on BOTH `dotnet build` and `dotnet test`. Omitting it from `dotnet test` causes the test runner to search for `Debug` binaries that don't exist (Pitfall 1 in RESEARCH.md).

The solution file path `src/PersonsAPI.sln` is used (not the API project `.csproj`) because the test command must reference all test projects. The `build-and-test` job must check out source (`actions/checkout@v4`) since it needs `src/PersonsAPI.sln`.

---

#### Complete workflow skeleton

**Source:** `08-RESEARCH.md` lines 370–456 (Code Examples — Complete Workflow Skeleton)

The RESEARCH.md contains a complete, verified workflow skeleton. The planner should treat this as the authoritative template:

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [main]
  workflow_dispatch:

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - name: Restore
        run: dotnet restore src/PersonsAPI.sln

      - name: Build
        run: dotnet build src/PersonsAPI.sln --configuration Release --no-restore

      - name: Test
        run: dotnet test src/PersonsAPI.sln --no-build --configuration Release --verbosity normal

  push-image:
    runs-on: ubuntu-latest
    needs: [build-and-test]
    permissions:
      contents: read
    env:
      IMAGE_URL: us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest
    steps:
      - uses: actions/checkout@v4

      - uses: google-github-actions/auth@v2
        with:
          credentials_json: '${{ secrets.GCP_SA_KEY }}'

      - uses: google-github-actions/setup-gcloud@v2

      - name: Configure Docker for Artifact Registry
        run: gcloud auth configure-docker us-central1-docker.pkg.dev --quiet

      - name: Build Docker image
        run: docker build -t ${{ env.IMAGE_URL }} .

      - name: Push Docker image
        run: docker push ${{ env.IMAGE_URL }}

  deploy:
    runs-on: ubuntu-latest
    needs: [push-image]
    permissions:
      contents: read
    env:
      IMAGE_URL: us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest
    steps:
      - uses: google-github-actions/auth@v2
        with:
          credentials_json: '${{ secrets.GCP_SA_KEY }}'

      - uses: google-github-actions/setup-gcloud@v2

      - name: Deploy to Cloud Run
        run: |
          gcloud run deploy persons-api \
            --image ${{ env.IMAGE_URL }} \
            --region us-central1 \
            --platform managed \
            --quiet

      - name: Print service URL
        run: |
          gcloud run services describe persons-api \
            --region us-central1 \
            --format='value(status.url)'
```

**Key structural decisions encoded in this skeleton:**
- `build-and-test` has no GCP auth steps — dotnet only
- `deploy` has no `actions/checkout@v4` — gcloud commands against already-pushed image; no source needed
- `IMAGE_URL` env var is duplicated in `push-image` and `deploy` — job isolation; env does not share across jobs
- `permissions: contents: read` is explicit per job; `id-token: write` is intentionally absent (SA key auth does not require it)
- No NuGet caching (`cache: true` on setup-dotnet requires `packages.lock.json` which this project does not have — RESEARCH.md Open Question 3)

---

## Shared Patterns

### GCP Secret Reference Convention
**Source:** `08-RESEARCH.md` lines 408–416; `08-CONTEXT.md` D-09
**Apply to:** Both `push-image` and `deploy` jobs

Two secrets, referenced uniformly throughout:

```yaml
# Secret reference pattern — use exactly these names in all workflow steps
${{ secrets.GCP_SA_KEY }}        # Full contents of key.json (minified, single-line JSON)
${{ secrets.GCP_PROJECT_ID }}    # GCP project ID string, e.g. personsapi-XXXXXX
```

**Minification requirement** (RESEARCH.md Pitfall 5): `GCP_SA_KEY` must be stored as single-line minified JSON to prevent parse errors in `google-github-actions/auth@v2`. The CLI command to minify before pasting: `cat key.json | tr -d '\n'`. The UI path to create secrets: GitHub repo → Settings → Secrets and variables → Actions → New repository secret.

### Job Isolation Rule
**Source:** `08-RESEARCH.md` Pitfall 4 (lines 333–340)
**Apply to:** `push-image` and `deploy` job design

Each GitHub Actions job runs on a fresh runner VM. Any step that authenticates to GCP (`google-github-actions/auth@v2` + `google-github-actions/setup-gcloud@v2`) must be repeated in every job that needs GCP access. The `build-and-test` job is explicitly exempted — it never needs GCP access.

### No `id-token: write` Rule
**Source:** `08-RESEARCH.md` Pitfall 2 (lines 315–322)
**Apply to:** All job `permissions:` blocks

For SA key auth (`credentials_json`), the correct permissions block is:

```yaml
permissions:
  contents: read
```

Do NOT add `id-token: write`. That permission is only for Workload Identity Federation. Including it unnecessarily broadens the OIDC token scope.

### `--quiet` on All gcloud Commands
**Source:** `08-RESEARCH.md` Pitfall 3 (lines 323–332)
**Apply to:** Every `gcloud` command in the workflow

Any `gcloud` command that can produce interactive prompts must include `--quiet`. For this workflow: `gcloud auth configure-docker` and `gcloud run deploy`. The `gcloud run services describe` command is read-only and non-interactive but including `--quiet` is harmless if desired.

### Image URL Derivation
**Source:** `08-CONTEXT.md` Established Patterns; `DEPLOYMENT.md` Step 6
**Apply to:** `push-image` and `deploy` jobs

The image URL follows the pattern established in Phase 7:

```
us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest
```

Fixed segments: `us-central1-docker.pkg.dev` (registry), `personsapi` (repository name), `personsapi` (image name), `:latest` (tag — D-05). Only the project ID segment is dynamic.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `.github/workflows/cicd.yml` | config (CI/CD workflow) | event-driven | No GitHub Actions workflows exist in this repository. The closest YAML file is `docker-compose.yml` (19 lines) which provides formatting conventions only. All command content comes from `DEPLOYMENT.md` Steps 4–8. The complete workflow skeleton from `08-RESEARCH.md` lines 370–456 is the authoritative template — it was synthesized from official `google-github-actions` documentation and validated Phase 7 gcloud commands. |

---

## Metadata

**Analog search scope:** `C:\ATS\Git\platform\` (solution root), `.github/workflows/` (empty), `.planning/phases/07-cloud-run-deployment/` (prior phase)
**Files scanned:** `docker-compose.yml` (19 lines), `Dockerfile` (41 lines), `.dockerignore` (11 lines), `.gitignore` (19 lines), `DEPLOYMENT.md` (375 lines), `.planning/phases/07-cloud-run-deployment/07-PATTERNS.md` (255 lines), `08-CONTEXT.md` (115 lines), `08-RESEARCH.md` (627 lines)
**Pattern extraction date:** 2026-06-04
