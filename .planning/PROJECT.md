# PersonsAPI

## What This Is

A .NET 10 Web API built with controllers (not Minimal API) that demonstrates Clean Architecture and Hexagonal Architecture applied together. The API manages personal data (first name, paternal last name, maternal last name, date of birth with calculated age) using EF Core InMemory provider for simulation. Designed as a learning exercise to internalize how these two architectural patterns coexist in a .NET project.

## Core Value

A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Full CRUD + PATCH operations for Person (GET all, GET by ID, POST, PUT, PATCH, DELETE)
- [ ] Rich Person domain model with calculated Age from DateOfBirth (no anemic models)
- [ ] Clean Architecture layer separation: Domain → Application → Infrastructure → Presentation
- [ ] Hexagonal Architecture: ports (interfaces) in Domain/Application, adapters (implementations) in Infrastructure and Presentation
- [ ] EF Core with InMemory provider — no real DB, but full EF patterns (DbContext, entities, repositories)
- [ ] Controllers with proper HTTP semantics (not Minimal API)
- [ ] All code in English
- [ ] Seeded in-memory data for immediate testing without setup

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
| EF Core InMemory over fake List<T> | Applies real EF patterns (DbContext, IQueryable) while keeping zero setup | — Pending |
| Clean + Hexagonal combined | Hexagonal defines port/adapter boundaries; Clean defines layer ownership | — Pending |
| Rich domain model on Person | Age calculation belongs to the entity — encapsulates business logic | — Pending |
| Controllers over Minimal API | Explicit requirement; controllers map better to Clean's Presentation layer | — Pending |

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
*Last updated: 2026-05-27 after initialization*
