# PersonsAPI

> A .NET 10 Web API that demonstrates **Clean Architecture** and **Hexagonal Architecture** applied together — where the domain drives everything and infrastructure is a detail.

Built as a deliberate learning exercise: every structural decision is intentional, documented, and traceable to an architectural principle. The codebase is designed to be read as much as run.

---

## Table of Contents

- [Why This Project Exists](#why-this-project-exists)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Domain Model](#domain-model)
- [API Contract](#api-contract)
- [Request Flow](#request-flow)
- [Getting Started](#getting-started)
- [Running Tests](#running-tests)
- [Build Status](#build-status)
- [Key Design Decisions](#key-design-decisions)
- [What NOT To Use Here](#what-not-to-use-here)

---

## Why This Project Exists

Most tutorials apply Clean Architecture as a folder convention. This project applies it as a **dependency rule enforced at the `.csproj` level** — if a layer takes the wrong dependency, the solution does not compile.

The goal is to answer one concrete question: *how do Clean Architecture and Hexagonal Architecture coexist in a real .NET project without fighting each other?*

---

## Architecture

### The Four-Layer Model

```
┌─────────────────────────────────────────────────────────────────┐
│                          API Layer                              │
│   PersonsController  ·  Program.cs  ·  OpenAPI + Scalar        │
│   Primary Adapter — translates HTTP ↔ Application commands      │
└──────────────────────────┬──────────────────────────────────────┘
                           │  calls
┌──────────────────────────▼──────────────────────────────────────┐
│                      Application Layer                          │
│   Commands / Queries / Handlers  ·  ValidationBehavior          │
│   IPersonRepository (port)  ·  DTOs  ·  Exceptions             │
│   Owns all use-case contracts — depends only on Domain          │
└──────────────────────────┬──────────────────────────────────────┘
                           │  calls (via interface)
┌──────────────────────────▼──────────────────────────────────────┐
│                    Infrastructure Layer                         │
│   PersonDbContext  ·  PersonRepository  ·  DataSeeder           │
│   Secondary Adapter — implements Application ports using EF Core│
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                        Domain Layer                             │
│   Person (rich entity)  ·  DomainException                     │
│   Zero outbound dependencies — pure C#, no NuGet               │
└─────────────────────────────────────────────────────────────────┘
```

### Dependency Rule

Dependencies flow **inward only**. No inner layer knows anything about the layer referencing it.

```
API  →  Application  →  Domain
              ↑
       Infrastructure
```

This is enforced at compile time through `<ProjectReference>` entries in each `.csproj`. If you add the wrong reference, the build breaks — not just a linter warning.

### Hexagonal (Ports & Adapters) Mapping

| Hexagonal Concept | Clean Architecture Layer | Concrete Example |
|-------------------|--------------------------|------------------|
| Primary Port | Application (interface) | Handler interfaces via Mediator |
| Primary Adapter | API (controller) | `PersonsController` dispatches commands |
| Secondary Port | Application (interface) | `IPersonRepository` in `Application/Ports/` |
| Secondary Adapter | Infrastructure | `PersonRepository : IPersonRepository` |

---

## Tech Stack

| Technology | Version | Role |
|------------|---------|------|
| .NET / C# | 10 / 14 | Runtime and language |
| ASP.NET Core | 10.0 | Web host and controllers |
| EF Core InMemory | 10.0.8 | Zero-setup persistence for local dev |
| Mediator (martinothamar) | 3.0.2 | Source-generated CQRS dispatcher (MIT, no reflection) |
| FluentValidation | 12.1.1 | Application-layer request validation |
| Microsoft.AspNetCore.OpenApi | 10.0.8 | OpenAPI 3.1 document generation |
| Scalar.AspNetCore | 2.14.14 | Interactive API explorer (replaces Swagger UI) |
| xUnit | 2.9.3 | Unit test framework |

> **Why Mediator over MediatR?** MediatR 13+ requires a commercial license. Mediator 3.x is MIT, source-generated (zero reflection overhead), and uses a MediatR-compatible API surface.

> **Why no AutoMapper?** AutoMapper v15+ is commercial. For a 4-field entity with one computed property, a 20-line static factory method (`PersonResponse.FromDomain`) is faster, more debuggable, and has zero dependency risk.

---

## Project Structure

```
PersonsAPI/
├── src/
│   ├── PersonsAPI.Domain/               # Zero dependencies — pure domain model
│   │   ├── Entities/
│   │   │   └── Person.cs               # Rich entity: private setters, factory, update methods
│   │   └── Exceptions/
│   │       └── DomainException.cs      # Thrown by invariant guards inside the domain
│   │
│   ├── PersonsAPI.Application/          # Depends on Domain only
│   │   ├── Ports/
│   │   │   └── IPersonRepository.cs    # Secondary port — implemented by Infrastructure
│   │   ├── Commands/
│   │   │   ├── CreatePersonCommand.cs  # Command + Validator + Handler in one file
│   │   │   ├── UpdatePersonCommand.cs
│   │   │   ├── PatchPersonCommand.cs
│   │   │   └── DeletePersonCommand.cs
│   │   ├── Queries/
│   │   │   ├── GetAllPersonsQuery.cs
│   │   │   └── GetPersonByIdQuery.cs
│   │   ├── DTOs/
│   │   │   ├── PersonResponse.cs       # Static FromDomain factory — no AutoMapper
│   │   │   ├── CreatePersonRequest.cs
│   │   │   ├── UpdatePersonRequest.cs
│   │   │   └── UpdatePersonDto.cs      # Nullable fields for PATCH semantics
│   │   ├── Behaviors/
│   │   │   └── ValidationBehavior.cs   # FluentValidation pipeline — runs before every handler
│   │   ├── Exceptions/
│   │   │   └── PersonNotFoundException.cs
│   │   └── ServiceCollectionExtensions.cs  # AddApplication() — registers validators only
│   │
│   ├── PersonsAPI.Infrastructure/       # Depends on Application + Domain [Phase 3]
│   │   └── (pending)
│   │
│   └── PersonsAPI.Api/                  # Depends on all layers [Phase 4]
│       └── (pending)
│
└── tests/
    ├── PersonsAPI.Domain.Tests/
    └── PersonsAPI.Application.Tests/
```

### File colocation rule

Each CQRS file contains the **command/query record + validator + handler** as a single unit. This is intentional: searching for `UpdatePersonCommand` takes you to everything related to that use case — not three separate files across three folders.

---

## Domain Model

`Person` is a **rich domain entity**. Business logic lives inside it, not in handlers or services.

```csharp
// The ONLY way to create a valid Person
var person = Person.Create(
    firstName:        "María",
    paternalLastName: "García",
    maternalLastName: "López",
    dateOfBirth:      new DateOnly(1990, 6, 15));

// Intention-revealing update methods — no direct property assignment
person.UpdateName("María José", "García", "López");
person.UpdateDateOfBirth(new DateOnly(1991, 3, 20));

// Age is computed on access — never stored in the database
int age = person.Age;
```

### Field invariants (enforced inside the entity, not in handlers)

| Field | Rules |
|-------|-------|
| `FirstName` | Not empty, 2–100 characters |
| `PaternalLastName` | Not empty, 2–100 characters |
| `MaternalLastName` | Not empty, 2–100 characters |
| `DateOfBirth` | Not in the future, not more than 150 years in the past |
| `Age` | Computed from `DateOfBirth` using month-and-day-aware algorithm — never stored |

---

## API Contract

Base URL: `http://localhost:5000/api`

| Method | Path | Description | Success | Error |
|--------|------|-------------|---------|-------|
| `GET` | `/persons` | List all persons | `200 OK` | — |
| `GET` | `/persons/{id}` | Get person by ID | `200 OK` | `404` |
| `POST` | `/persons` | Create a person | `201 Created` + `Location` header | `400` |
| `PUT` | `/persons/{id}` | Full replace | `200 OK` | `400`, `404` |
| `PATCH` | `/persons/{id}` | Partial update | `200 OK` | `400`, `404` |
| `DELETE` | `/persons/{id}` | Delete a person | `204 No Content` | `404` |

All error responses follow **RFC 9457 Problem Details** (`application/problem+json`). No custom error envelopes.

### Response shape

```json
{
  "id": 1,
  "firstName": "María",
  "paternalLastName": "García",
  "maternalLastName": "López",
  "dateOfBirth": "1990-06-15",
  "age": 35
}
```

### Create / Update request body

```json
{
  "firstName": "María",
  "paternalLastName": "García",
  "maternalLastName": "López",
  "dateOfBirth": "1990-06-15"
}
```

### PATCH request body (only include fields you want to change)

```json
{
  "firstName": "María José",
  "dateOfBirth": null
}
```

---

## Request Flow

Trace an HTTP request from controller to database and back:

```
HTTP POST /api/persons
    │
    ▼
PersonsController.Create(CreatePersonRequest dto)
    │  maps dto → CreatePersonCommand
    ▼
mediator.Send(CreatePersonCommand)
    │
    ▼  (pipeline behavior runs first)
ValidationBehavior<CreatePersonCommand, PersonResponse>
    │  runs CreatePersonCommandValidator
    │  → invalid? throws FluentValidation.ValidationException → 400
    │  → valid? passes through
    ▼
CreatePersonHandler.Handle(command)
    │  calls Person.Create(...)     ← domain invariants validated here too
    │  calls repository.AddAsync()
    ▼
PersonRepository (Infrastructure)
    │  EF Core InMemory store
    ▼
PersonResponse.FromDomain(person)   ← manual static mapping, no AutoMapper
    │
    ▼
HTTP 201 Created  +  Location: /api/persons/42
```

**Two validation layers on purpose:**
- Application layer (`FluentValidation`) — produces field-level 400 detail for the API consumer
- Domain layer (`Person.Create`) — enforces invariants regardless of who calls it, second line of defense

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- No database setup required — EF Core InMemory provider is used

### Build

```bash
dotnet build PersonsAPI.sln
```

### Run (available after Phase 4)

```bash
dotnet run --project src/PersonsAPI.Api/PersonsAPI.Api.csproj
```

Then open `http://localhost:5000/scalar` for the interactive API explorer.

### Restore dependencies

```bash
dotnet restore PersonsAPI.sln
```

---

## Running Tests

```bash
# All tests
dotnet test PersonsAPI.sln

# Domain layer tests only
dotnet test tests/PersonsAPI.Domain.Tests/PersonsAPI.Domain.Tests.csproj

# Application layer tests only
dotnet test tests/PersonsAPI.Application.Tests/PersonsAPI.Application.Tests.csproj
```

### What is tested

| Test project | Coverage |
|-------------|---------|
| `PersonsAPI.Domain.Tests` | `Person.Create` invariants, `Age` computation, `UpdateName`/`UpdateDateOfBirth` guards |
| `PersonsAPI.Application.Tests` | `ValidationBehavior` (D-10 short-circuit, pass-through, failure), `CreatePersonCommandValidator`, `PatchPersonCommandValidator`, `PersonResponse.FromDomain` mapping |

Tests do **not** mock the domain — validators and handlers run against the real domain entity. What gets mocked: `IPersonRepository` (the secondary port), since the Infrastructure layer doesn't exist yet in Phases 1–2.

---

## Build Status

| Layer | Status | Tests |
|-------|--------|-------|
| Domain | Complete | 15 passing |
| Application | Complete | 15 passing |
| Infrastructure | In progress | — |
| API | Pending | — |

---

## Key Design Decisions

### CQRS without a shared "service" class

There are no `PersonService`, `PersonManager`, or `PersonApplicationService` classes. Each use case is a self-contained handler. This is deliberate: a `PersonService` with 6 methods is an anemic service — it puts business logic in the wrong layer and creates an implicit coupling between all operations on a resource.

### `IPersonRepository` lives in Application, not Infrastructure

The port (interface) belongs to the layer that *uses* it, not the layer that *implements* it. This is core to Hexagonal Architecture: the Application layer defines the contract, Infrastructure is plugged in from the outside. If `IPersonRepository` lived in Infrastructure, Application would depend on Infrastructure — inverting the entire dependency graph.

### `ValidationBehavior` short-circuits on no-validator (D-10)

Queries (`GetAllPersonsQuery`, `GetPersonByIdQuery`) and `DeletePersonCommand` have no registered validators by design. `ValidationBehavior` checks for an empty validator list first and forwards immediately — zero overhead for requests that don't need validation.

### PATCH uses `UpdatePersonDto` (nullable fields), not `UpdatePersonRequest`

`UpdatePersonRequest` has non-nullable fields — it's designed for PUT (full replace). Using it for PATCH would require sending every field even when only one changes. `UpdatePersonDto` has `string?` and `DateOnly?` fields: null means "don't change this field." The handler applies the `dto.Field ?? person.Field` null-fallback pattern.

### Age is never stored

`Person.Age` is a computed property that runs on every access. EF Core will ignore it via `builder.Ignore(p => p.Age)` in Phase 3. This keeps the domain model as the single source of truth for age calculation — no risk of stale cached values in the database.

---

## What NOT To Use Here

These are explicit exclusions with reasons. Adding them would be a regression:

| Package / Pattern | Why Excluded |
|-------------------|-------------|
| **MediatR 13+** | Commercial license (paid key required from Jimmy Bogard). Use Mediator 3.0.2 (MIT) |
| **AutoMapper v15+** | Commercial license (RPL-1.5, April 2025). Use `PersonResponse.FromDomain` static factory |
| **Swashbuckle** | Dropped from .NET 9+ template. Use `Microsoft.AspNetCore.OpenApi` + Scalar |
| **Generic `IRepository<T>`** | Leaks EF Core abstractions, adds indirection without benefit. Use `IPersonRepository` directly |
| **`IPersonService` with 6 methods** | Anemic service anti-pattern — business logic escapes the domain, handlers become thin wrappers |
| **Minimal API endpoints** | Explicitly out of scope — controllers only, per project constraints |
| **`DataAnnotations` on DTOs** | Validation belongs in the Application layer (FluentValidation), not on the DTO |

---

*Maintained as a living document — updated as each phase completes.*
