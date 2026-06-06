# Phase 7: Cloud Run Deployment - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-03
**Phase:** 7-Cloud Run Deployment
**Areas discussed:** GCP config, Servicio Cloud Run, Artefacto de despliegue

---

## GCP config

### GCP Project ID

| Option | Description | Selected |
|--------|-------------|----------|
| Provide project ID | User types existing GCP Project ID | |
| Aún no tengo proyecto GCP | Plan includes project creation as first task | ✓ |

**User's choice:** No existing GCP project — plan starts with project creation.
**Notes:** `PROJECT_ID` placeholder used throughout all commands in DEPLOYMENT.md. Service name: `persons-api`. Artifact Registry repo: `personsapi`.

### Region

| Option | Description | Selected |
|--------|-------------|----------|
| us-central1 | Iowa — most common for demos, lower latency from US/Mexico | ✓ |
| us-east1 | South Carolina — US East Coast alternative | |
| southamerica-east1 | São Paulo — closest to Mexico in LATAM | |

**User's choice:** `us-central1`

### Resource Names

| Option | Description | Selected |
|--------|-------------|----------|
| Defaults (Recomendado) | Project: personsapi-<apellido>, AR repo: personsapi, CR service: persons-api | ✓ |
| Elegir en el plan | Placeholders to fill during execution | |
| Definir ahora | Custom names provided via Other | |

**User's choice:** Defaults — Artifact Registry repo: `personsapi`, Cloud Run service: `persons-api`.

---

## Servicio Cloud Run

### Min Instances

| Option | Description | Selected |
|--------|-------------|----------|
| 0 instancias mín | Scale to zero — $0 cost at rest, ~3-5s cold start | ✓ |
| 1 instancia mín | Always warm — no cold start, ~$7-15/month continuous cost | |

**User's choice:** 0 min instances (scale to zero).

### Access

| Option | Description | Selected |
|--------|-------------|----------|
| Público — allow-unauthenticated | Anyone with URL can call the API — simple curl verification | ✓ |
| Autenticado — requiere IAM token | Restricted to GCP identities — requires Bearer token | |

**User's choice:** Public — `--allow-unauthenticated`.

### Memory / CPU

| Option | Description | Selected |
|--------|-------------|----------|
| 512 MiB RAM + 1 CPU | Comfortable headroom above .NET 10 baseline (~180-250 MiB) | ✓ |
| 256 MiB RAM + 1 CPU | Cloud Run minimum — OOM risk during .NET cold start | |
| 1 GiB RAM + 1 CPU | Over-provisioned for this workload | |

**User's choice:** 512 MiB + 1 CPU.

---

## Artefacto de despliegue

### Deployment Artifact Type

| Option | Description | Selected |
|--------|-------------|----------|
| README de despliegue (DEPLOYMENT.md) | Full runbook at solution root with all gcloud commands step by step | ✓ |
| Script deploy.sh | Executable script automating the steps | |
| Solo comandos en CONTEXT.md | No separate delivery file | |

**User's choice:** `DEPLOYMENT.md` at solution root.

### Service Account Setup

| Option | Description | Selected |
|--------|-------------|----------|
| Sí — incluir setup de Service Account | Complete runbook from zero; SA reused in Phase 8 CI/CD | ✓ |
| No — solo pasos de despliegue | Assumes GCP auth already configured | |

**User's choice:** Yes — include Service Account creation with `roles/artifactregistry.writer` + `roles/run.admin`.

### Verification Tasks

| Option | Description | Selected |
|--------|-------------|----------|
| Tasks de verificación explícitas | Each success criterion has its own verification task | ✓ |
| Verificación implícita | Executor verifies at end without explicit task tracking | |

**User's choice:** Explicit verification tasks for all 4 ROADMAP success criteria.

---

## Claude's Discretion

- Exact gcloud flag ordering and quoting style in DEPLOYMENT.md
- Whether to include `gcloud config set project PROJECT_ID` as convenience step
- Docker tag strategy (`:latest` is fine for Phase 7)
- Whether to add a "Cleanup / Teardown" section at the end of DEPLOYMENT.md
- Startup probe configuration (Cloud Run defaults pointing at `/health`)

## Deferred Ideas

None — discussion stayed within phase scope.
