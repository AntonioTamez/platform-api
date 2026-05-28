<!-- GSD:project-start source:PROJECT.md -->

## Project

**PersonsAPI**

A .NET 10 Web API built with controllers (not Minimal API) that demonstrates Clean Architecture and Hexagonal Architecture applied together. The API manages personal data (first name, paternal last name, maternal last name, date of birth with calculated age) using EF Core InMemory provider for simulation. Designed as a learning exercise to internalize how these two architectural patterns coexist in a .NET project.

**Core Value:** A correctly layered, richly modeled API that proves Clean and Hexagonal Architecture work together — where the domain drives everything and infrastructure is a detail.

### Constraints

- **Framework**: .NET 10 — use latest C# features where they clarify intent
- **API style**: Controllers only — no Minimal API endpoints
- **Domain**: Rich models — business logic lives in the domain entity, not services
- **Language**: All identifiers, comments, and documentation in English

<!-- GSD:project-end -->

<!-- GSD:stack-start source:research/STACK.md -->

## Technology Stack

## Recommended Stack

### Core Framework

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET 10 | 10.0 (LTS) | Runtime and SDK | Required by project; LTS release through Nov 2027 |
| ASP.NET Core | 10.0 | Web host and controllers | Built into .NET 10 SDK, no separate package |
| C# 14 | ships with .NET 10 | Language | Newest features available with .NET 10 SDK |

### Data Access

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Microsoft.EntityFrameworkCore | 10.0.8 | EF Core ORM base | Teaching full EF patterns without real DB server |
| Microsoft.EntityFrameworkCore.InMemory | 10.0.8 | In-memory persistence | Zero-setup simulation as required; acceptable for learning scope |

### Validation

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| FluentValidation | 12.1.1 | Request/command validation | Strongly typed, expressive rules; stays in Application layer |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Bulk validator registration | `AddValidatorsFromAssembly()` scans the Application assembly automatically |

### CQRS / Mediator

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Mediator.SourceGenerator + Mediator.Abstractions | 3.0.2 (stable) | CQRS dispatcher | Source-generator based, free MIT license, MediatR-compatible API, zero reflection overhead. MediatR 13+ requires a commercial license key — not acceptable for a learning project without purchasing or registering. MediatR 12.5 (Apache) is an acceptable fallback if the team already knows it. |

### Object Mapping

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Manual mapping (static extension methods or factory methods) | n/a | Domain entity → DTO → Response | For a 4-field entity with one computed property, a 20-line static method is faster, more debuggable, and carries zero dependency risk. AutoMapper v15+ is commercial. Mapperly (4.3.1) is the correct automated alternative if the entity count grows. |

### API Documentation

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Microsoft.AspNetCore.OpenApi | 10.0.8 | OpenAPI 3.1 document generation | First-party, ships with .NET 10 SDK, no reflection gymnastics. Swashbuckle was removed from the `dotnet new webapi` template in .NET 9 and is no longer the recommended path. |
| Scalar.AspNetCore | 2.14.14 | Interactive API explorer UI | Modern Swagger UI replacement; dark mode by default, better request visualization. Pair with Microsoft.AspNetCore.OpenApi. |

### Supporting Libraries (Optional / Conditional)

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Ardalis.GuardClauses | 5.0.0 | Guard clause helpers in domain/app layer | Use if you want concise `Guard.Against.Null(x)` calls instead of inline throws. Optional — a plain `if (x is null) throw` is equally valid for a learning project. |
| Riok.Mapperly | 4.3.1 | Source-generated object mapping | Introduce only if the domain grows beyond 2-3 entities and manual mapping becomes repetitive. Free, MIT, compile-time safe, AOT-compatible. |

## What NOT to Use and Why

### AutoMapper

### MediatR 13+

### Anemic Service Classes

### Generic Repository Pattern Over IApplicationDbContext

### Swashbuckle.AspNetCore

### Minimal API

## Project Structure

### Solution Layout

### Layer Dependency Rule

### Ports and Adapters Mapping to Layers

| Hexagonal Concept | Clean Architecture Layer | Example in PersonsAPI |
|-------------------|-------------------------|-----------------------|
| Primary Port | Application (interface) | `IPersonService` or CQRS handler interface |
| Primary Adapter | Api (controller) | `PersonsController` calls Application handler |
| Secondary Port | Application or Domain (interface) | `IPersonRepository` or `IApplicationDbContext` |
| Secondary Adapter | Infrastructure (implementation) | `PersonRepository : IPersonRepository` using `AppDbContext` |

### Recommended Project File Names

## C# 14 / .NET 10 Features That Benefit This Architecture

### `field` Backed Properties — Use in Domain Entities

### Primary Constructors — Use in Application Service / Handler Classes

### Extension Members — Use for Domain Enrichment Without Pollution

### Null-Conditional Assignment — Use in Patch Operations

### Records — Use for DTOs and Commands/Queries

## Installation

### Application layer

### Infrastructure layer

### Api layer

### Optional (add only when needed)

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Mediator | Mediator 3.0.2 (martinothamar) | MediatR 12.5 (Apache, frozen) | MediatR 12.5 is acceptable fallback but frozen. MediatR 13+ requires commercial license. |
| Mapping | Manual static methods | Mapperly 4.3.1 | Mapperly is the right automated choice if entity count grows; overkill for one entity. |
| Mapping | Manual static methods | AutoMapper | Commercial (v15+), hides intent, encourages anemic services. |
| API Docs | Microsoft.AspNetCore.OpenApi + Scalar | Swashbuckle | Swashbuckle dropped from template in .NET 9, generates OpenAPI 3.0, third-party risk. |
| Repository | IApplicationDbContext interface | Generic IRepository<T> | Generic repo adds indirection without benefit; IApplicationDbContext preserves the EF surface area. |
| Validation | FluentValidation 12.1.1 | DataAnnotations | DataAnnotations live on the DTO, not the Application layer — wrong conceptual home. FluentValidation keeps validation logic in the Application layer where it belongs. |

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| EF Core InMemory version | HIGH | Verified directly on NuGet (10.0.8 confirmed) |
| FluentValidation version | HIGH | Verified on NuGet (12.1.1); confirmed .NET 10 support |
| FluentValidation v12 DI pattern | HIGH | Official docs confirm manual injection; FluentValidation.AspNetCore deprecated |
| Mediator (martinothamar) | HIGH | Verified on NuGet (3.0.2 stable); MIT license confirmed |
| MediatR licensing | HIGH | Jimmy Bogard official blog + GitHub discussion confirmed commercial from v13 |
| AutoMapper licensing | HIGH | Confirmed commercial from v15 (RPL-1.5); published April 2025 |
| Scalar / OpenApi recommendation | HIGH | Multiple sources + Microsoft template changes confirm |
| C# 14 features | HIGH | Verified against official Microsoft Learn docs (updated 2025-11-18) |
| Mapperly version | HIGH | Verified on NuGet (4.3.1) |
| Project structure (4-layer Clean) | HIGH | Consistent across ardalis template, codewithmukesh, c-sharpcorner; well-established |

## Sources

- [NuGet: Microsoft.EntityFrameworkCore.InMemory 10.0.8](https://www.nuget.org/packages/microsoft.entityframeworkcore.inmemory)
- [NuGet: FluentValidation 12.1.1](https://www.nuget.org/packages/fluentvalidation/)
- [NuGet: Scalar.AspNetCore 2.14.14](https://www.nuget.org/packages/Scalar.AspNetCore)
- [NuGet: Mediator.SourceGenerator 3.0.2](https://www.nuget.org/packages/Mediator.SourceGenerator/)
- [NuGet: Riok.Mapperly 4.3.1](https://www.nuget.org/packages/Riok.Mapperly)
- [What's new in C# 14 — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14)
- [FluentValidation ASP.NET Core docs — manual validation](https://docs.fluentvalidation.net/en/latest/aspnet.html)
- [Implementing Clean Architecture in .NET 10 — codewithmukesh](https://codewithmukesh.com/blog/clean-architecture-dotnet/)
- [AutoMapper and MediatR Licensing Update — Jimmy Bogard](https://www.jimmybogard.com/automapper-and-mediatr-licensing-update/)
- [Stop Conflating CQRS and MediatR — Milan Jovanovic](https://www.milanjovanovic.tech/blog/stop-conflating-cqrs-and-mediatr)
- [Swashbuckle Is Dead. Migrate to Scalar in .NET 10](https://dev.to/jfmeyers/swashbuckle-is-dead-heres-how-to-migrate-to-scalar-in-net-10-155d)
- [ASP.NET Core Dropped Swagger — codewithmukesh](https://codewithmukesh.com/blog/dotnet-swagger-alternatives-openapi/)
- [EF Core InMemory limitations — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/in-memory/)
- [Hexagonal Architecture with .NET — Francesco Del Re](https://engineering87.github.io/2025/07/19/exagonal-architecture.html)
- [ardalis/CleanArchitecture GitHub — ASP.NET Core 10 template](https://github.com/ardalis/CleanArchitecture)
- [Best Free Alternatives to AutoMapper (Mapperly) — ABP.IO](https://abp.io/community/articles/best-free-alternatives-to-automapper-in-.net-why-we-moved-to-mapperly-l9f5ii8s)

<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->

## Conventions

Conventions not yet established. Will populate as patterns emerge during development.
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->

## Architecture

Architecture not yet mapped. Follow existing patterns found in the codebase.
<!-- GSD:architecture-end -->

<!-- GSD:skills-start source:skills/ -->

## Project Skills

No project skills found. Add skills to any of: `.claude/skills/`, `.agents/skills/`, `.cursor/skills/`, `.github/skills/`, or `.codex/skills/` with a `SKILL.md` index file.
<!-- GSD:skills-end -->

<!-- GSD:workflow-start source:GSD defaults -->

## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:

- `/gsd-quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd-debug` for investigation and bug fixing
- `/gsd-execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->

<!-- GSD:profile-start -->

## Developer Profile

> Profile not yet configured. Run `/gsd-profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
