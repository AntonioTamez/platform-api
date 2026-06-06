---
phase: 08-ci-cd-pipeline
plan: "01"
subsystem: ci-cd
tags: [github-actions, cicd, cloud-run, artifact-registry, gcp]
dependency_graph:
  requires: [07-cloud-run-deployment]
  provides: [automated-ci-cd-pipeline]
  affects: [DEPLOYMENT.md, .github/workflows/cicd.yml]
tech_stack:
  added: [github-actions, google-github-actions/auth@v2, google-github-actions/setup-gcloud@v2]
  patterns: [three-job-pipeline, sa-key-auth, needs-dependency-gate, workflow_dispatch-manual-trigger]
key_files:
  created:
    - .github/workflows/cicd.yml
  modified:
    - DEPLOYMENT.md
decisions:
  - "SA key auth (credentials_json) over Workload Identity Federation — simpler for solo learning project, SA already created in Phase 7"
  - ":latest tag only — no SHA tagging; consistent with Phase 7 manual push approach"
  - "No NuGet caching (cache: true omitted) — no packages.lock.json in project"
  - "deploy job omits checkout and full Step 7 flags — preserves existing Cloud Run service config (port/memory/cpu/min-instances/allow-unauthenticated)"
  - "IMAGE_URL duplicated in push-image and deploy env — GitHub Actions job isolation; env vars do not cross job boundaries"
metrics:
  duration: "5 minutes"
  completed: "2026-06-05"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 1
---

# Phase 08 Plan 01: CI/CD Pipeline Summary

Three-job GitHub Actions workflow (build-and-test → push-image → deploy) automating the manual Cloud Run deployment runbook with a test gate and secrets setup documentation.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Author three-job CI/CD workflow | d44b215 | .github/workflows/cicd.yml (created) |
| 2 | Document GitHub Actions secrets setup | ed7479b | DEPLOYMENT.md (Step 9 appended) |

## What Was Built

### `.github/workflows/cicd.yml`

A three-job GitHub Actions pipeline triggered on push to `main` and `workflow_dispatch`:

1. **`build-and-test`** — dotnet restore + build (`--configuration Release`) + test (`--no-build --configuration Release`) against `src/PersonsAPI.sln`. Runs all 64 tests (Domain 32, Application 15, Infrastructure 5, Integration 12). No GCP auth steps. Uses `actions/setup-dotnet@v4` with `dotnet-version: '10.x'`, no NuGet cache.

2. **`push-image`** — `needs: [build-and-test]`. Authenticates to GCP via `google-github-actions/auth@v2` with `credentials_json: '${{ secrets.GCP_SA_KEY }}'`. Configures Docker auth for Artifact Registry (`gcloud auth configure-docker us-central1-docker.pkg.dev --quiet`). Builds and pushes `:latest` image to `us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:latest`.

3. **`deploy`** — `needs: [push-image]`. Same SA key auth. Runs `gcloud run deploy persons-api --image ... --region us-central1 --platform managed --quiet` (omits the full Step 7 flag set to preserve existing service config). Prints the live Cloud Run URL via `gcloud run services describe --format='value(status.url)'`.

Security controls applied: no `id-token: write` (T-08-04), no echoed secrets (T-08-01), all actions pinned to major-version tags (`@v4`/`@v2`, T-08-SC), `permissions: contents: read` on every job.

### `DEPLOYMENT.md` — Step 9

New section inserted between Step 8 and the Appendix (ToC entry added). Documents:
- Why `GCP_SA_KEY` must be minified single-line JSON (`cat key.json | tr -d '\n'`) — prevents JSON parse error in `google-github-actions/auth@v2` (Pitfall 5)
- Both creation paths: GitHub UI (Settings → Secrets and variables → Actions) and `gh secret set` CLI
- `workflow_dispatch` as the manual end-to-end test path without a real `main` push
- Security note: AES-256 at rest, automatic log masking, no-echo rule

## Deviations from Plan

None — plan executed exactly as written.

## Threat Mitigations Applied

| Threat ID | Mitigation |
|-----------|-----------|
| T-08-01 | `GCP_SA_KEY` passed only via `credentials_json:` — never in `run:` steps; no `echo` + secrets patterns |
| T-08-02 | Key stored as GitHub encrypted secret (AES-256); referenced by name; `key.json` already in `.gitignore` from Phase 7 |
| T-08-03 | SA `persons-api-deployer` holds only `roles/artifactregistry.writer` + `roles/run.admin` (set in Phase 7) |
| T-08-04 | Each job has `permissions: contents: read`; `id-token: write` absent; `id-token` string absent from file |
| T-08-SC | All four actions pinned to major-version tags from first-party orgs; no `@latest`/`@main` |
| T-08-05 | Minification requirement documented in Step 9 with the `tr -d '\n'` command and explanation |

## Known Stubs

None — no UI components, no data stubs. This plan produces YAML and documentation only.

## Threat Flags

None — no new network endpoints, auth paths, file access patterns, or schema changes introduced. The workflow authenticates to GCP using a pre-existing Service Account from Phase 7.

## Phase Verification Results

| Check | Result |
|-------|--------|
| `cicd.yml` is valid YAML | PASS |
| Three sequential jobs with needs: dependencies | PASS |
| `id-token` absent from cicd.yml | PASS |
| No `@latest` or `@main` action references | PASS |
| DEPLOYMENT.md Step 9 documents secrets and workflow_dispatch | PASS |

## Self-Check: PASSED

- `.github/workflows/cicd.yml` exists: confirmed
- `DEPLOYMENT.md` Step 9 section exists: confirmed
- Commit `d44b215` (Task 1): confirmed
- Commit `ed7479b` (Task 2): confirmed

## Next Steps

**Runtime verification (post-merge, requires secrets):** Maintainer creates `GCP_SA_KEY` and `GCP_PROJECT_ID` in GitHub repo Settings → Secrets and variables → Actions, then triggers workflow via `workflow_dispatch`. All three jobs should pass and the `deploy` job log should print the `https://persons-api-...run.app` URL. This confirms ROADMAP success criteria 1–4 and completes CICD-01.
