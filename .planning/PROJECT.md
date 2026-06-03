# PersonsAPI

## What This Is

A .NET 10 Web API built with controllers (not Minimal API) that demonstrates Clean Architecture and Hexagonal Architecture applied together. The API manages personal data (first name, paternal last name, maternal last name, date of birth with calculated age) using EF Core InMemory provider for simulation. Designed as a learning exercise to internalize how these two architectural patterns coexist in a .NET project.

## Core Value

A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

## Current Milestone: v2.0 Cloud Deployment

**Goal:** Containerize the PersonsAPI with Docker and deploy it to Google Cloud Run with a full CI/CD pipeline via GitHub Actions.

**Target features:**
- Multi-stage Dockerfile for the .NET 10 API
- docker-compose for local development parity
- GitHub Actions pipeline: build → test → push to Artifact Registry → deploy to Cloud Run
- Health check endpoint at `/health` (Cloud Run liveness requirement)
- Structured logging with Serilog in JSON format (Google Cloud Logging compatible)

## Current State

**Phase 5 complete 2026-06-03** — v2.0 Cloud Deployment in progress (1/4 phases done).

The PersonsAPI is a fully operational .NET 10 Web API with cloud-ready observability:
- **64 automated tests** — 32 domain, 15 application, 5 infrastructure, 12 integration — all passing
- Rich Person domain model with private setters, computed Age, factory, and update methods
- CQRS via Mediator.SourceGenerator 3.0.2 with FluentValidation pipeline behavior
- EF Core InMemory persistence with IPersonRepository port/adapter (PersonDbContext + PersonRepository)
- DataSeeder seeds 3 persons (María García López, Carlos Ramírez Martínez, Ana Flores Mendoza) on startup
- Six HTTP endpoints with RFC 9457 Problem Details (application/problem+json) error responses
- OpenAPI 3.1 document at `/openapi/v1.json` + Scalar interactive UI at `/scalar/v1`
- **Serilog CLEF JSON stdout logging** — Cloud Logging-compatible, EF Core/AspNetCore namespaces filtered to Warning
- **`GET /health`** — HTTP 200 + `{"status":"Healthy"}` (application/json) — Cloud Run liveness probe ready
- `dotnet run --project src/PersonsAPI.Api` → boots, seeds, serves all endpoints, emits JSON logs

## Requirements

### Validated (v1.0)

- ✓ Rich Person domain model with calculated Age from DateOfBirth (no anemic models) — v1.0
- ✓ EF Core InMemory provider (Domain layer isolated, zero PackageReference) — v1.0
- ✓ IPersonRepository port in Application layer — v1.0
- ✓ EF Core InMemory persistence adapter (PersonDbContext + PersonRepository) — v1.0
- ✓ Seeded in-memory data for immediate testing (3 persons via DataSeeder) — v1.0
- ✓ Full CRUD + PATCH operations (GET all, GET by id, POST, PUT, PATCH, DELETE) — v1.0
- ✓ Clean Architecture layer separation: Domain → Application → Infrastructure → Api — v1.0
- ✓ Hexagonal Architecture: ports in Domain/Application, adapters in Infrastructure and Api — v1.0
- ✓ Controllers with proper HTTP semantics (not Minimal API) — v1.0
- ✓ FluentValidation pipeline behavior in Application layer — v1.0
- ✓ RFC 9457 Problem Details for all error responses — v1.0
- ✓ OpenAPI documentation + Scalar interactive UI — v1.0
- ✓ All code in English — v1.0

### Validated (v2.0 — in progress)

- ✓ Health check endpoint at `/health` with JSON response (OBS-02) — Phase 5, 2026-06-03
- ✓ Structured logging with Serilog CLEF JSON format (OBS-01) — Phase 5, 2026-06-03

### Active (v2.0 — remaining)

- [ ] Multi-stage Dockerfile for the .NET 10 API (DOCK-01)
- [ ] docker-compose for local/cloud parity (DOCK-02)
- [ ] GitHub Actions CI/CD: build → test → push → deploy (CICD-01)
- [ ] Google Cloud Run deployment configuration (CLOUD-01)

### Deferred (v2.1+ candidates)

- [ ] Replace EF Core InMemory with SQLite/SQL Server and real migrations (PERS-01)
- [ ] Integration tests using EF Core SQLite in-memory (PERS-02)
- [ ] JWT authentication on write endpoints (SEC-01)
- [ ] Role-based authorization (admin vs. read-only) (SEC-02)

### Out of Scope

| Feature | Reason |
|---------|--------|
| Real database (SQL Server / SQLite) | EF InMemory covers the learning goal; DB wiring is v2 |
| Authentication / authorization | Not part of this architecture learning scope for v1 |
| Minimal API endpoints | Explicitly excluded — controllers only |
| Generic IRepository\<T\> | Anti-pattern for Hexagonal; use specific IPersonRepository |
| AutoMapper / MediatR 13+ | Commercial licenses — use manual mapping and Mediator 3.0.2 |
| Custom success envelope | Anti-pattern — use raw HTTP semantics for success |
| Swashbuckle | Removed from .NET 9+ template — use Microsoft.AspNetCore.OpenApi + Scalar |

## Context

- Target framework: .NET 10, C# 14
- Architecture patterns: Clean Architecture (layers) + Hexagonal Architecture (ports & adapters)
- Data persistence: EF Core InMemory provider (Microsoft.EntityFrameworkCore.InMemory 10.0.8)
- Person fields: FirstName, PaternalLastName, MaternalLastName, DateOfBirth, Age (computed)
- Age is derived from DateOfBirth at runtime — never stored
- CQRS mediator: Mediator.SourceGenerator 3.0.2 (martinothamar) — MIT, source-generated, zero reflection
- Validation: FluentValidation 12.1.1 via ValidationBehavior open generic in Mediator pipeline
- API docs: Microsoft.AspNetCore.OpenApi 10.0.8 + Scalar.AspNetCore 2.14.14
- JSON Patch: Microsoft.AspNetCore.JsonPatch.SystemTextJson 10.0.8 (STJ-based, not Newtonsoft)

## Constraints

- **Framework**: .NET 10 — use latest C# features where they clarify intent
- **API style**: Controllers only — no Minimal API endpoints
- **Domain**: Rich models — business logic lives in the domain entity, not services
- **Language**: All identifiers, comments, and documentation in English

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| EF Core InMemory over fake List<T> | Applies real EF patterns (DbContext, IQueryable) while keeping zero setup | ✓ PersonDbContext + PersonRepository implement full EF patterns; builder.Ignore(p => p.Age) prevents computed property mapping |
| Clean + Hexagonal combined | Hexagonal defines port/adapter boundaries; Clean defines layer ownership | ✓ IPersonRepository (port in Application) implemented by PersonRepository (adapter in Infrastructure); Application has zero EF references |
| Rich domain model on Person | Age calculation belongs to the entity — encapsulates business logic | ✓ Person.Create() enforces all invariants; Age computed in getter |
| Controllers over Minimal API | Explicit requirement; controllers map better to Clean's Presentation layer | ✓ PersonsController sealed [ApiController] with six actions; no Minimal API endpoints |
| DomainException inherits Exception directly | Application layer catches by type; not ArgumentException to avoid conflating domain violations with BCL errors | ✓ DomainException : Exception, guarded by inheritance test |
| Mediator.SourceGenerator over MediatR | MediatR 13+ requires commercial license; Mediator 3.0.2 is MIT, source-generated, zero reflection | ✓ Works with ServiceLifetime.Scoped to match DbContext; zero reflection overhead |
| UpdatePersonDto as mutable class | JsonPatchDocument<T>.ApplyTo() requires settable properties at runtime; positional records with init-only setters throw | ✓ Mutable class with { get; set; } properties; PATCH endpoint works correctly |
| JsonPatch content type: application/json-patch+json | STJ JsonPatch package registers formatter for RFC 6902 media type only; plain application/json returns 415 | ✓ Confirmed — PATCH requests must use application/json-patch+json content type |
| ResetableApiFactory for integration tests | Shared WebApplicationFactory + EF InMemory = ordering-dependent test failures; each fixture needs isolated DB | ✓ Guid.NewGuid() per factory instance eliminates cross-test state contamination |

---
## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-06-01 after v2.0 milestone start*
