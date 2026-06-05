# Phase 8: CI/CD Pipeline - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-04
**Phase:** 8-CI/CD Pipeline
**Areas discussed:** Triggers del workflow, Tagging de imágenes Docker, Scope de tests en CI

---

## Triggers del workflow

| Option | Description | Selected |
|--------|-------------|----------|
| Solo main | Solo push a `main` dispara el workflow. Sin PR checks. Simple, directo, cubre CICD-01. | ✓ |
| main + PR checks | PRs disparan build-and-test (sin push/deploy). Main dispara pipeline completo. | |

**User's choice:** Solo main
**Notes:** Proyecto de aprendizaje individual; PR checks no son necesarios. Se incluye `workflow_dispatch` para poder disparar manualmente desde GitHub UI (confirmado en pregunta de seguimiento).

---

## Workflow Dispatch

| Option | Description | Selected |
|--------|-------------|----------|
| Sí, incluir dispatch manual | Útil para testear el workflow la primera vez. Cero costo — 1 línea. | ✓ |
| No, solo push a main | Workflow minimalista; para probar hay que hacer push real. | |

**User's choice:** Sí, incluir `workflow_dispatch`
**Notes:** Útil especialmente para validar que los secrets de GCP están bien configurados antes del primer push real.

---

## Tagging de imágenes Docker

| Option | Description | Selected |
|--------|-------------|----------|
| Solo :latest | Cada push sobreescribe :latest. Simple y consistente con Phase 7. | ✓ |
| :latest + SHA tag | Cada imagen trazable al commit. Permite rollback por SHA. Storage mayor. | |

**User's choice:** Solo `:latest`
**Notes:** Consistente con la estrategia de Phase 7. Para un proyecto de aprendizaje la trazabilidad por SHA no es necesaria.

---

## Impresión de URL en deploy job

| Option | Description | Selected |
|--------|-------------|----------|
| Sí, mostrar URL en el log | `gcloud run services describe --format='value(status.url)'` al final del deploy job. | ✓ |
| No, solo deploy | URL se obtiene manualmente desde consola GCP. | |

**User's choice:** Sí, imprimir URL
**Notes:** Conveniente para verificar el deploy sin navegar a GCP Console.

---

## Scope de tests en CI

| Option | Description | Selected |
|--------|-------------|----------|
| Todos los tests (64) | Domain (32) + Application (15) + Infrastructure (5) + Integration (12). EF InMemory, sin deps externas. | ✓ |
| Solo unit tests (47) | Solo Domain + Application. Excluye Infrastructure e Integration. | |

**User's choice:** Todos los tests
**Notes:** EF Core InMemory garantiza que los tests de integration no requieren servicios externos. `dotnet test --no-build --configuration Release` corre todos sin configuración especial.

---

## GitHub Secrets naming

| Option | Description | Selected |
|--------|-------------|----------|
| GCP_SA_KEY + GCP_PROJECT_ID | Nombres convencionales. Compatibles con google-github-actions/auth. | ✓ |
| Otros nombres | Nombres personalizados para los secrets. | |

**User's choice:** `GCP_SA_KEY` y `GCP_PROJECT_ID`
**Notes:** Nombres convencionales en proyectos que usan google-github-actions. Compatible con `google-github-actions/auth@v2`.

---

## Claude's Discretion

- Versiones exactas de GitHub Actions (`actions/checkout@v4`, `google-github-actions/auth@v2`, etc.)
- Caching de paquetes NuGet entre runs
- Pinning de versión de .NET SDK (`dotnet-version: '10.x'`)
- Runner: `ubuntu-latest`
- Nombre del archivo de workflow (`cicd.yml` o `deploy.yml`)

## Deferred Ideas

- Ninguna — la discusión se mantuvo dentro del scope de la fase.
