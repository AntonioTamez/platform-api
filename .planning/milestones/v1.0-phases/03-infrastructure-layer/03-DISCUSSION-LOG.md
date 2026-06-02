# Phase 3: Infrastructure Layer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-30
**Phase:** 3-Infrastructure Layer
**Areas discussed:** Seeder data, DataSeeder API, Test project

---

## Seeder data

### ¿Cuántas personas sembrar al inicio?

| Option | Description | Selected |
|--------|-------------|----------|
| 3 personas | Mínimo suficiente para GET all vs GET by id, sin ruido. | ✓ |
| 5 personas | Más variedad para probar filtrado visual y diferenciar IDs. | |
| 4 personas | Balance intermedio. | |

**User's choice:** 3 personas
**Notes:** El mínimo del rango 3–5 es suficiente para el objetivo de aprendizaje.

---

### ¿Qué estilo de nombres usar en los datos sembrados?

| Option | Description | Selected |
|--------|-------------|----------|
| Nombres mexicanos reales | FirstName + PaternalLastName + MaternalLastName — formato para el que el proyecto fue diseñado. | ✓ |
| Nombres genéricos internacionales | John Doe Smith, Jane Brown Wilson — sin contexto cultural específico. | |

**User's choice:** Nombres mexicanos reales
**Notes:** Coherente con el dominio del proyecto (paternal + maternal last name).

---

### ¿Cómo distribuir las fechas de nacimiento de los 3 registros?

| Option | Description | Selected |
|--------|-------------|----------|
| Edades variadas | Una persona ~30, una ~45, una ~60 — demuestra que Age se calcula correctamente en diferentes rangos. | ✓ |
| Todas edades similares | Las 3 personas entre 25-35 años. Más simple pero no ejercita el rango del cálculo de Age. | |

**User's choice:** Edades variadas
**Notes:** Permite verificar visualmente que el algoritmo de cómputo de Age produce valores distintos y coherentes.

---

## DataSeeder API

### ¿Cómo debe exponer el DataSeeder su operación de siembra?

| Option | Description | Selected |
|--------|-------------|----------|
| Extension method en IServiceProvider | `app.Services.SeedAsync()` — patrón estándar .NET, limpio, no requiere DI. | ✓ |
| IHostedService / BackgroundService | Se registra como servicio, corre automáticamente. Oculta el flujo de startup. | |
| Static class con método estático | `DataSeeder.SeedAsync(dbContext)` — explícito pero menos idiomático. | |

**User's choice:** Extension method en IServiceProvider
**Notes:** Mantiene el flujo de startup visible en Program.cs, coherente con el objetivo de aprendizaje.

---

### ¿El seeder debe verificar si ya existen datos antes de sembrar (idempotente)?

| Option | Description | Selected |
|--------|-------------|----------|
| Sí, idempotente | `if (!context.Persons.Any())` — enseña el patrón correcto para producción. | ✓ |
| No, siembra siempre | Más simple para InMemory (se resetea de todos modos), pero no enseña el patrón correcto. | |

**User's choice:** Sí, idempotente
**Notes:** El objetivo de aprendizaje pesa más que la simplicidad; InMemory se resetea pero el patrón debe ser correcto.

---

### ¿El seeder debe registrarse en DI dentro de AddInfrastructure()?

| Option | Description | Selected |
|--------|-------------|----------|
| Externo a DI — solo se llama desde Program.cs | AddInfrastructure() registra DbContext y PersonRepository. Seeder es paso de inicialización, no servicio. | ✓ |
| Dentro de DI como transient/scoped | Se puede resolver desde el container, pero agrega un servicio que solo se usa una vez en startup. | |

**User's choice:** Externo a DI
**Notes:** Separación clara entre servicios de aplicación (DI) y pasos de inicialización (startup).

---

## Test project

### ¿Phase 3 debe incluir PersonsAPI.Infrastructure.Tests?

| Option | Description | Selected |
|--------|-------------|----------|
| Sí, incluir tests de repositorio | Coherente con Phases 1 y 2. Tests de PersonRepository contra EF InMemory demuestran el patrón. | ✓ |
| No, saltarse tests de infraestructura | EF InMemory no detecta constraint violations reales; tests reales requieren SQLite/SQL Server (v2). | |

**User's choice:** Sí, incluir tests de repositorio
**Notes:** Consistencia con las fases anteriores y objetivo de aprendizaje del patrón de test de repositorios.

---

### ¿Qué debe cubrir el test project de Infrastructure?

| Option | Description | Selected |
|--------|-------------|----------|
| CRUD completo del repositorio | GetAllAsync, GetByIdAsync, AddAsync, UpdateAsync, DeleteAsync — cada uno con InMemory DB aislado. | ✓ |
| Solo happy path (Add + GetById) | Mínimo para probar que el repositorio conecta con EF. Rápido pero no cubre Update/Delete. | |

**User's choice:** CRUD completo
**Notes:** Cubre todos los métodos del puerto IPersonRepository para validar la implementación completa.

---

### ¿Cada test de repositorio usa su propio InMemory DB aislado o comparten uno?

| Option | Description | Selected |
|--------|-------------|----------|
| BD aislada por test | `Guid.NewGuid().ToString()` como nombre del InMemory DB — evita contaminación entre tests. | ✓ |
| BD compartida por clase de test | Un DbContext por test class con xUnit IClassFixture. Más complejo sin ventaja con InMemory. | |

**User's choice:** BD aislada por test
**Notes:** Patrón estándar para tests de repositorio con EF InMemory. Simple y efectivo.

---

## Claude's Discretion

- Folder structure inside `PersonsAPI.Infrastructure/` (Persistence/, Repositories/, Seeder/ or flat)
- Internal EF Core property access strategy for private setters (HasField vs. reflection with protected ctor)
- Whether `PersonEntityConfiguration` is in a `Configurations/` subfolder or alongside `PersonDbContext`
- Naming of the extension method file (ServiceCollectionExtensions.cs pattern from Phase 2)

## Deferred Ideas

None — discussion stayed within phase scope.
