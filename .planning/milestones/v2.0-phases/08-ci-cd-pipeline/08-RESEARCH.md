# Phase 8: CI/CD Pipeline - Research

**Researched:** 2026-06-04
**Domain:** GitHub Actions + Google Cloud (Artifact Registry + Cloud Run) with .NET 10
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Trigger on push to `main` only — no PR checks.
- **D-02:** Include `workflow_dispatch` to allow manual triggering from GitHub Actions UI.
- **D-03:** Three sequential jobs: `build-and-test` → `push-image` → `deploy` (sequenced via `needs:`).
- **D-04:** A failed test in `build-and-test` blocks `push-image` and `deploy` via `needs:` dependency.
- **D-05:** Tag Docker images with `:latest` only. Each push to `main` overwrites `:latest`.
- **D-06:** The `deploy` job prints the public Cloud Run URL at the end (`gcloud run services describe --format='value(status.url)'`).
- **D-07:** Run all 64 tests in CI. Command: `dotnet test --no-build --configuration Release`.
- **D-08:** GCP authentication via Service Account JSON key using `google-github-actions/auth@v2`.
- **D-09:** Two GitHub Actions repository secrets: `GCP_SA_KEY` and `GCP_PROJECT_ID`.
- **D-10:** PLAN must include a task documenting how to create these secrets in GitHub.

### Claude's Discretion

- Exact GitHub Actions versions for each action (`actions/checkout`, `google-github-actions/auth`, `google-github-actions/setup-gcloud`)
- Whether to cache NuGet packages between runs
- `.NET SDK version pinning` in the workflow (`dotnet-version: '10.x'`)
- Exact job runner (`ubuntu-latest` is standard)
- Whether to add a `permissions: id-token: write` block (not needed for SA key auth)
- Workflow file name: `cicd.yml` or `deploy.yml`

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| CICD-01 | Every push to `main` automatically triggers build → tests → push to Artifact Registry → deploy to Cloud Run via GitHub Actions | Three-job workflow with `on: push: branches: [main]` + `workflow_dispatch`; `needs:` chain enforces ordering and gates; all 64 tests run in `build-and-test`; SA key auth enables push and deploy |
</phase_requirements>

---

## Summary

Phase 8 is purely a YAML authoring phase. The infrastructure (GCP project, Artifact Registry, Cloud Run service, Service Account with `key.json`) was fully provisioned in Phase 7. The workflow replicates `DEPLOYMENT.md` Steps 4–8 in CI, adding a dotnet test gate that the manual runbook skips.

The workflow uses three distinct tools from the `google-github-actions` organization: `auth@v2` authenticates the runner to GCP using the Service Account JSON key, `setup-gcloud@v2` installs the Cloud SDK so `gcloud` commands work, and then native shell steps handle Docker auth (`gcloud auth configure-docker`), image build and push, and `gcloud run deploy`. No GCP-native GitHub Actions deploy action is required — plain `gcloud run deploy` with the correct flags is sufficient and directly mirrors the manual runbook.

.NET 10 is already pre-installed on `ubuntu-latest` (Ubuntu 24.04) runners, so `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` is a safety pin, not a fresh install. The entire `build-and-test` job has no external service dependencies since all 64 tests use EF Core InMemory — there is nothing to provision, mock, or spin up.

**Primary recommendation:** Three-job workflow in `.github/workflows/cicd.yml` using `google-github-actions/auth@v2` + `google-github-actions/setup-gcloud@v2` + `gcloud auth configure-docker` + `gcloud run deploy`. Skip NuGet caching (no `packages.lock.json`, <5 packages, negligible benefit). Pin all action versions to major version tags (`@v2`, `@v4`). Do not include `id-token: write` permission — not needed for SA key auth.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Checkout source code | CI Runner | — | Standard first step in every job that needs source |
| .NET build + test | CI Runner (build-and-test job) | — | Runs on ubuntu-latest; .NET 10 pre-installed |
| Docker image build | CI Runner (push-image job) | — | `docker build` against the solution root Dockerfile |
| GCP authentication | CI Runner (push-image + deploy jobs) | — | SA key from GitHub secret; scoped to jobs that need GCP |
| Docker image push | Artifact Registry (GCP) | CI Runner triggers it | Runner authenticates and pushes; AR stores the image |
| Cloud Run deployment | Cloud Run (GCP) | CI Runner triggers it | `gcloud run deploy` invokes the GCP control plane |
| Secret storage | GitHub Repository Secrets | — | `GCP_SA_KEY` and `GCP_PROJECT_ID` live in repo settings |
| Deployment URL output | CI Runner (deploy job) | — | `gcloud run services describe` prints the URL to the log |

---

## Standard Stack

### Core (GitHub Actions)

| Action | Version | Purpose | Why This Version |
|--------|---------|---------|-----------------|
| `actions/checkout` | `@v4` | Check out repository source | Latest stable major; supports `persist-credentials` options [VERIFIED: github.com/actions/checkout/releases] |
| `actions/setup-dotnet` | `@v4` | Pin/ensure .NET SDK | Latest stable major; supports built-in NuGet caching via `cache: true` [VERIFIED: github.com/actions/setup-dotnet/releases] |
| `google-github-actions/auth` | `@v2` | Authenticate to GCP with SA JSON key | v2 is the floating alias for the v2 branch (latest: v2.1.13); D-08 locks this choice; no breaking changes in the v2.x series [VERIFIED: github.com/google-github-actions/auth/releases] |
| `google-github-actions/setup-gcloud` | `@v2` | Install Cloud SDK on runner | v2 is stable and production-ready; v3.0.x requires Node 24 which is not yet the default runner environment — v2 is the safer choice [VERIFIED: github.com/google-github-actions/setup-gcloud/releases] |

**Note on v3 actions:** Both `google-github-actions/auth` and `google-github-actions/setup-gcloud` have v3 releases. The v3.0.x releases require Node 24 on the runner. `ubuntu-latest` (Ubuntu 24.04) ships with Node 20, not Node 24. Using v3 may work if the runner auto-updates its Node toolchain, but v2 is the safe choice for this project to avoid a runtime compatibility surprise. Pin to `@v2` for both. [CITED: github.com/google-github-actions/setup-gcloud/releases — v3.0.0 "breaking changes: requires Node 24+"]

### Supporting Commands (run: steps, no actions)

| Command | Job | Purpose |
|---------|-----|---------|
| `dotnet restore` | build-and-test | Restore NuGet packages |
| `dotnet build --configuration Release --no-restore` | build-and-test | Compile all projects in Release mode |
| `dotnet test --no-build --configuration Release` | build-and-test | Run all 64 tests against already-built binaries |
| `gcloud auth configure-docker us-central1-docker.pkg.dev --quiet` | push-image | Register gcloud credential helper for Artifact Registry |
| `docker build -t IMAGE_URL .` | push-image | Build the production image (same as DEPLOYMENT.md Step 6) |
| `docker push IMAGE_URL` | push-image | Push to Artifact Registry |
| `gcloud run deploy persons-api --image IMAGE_URL --region us-central1 --quiet` | deploy | Re-deploy Cloud Run service with updated image |
| `gcloud run services describe persons-api --region us-central1 --format='value(status.url)'` | deploy | Print the live Cloud Run URL to the job log |

---

## Package Legitimacy Audit

This phase installs no npm, PyPI, or NuGet packages. GitHub Actions are referenced by YAML step declarations — the runner resolves them at execution time from `github.com/org/repo`. No `npm install` or equivalent occurs.

| Action | Source Org | Age | Usage | Source Repo | Verdict |
|--------|-----------|-----|-------|-------------|---------|
| `actions/checkout@v4` | GitHub, Inc. (official) | 6+ yrs | Billions/wk | github.com/actions/checkout | Approved — first-party GitHub |
| `actions/setup-dotnet@v4` | GitHub, Inc. (official) | 5+ yrs | Millions/wk | github.com/actions/setup-dotnet | Approved — first-party GitHub |
| `google-github-actions/auth@v2` | Google, Inc. (official) | 3+ yrs | Millions/wk | github.com/google-github-actions/auth | Approved — first-party Google |
| `google-github-actions/setup-gcloud@v2` | Google, Inc. (official) | 4+ yrs | Millions/wk | github.com/google-github-actions/setup-gcloud | Approved — first-party Google |

**Packages removed due to slopcheck verdict:** None (slopcheck not applicable — no registry packages installed).
**Packages flagged as suspicious:** None.

---

## Architecture Patterns

### System Architecture Diagram

```
push to main (or workflow_dispatch)
        |
        v
+----------------------+
|   build-and-test     |  ubuntu-latest
|  actions/checkout    |
|  setup-dotnet@v4     |
|  dotnet restore      |
|  dotnet build -c Rel |
|  dotnet test --no-build -c Rel  |--FAIL--> workflow stops (push-image + deploy blocked)
+----------------------+
        |
        | (needs: build-and-test)
        v
+----------------------+
|    push-image        |  ubuntu-latest (fresh runner)
|  actions/checkout    |
|  google-github-actions/auth@v2   |<-- secrets.GCP_SA_KEY
|  google-github-actions/setup-gcloud@v2 |
|  gcloud configure-docker         |
|  docker build -t IMAGE_URL:latest . |
|  docker push IMAGE_URL:latest    |-->  Artifact Registry
+----------------------+            us-central1-docker.pkg.dev/
        |
        | (needs: push-image)
        v
+----------------------+
|     deploy           |  ubuntu-latest (fresh runner)
|  google-github-actions/auth@v2   |<-- secrets.GCP_SA_KEY
|  google-github-actions/setup-gcloud@v2 |
|  gcloud run deploy persons-api   |-->  Cloud Run (us-central1)
|    --image IMAGE_URL:latest           pulls fresh :latest
|    --region us-central1         |
|    --quiet                      |
|  gcloud run services describe   |--> prints HTTPS URL to log
+----------------------+
```

### Recommended Project Structure

```
.github/
└── workflows/
    └── cicd.yml          # the single workflow file for this phase
```

### Pattern 1: Three-Job Sequential Pipeline with `needs:`

**What:** Each job declares `needs: [prior-job]` which prevents execution unless the prior job succeeded. GitHub Actions skips downstream jobs when an upstream job fails — no `if:` conditions needed.

**When to use:** When you need a hard gate (tests must pass before docker push; docker push must succeed before deploy).

**Example:**
```yaml
# Source: GitHub Actions documentation
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps: [...]

  push-image:
    runs-on: ubuntu-latest
    needs: [build-and-test]
    steps: [...]

  deploy:
    runs-on: ubuntu-latest
    needs: [push-image]
    steps: [...]
```

### Pattern 2: SA Key Auth via `google-github-actions/auth@v2`

**What:** Pass the full JSON key contents as a GitHub secret; the `credentials_json` input decodes and applies it as Application Default Credentials. Downstream steps (gcloud, docker) inherit the credentials automatically.

**When to use:** Service Account JSON key auth (D-08). Not Workload Identity Federation (explicitly out of scope per REQUIREMENTS.md).

**Example:**
```yaml
# Source: github.com/google-github-actions/auth docs/EXAMPLES.md
- uses: 'google-github-actions/auth@v2'
  with:
    credentials_json: '${{ secrets.GCP_SA_KEY }}'
```

**Important:** Do NOT include `id-token: write` in the job permissions block for this auth method. That permission is only required for Workload Identity Federation (which uses GitHub's OIDC token). For SA key auth, the minimal permissions block is:
```yaml
permissions:
  contents: read
```

### Pattern 3: Docker Auth to Artifact Registry via `gcloud auth configure-docker`

**What:** After `google-github-actions/auth@v2` and `google-github-actions/setup-gcloud@v2` run, calling `gcloud auth configure-docker` updates `~/.docker/config.json` on the runner to use the gcloud credential helper for the specified registry hostname. Standard `docker build` + `docker push` then work without extra credentials.

**When to use:** When `setup-gcloud` is already in the job (as it is here for the `gcloud run deploy` step). Avoids the complexity of `docker/login-action` with `token_format: access_token`. One command, same result.

**Example:**
```yaml
# Source: DEPLOYMENT.md Step 4 (already validated in Phase 7)
- name: Configure Docker for Artifact Registry
  run: gcloud auth configure-docker us-central1-docker.pkg.dev --quiet
```

**Alternative rejected:** `docker/login-action` with `token_format: access_token` requires setting `token_format: 'access_token'` on the auth step and piping `${{ steps.auth.outputs.access_token }}` to the login action. That's two extra moving parts for the same outcome. [ASSUMED: rejection reasoning]

### Pattern 4: `dotnet test --no-build --configuration Release`

**What:** After `dotnet build --configuration Release --no-restore`, the `--no-build` flag tells `dotnet test` to skip recompilation and run against the already-built `Release` binaries. The `--configuration Release` flag must match the build step's configuration.

**When to use:** Always in CI when a build step precedes the test step. Saves 15-30 seconds on small solutions.

**Critical:** Both `dotnet build` and `dotnet test` must use the same `--configuration`. Mixing `Release` build with `Debug` test (missing `--configuration Release`) causes the test runner to look for `Debug` binaries that don't exist. [VERIFIED: docs.github.com/en/actions/tutorials/build-and-test-code/net — "you can use the same commands you use locally"]

**Example:**
```yaml
- name: Build
  run: dotnet build src/PersonsAPI.sln --configuration Release --no-restore

- name: Test
  run: dotnet test src/PersonsAPI.sln --no-build --configuration Release --verbosity normal
```

### Pattern 5: `gcloud run deploy` Non-Interactive Flags

**What:** `--quiet` suppresses all interactive prompts and confirmation dialogs. Without it, `gcloud run deploy` may prompt for platform selection or billing confirmation, blocking CI indefinitely.

**Key flags for this project:**
- `--image` — required; full Artifact Registry URL
- `--region us-central1` — required; matches Phase 7 deployment region
- `--quiet` — required in CI; suppresses all prompts
- `--platform managed` — recommended for explicitness; prevents ambiguity if gcloud defaults change

**Example:**
```yaml
- name: Deploy to Cloud Run
  run: |
    gcloud run deploy persons-api \
      --image us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest \
      --region us-central1 \
      --platform managed \
      --quiet
```

**Note:** `--platform managed` does not appear in the official gcloud reference as a required flag — Cloud Run managed is the default. Including it is defensive and explicit without any downside. [ASSUMED: "default platform" claim based on service behavior — official docs do not explicitly state the default]

### Pattern 6: Fresh Runner Per Job — No Artifact Sharing Needed

**What:** Each job in a GitHub Actions workflow runs on a fresh, isolated runner VM. The `push-image` job cannot access build artifacts produced by `build-and-test`. However, since the image is tagged `:latest` (D-05), the `deploy` job only needs the image URL string — which is a constant derived from `${{ secrets.GCP_PROJECT_ID }}`. No `actions/upload-artifact` / `actions/download-artifact` is needed.

**When this matters:** If SHA-based tags were used (`image:${{ github.sha }}`), the deploy job would need to know the SHA from the push-image job via `outputs:`. With `:latest` only (D-05), this complexity disappears entirely.

### Anti-Patterns to Avoid

- **Using `@latest` or `@main` for action versions:** Floating tags can break workflows when an action releases breaking changes. Pin to `@v2`, `@v4`, etc.
- **Including `id-token: write` for SA key auth:** This permission is only for Workload Identity Federation. Including it unnecessarily broadens the token's scope.
- **Omitting `--configuration Release` from `dotnet test --no-build`:** Without it, the test runner searches for `Debug` binaries built by a `Release` build step — the path does not exist and the step fails.
- **Running `dotnet test` without `--no-build` after `dotnet build`:** Wastes time; each project is recompiled even though `dotnet build` already produced binaries.
- **Not setting `--quiet` on gcloud commands:** Interactive prompts in CI block the runner and the job eventually times out.
- **Committing `key.json` to git:** The `.gitignore` already excludes it from Phase 7. The GitHub secret (`GCP_SA_KEY`) must be populated from the `key.json` contents, not from a checked-in file.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| GCP authentication | Custom credential setup scripts | `google-github-actions/auth@v2` | Handles ADC, credential file creation, post-job cleanup, and secure secret decoding |
| gcloud CLI installation | Downloading the SDK manually | `google-github-actions/setup-gcloud@v2` | Installs correct version, sets PATH, integrates with auth@v2 ADC automatically |
| Docker auth to Artifact Registry | Manual token management | `gcloud auth configure-docker` (one line) | Reuses the active gcloud credentials already set by auth@v2; no token lifetime concerns |
| .NET SDK setup | Pre-checking runner toolchain versions | `actions/setup-dotnet@v4` | Ensures exact version is present even if runner toolchain changes; built-in NuGet caching |

**Key insight:** The `google-github-actions` action suite is written and maintained by Google. Each action handles the error-prone mechanics (credential file paths, ADC environment variables, post-job cleanup) that would require significant custom shell scripting to replicate correctly.

---

## Common Pitfalls

### Pitfall 1: Configuration Mismatch Between Build and Test Steps

**What goes wrong:** `dotnet test --no-build` fails with "could not find test assembly" or similar because the test runner looks for binaries in the `Debug` output path, but the `dotnet build` step compiled to `Release`.

**Why it happens:** `--no-build` skips recompilation but does not automatically infer the configuration used in the prior build step. The `--configuration` flag must be explicit on both `dotnet build` and `dotnet test`.

**How to avoid:** Both steps must include `--configuration Release`. The test step must be `dotnet test --no-build --configuration Release`.

**Warning signs:** Error message contains "build output directory" or "net10.0/Debug" path references when you expected Release.

### Pitfall 2: `id-token: write` Added for SA Key Auth

**What goes wrong:** Workflow works (the permission does not break SA key auth) but the job requests unnecessary permissions, broadening the OIDC token scope.

**Why it happens:** Many Google Cloud GitHub Actions examples target Workload Identity Federation and show `id-token: write`. Copying these examples without reading the auth method creates unnecessary permission escalation.

**How to avoid:** For `credentials_json` (SA key), the required permissions are only `contents: read` (for checkout). Omit `id-token: write` entirely.

**Warning signs:** Copying any official GCP example workflow will likely include `id-token: write` — check the auth method first.

### Pitfall 3: `gcloud run deploy` Blocking on Interactive Prompts

**What goes wrong:** The deploy job hangs until the job timeout (6 hours by default) because gcloud is waiting for a "Do you want to continue? (y/N)" prompt that no one can answer.

**Why it happens:** Omitting `--quiet` from `gcloud run deploy` (and other gcloud commands) leaves interactive prompts active. In a terminal they're harmless; in CI they block.

**How to avoid:** Always add `--quiet` to any `gcloud` command in a workflow that could prompt.

**Warning signs:** Job log shows gcloud output ending without an error but the job never progresses past that step.

### Pitfall 4: `push-image` Job Checks Out Code but SA Key Not Re-Authenticated

**What goes wrong:** The `push-image` job fails with "permission denied" on `docker push` because each job runs on a fresh runner — the auth step from `build-and-test` does not carry over.

**Why it happens:** GitHub Actions job isolation: each job gets a new runner VM. Authentication done in `build-and-test` is not visible to `push-image` or `deploy`.

**How to avoid:** Every job that needs GCP access must repeat the `google-github-actions/auth@v2` and `google-github-actions/setup-gcloud@v2` steps. The `build-and-test` job does NOT need these steps (it only runs dotnet commands).

**Warning signs:** Auth-related error in a job that doesn't include the auth steps.

### Pitfall 5: `GCP_SA_KEY` Secret Contains Whitespace or Newlines

**What goes wrong:** `google-github-actions/auth@v2` fails to parse the credentials JSON with an error like "invalid JSON" or "unexpected token".

**Why it happens:** When copying `key.json` contents to the GitHub secret, whitespace or trailing newlines can corrupt the JSON structure. GitHub compresses the displayed value but the actual stored bytes matter.

**How to avoid:** Minify the JSON before pasting into the GitHub secret. Use `cat key.json | tr -d '\n'` or copy the file content as a single line. The auth action README explicitly recommends storing the key as a minified single-line string. [CITED: github.com/google-github-actions/auth README]

**Warning signs:** Auth step fails immediately with JSON parse error rather than an authentication error.

### Pitfall 6: `.NET 10 Not Available on Runner (Edge Case)`

**What goes wrong:** `dotnet build` fails with "SDK version not found" if the runner image does not include .NET 10.

**Why it happens:** `ubuntu-latest` currently maps to Ubuntu 24.04 which includes .NET 10.0.x pre-installed. However, if `ubuntu-latest` ever maps to a new LTS without .NET 10, the pinned `actions/setup-dotnet@v4` step saves the run.

**How to avoid:** Include `actions/setup-dotnet@v4` with `dotnet-version: '10.x'` even though .NET 10 is currently pre-installed. This is defensive and adds negligible time when the SDK is already present.

**Warning signs:** Would only appear if `ubuntu-latest` changes its underlying OS image.

---

## Code Examples

Verified patterns from official sources and Phase 7 runbook:

### Complete Workflow Skeleton

```yaml
# Source: github.com/google-github-actions/auth EXAMPLES.md +
#         github.com/google-github-actions/setup-gcloud README +
#         DEPLOYMENT.md Steps 4-8

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

**Notes on this skeleton:**
- `build-and-test` does NOT include auth/gcloud steps — it only needs dotnet.
- `deploy` does NOT include `actions/checkout@v4` — it only runs gcloud commands against an already-pushed image.
- The `IMAGE_URL` env var is duplicated across jobs intentionally — job isolation means env vars don't share.
- `permissions: contents: read` is explicit for each job. No `id-token: write` — not needed for SA key auth.

### Printing the Cloud Run URL (D-06)

```bash
# Source: DEPLOYMENT.md Step 8 (already validated in Phase 7)
gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)'
```

### Creating GitHub Secrets via CLI (alternative to UI)

```bash
# Source: GitHub CLI docs (gh secret set)
# Requires gh CLI authenticated to the repository
gh secret set GCP_SA_KEY < key.json
gh secret set GCP_PROJECT_ID --body "your-project-id"
```

The UI path: GitHub repo → Settings → Secrets and variables → Actions → New repository secret.

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `google-github-actions/auth@v0` with `token_format: access_token` for docker login | `google-github-actions/auth@v2` + `gcloud auth configure-docker` | ~2022 | Simpler — no token piping to docker/login-action |
| `google/github-actions/setup-gcloud@v1` (separate Google org) | `google-github-actions/setup-gcloud@v2` | ~2022 | Consolidated under `google-github-actions` org |
| Swashbuckle for API docs | Scalar + Microsoft.AspNetCore.OpenApi | .NET 9 | Not relevant to CI/CD phase but consistent with CLAUDE.md |
| MediatR with reflection | Mediator.SourceGenerator 3.0.2 | 2023 | Not relevant to CI/CD phase |
| `actions/setup-dotnet@v2` | `actions/setup-dotnet@v4/v5` | 2024 | v4 added built-in NuGet caching; v5 added `dotnet-version: latest` |

**Deprecated/outdated:**
- `google-github-actions/deploy-cloudrun` action: Exists and works, but adds abstraction over `gcloud run deploy`. For a learning project that mirrors a manual runbook, raw `gcloud run deploy` is more transparent and directly matches `DEPLOYMENT.md`.
- `actions/setup-dotnet@v2` / `@v3`: Superseded; use v4 (or v5 — see note below).

**Note on `actions/setup-dotnet` v5:** Version 5.3.0 was released May 28, 2026. It adds `dotnet-version: latest` support. v4 remains stable and widely used. Either v4 or v5 is correct; pin to `@v4` for conservatism unless the team wants the `latest` channel feature.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `ubuntu-latest` maps to Ubuntu 24.04 at time of execution | Standard Stack | If it remaps to Ubuntu 22.04 or earlier, .NET 10 may not be pre-installed — mitigated by setup-dotnet@v4 pin |
| A2 | `google-github-actions/setup-gcloud@v2` works on a runner with Node 20 (not Node 24) | Standard Stack | v3.0.x requires Node 24; v2 documented behavior is compatible with Node 20. If v2 also silently requires Node 24, the install step fails |
| A3 | `--platform managed` is the correct flag value for Cloud Run fully managed (vs. GKE) | Code Examples | Wrong value would cause gcloud to error or deploy to wrong target |
| A4 | SA `persons-api-deployer` from Phase 7 has both `roles/artifactregistry.writer` AND `roles/run.admin` already assigned | Architecture Patterns | If only one role was assigned, either the docker push or the gcloud run deploy step fails with permission denied |
| A5 | `dotnet/login-action` alternative rejection reasoning | Architecture Patterns | If `gcloud auth configure-docker` stops working for some runner environment reason, fallback to `docker/login-action` with access_token is a known alternative |

---

## Open Questions

1. **`actions/setup-dotnet` v4 vs v5**
   - What we know: v4 is current stable, widely used; v5 (released 2026-05-28) adds `dotnet-version: latest` support
   - What's unclear: Whether v5 has any regressions in the first weeks after release
   - Recommendation: Pin to `@v4` for this phase. Conservative choice with no functional difference for `dotnet-version: '10.x'`.

2. **`setup-gcloud` version pinning**
   - What we know: v2 is stable production; v3.0.x requires Node 24 on the runner
   - What's unclear: Whether `ubuntu-latest` includes Node 24 by default (it ships Node 20 as of June 2026 per runner images)
   - Recommendation: Use `@v2` (maps to latest v2.x patch). If the first workflow run fails with a Node version error, upgrading to `@v3` is a one-line fix.

3. **NuGet caching worth adding?**
   - What we know: `actions/setup-dotnet@v4` supports `cache: true` built-in; requires `packages.lock.json` in the repo (none exists currently)
   - What's unclear: Whether adding lock file generation is worth the setup overhead for <5 packages
   - Recommendation: Skip caching entirely. For 5 packages (EF Core InMemory, FluentValidation, Mediator, Scalar, Serilog), restore takes ~10-15 seconds. The setup overhead to add `packages.lock.json` is not justified.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| git | actions/checkout | ✓ | 2.53.0 (local); runner has git built-in | — |
| Docker | push-image job | ✓ | 29.4.0 (local); Docker is pre-installed on ubuntu-latest | — |
| gcloud CLI | push-image, deploy jobs | ✓ | 571.0.0 (local); installed by setup-gcloud@v2 on runner | — |
| .NET 10 SDK | build-and-test job | ✓ | 10.0.202 (local); pre-installed on ubuntu-latest (10.0.300 on runner) | setup-dotnet@v4 installs it if missing |
| GitHub repository secrets | push-image, deploy jobs | Pending | — | Must be created manually before first run |
| `.github/workflows/` directory | workflow file | ✓ | Directory exists (empty) | — |

**Missing dependencies with no fallback:**
- `GCP_SA_KEY` secret — must be created in GitHub repo settings before the first run. Contents = `key.json` from Phase 7 (minified, single-line JSON).
- `GCP_PROJECT_ID` secret — must be created in GitHub repo settings before the first run. Value = GCP project ID string (e.g., `personsapi-XXXXXX`).

**Missing dependencies with fallback:**
- None.

---

## Security Domain

> `security_enforcement: true`, `security_asvs_level: 1` (ASVS Level 1 — opportunistic verification).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | Pipeline auth is GCP SA key, not end-user auth |
| V3 Session Management | No | CI pipeline has no user sessions |
| V4 Access Control | Yes (V4.1) | SA has least-privilege roles only: `roles/artifactregistry.writer` + `roles/run.admin` — no `Owner`/`Editor` |
| V5 Input Validation | No | Pipeline has no user-supplied input at runtime |
| V6 Cryptography | Partial | SA JSON key is a long-lived credential; stored in GitHub encrypted secrets (GitHub uses AES-256 at rest) |
| V7 Error Handling | Yes | Failed jobs expose build logs publicly if repo is public — logs should not echo secret values |
| V9 Communications | Yes (V9.1) | All gcloud and docker push traffic is HTTPS/TLS by default |

### Known Threat Patterns for GitHub Actions + GCP SA Key

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Secret exfiltration via `run: echo ${{ secrets.GCP_SA_KEY }}` | Info Disclosure | GitHub automatically masks secret values in logs; never echo secrets explicitly |
| Overly broad SA permissions | Elevation of Privilege | Least privilege: only `artifactregistry.writer` + `run.admin`; already established in Phase 7 |
| Third-party action supply chain compromise | Tampering | Pin actions to major version tags (`@v2`, `@v4`) from verified GitHub/Google orgs; avoid `@latest` |
| Whitespace in `GCP_SA_KEY` corrupting JSON | Tampering / DoS | Minify JSON before storing as secret; test with `workflow_dispatch` before merging |
| SA key never rotated | Info Disclosure | Key rotation is out of scope for this learning project but should be noted for production use |
| Public repo log exposure | Info Disclosure | GitHub masks declared secrets in logs; do not construct strings that embed secret values in non-secret variables |

### ASVS Level 1 Compliance Summary

This workflow meets ASVS Level 1 requirements for the CI/CD context:
- V4.1.1: SA uses least-privilege roles (established Phase 7)
- V6.2.1: Credentials stored in GitHub encrypted secrets (AES-256 at rest)
- V9.1.1: All network traffic uses TLS (gcloud and docker default behavior)
- V7.4.1: GitHub secret masking prevents credential exposure in logs

---

## Sources

### Primary (HIGH confidence)
- [github.com/google-github-actions/auth — releases + EXAMPLES.md] — SA key auth syntax, `credentials_json` input, no `id-token: write` required, latest version v2.1.13 (floating `@v2`)
- [github.com/google-github-actions/setup-gcloud — releases + README] — setup-gcloud@v2 usage after auth, v3.0.x requires Node 24 (breaking change)
- [github.com/actions/setup-dotnet — README caching section] — `cache: true` requires `packages.lock.json`; latest v5.3.0
- [github.com/actions/runner-images — Ubuntu2404-Readme.md] — .NET 10 pre-installed on ubuntu-latest (10.0.108, 10.0.204, 10.0.300)
- [docs.github.com/en/actions/tutorials/build-and-test-code/net] — `dotnet build --no-restore` + `dotnet test --no-build` canonical pattern
- [DEPLOYMENT.md — solution root] — canonical gcloud commands for docker auth, image push, Cloud Run deploy, URL output (all validated in Phase 7)

### Secondary (MEDIUM confidence)
- [github.com/actions/checkout — releases] — latest v6.0.3; `@v4` is the recommended stable pin
- [github.com/actions/cache — releases] — latest v5.0.5
- [dev.to/shivamjainn — Cloud Run + GitHub Actions article] — confirms `credentials_json` + `setup-gcloud` + `configure-docker` pattern works together
- [cheatsheetseries.owasp.org — GitHub Actions Security Cheat Sheet] — secret masking, pin action versions, least-privilege SA

### Tertiary (LOW confidence — flagged for validation)
- [`--platform managed` is the correct/default flag value] — documented usage in multiple examples but official gcloud reference does not list it as required; included defensively
- [setup-gcloud@v2 Node 20 compatibility] — based on release notes stating v3 requires Node 24, implying v2 works with Node 20; not explicitly confirmed by a test

---

## Metadata

**Confidence breakdown:**
- Standard Stack (actions): HIGH — verified from GitHub official release pages
- .NET 10 on ubuntu-latest: HIGH — verified from runner-images readme
- `credentials_json` auth pattern: HIGH — from official google-github-actions/auth EXAMPLES.md
- `dotnet test --no-build --configuration Release` pattern: HIGH — from official Microsoft docs
- `gcloud run deploy` flags: MEDIUM — `--quiet` and `--region` confirmed; `--platform managed` is defensive/assumed correct
- NuGet cache skip recommendation: HIGH — confirmed cache requires packages.lock.json which this project lacks
- Security domain: HIGH — ASVS mapping is straightforward for a CI pipeline; controls are inherited from Phase 7 SA setup

**Research date:** 2026-06-04
**Valid until:** 2026-09-04 (90 days — GitHub Actions major action versions are stable; re-check before upgrading to @v3 actions)
