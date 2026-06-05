---
plan: 07-01
phase: 07-cloud-run-deployment
status: complete
completed: 2026-06-04
requirements: [CLOUD-01]
---

# Plan 07-01 Summary: Gitignore Guard + DEPLOYMENT.md Runbook

## What Was Built

Two artifacts enabling manual Cloud Run deployment:

1. **`.gitignore` update** — Added `## GCP credentials (never commit service account keys)` section with `key.json` entry, after the existing `## OS files` section. The guard is active before any runbook step creates the key.

2. **`DEPLOYMENT.md`** (374 lines) — Complete Cloud Run deployment runbook at the solution root. Covers the full sequence from gcloud SDK installation on Windows through 4-criteria public URL verification. Uses `PROJECT_ID` as the single find-replace placeholder throughout.

## Tasks Completed

| # | Task | Status |
|---|------|--------|
| 1 | Add GCP credentials section to .gitignore | ✓ Complete |
| 2 | Author DEPLOYMENT.md Cloud Run runbook | ✓ Complete |

## Key Files

### Created
- `DEPLOYMENT.md` — 374-line runbook; 8 sequential steps + appendix teardown

### Modified
- `.gitignore` — Added 3 lines (blank line, section header, `key.json`)

## DEPLOYMENT.md Section Coverage

| Section | Decision(s) Implemented |
|---------|------------------------|
| Prerequisites | gcloud SDK Windows installer URL; Docker Desktop check |
| Step 1: GCP Project Setup | D-01 (new project), D-14 (PROJECT_ID placeholder), billing-before-APIs warning |
| Step 2: Enable Required APIs | Both `run.googleapis.com` and `artifactregistry.googleapis.com` |
| Step 3: Artifact Registry | D-02 (us-central1), D-03 (personsapi repo) |
| Step 4: Docker Auth | `gcloud auth configure-docker us-central1-docker.pkg.dev` |
| Step 5: Service Account | D-09 (least privilege: writer + run.admin), D-10 (keep for Phase 8), D-11 (JSON key) |
| Step 6: Build + Push | D-03 image path; Dockerfile port compatibility note |
| Step 7: Deploy | D-04 (persons-api), D-05 (min-instances 0), D-06 (512Mi/1cpu), D-07 (public), --port 8080 warning, ASPNETCORE_ENVIRONMENT=Production |
| Step 8: Verify | D-15 all 4 success criteria; SC-4 Cloud Logging Explorer URL |
| Appendix | Cleanup/teardown (discretionary, included) |

## Threat Mitigations

| Threat | Mitigation Applied |
|--------|--------------------|
| T-07-01: key.json exposure | `.gitignore` guard active; Step 5 never-commit Warning; secure storage note |
| T-07-02: over-permissioned SA | Explicit least-privilege annotation in Step 5; roles/run.admin + roles/artifactregistry.writer only |

## Self-Check

- [x] `.gitignore` contains `key.json` under `## GCP credentials` section
- [x] `git check-ignore key.json` returns `key.json`
- [x] All 9 pre-existing .gitignore entries retained
- [x] DEPLOYMENT.md is 374 lines (>= 120 minimum)
- [x] All required substrings verified by automated check (17/17 passed)
- [x] Every command uses literal `PROJECT_ID` placeholder (no angle-brackets or brackets)
- [x] Deploy command uses `--port 8080` and does NOT contain `--port 80`
- [x] Step 7 contains all required flags: `--port 8080`, `--memory 512Mi`, `--cpu 1`, `--min-instances 0`, `--allow-unauthenticated`, `ASPNETCORE_ENVIRONMENT=Production`
- [x] Step 8 maps all 4 ROADMAP success criteria with concrete commands
- [x] Both tasks committed atomically (2 commits: chore(07-01), docs(07-01))

## Self-Check: PASSED
