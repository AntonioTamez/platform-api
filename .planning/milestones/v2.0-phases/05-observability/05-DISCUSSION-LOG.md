# Phase 5: Observability - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-02
**Phase:** 5-Observability
**Areas discussed:** Implementación de /health, Formato de logs en Development, Patrón de inicialización de Serilog

---

## Implementación de /health

| Option | Description | Selected |
|--------|-------------|----------|
| AddHealthChecks() + MapHealthChecks | Middleware nativo de ASP.NET Core. No es un endpoint Minimal API — es infraestructura del host. Facilita agregar readiness/liveness separados en v3. Devuelve JSON `{"status":"Healthy"}`. | ✓ |
| HealthController con [Route("health")] | Consistente con 'controllers only'. Más verboso. Mezcla infraestructura con la capa de API. | |
| MapGet simple (/health, () => "Healthy") | Mínimo. Pero es Minimal API — el constraint 'controllers only' lo excluye explícitamente. | |

**User's choice:** AddHealthChecks() + MapHealthChecks

| Option | Description | Selected |
|--------|-------------|----------|
| JSON por defecto de ASP.NET Core | `{"status":"Healthy"}`, content-type: application/json. Cloud Run solo verifica HTTP 200. | ✓ |
| Texto plano "Healthy" | ResponseWriter personalizado. Más código, no estandarizado. | |
| JSON personalizado con campos extra | Incluir versión, uptime, timestamp. Más trabajo, fuera del scope de OBS-02. | |

**User's choice:** JSON por defecto de ASP.NET Core

| Option | Description | Selected |
|--------|-------------|----------|
| Anónimo — sin auth requerida | Cloud Run liveness probe llama a /health sin credenciales. MapHealthChecks lo maneja automáticamente. | ✓ |
| Con auth (igual que el resto de la API) | No compatible con Cloud Run liveness probe — probe fallaría con 401. | |

**User's choice:** Anónimo — sin auth requerida

**Notes:** El "controllers only" constraint se interpreta como aplica a endpoints de dominio/negocio, no a infraestructura. MapHealthChecks es infraestructura del host, no un endpoint Minimal API.

---

## Formato de logs en Development

| Option | Description | Selected |
|--------|-------------|----------|
| JSON siempre, todos los entornos | Consistente. Success criterion requiere `dotnet run` produzca JSON. Sin configuración condicional. | ✓ |
| JSON en Production, consola legible en Development | Mejor DX localmente. Requiere configuración condicional en Program.cs o appsettings por entorno. | |
| JSON + consola en Development (doble sink) | Ambos outputs simultáneamente en Development. Más código, poco valor para aprendizaje. | |

**User's choice:** JSON siempre, todos los entornos

| Option | Description | Selected |
|--------|-------------|----------|
| Information en ambos | Simple y consistente. Information captura requests HTTP sin ruido de Debug. | ✓ |
| Debug en Development, Information en Production | Más verboso en dev. Requiere appsettings.Development.json con override. | |
| Warning en Production, Information en Development | Menos ruido en prod. Puede perder contexto de requests normales. | |

**User's choice:** Information en ambos

| Option | Description | Selected |
|--------|-------------|----------|
| No — filtrar EF Core a Warning | EF Core InMemory emite queries verbosas en Information. Filtrar a Warning es la práctica estándar. | ✓ |
| Sí — mantener Information para EF Core | Útil para debugging, pero ruidoso en producción y sube costos de ingest en Cloud Logging. | |

**User's choice:** Filtrar Microsoft.EntityFrameworkCore a Warning

---

## Patrón de inicialización de Serilog

| Option | Description | Selected |
|--------|-------------|----------|
| UseSerilog() simple en builder.Host | `builder.Host.UseSerilog((ctx, config) => ...)`. Simple, limpio, todo en Program.cs. Suficiente para aprendizaje. | ✓ |
| Bootstrap logger en dos fases | `Log.Logger = new LoggerConfiguration().CreateBootstrapLogger()` antes del builder. Captura errores de startup pre-host. Patrón de producción real, más verboso. | |

**User's choice:** UseSerilog() simple en builder.Host

| Option | Description | Selected |
|--------|-------------|----------|
| Programática en Program.cs | Todo inline en UseSerilog(). Sin paquetes extra. Los 2 paquetes definidos son suficientes. | ✓ |
| Vía appsettings.json | Requiere 3er paquete (Serilog.Settings.Configuration) — contradice la decisión de solo 2 paquetes nuevos. | |

**User's choice:** Programática en Program.cs

| Option | Description | Selected |
|--------|-------------|----------|
| Suprimir logging en tests | NullLogger o Fatal level en ResetableApiFactory. Mantiene output de tests limpio. | ✓ |
| Serilog activo en tests igual que en producción | Consistencia total. Contamina output de tests con JSON. | |
| Que el planner decida | Sin decisión explícita. | |

**User's choice:** Suprimir logging en tests (NullLogger o Fatal level en ResetableApiFactory)

---

## Claude's Discretion

- Estilo exacto del lambda `UseSerilog()` en Program.cs
- Si mantener o eliminar la sección `"Logging"` de appsettings.json (el config programático de Serilog la supersede)
- Opciones exactas de `CompactJsonFormatter` (defaults son suficientes)

## Deferred Ideas

- Separate liveness/readiness endpoints (`/health/live`, `/health/ready`) — deferred to v3 as OBS-03/OBS-04
- Serilog severity mapping to Cloud Logging severity icons — deferred to v3; out of scope per REQUIREMENTS.md
- Bootstrap logger (two-phase Serilog init) — noted as production-grade pattern; too complex for this learning scope
