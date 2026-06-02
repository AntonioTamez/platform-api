# Phase 1: Domain Layer - Pattern Map

**Mapped:** 2026-05-27
**Files analyzed:** 3 (2 source files + 1 project file)
**Analogs found:** 0 / 3 — greenfield project, no existing code

---

## File Classification

| New File | Role | Data Flow | Closest Analog | Match Quality |
|----------|------|-----------|----------------|---------------|
| `PersonsAPI.Domain/PersonsAPI.Domain.csproj` | config | n/a | None — greenfield | no analog |
| `PersonsAPI.Domain/Entities/Person.cs` | model | n/a (pure domain, no I/O) | None — greenfield | no analog |
| `PersonsAPI.Domain/Exceptions/DomainException.cs` | utility | n/a (exception type) | None — greenfield | no analog |

---

## No Analog Found

All three files have no codebase analog. This is Phase 1 of a greenfield project — the Domain layer sets the patterns all subsequent phases follow. The planner must use RESEARCH.md patterns exclusively.

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| `PersonsAPI.Domain/PersonsAPI.Domain.csproj` | config | n/a | No existing projects in repo |
| `PersonsAPI.Domain/Entities/Person.cs` | model | n/a | No existing domain entities |
| `PersonsAPI.Domain/Exceptions/DomainException.cs` | utility | n/a | No existing exception types |

---

## Pattern Assignments

### `PersonsAPI.Domain/PersonsAPI.Domain.csproj` (config)

**Analog:** None — derived from RESEARCH.md §Code Examples "Minimal Domain .csproj"

**Canonical pattern:**
```xml
<!-- Source: RESEARCH.md §Code Examples; requirement INFRA-02 -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

  <!-- Zero <PackageReference> entries — enforced by INFRA-02 -->

</Project>
```

**Rules:**
- `LangVersion` must be `14` (unlocks C# 14 `field` keyword if needed)
- `Nullable` must be `enable` (all-nullable-aware code)
- Zero `<PackageReference>` elements — any reference to EF Core or any NuGet package is a build-level violation of INFRA-02
- No `<ProjectReference>` entries — Domain is the innermost layer, no project dependencies

---

### `PersonsAPI.Domain/Entities/Person.cs` (model, no I/O)

**Analog:** None — derived from RESEARCH.md §Architecture Patterns "Pattern 2: Idiomatic Private-Setter Construction via Static Factory"

**Namespace pattern:**
```csharp
// File: PersonsAPI.Domain/Entities/Person.cs
namespace PersonsAPI.Domain.Entities;
```

**Class declaration pattern:**
```csharp
// Source: RESEARCH.md §Open Questions item 2 — seal the entity
// Source: CONTEXT.md D-13, D-14
public sealed class Person
```

**Property declaration pattern** (all mutable properties use `private set`):
```csharp
// Source: CONTEXT.md D-01, D-06, D-07; RESEARCH.md Pattern 2
public int Id { get; private set; }
public string FirstName { get; private set; } = string.Empty;
public string PaternalLastName { get; private set; } = string.Empty;
public string MaternalLastName { get; private set; } = string.Empty;
public DateOnly DateOfBirth { get; private set; }
```

**Computed Age property pattern:**
```csharp
// Source: CONTEXT.md D-08, D-11; RESEARCH.md §Code Examples "Canonical Age Calculation"
/// <summary>
/// Age computed from DateOfBirth on every access. Never stored in the database.
/// EF Core ignores this property via builder.Ignore(p => p.Age) in Phase 3.
/// </summary>
public int Age
{
    get
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - DateOfBirth.Year;
        // Subtract 1 if the birthday has not yet occurred this calendar year
        if (DateOfBirth.Month > today.Month
            || (DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day))
        {
            age--;
        }
        return age;
    }
}
```

**EF materialization constructor pattern:**
```csharp
// Source: CONTEXT.md D-14; RESEARCH.md Pitfall 1, Pitfall 4
/// <summary>
/// Required by EF Core for entity materialization during queries.
/// Do not use in application code — use Person.Create() instead.
/// </summary>
protected Person() { }
```

**Static factory method pattern:**
```csharp
// Source: CONTEXT.md D-03, D-09, D-10, D-13; RESEARCH.md Pattern 1 + Pattern 2
public static Person Create(
    string firstName,
    string paternalLastName,
    string maternalLastName,
    DateOnly dateOfBirth)
{
    ValidateName(firstName, nameof(firstName));
    ValidateName(paternalLastName, nameof(paternalLastName));
    ValidateName(maternalLastName, nameof(maternalLastName));
    ValidateDateOfBirth(dateOfBirth);

    return new Person
    {
        FirstName = firstName,
        PaternalLastName = paternalLastName,
        MaternalLastName = maternalLastName,
        DateOfBirth = dateOfBirth
    };
}
```

**Update method pattern** (re-runs same guards before mutating):
```csharp
// Source: CONTEXT.md D-15; RESEARCH.md Pattern 2
public void UpdateName(string firstName, string paternalLastName, string maternalLastName)
{
    ValidateName(firstName, nameof(firstName));
    ValidateName(paternalLastName, nameof(paternalLastName));
    ValidateName(maternalLastName, nameof(maternalLastName));
    FirstName = firstName;
    PaternalLastName = paternalLastName;
    MaternalLastName = maternalLastName;
}

public void UpdateDateOfBirth(DateOnly dateOfBirth)
{
    ValidateDateOfBirth(dateOfBirth);
    DateOfBirth = dateOfBirth;
}
```

**Private guard helper pattern** (DRY — avoids 3 identical inline blocks):
```csharp
// Source: CONTEXT.md D-09, §Specific Ideas; RESEARCH.md §Don't Hand-Roll
private static void ValidateName(string value, string fieldName)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new DomainException($"{fieldName} cannot be null, empty, or whitespace.");
    if (value.Length < 2)
        throw new DomainException($"{fieldName} must be at least 2 characters.");
    if (value.Length > 100)
        throw new DomainException($"{fieldName} cannot exceed 100 characters.");
}

private static void ValidateDateOfBirth(DateOnly dateOfBirth)
{
    var today = DateOnly.FromDateTime(DateTime.Today);
    if (dateOfBirth > today)
        throw new DomainException("DateOfBirth cannot be in the future.");
    if (dateOfBirth < today.AddYears(-150))
        throw new DomainException("DateOfBirth cannot be more than 150 years in the past.");
}
```

**Anti-patterns to reject (from RESEARCH.md):**
- Public or auto property setters (`public string FirstName { get; set; }`) — all properties are `private set`
- Storing `Age` as a field or property with a setter
- Any `using Microsoft.EntityFrameworkCore;` import
- EF data annotations (`[Key]`, `[Required]`, `[Column]`) on any property
- Guard clauses or validation logic inside `protected Person() { }` constructor
- Two parameterless constructors (C# compile error CS0111)

---

### `PersonsAPI.Domain/Exceptions/DomainException.cs` (utility)

**Analog:** None — derived from RESEARCH.md §Architecture Patterns "Pattern 3: DomainException as a Custom Exception"

**Canonical pattern:**
```csharp
// Source: CONTEXT.md D-03, D-04, D-05; RESEARCH.md Pattern 3 and Pitfall 5
namespace PersonsAPI.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated.
/// The message describes the specific business rule violation in plain English.
/// Caught by the Application layer to produce appropriate HTTP error responses.
/// Do not catch this in the Domain layer — let it propagate.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

**Rules:**
- Inherits directly from `Exception`, not from `ArgumentException` or any other BCL exception subclass (D-05, Pitfall 5)
- `sealed` — no subclasses planned or needed
- Two constructors: message-only and message+innerException (standard Exception contract)
- No additional properties — the message string carries all violation detail (D-05)
- Namespace is `PersonsAPI.Domain.Exceptions`, not `PersonsAPI.Domain.Entities`

---

## Shared Patterns

### Error Contract

**Apply to:** `Person.cs` (both Create and update methods)

All domain invariant failures throw `DomainException` — no other exception type is thrown intentionally from domain code. BCL exceptions (`NullReferenceException`, `InvalidOperationException`) may surface from unexpected bugs and should NOT be caught at the domain layer.

```csharp
// Source: CONTEXT.md D-03; RESEARCH.md Pitfall 5
// Throw pattern — always DomainException, always English message
throw new DomainException($"{fieldName} must be at least 2 characters.");

// Never:
throw new ArgumentException(...);           // wrong type — conflates programming error with domain violation
throw new InvalidOperationException(...);   // wrong type — used for state machine violations
```

### C# 14 / .NET 10 Language Features Available

**Apply to:** All files in this phase

| Feature | Available | Recommended Use in This Phase |
|---------|-----------|-------------------------------|
| `private set` on properties | Yes (C# 3+) | Primary pattern for all Person properties |
| `field` keyword (semi-auto properties) | Yes (C# 14) | Optional alternative if setter-level guard inline is preferred; not the primary pattern here |
| File-scoped namespaces (`namespace X;`) | Yes (C# 10+) | Use throughout — no braced namespace blocks |
| `sealed` class modifier | Yes | Apply to both `Person` and `DomainException` |
| `string.IsNullOrWhiteSpace()` | Yes (.NET BCL) | Use in `ValidateName()` |
| `DateOnly` | Yes (.NET 6+) | Use for `DateOfBirth` property and age calculation |
| `nameof()` | Yes (C# 6+) | Use in `ValidateName()` and `ValidateDateOfBirth()` calls to pass field names |

### Naming Conventions

**Apply to:** All files in this phase

- Class names: PascalCase (`Person`, `DomainException`)
- Method names: PascalCase (`Create`, `UpdateName`, `ValidateName`)
- Property names: PascalCase (`FirstName`, `DateOfBirth`, `Age`)
- Private methods: PascalCase (`ValidateName`, `ValidateDateOfBirth`) — C# convention, not camelCase
- Parameter names: camelCase (`firstName`, `paternalLastName`, `dateOfBirth`)
- All identifiers, comments, and XML docs in English (CLAUDE.md constraint)

### XML Documentation Comments

**Apply to:** All public and protected members in `Person.cs`; class-level doc on `DomainException`

Use `/// <summary>` blocks on:
- The `Age` property (explains "never stored, EF ignores it")
- The `protected Person()` constructor (explains EF-only purpose)
- The `Create()` static method (explains it is the only valid construction path)
- The `DomainException` class itself

---

## Metadata

**Analog search scope:** `C:\ATS\Git\platform` (entire working directory)
**Files scanned:** 1 (`CLAUDE.md` only — confirmed greenfield)
**Pattern source:** RESEARCH.md §Architecture Patterns, §Code Examples, §Common Pitfalls; CONTEXT.md §Implementation Decisions
**Pattern extraction date:** 2026-05-27
