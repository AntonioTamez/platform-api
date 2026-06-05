---
plan: 07-02
phase: 07-cloud-run-deployment
status: complete
completed: 2026-06-04
requirements: [CLOUD-01]
---

# Plan 07-02 Summary: Cloud Run Live Deployment Verified

## What Was Built

PersonsAPI deployed and running publicly on Google Cloud Run. All 4 ROADMAP Phase 7 success criteria confirmed against the live HTTPS URL.

## Tasks Completed

| # | Task | Status |
|---|------|--------|
| 1 | Execute DEPLOYMENT.md runbook against GCP account | ✓ Complete |
| 2 | Verify all 4 ROADMAP success criteria against live URL | ✓ Complete |

## Success Criteria Verification

| SC | Criterion | Result |
|----|-----------|--------|
| SC-1 | `curl /health` returns HTTP 200 from public internet | ✓ Passed |
| SC-2 | `curl /api/persons` returns 3 seeded persons | ✓ Passed |
| SC-3 | Cloud Run revision Ready=True, no crash loop | ✓ Passed |
| SC-4 | JSON log entries visible in Cloud Logging Explorer | ✓ Passed |

## Infrastructure Created

- GCP project created and billing linked
- Cloud Run and Artifact Registry APIs enabled
- Artifact Registry repository `personsapi` in `us-central1`
- Docker image pushed: `us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest`
- Service account `persons-api-deployer` with least-privilege roles (artifactregistry.writer + run.admin)
- Cloud Run service `persons-api` deployed in `us-central1` with public HTTPS URL
- Service account JSON key (`key.json`) preserved locally for Phase 8 reuse, gitignored

## Configuration Deployed

| Parameter | Value |
|-----------|-------|
| Region | us-central1 |
| Port | 8080 |
| Memory | 512Mi |
| CPU | 1 vCPU |
| Min instances | 0 (scale to zero) |
| Auth | --allow-unauthenticated |
| Environment | ASPNETCORE_ENVIRONMENT=Production |

## Threat Mitigations

| Threat | Result |
|--------|--------|
| T-07-02-01: key.json staged to git | ✓ Mitigated — gitignored, confirmed absent from `git status` |
| T-07-02-02: Public unauthenticated endpoints | Accepted — intentional for learning/demo project |
| T-07-02-03: Cold start 503 on first request | Accepted — handled by `curl --max-time 30` |

## Self-Check

- [x] Cloud Run service `persons-api` deployed and serving traffic
- [x] SC-1: GET /health → HTTP 200 `{"status":"Healthy"}`
- [x] SC-2: GET /api/persons → HTTP 200, 3 seeded persons
- [x] SC-3: Revision Ready=True, no crash loop
- [x] SC-4: JSON logs visible in Cloud Logging Explorer
- [x] key.json not committed to git
- [x] CLOUD-01 requirement satisfied

## Self-Check: PASSED
