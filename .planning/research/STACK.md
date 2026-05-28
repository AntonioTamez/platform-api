# Technology Stack

**Project:** PersonsAPI — .NET 10 Clean + Hexagonal Architecture Web API
**Researched:** 2026-05-27
**Research mode:** Ecosystem

---

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

---

## What NOT to Use and Why

### AutoMapper

AutoMapper v15.0+ requires a commercial license (RPL-1.5 or paid). Even older free versions are the wrong choice here: AutoMapper hides what your mapping does, makes debugging hard, and encourages anemic service classes that exist only to call `.Map<T>()`. For a domain with one rich entity, manual mapping is clearer and teaches the concepts better.

**Instead:** Write a `PersonResponse PersonResponse.From(Person person)` factory method or a `ToResponse()` extension method.

### MediatR 13+

MediatR 13.0+ requires a registered license key at startup. It is no longer free software for anything beyond community/individual use. MediatR 12.5.x (Apache 2.0) remains free but is frozen. `Mediator` by martinothamar is the recommended drop-in replacement: same `IRequest<T>` / `IRequestHandler<T,R>` API, source-generated dispatch (no reflection), MIT licensed, benchmarked 4x faster.

**If the team insists on MediatR:** pin to `12.5.0` explicitly. Do not upgrade to 13+.

### Anemic Service Classes

A service class that takes a `PersonRepository`, reads a person, sets `person.FirstName = dto.FirstName`, and calls `Save()` is anemic. All setters are public, all logic is outside the entity. This is the pattern this project explicitly prohibits. Business logic (Age calculation, name invariants) lives in `Person` itself via private setters and behavior methods.

### Generic Repository Pattern Over IApplicationDbContext

Wrapping `DbContext` in a `IRepository<T>` interface adds a layer that gives you nothing in a project with one in-memory provider. The recommended pattern (per ardalis Clean Architecture template and codewithmukesh) is to expose an `IApplicationDbContext` interface (defined in Application, implemented by `AppDbContext` in Infrastructure). This preserves the dependency rule without the leaky abstraction of a generic repository.

### Swashbuckle.AspNetCore

Swashbuckle was removed from the official `dotnet new webapi` template starting .NET 9. It generates OpenAPI 3.0 (not 3.1), its maintenance has slowed, and it is a third-party dependency you no longer need. Use `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` instead.

### Minimal API

Explicitly out of scope per project requirements. Controllers map more naturally to Clean Architecture's Presentation layer and are easier to reason about in this learning context.

---

## Project Structure

### Solution Layout

```
PersonsAPI.sln
src/
  PersonsAPI.Domain/           → Zero NuGet dependencies. Entities, ports (interfaces), value objects.
  PersonsAPI.Application/      → Depends on Domain only. Commands, queries, handlers, DTOs, validation.
  PersonsAPI.Infrastructure/   → Depends on Application. DbContext, repository adapters, EF configuration.
  PersonsAPI.Api/              → Depends on Application + Infrastructure. Controllers, DI wiring, Program.cs.
tests/
  PersonsAPI.UnitTests/        → Tests Domain + Application in isolation (no I/O).
  PersonsAPI.IntegrationTests/ → Tests the full stack with InMemory EF or WebApplicationFactory.
```

### Layer Dependency Rule

```
Api  →  Infrastructure  →  Application  →  Domain
```

Domain has ZERO outbound dependencies. Application references Domain only. Infrastructure and Api reference Application (and indirectly Domain). No layer references a layer above it in the chain.

### Ports and Adapters Mapping to Layers

| Hexagonal Concept | Clean Architecture Layer | Example in PersonsAPI |
|-------------------|-------------------------|-----------------------|
| Primary Port | Application (interface) | `IPersonService` or CQRS handler interface |
| Primary Adapter | Api (controller) | `PersonsController` calls Application handler |
| Secondary Port | Application or Domain (interface) | `IPersonRepository` or `IApplicationDbContext` |
| Secondary Adapter | Infrastructure (implementation) | `PersonRepository : IPersonRepository` using `AppDbContext` |

### Recommended Project File Names

```
PersonsAPI.Domain.csproj
PersonsAPI.Application.csproj       <PackageReference Include="FluentValidation" />
                                     <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
                                     <PackageReference Include="Mediator.Abstractions" />
PersonsAPI.Infrastructure.csproj    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
                                     <PackageReference Include="Mediator.SourceGenerator" />
PersonsAPI.Api.csproj               <PackageReference Include="Microsoft.AspNetCore.OpenApi" />
                                     <PackageReference Include="Scalar.AspNetCore" />
```

The Domain project references nothing. The Application project references only FluentValidation and the Mediator abstractions. Infrastructure references EF Core and the source generator. Api references the UI/OpenAPI packages.

---

## C# 14 / .NET 10 Features That Benefit This Architecture

### `field` Backed Properties — Use in Domain Entities

The `field` keyword lets you add validation logic to a property setter without declaring a separate backing field. This is valuable in rich domain entities where you need to enforce invariants on assignment.

```csharp
// C# 14 — field keyword
public class Person
{
    public string FirstName
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("FirstName cannot be blank.")
            : value.Trim();
    }
}
```

This eliminates the boilerplate of a `private string _firstName` declaration while keeping encapsulation. **Confidence: HIGH** (official Microsoft docs, ships with .NET 10).

### Primary Constructors — Use in Application Service / Handler Classes

Primary constructors reduce DI injection boilerplate in handler classes. They are already available for classes since C# 12 but are worth standardizing on for all Application layer handlers.

```csharp
// Primary constructor — no private field declarations needed
public sealed class GetPersonByIdHandler(IApplicationDbContext db)
    : IRequestHandler<GetPersonByIdQuery, PersonResponse?>
{
    public async ValueTask<PersonResponse?> Handle(GetPersonByIdQuery request, CancellationToken ct)
        => await db.Persons.Where(p => p.Id == request.Id)
                   .Select(p => PersonResponse.From(p))
                   .FirstOrDefaultAsync(ct);
}
```

**Confidence: HIGH** (available since C# 12, confirmed stable in C# 14).

### Extension Members — Use for Domain Enrichment Without Pollution

C# 14 adds extension properties and static extension members. You can add computed properties (e.g., `Age`) as extension properties on `Person` if you want to keep the entity pure (no calculated field in the entity itself). This is a minor design choice — the project currently puts Age calculation inside the entity, which is also valid.

```csharp
// C# 14 extension property
extension(Person person)
{
    public int Age => DateTime.Today.Year - person.DateOfBirth.Year
        - (DateTime.Today.DayOfYear < person.DateOfBirth.DayOfYear ? 1 : 0);
}
```

**Confidence: HIGH** (official Microsoft docs for C# 14, ships with .NET 10).

### Null-Conditional Assignment — Use in Patch Operations

The PATCH endpoint (partial update) will need to update only provided fields. C# 14 null-conditional assignment makes this concise and safe.

```csharp
// C# 14 — null-conditional assignment
person?.FirstName = dto.FirstName;  // only assigns if person is not null
```

**Confidence: HIGH** (official Microsoft docs for C# 14).

### Records — Use for DTOs and Commands/Queries

Records (available since C# 9) remain the idiomatic choice for immutable DTOs, commands, and queries in the Application layer. They provide value equality, `with` expressions, and concise declaration.

```csharp
public sealed record CreatePersonCommand(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth) : IRequest<PersonResponse>;
```

**Confidence: HIGH**.

---

## Installation

### Application layer

```xml
<PackageReference Include="FluentValidation" Version="12.1.1" />
<PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="12.1.1" />
<PackageReference Include="Mediator.Abstractions" Version="3.0.2" />
```

### Infrastructure layer

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.8" />
<PackageReference Include="Mediator.SourceGenerator" Version="3.0.2">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

### Api layer

```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.8" />
<PackageReference Include="Scalar.AspNetCore" Version="2.14.14" />
```

### Optional (add only when needed)

```xml
<PackageReference Include="Ardalis.GuardClauses" Version="5.0.0" />
<PackageReference Include="Riok.Mapperly" Version="4.3.1" />
```

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| Mediator | Mediator 3.0.2 (martinothamar) | MediatR 12.5 (Apache, frozen) | MediatR 12.5 is acceptable fallback but frozen. MediatR 13+ requires commercial license. |
| Mapping | Manual static methods | Mapperly 4.3.1 | Mapperly is the right automated choice if entity count grows; overkill for one entity. |
| Mapping | Manual static methods | AutoMapper | Commercial (v15+), hides intent, encourages anemic services. |
| API Docs | Microsoft.AspNetCore.OpenApi + Scalar | Swashbuckle | Swashbuckle dropped from template in .NET 9, generates OpenAPI 3.0, third-party risk. |
| Repository | IApplicationDbContext interface | Generic IRepository<T> | Generic repo adds indirection without benefit; IApplicationDbContext preserves the EF surface area. |
| Validation | FluentValidation 12.1.1 | DataAnnotations | DataAnnotations live on the DTO, not the Application layer — wrong conceptual home. FluentValidation keeps validation logic in the Application layer where it belongs. |

---

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

---

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
