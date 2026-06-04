# Roadmap: PersonsAPI

## Milestones

- ✅ **v1.0 PersonsAPI MVP** — Phases 1-4 (shipped 2026-06-02)
- [ ] **v2.0 Cloud Deployment** — Phases 5-8

## Phases

<details>
<summary>✅ v1.0 PersonsAPI MVP (Phases 1-4) — SHIPPED 2026-06-02</summary>

- [x] Phase 1: Domain Layer (2/2 plans) — completed 2026-05-29
- [x] Phase 2: Application Layer (3/3 plans) — completed 2026-05-29
- [x] Phase 3: Infrastructure Layer (3/3 plans) — completed 2026-05-31
- [x] Phase 4: API Layer (3/3 plans) — completed 2026-06-02

Full phase details archived in `.planning/milestones/v1.0-ROADMAP.md`

</details>

### v2.0 Cloud Deployment

- [x] **Phase 5: Observability** - Add Serilog JSON logging and `/health` endpoint locally (completed 2026-06-03)
- [x] **Phase 6: Containerization** - Build multi-stage Dockerfile and docker-compose for local parity (completed 2026-06-04)
- [ ] **Phase 7: Cloud Run Deployment** - Deploy container to Google Cloud Run manually
- [ ] **Phase 8: CI/CD Pipeline** - Automate build → test → push → deploy via GitHub Actions

## Phase Details

### Phase 5: Observability

**Goal**: The running API emits structured JSON logs and responds to health checks
**Depends on**: Nothing (builds on v1.0 which is complete)
**Requirements**: OBS-01, OBS-02
**Success Criteria** (what must be TRUE):

  1. `dotnet run` produces JSON-formatted log lines on stdout (not plain text)
  2. `GET /health` returns HTTP 200 OK with a plain-text or JSON body
  3. All 64 existing tests still pass after logging changes
  4. Log output is parseable JSON — Cloud Logging can ingest it without transformation

**Plans**: 1 planPlans:

- [x] 05-01-PLAN.md — Add Serilog CLEF JSON logging and /health endpoint to the Api layer (3 tasks) — completed 2026-06-03

**UI hint**: no

### Phase 6: Containerization

**Goal**: Developer can build and run the full API in a container locally
**Depends on**: Phase 5
**Requirements**: DOCK-01, DOCK-02
**Success Criteria** (what must be TRUE):

  1. `docker build -t personsapi .` at the solution root completes without error
  2. `docker compose up` brings the API up and `curl localhost:8080/health` returns 200 OK
  3. `curl localhost:8080/api/persons` returns the 3 seeded persons
  4. Container logs show JSON-formatted Serilog output

**Plans**: 2 plansPlans:
**Wave 1**

- [x] 06-01-PLAN.md — Remove HTTPS redirect; create Dockerfile + .dockerignore; `docker build` succeeds (DOCK-01)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 06-02-PLAN.md — Create docker-compose.yml; verify `docker compose up` serves all endpoints with JSON logs (DOCK-02)

**UI hint**: no

### Phase 7: Cloud Run Deployment

**Goal**: API is publicly reachable at a Google Cloud Run HTTPS URL
**Depends on**: Phase 6
**Requirements**: CLOUD-01
**Success Criteria** (what must be TRUE):

  1. `curl https://<cloud-run-url>/health` returns HTTP 200 OK from the public internet
  2. `curl https://<cloud-run-url>/api/persons` returns the 3 seeded persons
  3. Cloud Run startup probe passes (no container crash loop)
  4. Google Cloud Logging shows JSON log entries from the running service

**Plans**: 2 plans
Plans:
**Wave 1**

- [ ] 07-01-PLAN.md — Add key.json to .gitignore; author DEPLOYMENT.md Cloud Run runbook at solution root (CLOUD-01)

**Wave 2** *(blocked on Wave 1 completion)*

- [ ] 07-02-PLAN.md — Execute runbook against GCP account; verify all 4 success criteria against the live public URL (CLOUD-01)

**UI hint**: no

### Phase 8: CI/CD Pipeline

**Goal**: Every push to `main` automatically builds, tests, and deploys to Cloud Run
**Depends on**: Phase 7
**Requirements**: CICD-01
**Success Criteria** (what must be TRUE):

  1. A push to `main` triggers the GitHub Actions workflow without manual intervention
  2. The workflow run shows three sequential jobs: build-and-test → push-image → deploy
  3. A failed test in the build job blocks the push and deploy jobs
  4. After a successful run, the Cloud Run service serves the updated image within minutes

**Plans**: TBD
**UI hint**: no

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Domain Layer | v1.0 | 2/2 | Complete | 2026-05-29 |
| 2. Application Layer | v1.0 | 3/3 | Complete | 2026-05-29 |
| 3. Infrastructure Layer | v1.0 | 3/3 | Complete | 2026-05-31 |
| 4. API Layer | v1.0 | 3/3 | Complete | 2026-06-02 |
| 5. Observability | v2.0 | 1/1 | Complete | 2026-06-03 |
| 6. Containerization | v2.0 | 2/2 | Complete    | 2026-06-04 |
| 7. Cloud Run Deployment | v2.0 | 0/2 | Not started | - |
| 8. CI/CD Pipeline | v2.0 | 0/? | Not started | - |
