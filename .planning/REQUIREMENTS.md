# Requirements: PersonsAPI

**Defined:** 2026-05-27
**Core Value:** A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

## v1 Requirements

### Domain Model

- [x] **DOM-01**: Person entity encapsulates FirstName, PaternalLastName, MaternalLastName, and DateOfBirth with private setters — no public property mutation
- [x] **DOM-02**: Person entity exposes a computed Age property derived from DateOfBirth using DateOnly comparison (month + day aware) — never stored
- [x] **DOM-03**: Person entity provides a static factory method Person.Create() that validates invariants and is the only way to construct a valid instance
- [x] **DOM-04**: Person entity exposes intention-revealing update methods (e.g., UpdateName, UpdateDateOfBirth) — external code never assigns properties directly

### Read Operations

- [x] **READ-01**: User can retrieve a list of all persons via GET /api/persons
- [x] **READ-02**: User can retrieve a single person by ID via GET /api/persons/{id} — returns 404 if not found

### Write Operations

- [x] **WRITE-01**: User can create a new person via POST /api/persons — returns 201 with Location header
- [x] **WRITE-02**: User can fully replace a person via PUT /api/persons/{id} — returns 200, 404 if not found
- [x] **WRITE-03**: User can partially update a person via PATCH /api/persons/{id} using JSON Patch on a DTO (not the domain entity) — returns 200, 404 if not found
- [x] **WRITE-04**: User can delete a person via DELETE /api/persons/{id} — returns 204, 404 if not found

### Error Handling

- [x] **ERR-01**: All error responses follow RFC 9457 Problem Details format (application/problem+json) — no custom envelope for errors
- [x] **ERR-02**: Validation errors return 400 with Problem Details listing all field violations
- [x] **ERR-03**: Missing resource errors return 404 with Problem Details

### Validation

- [x] **VAL-01**: Input validation runs in the Application layer via a FluentValidation pipeline behavior — not in controllers
- [x] **VAL-02**: Domain invariant validation runs inside Person.Create() and update methods — not in handlers

### Infrastructure

- [x] **INFRA-01**: EF Core InMemory provider is used as the persistence adapter — no real database required
- [x] **INFRA-02**: Domain project has zero EF Core NuGet references — isolation enforced at .csproj level
- [x] **INFRA-03**: IPersonRepository port interface lives in the Application layer — not in Infrastructure
- [x] **INFRA-04**: Application seeds 3–5 hardcoded Person records on startup for immediate manual testing

### API Documentation

- [x] **DOC-01**: OpenAPI specification is generated via Microsoft.AspNetCore.OpenApi
- [x] **DOC-02**: Scalar interactive UI is available at /scalar for manual exploration

## v2 Requirements

### Persistence

- **PERS-01**: Replace EF Core InMemory with SQL Server or SQLite with real migrations
- **PERS-02**: Add integration tests using EF Core SQLite in-memory (catches constraint violations InMemory ignores)

### Security

- **SEC-01**: JWT authentication on write endpoints
- **SEC-02**: Role-based authorization (admin vs. read-only)

### Observability

- **OBS-01**: Structured logging with Serilog
- **OBS-02**: Health check endpoint at /health

## Out of Scope

| Feature | Reason |
|---------|--------|
| Real database (SQL Server / SQLite) | EF InMemory covers the learning goal; DB wiring is v2 |
| Authentication / authorization | Not part of this architecture learning scope |
| Minimal API endpoints | Explicitly excluded — controllers only |
| Generic IRepository\<T\> | Anti-pattern for Hexagonal; use specific IPersonRepository |
| AutoMapper / MediatR 13+ | Commercial licenses — use manual mapping and Mediator 3.0.2 |
| Custom success envelope ({ data, meta }) | Anti-pattern — use raw HTTP semantics for success |
| Swashbuckle | Dead in .NET 10 — use Microsoft.AspNetCore.OpenApi + Scalar |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| DOM-01 | Phase 1 | Complete |
| DOM-02 | Phase 1 | Complete |
| DOM-03 | Phase 1 | Complete |
| DOM-04 | Phase 1 | Complete |
| VAL-02 | Phase 1 | Complete |
| INFRA-02 | Phase 1 | Complete |
| READ-01 | Phase 2 | Complete |
| READ-02 | Phase 2 | Complete |
| WRITE-01 | Phase 2 | Complete |
| WRITE-02 | Phase 2 | Complete |
| WRITE-03 | Phase 2 | Complete |
| WRITE-04 | Phase 2 | Complete |
| VAL-01 | Phase 2 | Complete |
| INFRA-03 | Phase 2 | Complete |
| INFRA-01 | Phase 3 | Complete |
| INFRA-04 | Phase 3 | Complete |
| ERR-01 | Phase 4 | Complete |
| ERR-02 | Phase 4 | Complete |
| ERR-03 | Phase 4 | Complete |
| DOC-01 | Phase 4 | Complete |
| DOC-02 | Phase 4 | Complete |

**Coverage:**

- v1 requirements: 21 total
- Mapped to phases: 21 (Phase 1: 6, Phase 2: 8, Phase 3: 2, Phase 4: 5)
- Unmapped: 0

---
*Requirements defined: 2026-05-27*
*Last updated: 2026-05-27 after roadmap creation — full traceability resolved*
