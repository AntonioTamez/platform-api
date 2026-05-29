# Phase 2: Application Layer - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 2-Application Layer
**Areas discussed:** PATCH command design, Not-found contract, DTO design, Validator scope

---

## PATCH Command Design

| Option | Description | Selected |
|--------|-------------|----------|
| Controller applies patch, passes clean command | Controller receives `JsonPatchDocument<UpdatePersonDto>`, applies it to a fresh DTO, then sends `PatchPersonCommand(id, dto)`. Application layer stays free of ASP.NET Core types. | ✓ |
| Command carries the patch document | `PatchPersonCommand` carries `JsonPatchDocument<UpdatePersonDto>` and the handler applies it — pulls `Microsoft.AspNetCore.JsonPatch` into Application. | |

**User's choice:** Controller applies patch, passes clean command

**Notes:** Follow-up question on command shape — `PatchPersonCommand` carries `UpdatePersonDto` (nullable fields), not explicit individual nullable parameters. Handler applies only non-null fields via domain update methods.

---

## Not-Found Contract

| Option | Description | Selected |
|--------|-------------|----------|
| Returns `Person?` (null) — handler converts to `PersonNotFoundException` | Repository returns null; handler throws; API layer catches and maps to 404. | ✓ |
| Repository throws directly | `IPersonRepository.GetByIdAsync` throws on not-found — mixes use-case semantics into the port contract. | |
| Returns a Result type | `Result<Person>` or `Option<Person>` — contradicts D-03 (no Result<T> pattern). | |

**User's choice:** `Person?` — handler throws `PersonNotFoundException`

| Option | Description | Selected |
|--------|-------------|----------|
| Application layer — `Application/Exceptions/PersonNotFoundException` | Use-case concern; API layer catches it for 404 mapping. | ✓ |
| Domain layer — alongside `DomainException` | Puts persistence awareness into the domain. | |

**User's choice:** Application layer

---

## DTO Design

| Option | Description | Selected |
|--------|-------------|----------|
| Separate `CreatePersonRequest` and `UpdatePersonRequest` | Distinct types per operation; validators can be field-exact. | ✓ |
| One shared `PersonRequest` | Simpler but loses create-vs-update semantics. | |

**User's choice:** Separate request types

| Option | Description | Selected |
|--------|-------------|----------|
| All 5 fields including computed Age | `PersonResponse` surfaces Age from the domain entity — demonstrates computed-property pattern. | ✓ |
| Only stored fields, no Age | Omits a project learning goal. | |

**User's choice:** Include Age in `PersonResponse`

| Option | Description | Selected |
|--------|-------------|----------|
| Static factory on `PersonResponse` | `PersonResponse.FromDomain(Person p)` — zero dependency, debuggable, consistent with CLAUDE.md. | ✓ |
| Inside the handler directly | Every handler duplicates the mapping. | |

**User's choice:** Static factory `PersonResponse.FromDomain`

---

## Validator Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Write commands only | Validators for Create, Update, Patch. Read queries skip — their inputs are route params, not user-supplied bodies. | ✓ |
| All requests including queries | Adds ceremony for trivially-constrained route params. | |

**User's choice:** Write commands only

| Option | Description | Selected |
|--------|-------------|----------|
| Mirror domain rules (not-empty, length 2–100, DateOfBirth not future) | Correct layering — Application catches bad input at the boundary for field-level 400 detail; Domain enforces invariants as second defense. | ✓ |
| Minimal not-empty checks only | Loses field-level detail required by ERR-02. | |

**User's choice:** Mirror domain rules

---

## Claude's Discretion

- Folder structure within `PersonsAPI.Application` (Commands/, Queries/, DTOs/, Ports/, Behaviors/, Exceptions/)
- Assembly marker interface (`IApplicationMarker`) or equivalent for validator scanning
- Whether commands/queries use `record` or `class` (records strongly preferred)

## Deferred Ideas

None — discussion stayed within phase scope.
