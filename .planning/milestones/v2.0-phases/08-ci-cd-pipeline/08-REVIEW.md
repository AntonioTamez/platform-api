---
phase: 08-ci-cd-pipeline
reviewed: 2026-06-04T00:00:00Z
depth: standard
files_reviewed: 2
files_reviewed_list:
  - .github/workflows/cicd.yml
  - DEPLOYMENT.md
findings:
  critical: 2
  warning: 3
  info: 2
  total: 7
status: issues_found
---

# Phase 8: Code Review Report

**Reviewed:** 2026-06-04
**Depth:** standard
**Files Reviewed:** 2
**Status:** issues_found

---

## Summary

The CI/CD workflow (`cicd.yml`) is structurally sound: job sequencing is correct (`build-and-test` → `push-image` → `deploy`), action versions are pinned to stable majors (`@v4`, `@v2`), `--configuration Release` appears on both `dotnet build` and `dotnet test --no-build`, `id-token: write` is correctly absent, and the `gcloud run deploy` flag set is intentionally minimal (preserves existing service config). The `google-github-actions/auth@v2` step uses `credentials_json:` correctly — no secret echoing anywhere.

Two critical defects exist:

1. The workflow trigger fires on `branches: [main]` but the repository's default branch is `master`. The pipeline will never trigger on a normal push.
2. The `deploy` job uses `:latest` as both push tag and deploy image — there is no digest pinning, meaning the Cloud Run revision can receive a stale cached image from a prior run rather than the image just built in the same workflow run.

Three warnings concern missing `--allow-unauthenticated` on the CI deploy step (leaving the service potentially inaccessible after the first CI-driven redeploy), lack of explicit `--project` flag in both GCP jobs (auth picks up the project from the SA key, but it is not the only or most robust source), and a contradiction between the DEPLOYMENT.md runbook Step 7 (which uses the full flag set including `--port 8080`) and the CI workflow (which intentionally omits those flags). The runbook documents the full-flag initial deploy correctly, but Step 9 claims CI "deploys the updated image" — it does, but omitting `--allow-unauthenticated` from CI can silently re-lock the service to authenticated-only access if Cloud Run's default changes or if the service is torn down and recreated via CI.

---

## Critical Issues

### CR-01: Workflow trigger branch mismatch — pipeline never fires on push

**File:** `.github/workflows/cicd.yml:5`
**Issue:** The trigger is `branches: [main]`. The repository's actual default branch is `master` (confirmed via `git branch -a` — `remotes/origin/HEAD -> origin/master`). Every push to `master` silently skips the pipeline. The `workflow_dispatch` manual trigger still works, so the bug is not immediately visible. A developer pushing to `master` will observe no CI run and no failure — the pipeline simply does not execute. This means the test gate, image push, and deployment are all bypassed on every normal commit.

**Fix:**
```yaml
on:
  push:
    branches: [master]
  workflow_dispatch:
```

If the team intends to rename the branch to `main` at some point, add both during the transition period:
```yaml
on:
  push:
    branches: [master, main]
  workflow_dispatch:
```

---

### CR-02: Image deployed by digest-less `:latest` tag — stale image risk

**File:** `.github/workflows/cicd.yml:35` and `.github/workflows/cicd.yml:60`
**Issue:** Both `push-image` and `deploy` jobs resolve the image via the `:latest` tag:
```
IMAGE_URL: us-central1-docker.pkg.dev/.../personsapi:latest
```
The `push-image` job pushes a new image to `:latest`. The `deploy` job then calls `gcloud run deploy --image ${{ env.IMAGE_URL }}`, which passes the `:latest` tag to Cloud Run. Cloud Run resolves the tag to a digest at deploy time — this is generally fine when the jobs run sequentially in the same workflow. However, there is a silent failure mode: if the Docker push in `push-image` succeeds but the tag resolution at deploy time hits a cached or stale record in Artifact Registry, Cloud Run may pin the prior image digest to the new revision. The safe pattern is to capture the image digest from the push step and pass it explicitly to `gcloud run deploy`.

**Fix — capture and pass the digest:**
```yaml
# In push-image job:
- name: Push Docker image
  id: push
  run: |
    docker push ${{ env.IMAGE_URL }}
    DIGEST=$(docker inspect --format='{{index .RepoDigests 0}}' ${{ env.IMAGE_URL }})
    echo "digest=$DIGEST" >> $GITHUB_OUTPUT

# In deploy job — needs push-image output, so add 'needs' and reference the output:
# (jobs.push-image.outputs.digest must also be declared as a job output)
- name: Deploy to Cloud Run
  run: |
    gcloud run deploy persons-api \
      --image ${{ needs.push-image.outputs.digest }} \
      --region us-central1 \
      --platform managed \
      --quiet
```

Alternatively, as a minimal fix, pass the tagged URL directly (current behavior) but document the known limitation. The digest approach is the correct production posture.

---

## Warnings

### WR-01: `--allow-unauthenticated` absent from CI deploy step — service may become authenticated-only after CI redeploy

**File:** `.github/workflows/cicd.yml:69-74`
**Issue:** The manual first-deploy command in `DEPLOYMENT.md` Step 7 includes `--allow-unauthenticated`. The CI workflow's `deploy` job omits it. When `gcloud run deploy` updates an existing service **without** `--allow-unauthenticated`, Cloud Run preserves the existing IAM policy only if the service already exists with that policy set. However, if the service is ever deleted and recreated via CI (e.g., during a teardown-and-redeploy cycle), the first CI-driven deploy will create a new service with Cloud Run's default IAM policy, which is **authenticated-only**. The health check endpoint and all API endpoints immediately return 403 to unauthenticated callers, including the verification step's `curl` commands in DEPLOYMENT.md Step 8.

Additionally, the DEPLOYMENT.md Step 9 success criteria table states "Updated image deployed to Cloud Run" as verified by "`deploy` job green" — this passes even if the service becomes 403-only, creating a false-green scenario.

**Fix:** Add `--allow-unauthenticated` to the CI deploy step (matching the Phase 7 pattern) so CI can recreate the service safely:
```yaml
- name: Deploy to Cloud Run
  run: |
    gcloud run deploy persons-api \
      --image ${{ env.IMAGE_URL }} \
      --region us-central1 \
      --platform managed \
      --allow-unauthenticated \
      --quiet
```
This is idempotent — passing it on an already-public service is a no-op.

---

### WR-02: `--project` flag absent from all `gcloud` commands — implicit project resolution can fail silently

**File:** `.github/workflows/cicd.yml:46`, `.github/workflows/cicd.yml:70-74`, `.github/workflows/cicd.yml:78-80`
**Issue:** All `gcloud` commands in the workflow rely on the project being set implicitly by `google-github-actions/setup-gcloud@v2` after the `auth` step. The `auth@v2` action sets `CLOUDSDK_CORE_PROJECT` from the SA key's `project_id` field. This works in the happy path but is an invisible dependency. If the SA key's `project_id` field does not match `GCP_PROJECT_ID` (e.g., the key was created in a different project than the one being deployed to, which is a plausible misconfiguration), `gcloud run deploy` and `gcloud run services describe` silently target the wrong project, producing confusing "service not found" errors that do not mention project mismatch.

**Fix:** Pass `--project ${{ secrets.GCP_PROJECT_ID }}` explicitly to every `gcloud` command that targets a specific project:
```yaml
- name: Configure Docker for Artifact Registry
  run: gcloud auth configure-docker us-central1-docker.pkg.dev --project ${{ secrets.GCP_PROJECT_ID }} --quiet

- name: Deploy to Cloud Run
  run: |
    gcloud run deploy persons-api \
      --image ${{ env.IMAGE_URL }} \
      --project ${{ secrets.GCP_PROJECT_ID }} \
      --region us-central1 \
      --platform managed \
      --quiet

- name: Print service URL
  run: |
    gcloud run services describe persons-api \
      --project ${{ secrets.GCP_PROJECT_ID }} \
      --region us-central1 \
      --format='value(status.url)'
```

---

### WR-03: DEPLOYMENT.md Step 7 command includes `--port 8080` but CI workflow omits it — documentation inconsistency creates operational risk

**File:** `DEPLOYMENT.md:224-232`
**Issue:** DEPLOYMENT.md Step 7 documents `--port 8080` as mandatory with a prominent warning: *"Use `--port 8080` exactly as shown. Do not use `--port 80`. The container listens on port 8080 ... Using `--port 80` causes Cloud Run to inject `PORT=80` while the container still listens on 8080, resulting in a port mismatch, failed health checks, and a container crash loop."*

The CI workflow intentionally omits `--port 8080` (per PATTERNS.md, preserving existing service config is correct for image-only updates). This is the right technical decision for CI — but the documentation does not acknowledge this split. A developer reading DEPLOYMENT.md Step 7 and then looking at `cicd.yml` will see a contradiction and may either: (a) "fix" the CI workflow by adding `--port 8080`, or (b) conclude the CI workflow is broken and add a full flag set that overwrites tuned settings.

The DEPLOYMENT.md runbook's Step 7 configuration table (lines 246-254) should note which flags are initial-deploy-only vs. carried by CI. Without this annotation the warning in Step 7 actively undermines confidence in the CI workflow.

**Fix — add a note to DEPLOYMENT.md Step 7** (documentation fix, not a code change):
```
> **Note (CI/CD):** The Phase 8 CI/CD pipeline (`cicd.yml`) intentionally omits `--port`,
> `--memory`, `--cpu`, `--min-instances`, `--allow-unauthenticated`, and `--set-env-vars`
> from `gcloud run deploy`. Those flags configure the **initial** service. On subsequent
> image-only deploys Cloud Run preserves existing configuration unless a flag explicitly
> overrides it. Do not add the full flag set to `cicd.yml` — it would overwrite any
> settings tuned after Phase 7 deployment.
```

---

## Info

### IN-01: `:latest` image tag in Artifact Registry grows unbounded — no tag retention policy

**File:** `.github/workflows/cicd.yml:35`
**Issue:** Every CI run overwrites the `:latest` tag in Artifact Registry, but Artifact Registry retains all previous image digests (only the tag pointer moves). Over many CI runs, untagged image digests accumulate in the repository and are never cleaned up. This is not a correctness issue but leads to storage cost growth and makes `gcloud artifacts docker images list` output unreadable over time.

**Fix:** Add a cleanup step after push, or configure an Artifact Registry cleanup policy in the GCP Console (Artifact Registry → repository → Edit → Cleanup policies → Keep most recent N). Alternatively, add a versioned tag alongside `:latest`:
```yaml
- name: Push Docker image
  run: |
    docker push ${{ env.IMAGE_URL }}
    docker tag ${{ env.IMAGE_URL }} \
      us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:${{ github.sha }}
    docker push \
      us-central1-docker.pkg.dev/${{ secrets.GCP_PROJECT_ID }}/personsapi/personsapi:${{ github.sha }}
```

---

### IN-02: DEPLOYMENT.md minification instruction uses `tr -d '\n'` — does not strip carriage returns on Windows

**File:** `DEPLOYMENT.md:357-359`
**Issue:** The minification command is:
```bash
cat key.json | tr -d '\n'
```
On Windows, `key.json` may contain `\r\n` line endings (CRLF), depending on the editor or git config (`core.autocrlf`). `tr -d '\n'` strips `\n` only, leaving `\r` characters embedded in the single-line output. The `google-github-actions/auth@v2` JSON parser may fail with a parse error on a key containing embedded `\r` bytes. The `.gitignore` correctly excludes `key.json`, so this is about the local Windows developer running the minification command.

**Fix:** Update the minification command to strip both `\r` and `\n`, or recommend `jq -c .` (which re-serializes to canonical single-line JSON):
```bash
# Portable — strips both \r and \n
cat key.json | tr -d '\r\n'

# Better alternative — re-serializes to canonical compact JSON (requires jq)
cat key.json | jq -c .
```
Also note that the `gh secret set GCP_SA_KEY < key.json` Option B command (line 384) does not minify — the `gh` CLI stores the file contents as-is, including newlines. The note on line 389 says "`gh secret set` reads the file and stores it correctly without manual minification," but this is only true if `google-github-actions/auth@v2` accepts newlines in the secret, which it does according to the action's current README. The note should be more precise: the `gh` CLI stores newlines but `auth@v2` handles them. If that behavior changes this note will mislead.

---

_Reviewed: 2026-06-04_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
