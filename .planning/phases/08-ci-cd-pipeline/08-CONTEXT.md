# Phase 8: CI/CD Pipeline - Context

**Gathered:** 2026-06-04
**Status:** Ready for planning

<domain>
## Phase Boundary

Automate the manual Cloud Run deployment process (captured in `DEPLOYMENT.md`) into a GitHub Actions workflow. Every push to `main` triggers a three-job pipeline: build-and-test → push-image → deploy. The workflow authenticates to GCP using the Service Account JSON key created in Phase 7, pushes the Docker image to Artifact Registry, and deploys to the existing Cloud Run service `persons-api`.

No application code changes in this phase. All work is `.github/workflows/` YAML and secrets setup documentation.

</domain>

<decisions>
## Implementation Decisions

### Workflow Triggers
- **D-01:** Trigger on push to `main` only — no PR checks. Covers CICD-01 exactly. Appropriate for a solo learning project without a team review flow.
- **D-02:** Include `workflow_dispatch` to allow manual triggering from GitHub Actions UI. Useful for testing the workflow the first time without making a real push to `main`. Zero cost — 1 extra line.

### Job Structure
- **D-03:** Three sequential jobs as specified by ROADMAP success criterion #2:
  1. `build-and-test` — dotnet build + dotnet test (all 64 tests)
  2. `push-image` — docker build + docker push to Artifact Registry (needs: `build-and-test`)
  3. `deploy` — gcloud run deploy (needs: `push-image`)
- **D-04:** A failed test in `build-and-test` blocks `push-image` and `deploy` via GitHub Actions `needs:` dependency — satisfies ROADMAP success criterion #3.

### Image Tagging
- **D-05:** Tag Docker images with `:latest` only. Each push to `main` overwrites `:latest` in Artifact Registry. No SHA tagging — simple and consistent with Phase 7 approach.
- **D-06:** The `deploy` job prints the public Cloud Run URL at the end of the run (`gcloud run services describe --format='value(status.url)'`). No need to navigate to GCP Console to verify the deployment.

### Test Scope
- **D-07:** Run all 64 tests in CI: Domain (32) + Application (15) + Infrastructure (5) + Integration (12). All test projects use EF Core InMemory — no external dependencies, no special CI setup required. Command: `dotnet test --no-build --configuration Release`.

### Authentication and Secrets
- **D-08:** GCP authentication via Service Account JSON key using `google-github-actions/auth@v2`. The Service Account `persons-api-deployer` was created in Phase 7 with `roles/artifactregistry.writer` + `roles/run.admin`.
- **D-09:** Two GitHub Actions repository secrets:
  - `GCP_SA_KEY` — full contents of `key.json` (Service Account JSON key from Phase 7)
  - `GCP_PROJECT_ID` — GCP project ID string (e.g., `personsapi-XXXXXX`)
- **D-10:** The PLAN must include a task that documents how to create these secrets in GitHub (Settings → Secrets → Actions) so the workflow is runnable immediately after the YAML is merged.

### Claude's Discretion
- Exact GitHub Actions versions for each action (`actions/checkout`, `google-github-actions/auth`, `google-github-actions/setup-gcloud`)
- Whether to cache NuGet packages between runs (standard practice; adds ~30s to first run but saves on subsequent runs)
- `.NET SDK version pinning` in the workflow (`dotnet-version: '10.x'`)
- Exact job runner (`ubuntu-latest` is standard)
- Whether to add a `permissions: id-token: write` block (not needed for SA key auth, only for WIF)
- Workflow file name: `cicd.yml` or `deploy.yml`

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase Scope and Requirements
- `.planning/ROADMAP.md` — Phase 8 goal, 4 success criteria, dependency on Phase 7
- `.planning/REQUIREMENTS.md` — CICD-01 definition (full pipeline requirement)

### Project Constraints
- `.planning/PROJECT.md` — Current state (Phase 7 complete, service live on Cloud Run), Key Decisions table
- `CLAUDE.md` — Technology stack, constraints (.NET 10, C# 14, controllers only)

### Prior Phase Context (infrastructure already built)
- `.planning/phases/07-cloud-run-deployment/07-CONTEXT.md` — D-02 (region us-central1), D-03 (image URL pattern), D-04 (service name `persons-api`), D-09 (SA name and roles), D-11 (JSON key auth approach)
- `DEPLOYMENT.md` — **Critical**: complete gcloud command reference for all deploy steps; the CI/CD workflow automates exactly what this runbook documents manually

### Existing Infrastructure
- `Dockerfile` — the build source; same multi-stage build used in Phase 7 push
- `.github/workflows/` — target directory for the new workflow YAML (currently empty)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Dockerfile` at solution root: Production-ready multi-stage image — no changes needed. The CI workflow runs `docker build -t IMAGE_URL:latest .` from the solution root, identical to the manual step in DEPLOYMENT.md.
- `.dockerignore` — already excludes `tests/`, `bin/`, `obj/`, `.planning/`, `.claude/`. Docker build context in CI is clean.
- `src/PersonsAPI.sln` — solution file used for `dotnet build` and `dotnet test` in the `build-and-test` job.

### Established Patterns
- Image URL format: `us-central1-docker.pkg.dev/$GCP_PROJECT_ID/personsapi/personsapi:latest`
- Cloud Run service name: `persons-api`, region: `us-central1`
- Port: `8080` (already set via `ASPNETCORE_HTTP_PORTS` in Dockerfile)
- Docker authentication to Artifact Registry: `gcloud auth configure-docker us-central1-docker.pkg.dev` (already documented in DEPLOYMENT.md Step 4)

### Integration Points
- `.github/workflows/*.yml` — new file in empty directory; no existing workflow to extend
- GitHub repository secrets (`GCP_SA_KEY`, `GCP_PROJECT_ID`) — must be created manually in GitHub before the first run; the plan should include a task documenting this

</code_context>

<specifics>
## Specific Ideas

- The workflow essentially automates DEPLOYMENT.md Steps 4–8 (Docker auth → build → push → gcloud run deploy). The `build-and-test` job adds the testing gate that the manual runbook skips.
- `workflow_dispatch` trigger enables testing the workflow end-to-end without a real `main` push — especially useful for the first run while verifying secrets are set up correctly.
- Printing the Cloud Run URL at the end of the deploy job (`gcloud run services describe --format='value(status.url)'`) replaces the manual "go to GCP Console" verification step.

</specifics>

<deferred>
## Deferred Ideas

- None — discussion stayed within phase scope.

</deferred>

---

*Phase: 8-CI/CD Pipeline*
*Context gathered: 2026-06-04*
