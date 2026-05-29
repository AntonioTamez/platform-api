# Roadmap: PersonsAPI

## Overview

PersonsAPI is built layer-by-layer following Clean + Hexagonal Architecture dependency order. Domain is built first (zero dependencies), Application defines ports and use-case contracts second, Infrastructure implements those ports third, and Api wires everything together last. Each phase produces a compilable artifact the next phase references. This order is non-negotiable — any other sequence creates unresolvable project references at compile time. Granularity is coarse: four phases, one per architectural layer.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Domain Layer** - Person entity with rich model, computed Age, factory method, and update methods — zero outbound dependencies (completed 2026-05-29)
- [ ] **Phase 2: Application Layer** - Ports, CQRS commands/queries, handlers, DTOs, and FluentValidation pipeline behavior
- [ ] **Phase 3: Infrastructure Layer** - EF Core InMemory adapter, PersonDbContext, repository implementation, and data seeder
- [ ] **Phase 4: API Layer** - PersonsController, Program.cs wiring, Problem Details, OpenAPI + Scalar, and PATCH endpoint

## Phase Details

### Phase 1: Domain Layer
**Goal**: The Person domain entity is fully modeled with private setters, computed Age, a static factory, and intention-revealing update methods — and the Domain project has zero EF Core or framework dependencies
**Depends on**: Nothing (first phase)
**Requirements**: DOM-01, DOM-02, DOM-03, DOM-04, VAL-02, INFRA-02
**Success Criteria** (what must be TRUE):
  1. Person entity enforces all field invariants inside Person.Create() — calling Create() with invalid data raises a domain exception, never reaches the caller
  2. Person.Age returns the correct integer age computed from DateOfBirth using month-and-day-aware DateOnly comparison — never a stored column
  3. Person entity exposes UpdateName() and UpdateDateOfBirth() as the only mutation paths — no public property setters exist
  4. PersonsAPI.Domain.csproj contains zero Microsoft.EntityFrameworkCore PackageReference entries — isolation is enforced at the .csproj level, not by convention
**Plans**: 2 plans
- [x] 01-01-PLAN.md — Solution + zero-dependency Domain library + DomainException + xUnit test harness (INFRA-02, VAL-02)
- [x] 01-02-PLAN.md — Person rich domain entity built test-first: invariants, computed Age, factory, update methods (DOM-01..04, VAL-02)

### Phase 2: Application Layer
**Goal**: The Application layer owns all port interfaces and use-case contracts — IPersonRepository lives in Application/Ports/, every CRUD + PATCH operation has a command or query record plus a handler, DTOs are defined, and a FluentValidation pipeline behavior intercepts all requests before handlers run
**Depends on**: Phase 1
**Requirements**: READ-01, READ-02, WRITE-01, WRITE-02, WRITE-03, WRITE-04, VAL-01, INFRA-03
**Success Criteria** (what must be TRUE):
  1. IPersonRepository is declared in PersonsAPI.Application — no reference to it exists in Domain or Infrastructure at definition time
  2. GetAllPersonsQuery, GetPersonByIdQuery, CreatePersonCommand, UpdatePersonCommand, PatchPersonCommand, and DeletePersonCommand each have a corresponding handler registered via AddApplication()
  3. ValidationBehavior<TRequest, TResponse> executes FluentValidation validators before any handler runs — an invalid request never reaches the handler body
  4. All command and query records, DTOs, and request types compile with references only to Domain and Mediator.Abstractions — no Infrastructure or ASP.NET Core types appear in Application
**Plans**: 3 plans
- [x] 02-01-PLAN.md — Application library + IPersonRepository port + PersonNotFoundException + four DTO records + xUnit test project (INFRA-03)
- [x] 02-02-PLAN.md — Six CQRS handlers (queries + commands) with three FluentValidation validators, including PatchPersonHandler null-fallback pattern (READ-01, READ-02, WRITE-01, WRITE-02, WRITE-03, WRITE-04)
- [ ] 02-03-PLAN.md — ValidationBehavior pipeline behavior + AddApplication() DI registration (VAL-01)

### Phase 3: Infrastructure Layer
**Goal**: The Infrastructure layer provides a working EF Core InMemory persistence adapter — PersonDbContext, PersonEntityConfiguration (with builder.Ignore(p => p.Age)), PersonRepository implementing IPersonRepository, and a DataSeeder that populates 3–5 persons on startup
**Depends on**: Phase 2
**Requirements**: INFRA-01, INFRA-04
**Success Criteria** (what must be TRUE):
  1. Starting the application produces 3–5 seeded Person records retrievable from the in-memory store without any manual setup
  2. PersonEntityConfiguration excludes the Age property from EF mapping via builder.Ignore() — no Age column or shadow property exists in the model
  3. PersonRepository implements every IPersonRepository method and returns Task<IReadOnlyList<Person>> for list queries — no IQueryable<Person> leaks beyond the repository boundary
**Plans**: TBD

### Phase 4: API Layer
**Goal**: PersonsController exposes all six HTTP endpoints with correct semantics, Program.cs is the sole composition root wiring all layers, Problem Details (RFC 9457) is the only error response format, and OpenAPI + Scalar are available for immediate interactive exploration
**Depends on**: Phase 3
**Requirements**: ERR-01, ERR-02, ERR-03, DOC-01, DOC-02
**Success Criteria** (what must be TRUE):
  1. GET /api/persons returns 200 with the seeded persons list; GET /api/persons/{id} returns 200 for a known ID and 404 Problem Details for an unknown ID
  2. POST /api/persons returns 201 with a Location header; PUT /api/persons/{id} returns 200; PATCH /api/persons/{id} applies JSON Patch via UpdatePersonDto (not the domain entity) and returns 200; DELETE /api/persons/{id} returns 204
  3. Any validation failure (missing field, invalid value) returns 400 with application/problem+json listing all field violations — no raw ModelState or custom envelope
  4. Navigating to /scalar in a browser opens the Scalar interactive UI with all six endpoints documented and executable
**UI hint**: yes
**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Domain Layer | 2/2 | Complete   | 2026-05-29 |
| 2. Application Layer | 2/3 | In Progress|  |
| 3. Infrastructure Layer | 0/TBD | Not started | - |
| 4. API Layer | 0/TBD | Not started | - |
