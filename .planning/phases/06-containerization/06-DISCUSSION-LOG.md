# Phase 6: Containerization - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-02
**Phase:** 6-Containerization
**Areas discussed:** HTTPS y Puerto, Test Stage en Dockerfile, Ambiente de docker-compose

---

## HTTPS y Puerto

### ¿Cómo debe configurarse el puerto en el contenedor?

| Option | Description | Selected |
|--------|-------------|----------|
| ASPNETCORE_HTTP_PORTS=8080 | Variable de entorno moderna (.NET 8+). Kestrel escucha solo en HTTP:8080. Directa y sin ambigüedad. | ✓ |
| ASPNETCORE_URLS=http://+:8080 | Forma tradicional, también funciona. Más verbosa que HTTP_PORTS pero muy conocida. | |

**User's choice:** ASPNETCORE_HTTP_PORTS=8080
**Notes:** Modern .NET 8+ approach preferred over the traditional ASPNETCORE_URLS form.

### ¿Qué hacer con UseHttpsRedirection() para el contenedor?

| Option | Description | Selected |
|--------|-------------|----------|
| Remover UseHttpsRedirection() completamente | El API está diseñado para Cloud Run donde TLS es terminado por el proxy. No hay escenario donde quieras HTTPS en el contenedor mismo. | ✓ |
| Condicionar por environment | Mantén el código pero solo activa en Development local (fuera de contenedor). Más código, más complejidad. | |

**User's choice:** Remover completamente
**Notes:** Unconditional removal — no environment-conditional logic needed.

---

## Test Stage en Dockerfile

### ¿El Dockerfile debe incluir una etapa de dotnet test?

| Option | Description | Selected |
|--------|-------------|----------|
| No — tests quedan para CI/CD | El Dockerfile produce solo el runtime image. Los tests se ejecutan por separado. Fase 8 configura el pipeline build → test → push. Evita correr integration tests dentro del docker build. | ✓ |
| Sí — incluir etapa test en el Dockerfile | docker build falla si tests no pasan. Contra: integration tests con WebApplicationFactory pueden requerir configuración especial en el contexto del build. | |

**User's choice:** No test stage in Dockerfile
**Notes:** Testing is a CI/CD concern, handled in Phase 8. Integration tests with WebApplicationFactory add complexity in the Docker build context.

### ¿El Dockerfile debe incluir el proyecto de tests o ignorarlo completamente?

| Option | Description | Selected |
|--------|-------------|----------|
| Ignorar tests — solo COPY src/ | La imagen final es más pequeña y limpia. tests/ no se copia al contenedor. | ✓ |
| COPY todo — incluyendo tests/ para posible uso futuro | Más flexible pero la imagen de producción lleva código de test innecesario. | |

**User's choice:** Only COPY src/
**Notes:** Production image should not contain test code.

---

## Ambiente de docker-compose

### ¿Qué ASPNETCORE_ENVIRONMENT debe usar el contenedor en docker-compose?

| Option | Description | Selected |
|--------|-------------|----------|
| Development | Local dev parity: Scalar UI accesible en /scalar, errores detallados, misma experiencia que dotnet run. El objetivo de docker-compose es paridad local. | ✓ |
| Production | Imagen más cercana a lo que correrá en Cloud Run. Pero actualmente Program.cs no cambia behavior por entorno — Scalar sigue activo. | |

**User's choice:** Development
**Notes:** docker-compose is for local development parity — Development environment is the right choice.

### ¿El docker-compose.yml debe incluir un healthcheck nativo (Docker-level)?

| Option | Description | Selected |
|--------|-------------|----------|
| Sí — definir healthcheck en docker-compose | Docker marca el contenedor como healthy/unhealthy basándose en /health. Mejor observabilidad local con docker ps. | ✓ |
| No — solo el endpoint /health sin Docker healthcheck | Más simple. El endpoint /health existe y es alcanzable; Docker healthcheck es una adición opcional. | |

**User's choice:** Yes — include Docker healthcheck
**Notes:** Healthcheck interval 30s, timeout 5s, retries 3.

---

## Claude's Discretion

- Exact layer ordering within the `build` stage (COPY sln vs csproj ordering details)
- Whether to use `--no-restore` on `dotnet build` after an explicit `dotnet restore` step
- Exact `dotnet publish` flags (e.g., `-c Release --no-restore -o /app/publish`)
- docker-compose service name (e.g., `personsapi` or `api`)

## Deferred Ideas

None — discussion stayed within phase scope.
