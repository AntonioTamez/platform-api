# PersonsAPI — Cloud Run Deployment Runbook

> **Manual deployment guide for deploying PersonsAPI to Google Cloud Run.**
> Follow every step in order; do a find-replace of `PROJECT_ID` with your actual GCP project ID before executing commands.

This runbook takes a fresh GCP account from zero to a publicly reachable Cloud Run HTTPS URL serving the PersonsAPI. The validated Phase 6 Docker image is the deploy source — no application code changes are required.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Step 1: GCP Project Setup](#step-1-gcp-project-setup)
- [Step 2: Enable Required APIs](#step-2-enable-required-apis)
- [Step 3: Create Artifact Registry Repository](#step-3-create-artifact-registry-repository)
- [Step 4: Configure Docker Authentication](#step-4-configure-docker-authentication)
- [Step 5: Create Service Account](#step-5-create-service-account)
- [Step 6: Build, Tag, and Push Docker Image](#step-6-build-tag-and-push-docker-image)
- [Step 7: Deploy to Cloud Run](#step-7-deploy-to-cloud-run)
- [Step 8: Verify Deployment](#step-8-verify-deployment)
- [Step 9: GitHub Actions CI/CD Secrets Setup](#step-9-github-actions-cicd-secrets-setup)
- [Appendix: Cleanup / Teardown](#appendix-cleanup--teardown)

---

## Prerequisites

### 1. Install the gcloud CLI (Google Cloud SDK)

> **Note:** The gcloud CLI is not installed by default on Windows. Install it before running any command in this runbook.

Download and run the Windows installer:

```
https://dl.google.com/dl/cloudsdk/channels/rapid/GoogleCloudSDKInstaller.exe
```

After installation, open a new PowerShell window and initialize:

```bash
gcloud init
```

`gcloud init` walks through authentication and sets a default project. When prompted, sign in with your Google account.

### 2. Authenticate

If not authenticated after `gcloud init`, run:

```bash
gcloud auth login
```

### 3. Verify Docker Desktop

Docker Desktop must be running for Steps 6 (build and push). Verify:

```bash
docker version
```

Expected output: Client and Server version lines. If Docker is not installed, download it from [https://www.docker.com/products/docker-desktop/](https://www.docker.com/products/docker-desktop/).

---

## Step 1: GCP Project Setup

Create a new GCP project and set it as the active project:

```bash
gcloud projects create PROJECT_ID --name="PersonsAPI"
gcloud config set project PROJECT_ID
```

> **Warning:** A billing account (credit card) must be linked to the project **before** enabling APIs in Step 2. `gcloud services enable` will fail with a billing error if billing is not active. Create or link a billing account in the GCP Console (Billing section) first.
>
> The $300 free credit covers this project entirely — no charges are expected.

List your billing accounts and link one to the project:

```bash
gcloud billing accounts list
gcloud billing projects link PROJECT_ID --billing-account=BILLING_ACCOUNT_ID
```

Replace `BILLING_ACCOUNT_ID` with the ID returned by `gcloud billing accounts list` (format: `XXXXXX-XXXXXX-XXXXXX`).

> **Note:** If you need to create a new billing account (first time using GCP), go to [https://console.cloud.google.com](https://console.cloud.google.com) → Billing → Create billing account. Enter your credit card details. The $300 free credit activates automatically.

---

## Step 2: Enable Required APIs

Enable both the Cloud Run and Artifact Registry APIs:

```bash
gcloud services enable run.googleapis.com artifactregistry.googleapis.com
```

> **Note:** Both APIs are required. Cloud Run hosts the container; Artifact Registry stores the Docker image. Enabling only `run.googleapis.com` will cause a push failure in Step 6.

Verify they are enabled:

```bash
gcloud services list --enabled --filter="name:(run.googleapis.com OR artifactregistry.googleapis.com)"
```

Expected: two entries, both with `STATE: ENABLED`.

---

## Step 3: Create Artifact Registry Repository

Create a Docker repository in `us-central1` to store the PersonsAPI image:

```bash
gcloud artifacts repositories create personsapi \
  --repository-format=docker \
  --location=us-central1 \
  --description="PersonsAPI Docker images"
```

Verify the repository was created:

```bash
gcloud artifacts repositories list --location=us-central1
```

Expected: one row showing `personsapi` with format `DOCKER` and location `us-central1`.

---

## Step 4: Configure Docker Authentication

Authorize Docker to push images to Artifact Registry in `us-central1`:

```bash
gcloud auth configure-docker us-central1-docker.pkg.dev
```

This command updates `~/.docker/config.json` to use the gcloud credential helper for the `us-central1-docker.pkg.dev` registry. You only need to run this once per machine.

> **Note:** If you get an "access denied" error during `docker push` in Step 6, re-run this command — the gcloud auth token may have expired.

---

## Step 5: Create Service Account

Create a dedicated service account for deploying PersonsAPI. This account is also used in Phase 8 (GitHub Actions CI/CD), so save the JSON key after creating it.

### Create the service account

```bash
gcloud iam service-accounts create persons-api-deployer \
  --display-name="PersonsAPI Deployer" \
  --project=PROJECT_ID
```

### Grant least-privilege IAM roles

Grant only the two roles needed — nothing more:

```bash
gcloud projects add-iam-policy-binding PROJECT_ID \
  --member="serviceAccount:persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/artifactregistry.writer"

gcloud projects add-iam-policy-binding PROJECT_ID \
  --member="serviceAccount:persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com" \
  --role="roles/run.admin"
```

- `roles/artifactregistry.writer` — push Docker images to Artifact Registry
- `roles/run.admin` — deploy and manage Cloud Run services

> **Note:** Do not grant `Owner` or `Editor` — principle of least privilege. These two roles are sufficient for Phase 7 deployment and Phase 8 GitHub Actions.

### Download the JSON key

```bash
gcloud iam service-accounts keys create key.json \
  --iam-account=persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com
```

> **Warning:** **Never commit `key.json` to git.** The `.gitignore` at the solution root already excludes it. Verify with `git status` — `key.json` must not appear as untracked or staged.
>
> **Keep this file safe** — it is reused in Phase 8 (GitHub Actions CI/CD) as a GitHub Actions secret. Store it in a password manager or secure location.
>
> This API is fully public (`--allow-unauthenticated` in Step 7). Any person with the Cloud Run URL can call all endpoints including POST/PUT/DELETE. This is intentional for this learning project with seeded non-PII data.

---

## Step 6: Build, Tag, and Push Docker Image

From the **solution root** (the directory containing the `Dockerfile`):

```bash
docker build -t us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest .
```

Then push to Artifact Registry:

```bash
docker push us-central1-docker.pkg.dev/PROJECT_ID/personsapi/personsapi:latest
```

> **Note:** The `.` at the end of `docker build` refers to the solution root where the `Dockerfile` lives. Run both commands from `C:\ATS\Git\platform\` (or your equivalent checkout directory).

Verify the image was pushed:

```bash
gcloud artifacts docker images list us-central1-docker.pkg.dev/PROJECT_ID/personsapi
```

Expected: one row with the image digest and `:latest` tag.

---

## Step 7: Deploy to Cloud Run

Deploy the image to Cloud Run in `us-central1`:

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

> **Warning:** Use `--port 8080` exactly as shown. **Do not use `--port 80`.** The container listens on port 8080 (`ASPNETCORE_HTTP_PORTS=8080` in the Dockerfile). Using `--port 80` causes Cloud Run to inject `PORT=80` while the container still listens on 8080, resulting in a port mismatch, failed health checks, and a container crash loop.

> **Note (CI/CD):** The GitHub Actions workflow in `.github/workflows/cicd.yml` intentionally omits `--port`, `--memory`, `--cpu`, `--min-instances`, `--allow-unauthenticated`, and `--set-env-vars` from `gcloud run deploy`. These flags are **initial-deploy-only** — once Cloud Run stores the service configuration, incremental deploys need only `--image`, `--region`, and `--platform` to update the running revision. Omitting them in CI preserves the configuration set in this step rather than resetting it on every push.

After a successful deploy, `gcloud run deploy` prints the service URL:

```
Service URL: https://persons-api-<hash>-uc.a.run.app
```

Save this URL — it is used in Step 8 to verify all success criteria.

**Configuration notes:**

| Flag | Value | Reason |
|------|-------|--------|
| `--port 8080` | 8080 | Matches `ASPNETCORE_HTTP_PORTS=8080` in Dockerfile |
| `--memory 512Mi` | 512 MiB | Sufficient headroom for .NET 10 + EF InMemory (~180-250 MiB baseline) |
| `--cpu 1` | 1 vCPU | Standard for a learning/demo API |
| `--min-instances 0` | 0 | Scale to zero when idle — cost is $0 at rest |
| `--allow-unauthenticated` | enabled | Required for verification with plain `curl`; intentional for this demo |
| `ASPNETCORE_ENVIRONMENT=Production` | Production | Correct for Cloud Run; disables the Scalar UI (`/scalar`) in production |

> **Note on cold start:** With `--min-instances 0`, Cloud Run scales to zero after inactivity. The first request after an idle period wakes the container. .NET 10 startup + EF InMemory seed takes approximately 2–4 seconds. Use `curl --max-time 30` in Step 8 to account for this.
>
> **Note on Scalar UI:** Setting `ASPNETCORE_ENVIRONMENT=Production` disables the `/scalar` interactive API explorer. This is acceptable for a deployed service. If you want Scalar accessible on the live URL, change to `Development` (with the understanding it is then publicly accessible).

---

## Step 8: Verify Deployment

Extract the service URL:

```bash
SERVICE_URL=$(gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.url)')
echo $SERVICE_URL
```

On Windows PowerShell:

```powershell
$SERVICE_URL = gcloud run services describe persons-api `
  --region us-central1 `
  --format='value(status.url)'
Write-Host $SERVICE_URL
```

Run the following verification commands to confirm all 4 ROADMAP success criteria:

| SC | Criterion | Command | Expected Output |
|----|-----------|---------|-----------------|
| SC-1 | Health endpoint returns 200 from public internet | `curl --max-time 30 "$SERVICE_URL/health"` | HTTP 200, body: `{"status":"Healthy"}` |
| SC-2 | Returns 3 seeded persons | `curl "$SERVICE_URL/api/persons"` | HTTP 200, JSON array with 3 persons |
| SC-3 | No crash loop | `gcloud run services describe persons-api --region us-central1 --format='value(status.conditions)'` | `Ready` condition is `True`; no repeated container failures |
| SC-4 | JSON logs in Cloud Logging | Open URL in browser (see below) | JSON-structured log entries visible |

### SC-1: Health endpoint

```bash
curl --max-time 30 "$SERVICE_URL/health"
```

Expected: `{"status":"Healthy"}` with HTTP 200. The `--max-time 30` flag handles the cold-start delay from scale-to-zero.

> **Tip:** If you get a 503 on the first request, wait 5 seconds and retry — the container may still be starting. A persistent 503 indicates a port mismatch; confirm `--port 8080` was used in Step 7.

### SC-2: Seeded persons

```bash
curl "$SERVICE_URL/api/persons"
```

Expected: HTTP 200 with a JSON array containing 3 persons (Carlos Herrera López, Ana García Martínez, Luis Morales Reyes — the seeded in-memory data from startup).

### SC-3: Revision health

```bash
gcloud run services describe persons-api \
  --region us-central1 \
  --format='value(status.conditions)'
```

Expected: output includes `Ready` with `status: True`. No `ContainerFailed` or crash loop messages.

### SC-4: Cloud Logging

Open this URL in a browser (replace PROJECT_ID with your actual project ID):

```
https://console.cloud.google.com/logs/query?project=PROJECT_ID
```

In the Query builder, enter:

```
resource.type="cloud_run_revision" AND resource.labels.service_name="persons-api"
```

Expected: JSON-structured log entries appear. Each entry is a Serilog CLEF JSON object.

> **Note:** Log entries appear with `DEFAULT` severity (no colored severity badges). This is expected — Serilog CLEF emits a `@l` field but Cloud Logging expects a `severity` field for automatic mapping. Severity icons are a deferred enhancement (OBS-03, v3). The log entries themselves are valid JSON and fully readable.

---

## Step 9: GitHub Actions CI/CD Secrets Setup

The `.github/workflows/cicd.yml` workflow requires two GitHub repository secrets before it can run. Create them once — the pipeline uses them automatically on every push to `main` and on every `workflow_dispatch` manual trigger.

### Required Secrets

| Secret Name | Value | Where to Find It |
|-------------|-------|-----------------|
| `GCP_SA_KEY` | Full contents of `key.json` (minified — see below) | The Service Account JSON key downloaded in Step 5 |
| `GCP_PROJECT_ID` | GCP project ID string (e.g. `personsapi-XXXXXX`) | The value you substituted for `PROJECT_ID` throughout this runbook |

### Why `GCP_SA_KEY` Must Be Minified

> **Warning:** Copy the raw `key.json` file contents **as a single line** — whitespace and newlines in the stored secret corrupt the JSON and cause `google-github-actions/auth@v2` to fail immediately with a JSON parse error (not an authentication error). This is a common setup mistake.

Minify before pasting:

```bash
cat key.json | tr -d '\r\n'
```

Copy the single-line output. Use that as the secret value.

> **Note (Windows):** `tr -d '\r\n'` strips both carriage returns and newlines, which is required when minifying on Windows (where line endings are `\r\n`). Alternatively: `jq -c . key.json`.

### Option A: GitHub UI (Recommended for First-Time Setup)

1. Open your GitHub repository in a browser.
2. Navigate to **Settings** → **Secrets and variables** → **Actions**.
3. Click **New repository secret**.
4. Create the first secret:
   - **Name:** `GCP_SA_KEY`
   - **Secret:** paste the minified single-line JSON from `key.json`
5. Click **Add secret**.
6. Click **New repository secret** again.
7. Create the second secret:
   - **Name:** `GCP_PROJECT_ID`
   - **Secret:** your GCP project ID string (e.g. `personsapi-XXXXXX`)
8. Click **Add secret**.

### Option B: GitHub CLI (Alternative)

Requires the `gh` CLI authenticated to the repository (`gh auth login`):

```bash
# GCP_SA_KEY — gh secret set reading from file handles single-line storage automatically
gh secret set GCP_SA_KEY < key.json

# GCP_PROJECT_ID — supply the project ID directly
gh secret set GCP_PROJECT_ID --body "your-project-id"
```

> **Note:** `gh secret set GCP_SA_KEY < key.json` reads the file and stores it correctly without manual minification.

### Verifying the Pipeline Without a Real Push

Once both secrets exist, trigger the workflow manually to verify the full pipeline end-to-end without pushing to `main`:

1. Open your GitHub repository → **Actions** tab.
2. Select **CI/CD Pipeline** from the left sidebar.
3. Click **Run workflow** → **Run workflow** (uses the `workflow_dispatch` trigger).
4. Watch the three jobs run in sequence: `build-and-test` → `push-image` → `deploy`.
5. Open the `deploy` job log. The final **Print service URL** step prints the live Cloud Run HTTPS URL.

A passing run confirms all four v2.0 ROADMAP success criteria:

| Criterion | Verified By |
|-----------|-------------|
| 64 tests pass in CI | `build-and-test` job green |
| Three sequential jobs with test gate | `push-image` and `deploy` blocked until `build-and-test` passes |
| Updated image deployed to Cloud Run | `deploy` job green, URL printed |
| Pipeline triggered by push to main | Confirmed by `on: push: branches: [main]` in workflow |

### Security Notes

> **Note:** GitHub stores repository secrets encrypted (AES-256 at rest) and automatically masks any declared secret value in workflow run logs. A secret that appears in a log line is replaced with `***`.
>
> To maintain this protection:
> - Never construct a non-secret environment variable or output that embeds a secret value.
> - Never use `echo ${{ secrets.GCP_SA_KEY }}` or any similar `echo` command in a workflow step.
> - The `cicd.yml` workflow passes `GCP_SA_KEY` only via `credentials_json:` in the `google-github-actions/auth@v2` step — never in a `run:` command.

---

## Appendix: Cleanup / Teardown

Run these commands if you want to tear down the Cloud Run resources and stop incurring any GCP costs:

### Delete the Cloud Run service

```bash
gcloud run services delete persons-api --region us-central1 --quiet
```

### Delete the Artifact Registry repository (and all images)

```bash
gcloud artifacts repositories delete personsapi \
  --location=us-central1 \
  --quiet
```

### Delete the service account

```bash
gcloud iam service-accounts delete \
  persons-api-deployer@PROJECT_ID.iam.gserviceaccount.com \
  --quiet
```

### Delete the GCP project entirely

```bash
gcloud projects delete PROJECT_ID
```

> **Warning:** Deleting the project removes all resources permanently and cannot be undone. You will lose billing history and the $300 free credit if it was not fully used.

---

*Phase 7: Cloud Run Deployment — PersonsAPI v2.0*
