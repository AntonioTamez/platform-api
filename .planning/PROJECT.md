# PersonsAPI

## What This Is

A .NET 10 Web API built with controllers (not Minimal API) that demonstrates Clean Architecture and Hexagonal Architecture applied together. The API manages personal data (first name, paternal last name, maternal last name, date of birth with calculated age) using EF Core InMemory provider for simulation. Designed as a learning exercise to internalize how these two architectural patterns coexist in a .NET project.

## Core Value

A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

## Requirements

### Validated

- [x] Rich Person domain model with calculated Age from DateOfBirth (no anemic models) — Validated in Phase 01: domain-layer
- [x] EF Core InMemory provider (Domain layer isolated, zero PackageReference) — Validated in Phase 01: domain-layer
- [x] EF Core InMemory persistence adapter (PersonDbContext + PersonRepository) with IPersonRepository port — Validated in Phase 03: infrastructure-layer
- [x] Seeded in-memory data for immediate testing without setup (3 persons via DataSeeder) — Validated in Phase 03: infrastructure-layer

### Active

(No active requirements — all milestone requirements validated)

### Completed

- [x] Full CRUD + PATCH operations for Person (GET all, GET by ID, POST, PUT, PATCH, DELETE) — Validated in Phase 04: api-layer
- [x] Clean Architecture layer separation: Domain → Application → Infrastructure → Presentation — Validated in Phase 04: api-layer
- [x] Hexagonal Architecture: ports (interfaces) in Domain/Application, adapters (implementations) in Infrastructure and Presentation — Validated in Phase 04: api-layer
- [x] Controllers with proper HTTP semantics (not Minimal API) — Validated in Phase 04: api-layer
- [x] All code in English — Validated throughout all phases

### Out of Scope

- SQL Server / SQLite / real database — deferred; EF InMemory covers the learning goal
- Authentication/authorization — not part of this learning scope
- Minimal API approach — explicitly excluded per requirements
- Anemic domain models — explicitly prohibited

## Context

- Target framework: .NET 10
- Architecture patterns: Clean Architecture (layers) + Hexagonal Architecture (ports & adapters)
- Data persistence: EF Core InMemory provider (Microsoft.EntityFrameworkCore.InMemory)
- Person fields: FirstName, PaternalLastName, MaternalLastName, DateOfBirth, Age (computed)
- Age is derived from DateOfBirth at runtime — never stored
- This is a learning/reference project that can evolve into a production foundation

## Constraints

- **Framework**: .NET 10 — use latest C# features where they clarify intent
- **API style**: Controllers only — no Minimal API endpoints
- **Domain**: Rich models — business logic lives in the domain entity, not services
- **Language**: All identifiers, comments, and documentation in English

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| EF Core InMemory over fake List<T> | Applies real EF patterns (DbContext, IQueryable) while keeping zero setup | Confirmed in Phase 03: PersonDbContext + PersonRepository implement full EF patterns; builder.Ignore(p => p.Age) prevents computed property mapping |
| Clean + Hexagonal combined | Hexagonal defines port/adapter boundaries; Clean defines layer ownership | Confirmed in Phase 03: IPersonRepository (port in Application) implemented by PersonRepository (adapter in Infrastructure); Application has zero EF references |
| Rich domain model on Person | Age calculation belongs to the entity — encapsulates business logic | Confirmed in Phase 01: Person.Create() enforces all invariants; Age computed in getter |
| Controllers over Minimal API | Explicit requirement; controllers map better to Clean's Presentation layer | Confirmed in Phase 04: PersonsController sealed [ApiController] with six actions; no Minimal API endpoints anywhere in solution |
| DomainException inherits Exception directly | Application layer catches by type; not ArgumentException to avoid conflating domain violations with BCL errors | Confirmed in Phase 01: DomainException : Exception, guarded by inheritance test |

## Current State

Phase 4 complete — all four milestone phases delivered. The PersonsAPI is a fully operational .NET 10 Web API with:
- Rich domain model (Person entity, DomainException, Age computation)
- CQRS via Mediator.SourceGenerator with FluentValidation pipeline
- EF Core InMemory persistence with IPersonRepository port/adapter
- Six HTTP endpoints with RFC 9457 Problem Details error responses
- OpenAPI 3.1 documentation + Scalar interactive UI
- 62 automated tests (domain, application, infrastructure, integration) — all passing

## Evolution

This document evolves at phase transitions and milestone boundaries.

Last updated: Phase 4 complete (2026-06-01)
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-05-31 after Phase 03 (infrastructure-layer) completion*
