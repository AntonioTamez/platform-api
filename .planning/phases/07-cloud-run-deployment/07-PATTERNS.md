# Phase 7: Cloud Run Deployment - Pattern Map

**Mapped:** 2026-06-03
**Files analyzed:** 2 (1 new, 1 modified)
**Analogs found:** 1 / 2 (DEPLOYMENT.md has no codebase analog — it is the first deployment runbook; .gitignore has an exact analog in itself)

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `DEPLOYMENT.md` | documentation | n/a (ops runbook) | `README.md` (solution root) | partial — same file role (root-level reference doc), different content type (bash commands vs. architecture description) |
| `.gitignore` | config | n/a (VCS filter) | `.gitignore` (itself) | exact — single-entry addition |

---

## Pattern Assignments

### `DEPLOYMENT.md` (documentation, ops runbook)

**Analog:** `README.md` (solution root) — closest structural match: a root-level, standalone reference document with headers, code blocks, and a sequential narrative. The content domain differs (deployment ops vs. architecture docs) but the Markdown conventions transfer directly.

**Structure pattern from `README.md`** (lines 1–20):

```markdown
# PersonsAPI

> [one-line summary with bold emphasis on key concept]

[Two-sentence context paragraph explaining purpose and audience.]

---

## Table of Contents

- [Section 1](#anchor)
- [Section 2](#anchor)
...

---

## Section 1

[Content with fenced code blocks, tables, and inline emphasis.]
```

**Fenced code block style** (`README.md` lines 283–316): Use triple-backtick with explicit language hint on every code block:

```markdown
```bash
gcloud run deploy persons-api \
  --image ...
```
```

```markdown
```powershell
Get-Content key.json | docker login ...
```
```

Line continuation with backslash (`\`) inside `bash` blocks. No semicolons on chained commands — use `&&` when sequencing is required.

**Inline placeholder pattern** (established in CONTEXT.md D-14, RESEARCH.md D-14):

All gcloud commands use `PROJECT_ID` as the literal placeholder string. The user does a find-replace before executing. Example from RESEARCH.md Code Examples (line 240):

```bash
gcloud iam service-accounts create persons-api-deployer \
  --display-name="PersonsAPI Deployer" \
  --project=PROJECT_ID
```

**Recommended DEPLOYMENT.md top-level structure** (from RESEARCH.md Architecture Patterns, lines 144–177):

```
# PersonsAPI — Cloud Run Deployment Runbook

> [one-line description]

## Prerequisites
## Step 1: GCP Project Setup
## Step 2: Enable Required APIs
## Step 3: Create Artifact Registry Repository
## Step 4: Configure Docker Authentication
## Step 5: Create Service Account
## Step 6: Build, Tag, and Push Docker Image
## Step 7: Deploy to Cloud Run
## Step 8: Verify Deployment
## Appendix: Cleanup / Teardown (optional)
```

**Note and warning callout pattern** — `README.md` uses `>` blockquotes for contextual notes (line 101–103). Apply this pattern in DEPLOYMENT.md for pitfall warnings (e.g., billing-before-APIs, never commit key.json):

```markdown
> **Note:** Link your billing account before running `gcloud services enable`. The enable command
> will fail silently or return a billing error if billing is not active on the project.

> **Warning:** Never commit `key.json` to git. Add it to `.gitignore` immediately after creation
> (Step 5). Store the key in a secure location — it is reused in Phase 8 GitHub Actions.
```

**README.md table pattern** (lines 79–84): Use for the ASPNETCORE_ENVIRONMENT trade-off or verification checklist:

```markdown
| Check | Command | Expected |
|-------|---------|----------|
| Health endpoint | `curl https://<url>/health` | HTTP 200, `{"status":"Healthy"}` |
| Seeded persons | `curl https://<url>/api/persons` | HTTP 200, JSON array with 3 persons |
```

**Complete gcloud run deploy command** (from RESEARCH.md Code Examples, lines 330–341 — verbatim, verified against official docs):

```bash
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

**Verification commands block** (from RESEARCH.md Code Examples, lines 360–381):

```bash
# Get service URL
SERVICE_URL=$(gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)')

# SC-1: Health check
curl --max-time 30 "$SERVICE_URL/health"
# Expected: HTTP 200, body: {"status":"Healthy"}

# SC-2: Seeded persons
curl "$SERVICE_URL/api/persons"
# Expected: HTTP 200, JSON array with 3 persons

# SC-3: No crash loop
gcloud run services describe persons-api --region us-central1 --format='value(status.conditions)'

# SC-4: Logs — open in browser
# https://console.cloud.google.com/logs/query?project=PROJECT_ID
# Filter: resource.type="cloud_run_revision" AND resource.labels.service_name="persons-api"
```

---

### `.gitignore` (config, VCS filter)

**Analog:** `.gitignore` itself (lines 1–17) — this is a single-entry addition to the existing file.

**Existing pattern** (`.gitignore` lines 1–17):

```
## .NET build output
bin/
obj/

## NuGet packages
*.nupkg
*.nuspec

## User-specific files
*.user
*.suo
.vs/

## OS files
.DS_Store
Thumbs.db
```

**Required addition** — append a new section following the same comment-header convention:

```
## GCP credentials (never commit service account keys)
key.json
```

Place this block after the last existing section (`## OS files`). The `key.json` entry must be added before any plan task that creates the key (Step 5 in DEPLOYMENT.md) to prevent accidental staging.

---

## Shared Patterns

### Placeholder Substitution Convention
**Source:** CONTEXT.md D-14; RESEARCH.md throughout Code Examples
**Apply to:** All gcloud commands in DEPLOYMENT.md

Use `PROJECT_ID` as the exact placeholder string — no brackets, no angle-brackets, no underscores alternatives. This makes find-replace unambiguous. The full Artifact Registry image path is:

```
us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest
```

The service account email pattern is:
```
persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com
```

### Port Compatibility Reference
**Source:** `Dockerfile` line 36-37; `docker-compose.yml` lines 12–13; RESEARCH.md Pattern 1 (lines 180–199)
**Apply to:** DEPLOYMENT.md Step 7 (gcloud run deploy) — must use `--port 8080`

The Dockerfile sets `ENV ASPNETCORE_HTTP_PORTS=8080` and `EXPOSE 8080`. Cloud Run injects `PORT=8080` by default. The `--port 8080` flag in `gcloud run deploy` makes this explicit and prevents mismatch. Do NOT use `--port 80`.

```dockerfile
# Dockerfile lines 36-37 — the binding facts that constrain Step 7
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
```

### Health Endpoint Reference
**Source:** `docker-compose.yml` lines 13–18; CONTEXT.md code_context section
**Apply to:** DEPLOYMENT.md Step 8 (verification) and any Cloud Run probe notes

The `/health` endpoint already returns `HTTP 200` with `{"status":"Healthy"}`. The docker-compose healthcheck establishes the proven curl invocation pattern:

```yaml
# docker-compose.yml lines 13–18 — proven health probe
healthcheck:
  test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
  interval: 30s
  timeout: 5s
  retries: 3
  start_period: 30s
```

For the Cloud Run public URL, replace `http://localhost:8080` with `$SERVICE_URL` and add `--max-time 30` to handle cold start (scale-to-zero with 0 min instances):

```bash
curl --max-time 30 "$SERVICE_URL/health"
```

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `DEPLOYMENT.md` | documentation | n/a (ops runbook) | No deployment runbook exists in this project. `README.md` is the closest structural analog (root-level reference doc with Markdown conventions) but covers architecture, not operations. Use RESEARCH.md Architecture Patterns section (lines 144–177) for section structure and Code Examples (lines 329–381) for all gcloud commands verbatim. |

---

## Metadata

**Analog search scope:** `C:\ATS\Git\platform\` (solution root), `.planning/phases/06-containerization/` (prior phase pattern map)
**Files scanned:** `README.md` (382 lines), `Dockerfile` (41 lines), `docker-compose.yml` (19 lines), `.gitignore` (17 lines), `.planning/phases/06-containerization/06-PATTERNS.md` (245 lines), `.planning/PROJECT.md` (139 lines)
**Pattern extraction date:** 2026-06-03
