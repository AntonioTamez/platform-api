# Phase 4: API Layer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-31
**Phase:** 4-api-layer
**Areas discussed:** Manejo de excepciones, Estructura de errores 400, Endpoint PATCH

---

## Manejo de excepciones

| Option | Description | Selected |
|--------|-------------|----------|
| IExceptionHandler (ASP.NET Core 8+) | Dos clases separadas registradas con AddExceptionHandler<T>(). Limpio, testeable, encadenado automáticamente. | ✓ |
| Middleware personalizado | ExceptionHandlingMiddleware con try/catch global. Más verboso. | |
| UseExceptionHandler con lambda | Compacto pero dificulta pruebas y se vuelve ruidoso con múltiples tipos. | |

**User's choice:** IExceptionHandler (patrón moderno ASP.NET Core 8+/10)

---

| Option | Description | Selected |
|--------|-------------|----------|
| Mínimo RFC 9457 | Solo type, title, status, detail. Sin instance. | ✓ |
| Con instance | Agrega instance: '/api/persons/{id}' al 404. | |

**User's choice:** Mínimo RFC 9457 para 404 Problem Details.

---

| Option | Description | Selected |
|--------|-------------|----------|
| AddProblemDetails() global | Configura el serializador una vez. Handlers solo llenan campos. | ✓ |
| Manual en cada handler | Cada handler crea new ProblemDetails { } explícitamente. | |

**User's choice:** AddProblemDetails() global en Program.cs.

---

## Estructura de errores 400 (validación)

| Option | Description | Selected |
|--------|-------------|----------|
| Diccionario por campo | errors: { "Field": ["msg"] }. Formato estándar [ApiController]. | ✓ |
| Lista plana de objetos | errors: [{ property, message }]. Más genérico. | |

**User's choice:** Diccionario por campo — compatible con el formato estándar de ASP.NET Core.

---

| Option | Description | Selected |
|--------|-------------|----------|
| Mensaje general en detail | detail: "One or more validation errors occurred." | ✓ |
| Sin detail | Omitir el campo detail. | |

**User's choice:** Mensaje general en detail.

---

## Endpoint PATCH

| Option | Description | Selected |
|--------|-------------|----------|
| ApplyTo + TryValidateModel | ApplyTo con ModelState, verificar IsValid. | ✓ |
| ApplyTo + validación manual | Dejar todo a ValidationBehavior de FluentValidation. | |

**User's choice:** ApplyTo + verificar ModelState.IsValid después.

---

| Option | Description | Selected |
|--------|-------------|----------|
| DTO vacío (todos null) | Patch aplica sobre new UpdatePersonDto(null, null, null, null). | ✓ |
| DTO precargado con valores actuales | Buscar persona primero, precargar el DTO. | |

**User's choice:** DTO vacío — solo los campos del patch document se populan.

---

| Option | Description | Selected |
|--------|-------------|----------|
| [FromBody] explícito | [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc. Explícito. | ✓ |
| Sin atributo (inferido) | [ApiController] lo infiere. Funciona pero menos explícito. | |

**User's choice:** [FromBody] explícito en la firma del action.

---

## Claude's Discretion

- Versiones exactas de packages en PersonsAPI.Api.csproj (ver CLAUDE.md)
- Orden del middleware pipeline en Program.cs
- Configuración de Scalar (título, descripción)
- Atributos del controller ([ApiController], [Route])
- Supresión del 400 automático de [ApiController]

## Deferred Ideas

Ninguna — la discusión se mantuvo dentro del scope de la fase.
