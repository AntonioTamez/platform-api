# Cómo continuar desde aquí

**Última actualización:** 2026-06-04
**Proyecto:** PersonsAPI (v2.0 — Cloud Deployment milestone)
**Rama git:** `master`

---

## Estado actual

La **Fase 8 (CI/CD Pipeline)** está ejecutada y verificada automáticamente.
Hay **1 item pendiente de verificación humana** antes de poder marcar la fase como completa.

### Qué se hizo en esta sesión

1. `/gsd-plan-phase 8` — Planificación completa
   - Investigación: `08-RESEARCH.md` — GitHub Actions patterns, SA key auth, pitfalls
   - Plan: `08-01-PLAN.md` — 2 tareas, 1 wave
2. `/gsd-execute-phase 8` — Ejecución completa
   - Creó `.github/workflows/cicd.yml` — 3 jobs: `build-and-test` → `push-image` → `deploy`
   - Actualizó `DEPLOYMENT.md` — agregó Step 9 (instrucciones de secrets de GitHub)
   - Code review: 2 críticos, 3 warnings, 2 info — **todos resueltos** en commit `b6c4a54`
   - Verificación: 9/9 must-haves verificados automáticamente
3. Estado: **esperando verificación humana** (run en vivo con secrets reales)

### Correcciones aplicadas durante la sesión (code review)

| Fix | Archivo | Descripción |
|-----|---------|-------------|
| CR-01 | `cicd.yml` | Branch trigger cambiado de `[main]` a `[master]` (el repo usa `master`) |
| WR-01 | `cicd.yml` | Añadido `--allow-unauthenticated` al deploy step |
| WR-02 | `cicd.yml` | Añadido `--project ${{ secrets.GCP_PROJECT_ID }}` a los comandos gcloud |
| WR-03 | `DEPLOYMENT.md` | Nota explicando qué flags son "initial-deploy-only" vs CI |
| IN-02 | `DEPLOYMENT.md` | `tr -d '\n'` → `tr -d '\r\n'` en comando de minificación de key.json |

---

## Lo que falta hacer

### Paso 1 — Setup de secrets en GitHub (acción manual, ~5 minutos)

Sigue las instrucciones en **`DEPLOYMENT.md` → Step 9** para crear los dos secrets:

| Secret | Valor | Dónde obtenerlo |
|--------|-------|-----------------|
| `GCP_SA_KEY` | Contenido de `key.json` minificado en una línea | Fase 7 — `key.json` descargado de GCP Console. Minificar: `cat key.json \| tr -d '\r\n'` |
| `GCP_PROJECT_ID` | ID del proyecto GCP (ej. `personsapi-XXXXXX`) | GCP Console → selector de proyecto en la barra superior |

**Ruta en GitHub:** repo → Settings → Secrets and variables → Actions → New repository secret

### Paso 2 — Validar el pipeline (acción manual)

1. En GitHub, ir a la pestaña **Actions**
2. Seleccionar **CI/CD Pipeline** en el panel izquierdo
3. Click **Run workflow** → **Run workflow** (rama `master`)
4. Esperar ~3-5 minutos
5. Confirmar que los 3 jobs aparecen en verde:
   - `build-and-test` — 64 tests pasados
   - `push-image` — imagen pusheada a Artifact Registry
   - `deploy` — URL impresa en el log (ej. `https://persons-api-abc123-uc.a.run.app`)

### Paso 3 — Marcar la fase como completa

Una vez que el pipeline corra en verde, continuar en Claude Code:

```
/gsd-execute-phase 8
```

Cuando pregunte el checkpoint, responder `"approved"`.

Esto marcará la Fase 8 como **Complete** en STATE.md y ROADMAP.md, actualizará PROJECT.md y presentará el resumen final del milestone v2.0.

---

## Contexto técnico del workflow

```yaml
# .github/workflows/cicd.yml — estructura resumida
on:
  push:
    branches: [master]
  workflow_dispatch:

jobs:
  build-and-test:   # dotnet build + dotnet test (64 tests, --configuration Release)
  push-image:       # docker build + push :latest a Artifact Registry (needs: build-and-test)
  deploy:           # gcloud run deploy persons-api + imprime URL (needs: push-image)
```

**Autenticación GCP:** `google-github-actions/auth@v2` con `credentials_json: GCP_SA_KEY`
**Imagen:** `us-central1-docker.pkg.dev/<PROJECT_ID>/personsapi/personsapi:latest`
**Servicio Cloud Run:** `persons-api`, región `us-central1`

---

## Archivos clave de esta fase

| Archivo | Descripción |
|---------|-------------|
| `.github/workflows/cicd.yml` | El workflow de GitHub Actions (artefacto principal) |
| `DEPLOYMENT.md` | Manual completo incluyendo Step 9 (secrets setup) |
| `.planning/phases/08-ci-cd-pipeline/08-01-PLAN.md` | Plan ejecutado |
| `.planning/phases/08-ci-cd-pipeline/08-VERIFICATION.md` | Reporte de verificación (status: human_needed) |
| `.planning/phases/08-ci-cd-pipeline/08-HUMAN-UAT.md` | Item UAT pendiente del run en vivo |
| `.planning/phases/08-ci-cd-pipeline/08-REVIEW.md` | Reporte de code review (todos los issues resueltos) |

---

## Comando de retoma rápida

```
/clear
/gsd-execute-phase 8
```

Responder `"approved"` al checkpoint de verificación humana (después de haber validado el pipeline en GitHub Actions).

---

*Eliminar este archivo una vez que la Fase 8 esté marcada como Complete.*
