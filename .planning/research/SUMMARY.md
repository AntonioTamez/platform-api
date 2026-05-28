# Project Research Summary

**Project:** PersonsAPI - .NET 10 Clean Architecture + Hexagonal Architecture Web API
**Domain:** REST API / Domain-Driven Design / Learning Reference
**Researched:** 2026-05-27
**Confidence:** HIGH

## Executive Summary

PersonsAPI is a controller-based .NET 10 Web API built to demonstrate Clean Architecture and Hexagonal Architecture applied together. The domain is one aggregate root (Person) with five fields, one computed property (Age), and full CRUD plus PATCH - so the architectural patterns are the learning subject. Experts build this with a 4-project solution: Domain, Application, Infrastructure, Api.

The recommended stack avoids three ecosystem traps. MediatR 13+ and AutoMapper 15+ moved to commercial licenses in 2025. The replacements are Mediator by martinothamar (MIT, source-generated) and manual static mapping respectively. Swashbuckle was removed from .NET 9/10 templates - Microsoft.AspNetCore.OpenApi plus Scalar.AspNetCore is the first-party replacement. PATCH requires Microsoft.AspNetCore.JsonPatch.SystemTextJson (not the legacy Newtonsoft package).

The primary risk is architectural discipline, not technical complexity. Three failure modes invalidate the learning goal: (1) an anemic Person entity with business logic in handlers, (2) EF Core leaking into Domain, and (3) IPersonRepository misplaced in Infrastructure instead of Application. Build layer-by-layer: Domain, Application, Infrastructure, Api.

## Key Findings

### Recommended Stack

The stack is deliberately minimal. .NET 10 with C# 14 provides the runtime. EF Core InMemory 10.0.8 provides realistic persistence patterns. FluentValidation 12.1.1 handles input validation via a ValidationBehavior pipeline. Mediator 3.0.2 (martinothamar, MIT) dispatches CQRS commands with source-generated dispatch. API documentation uses Microsoft.AspNetCore.OpenApi 10.0.8 plus Scalar.AspNetCore 2.14.14. No mapping library needed.

C# 14 features: field-backed properties for domain invariants, primary constructors in handler classes, records for commands/queries/DTOs, null-conditional assignment for PATCH operations.

**Core technologies:**
- **.NET 10 / ASP.NET Core 10 / C# 14**: Runtime, web host, language - LTS through Nov 2027
- **EF Core InMemory 10.0.8**: Persistence simulation - full DbContext/repository patterns with zero setup
- **Mediator 3.0.2 (martinothamar)**: CQRS dispatch - MIT licensed, source-generated; MediatR 13+ is commercial
- **FluentValidation 12.1.1**: Application-layer validation via AddValidatorsFromAssembly()
- **Microsoft.AspNetCore.OpenApi 10.0.8 + Scalar.AspNetCore 2.14.14**: First-party OpenAPI 3.1; Swashbuckle is off the recommended path
- **Microsoft.AspNetCore.JsonPatch.SystemTextJson**: PATCH support - System.Text.Json-native; do NOT use the Newtonsoft version in .NET 10
- **Manual static mapping**: PersonResponse.From(Person) factory method - AutoMapper 15+ is commercial (RPL-1.5)

**Key licensing decisions:**
- MediatR 13+ requires a commercial license key - use Mediator 3.0.2 (MIT, recommended) or pin MediatR to 12.5.0 (Apache 2.0, frozen)
- AutoMapper v15+ is commercial (RPL-1.5) - use manual mapping or Riok.Mapperly 4.3.1 (MIT) if entity count grows
- Swashbuckle removed from .NET 9/10 templates - use Microsoft.AspNetCore.OpenApi + Scalar.AspNetCore
- PATCH in .NET 10: use Microsoft.AspNetCore.JsonPatch.SystemTextJson, not the Newtonsoft-based package

**Explicitly excluded:** AutoMapper v15+, MediatR 13+, Swashbuckle, Generic IRepository<T>, Minimal API

### Expected Features

All CRUD operations plus PATCH are table stakes. Missing any one means the project does not demonstrate the full command/query separation pattern.

**Must have (table stakes):**
- GET all persons - baseline query demonstrating read path
- GET person by ID - 404 via Problem Details when not found
- POST person - validates invariants in domain, maps to CreatePersonCommand, returns 201 with Location header
- PUT person - full replacement update via UpdatePersonCommand
- PATCH person - JSON Patch (RFC 6902) via Microsoft.AspNetCore.JsonPatch.SystemTextJson; typed to mutable DTO, not the domain entity
- DELETE person - 204 No Content or 404
- Computed Age property - DateOnly with month+day comparison; never stored, never a DB column
- Seeded in-memory data - at least 3-5 persons for immediate demo
- Rich domain model - private constructor + private setters + intention-revealing update methods
- EF Core InMemory persistence - full DbContext / repository pattern
- Controller-based API with [ApiController] - automatic model-state and Problem Details wiring
- Problem Details (RFC 7807/9457) - AddProblemDetails() + UseExceptionHandler() + UseStatusCodePages()
- CQRS via Mediator - command/query separation, one handler per use case
- FluentValidation as ValidationBehavior<TRequest, TResponse> pipeline behavior
- Global IExceptionHandler - maps unhandled exceptions to Problem Details

**Should have (learning differentiators):** Result<T>/ErrorOr pattern, logging behavior, port/adapter naming conventions in code comments

**Defer to v2+:** Value objects, authentication/authorization, pagination, Unit of Work, test projects

### Architecture Approach

The solution combines Clean Architecture four-ring layering with Hexagonal Architecture port/adapter vocabulary. Four projects with compiler-enforced dependency direction: Domain (zero outbound references) -> Application -> Infrastructure, with Api -> Application + Infrastructure (composition root only). IPersonRepository belongs in Application, not Domain or Infrastructure.

**Major components:**
1. **PersonsAPI.Domain** - Person entity (private setters, computed Age, private Person() {} for EF); zero NuGet dependencies
2. **PersonsAPI.Application** - Commands/queries/handlers, IPersonRepository port in Ports/, PersonDto + request DTOs, FluentValidation, ValidationBehavior, AddApplication()
3. **PersonsAPI.Infrastructure** - AppDbContext, PersonEntityConfiguration (builder.Ignore(p => p.Age)), PersonRepository, DataSeeder, AddInfrastructure()
4. **PersonsAPI.Api** - PersonsController (depends only on ISender), Program.cs composition root, OpenAPI + Scalar, Problem Details middleware

**Key data flow rules:**
- Domain entities: Domain to Application (inside handlers only); never reach the Api layer
- DTOs: Application to Api (controller responses)
- EF Core types (DbContext, DbSet): Infrastructure only - never cross into Application or Domain
- Mediator types (IRequest, IRequestHandler): Application only - never in Domain
- builder.Ignore(p => p.Age) required in PersonEntityConfiguration - EF must not persist the computed property
- EF Core must be zero-reference in the Domain project (no Microsoft.EntityFrameworkCore PackageReference)

### Critical Pitfalls

1. **Anemic Person entity** - Public setters, business logic in handlers. Prevention: private constructor + private set + private Person() {} for EF + UpdateName/UpdateDateOfBirth methods with guards.

2. **EF Core leaking into Domain** - [Key], [Column], [Required] annotations on Person.cs; or IPersonRepository returning IQueryable<Person>. Prevention: zero EF Core PackageReference in Domain.csproj; Fluent API only; repository returns Task<IReadOnlyList<Person>>.

3. **Port interface in wrong layer** - IPersonRepository in Domain or Infrastructure. Prevention: Application defines ports; Infrastructure implements adapters. Application never references Infrastructure.

4. **Wrong Age calculation** - Year subtraction only without month+day comparison. Prevention: use DateOnly; subtract 1 if DateOfBirth.Month > today.Month or (same month and DateOfBirth.Day > today.Day).

5. **PATCH typed to domain entity** - JsonPatchDocument<Person> forces public setters. Prevention: JsonPatchDocument<UpdatePersonDto>; ModelState passed to ApplyTo(); validate after ApplyTo() not before.

6. **Missing private EF constructor** - No private Person() {} causes InvalidOperationException when EF materializes entities.

## Implications for Roadmap

The natural build order follows layer dependencies. Each phase produces a compilable artifact the next phase references. This order is non-negotiable.

### Phase 1: Domain Foundation

**Rationale:** Domain has zero outbound dependencies and is the contract everything else references. Highest-risk phase - mistakes here propagate into every layer. Build and verify in isolation.

**Delivers:** Person entity - private constructor, private set on all properties, computed Age (DateOnly month+day), UpdateName/UpdateDateOfBirth methods, private Person() {} for EF. PersonsAPI.Domain.csproj: zero NuGet PackageReferences.

**Addresses:** Rich domain model requirement, calculated Age requirement

**Avoids:** Pitfall 1 (anemic entity), Pitfall 2 (business logic in Application), Pitfall 7 (wrong Age algorithm), Pitfall 10 (missing EF constructor)

**Research flag:** None - rich domain entity patterns are extensively documented at HIGH confidence.

### Phase 2: Application Layer (Ports, Commands, Queries, DTOs, Validation)

**Rationale:** Application defines port interfaces and use-case contracts. Handlers can be skeletal. Goal: establish all types that Infrastructure and Api depend on, with IPersonRepository in Application/Ports/.

**Delivers:** IPersonRepository (Application/Ports/), all command/query records (CreatePersonCommand, UpdatePersonCommand, PatchPersonCommand, DeletePersonCommand, GetAllPersonsQuery, GetPersonByIdQuery), all DTOs (PersonDto, CreatePersonRequest, UpdatePersonRequest, PatchPersonRequest), FluentValidation validators, ValidationBehavior<TRequest, TResponse>, AddApplication().

**Uses:** Mediator.Abstractions 3.0.2, FluentValidation 12.1.1, FluentValidation.DependencyInjectionExtensions 12.1.1

**Avoids:** IPersonRepository in wrong layer, circular project references (Application references only Domain)

**Research flag:** None - CQRS handler patterns and FluentValidation pipeline behaviors are thoroughly documented.

### Phase 3: Infrastructure Layer (EF Core Adapter + Repository)

**Rationale:** Infrastructure implements Application ports. All EF Core configuration lives here. InMemory provider enables immediate end-to-end testing.

**Delivers:** AppDbContext with DbSet<Person>, PersonEntityConfiguration (Fluent API + builder.Ignore(p => p.Age)), PersonRepository implementing IPersonRepository (returns Task<IReadOnlyList<Person>>), DataSeeder (3-5 persons), AddInfrastructure().

**Uses:** Microsoft.EntityFrameworkCore.InMemory 10.0.8, Mediator.SourceGenerator 3.0.2 (analyzer-only reference)

**Avoids:** EF annotations in Domain, IQueryable leakage, Singleton DbContext anti-pattern

**Research flag:** None - EF Core Fluent API and InMemory provider are official Microsoft patterns at HIGH confidence.

### Phase 4: Api Layer (Controllers, Composition Root, OpenAPI, PATCH)

**Rationale:** Api is built last - it wires all other layers. Controllers depend only on ISender. Program.cs is the sole composition root. Build read path first, then writes, PATCH last.

**Delivers:** PersonsController (6 endpoints), Program.cs composition root, Problem Details plumbing, global IExceptionHandler, OpenAPI + Scalar registration. HTTP semantics: 200 GET / 201 POST with Location / 204 DELETE / 400 validation / 404 not found.

**Uses:** Microsoft.AspNetCore.OpenApi 10.0.8, Scalar.AspNetCore 2.14.14, Microsoft.AspNetCore.JsonPatch.SystemTextJson

**Avoids:** Controller as port (driving adapter only), PATCH typed to domain entity, business logic in controllers

**Research flag:** PATCH needs care. JsonPatchDocument<T> must target UpdatePersonDto (not Person). ModelState must be passed to ApplyTo(); validate after ApplyTo() not before. Microsoft.AspNetCore.JsonPatch.SystemTextJson does not support dynamic types.

### Phase Ordering Rationale

- **Layer dependencies make this order non-negotiable.** Any other sequence produces unresolvable project references at compile time.
- **Pitfall concentration justifies building Domain in isolation.** Highest-density pitfall zone is Phase 1.
- **PATCH is last in Phase 4.** Most complex write operation; depends on simpler operations being confirmed working.
- **Test projects deferred.** Named future phase after full CRUD surface is working.

### Research Flags

Phases needing careful implementation attention:
- **Phase 4 (PATCH):** Multiple documented failure modes. Consider a dedicated research-phase pass on PATCH mechanics before implementation.

Phases with well-documented standard patterns (no additional research needed):
- **Phase 1 (Domain):** Rich entity patterns, DateOnly age calculation, private EF constructors - HIGH confidence.
- **Phase 2 (Application):** Mediator 3.0.2 + FluentValidation pipeline - stable, standard patterns.
- **Phase 3 (Infrastructure):** EF Core Fluent API + InMemory - official Microsoft patterns.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Package versions verified on NuGet; licensing verified via official announcements; Swashbuckle removal confirmed by Microsoft template changes |
| Features | HIGH | Aligns with official .NET 10 docs; PATCH package verified against Microsoft Learn JsonPatch docs (.NET 10) |
| Architecture | HIGH | Verified against Herberto Graca, Microsoft DDD/CQRS eBook (April 2026), Jason Taylor template, codewithmukesh .NET 10 guide |
| Pitfalls | HIGH | Verified against multiple independent .NET-specific sources and official EF Core docs |

**Overall confidence:** HIGH

### Gaps to Address

- **Mediator 3.0.2 vs. MediatR 12.5 final decision:** Decide before Phase 2. Mediator recommended (MIT, source-generated). MediatR 12.5.0 (Apache 2.0, frozen) is acceptable fallback. Pipeline behavior registration differs slightly.
- **DateOnly + EF Core InMemory compatibility:** Supported since EF Core 8. Confirm with a smoke test during Phase 3.
- **Microsoft.AspNetCore.JsonPatch.SystemTextJson concrete type requirement:** Does not support dynamic types. Confirm UpdatePersonDto is a concrete class or record before Phase 4.

## Sources

### Primary (HIGH confidence)
- Microsoft Learn - C# 14: https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14
- Microsoft Learn - JSON Patch in ASP.NET Core (.NET 10): https://learn.microsoft.com/en-us/aspnet/core/web-api/jsonpatch?view=aspnetcore-10.0
- Microsoft Learn - Error handling ASP.NET Core (.NET 10): https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0
- Microsoft .NET Architecture Guide - EF Core infrastructure layer: https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core
- Microsoft Learn - DateOnly and TimeOnly: https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly
- NuGet - EF Core InMemory 10.0.8, FluentValidation 12.1.1, Mediator.SourceGenerator 3.0.2, Scalar.AspNetCore 2.14.14 (all verified)
- Herberto Graca - Explicit Architecture: https://herbertograca.com/2017/11/16/explicit-architecture-01-ddd-hexagonal-onion-clean-cqrs-how-i-put-it-all-together/
- Jason Taylor CleanArchitecture template: https://github.com/jasontaylordev/CleanArchitecture

### Secondary (MEDIUM confidence)
- Jimmy Bogard - AutoMapper and MediatR Licensing: https://www.jimmybogard.com/automapper-and-mediatr-licensing-update/
- codewithmukesh - Clean Architecture .NET 10: https://codewithmukesh.com/blog/clean-architecture-dotnet/
- codewithmukesh - CQRS and MediatR: https://codewithmukesh.com/blog/cqrs-and-mediatr-in-aspnet-core/
- Milan Jovanovic - Problem Details: https://www.milanjovanovic.tech/blog/problem-details-for-aspnetcore-apis
- Milan Jovanovic - CQRS + FluentValidation: https://www.milanjovanovic.tech/blog/cqrs-validation-with-mediatr-pipeline-and-fluentvalidation
- Code Maze - Hexagonal Architecture in C#: https://code-maze.com/csharp-hexagonal-architectural-pattern/
- DEV - Infrastructure Layer EF Core Without Leakage: https://dev.to/bspann/clean-architecture-in-net-10-the-infrastructure-layer-ef-core-without-the-leakage-55dn
- ardalis/CleanArchitecture template: https://github.com/ardalis/CleanArchitecture

### Tertiary (MEDIUM-LOW confidence)
- Swashbuckle Is Dead / Scalar migration: https://dev.to/jfmeyers/swashbuckle-is-dead-heres-how-to-migrate-to-scalar-in-net-10-155d (corroborated by Microsoft template changes)
- Domain vs Application Layer: https://bytecrafted.dev/domain-vs-application-layer-clean-architecture/

---
*Research completed: 2026-05-27*
*Ready for roadmap: yes*
