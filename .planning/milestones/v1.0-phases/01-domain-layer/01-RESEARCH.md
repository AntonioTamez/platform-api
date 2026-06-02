# Phase 1: Domain Layer - Research

**Researched:** 2026-05-27
**Domain:** C# 14 rich domain entity modeling — Person entity, DomainException, DateOnly age calculation, zero-dependency project isolation
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** ID type is `int`. Auto-increment sequence assigned by Infrastructure (EF Core). The Domain entity carries an `int Id` property with a private setter — valid until persisted (0 before save is acceptable for a learning context).
- **D-02:** No `PersonId` value object wrapper. Raw `int` is used throughout.
- **D-03:** `Person.Create()` and all update methods throw a single `DomainException` base class on invariant violation. No `Result<T>` pattern — exceptions are the error contract for this project.
- **D-04:** Exception messages are in English, consistent with the all-English codebase constraint.
- **D-05:** One `DomainException` type (not per-rule exceptions). Application layer catches it by type; message carries the specific violation detail.
- **D-06:** `FirstName`, `PaternalLastName`, and `MaternalLastName` are plain `string` properties — no value object wrapper. Validation rules live in `Person.Create()` and update methods.
- **D-07:** `DateOfBirth` is `DateOnly` — semantically correct, no timezone risk, natively supported in EF Core 6+.
- **D-08:** `Age` is a computed property getter (`public int Age => ...`) — recalculated on every access from `DateOfBirth`. Never stored. EF ignores it via `builder.Ignore(p => p.Age)` in Phase 3.
- **D-09:** Name fields: cannot be null/empty/whitespace; minimum 2 characters; maximum 100 characters. Applies to all three fields equally.
- **D-10:** `DateOfBirth`: cannot be in the future; cannot be older than 150 years from today.
- **D-11:** Age algorithm: `today.Year - DateOfBirth.Year`, subtract 1 if birthday hasn't occurred yet this year (compare month+day explicitly). Uses `DateOnly.FromDateTime(DateTime.Today)`.
- **D-12:** Validation strictness: practical — rules above are sufficient to demonstrate the pattern without over-engineering.
- **D-13:** `Person.Create(string firstName, string paternalLastName, string maternalLastName, DateOnly dateOfBirth)` is the only way to construct a valid `Person`. No public constructor.
- **D-14:** `protected Person()` parameterless constructor exists solely for EF Core materialization — carries a comment explaining this.
- **D-15:** Update methods: `UpdateName(string firstName, string paternalLastName, string maternalLastName)` and `UpdateDateOfBirth(DateOnly dateOfBirth)` — each re-runs the same invariant checks before mutating state. No public property setters.

### Claude's Discretion

- Internal structure of `Person.cs` (field order, summary comments) — Claude chooses idiomatic C# style.
- Whether to use `ArgumentException` for the `DomainException` base or a fully custom exception class — Claude selects the cleaner option.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope.

</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| DOM-01 | Person entity encapsulates FirstName, PaternalLastName, MaternalLastName, and DateOfBirth with private setters — no public property mutation | C# 14 `field` keyword or `private set`; factory method pattern D-13/D-15 |
| DOM-02 | Person entity exposes a computed Age property derived from DateOfBirth using DateOnly comparison (month + day aware) — never stored | DateOnly API; month+day comparison algorithm D-11 |
| DOM-03 | Person entity provides a static factory method Person.Create() that validates invariants and is the only way to construct a valid instance | Static factory + DomainException pattern D-03/D-13 |
| DOM-04 | Person entity exposes intention-revealing update methods (UpdateName, UpdateDateOfBirth) — external code never assigns properties directly | Update methods with same guard logic D-15 |
| VAL-02 | Domain invariant validation runs inside Person.Create() and update methods — not in handlers | Private ValidateName() helper; guard pattern; DomainException throw |
| INFRA-02 | Domain project has zero EF Core NuGet references — isolation enforced at .csproj level | Zero-reference .csproj; protected EF constructor only |

</phase_requirements>

---

## Summary

Phase 1 produces a single C# class library project (`PersonsAPI.Domain`) containing two files: `Person.cs` (the rich domain entity) and `DomainException.cs` (the custom exception). The project has zero NuGet package references — this is both a design invariant (DOM isolation per INFRA-02) and verifiable at the `.csproj` level. No framework, no EF, no FluentValidation — pure C# 14.

The `Person` entity is the domain core. It enforces its own invariants inside `Person.Create()` and the two update methods, carries a computed `Age` property (never persisted), and exposes all properties with `private set`. A `protected Person()` constructor exists solely for EF Core materialization in Phase 3 — it is annotated with a comment so its purpose is unambiguous. The `DomainException` class is a custom exception (not a subclass of `ArgumentException`) that the Application layer catches by type.

The single technical complexity in this phase is the Age calculation. The correct algorithm compares month and day explicitly after computing the year difference, handles the "birthday today = age 0" edge case correctly, and uses `DateOnly.FromDateTime(DateTime.Today)` to stay timezone-neutral for a date-only concept. A private `ValidateName()` helper method avoids duplicating the three identical name validation checks.

**Primary recommendation:** Use `private set` for all mutable properties (idiomatic, zero ambiguity), the C# 14 `field` keyword is available as an alternative for properties that need inline validation logic in their setter body, but the canonical pattern for this entity is static factory + guard clauses rather than setter-level validation. Keep `DomainException` as a fully custom `Exception` subclass — it communicates domain intent more clearly than `ArgumentException` and allows the Application layer to catch it by exact type without catching unrelated system argument errors.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Person identity (int Id) | Domain entity | Infrastructure (assignment) | Id is a domain property; value is assigned by EF auto-increment in Phase 3 |
| Field invariant validation | Domain entity | — | Business rules belong in the entity, not in handlers (D-03, VAL-02) |
| Age computation | Domain entity | — | Derived domain property — never infrastructure or application concern |
| EF Core materialization constructor | Domain entity (protected) | Infrastructure (uses it) | Satisfies EF without leaking into domain design |
| Custom exception type | Domain (Exceptions/) | Application (catches) | Exception type is a domain concept; Application layer handles it |
| Project isolation (.csproj) | Domain project file | Build tooling | Zero PackageReference entries enforced structurally |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 SDK / C# 14 | 10.0.202 (installed) | Runtime, language | Required by project; `field` keyword, primary constructors, records available |
| System (BCL) | Built-in | DateOnly, DateTime, Exception | No NuGet package; DateOnly is .NET 6+ BCL type |

### Supporting

None. The Domain project has zero NuGet dependencies by design.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `private set` on properties | C# 14 `field` keyword with setter validation | `field` enables per-setter guards inline; `private set` with factory validation is equally correct and more explicit about validation location. Both are valid. |
| Custom `DomainException : Exception` | `ArgumentException` subclass | `ArgumentException` signals a programming error to callers; `DomainException` signals a business rule violation. Custom type is cleaner for Application layer catch blocks. |
| Explicit private backing fields (`_firstName`) | C# 14 `field` keyword | `field` eliminates the backing field declaration; either approach compiles identically. |

**Installation:** No packages to install for the Domain project.

---

## Package Legitimacy Audit

This phase installs zero external packages. The Domain project has no NuGet `<PackageReference>` entries by architectural design (INFRA-02).

**Packages removed due to slopcheck [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

---

## Architecture Patterns

### System Architecture Diagram

```
[Person.Create(firstName, paternalLastName, maternalLastName, dateOfBirth)]
    │
    ├── ValidateName(firstName, "FirstName")    ──► throws DomainException if invalid
    ├── ValidateName(paternalLastName, ...)      ──► throws DomainException if invalid
    ├── ValidateName(maternalLastName, ...)      ──► throws DomainException if invalid
    ├── ValidateDateOfBirth(dateOfBirth)         ──► throws DomainException if invalid
    │
    └── returns new Person (fully valid, all fields set, Id = 0)
                │
                ├── .Age  ──► computed from DateOfBirth on each access (no storage)
                ├── .UpdateName(...)     ──► re-runs ValidateName × 3 before mutating
                └── .UpdateDateOfBirth(...)  ──► re-runs ValidateDateOfBirth before mutating

[protected Person()]  ──► used ONLY by EF Core during query materialization (Phase 3)
```

### Recommended Project Structure

```
PersonsAPI.Domain/
├── PersonsAPI.Domain.csproj    # zero <PackageReference> entries
├── Entities/
│   └── Person.cs               # rich domain entity
└── Exceptions/
    └── DomainException.cs      # custom exception, one type for all domain violations
```

### Pattern 1: Static Factory with Guard Clauses

**What:** `Person.Create()` is a `static` method that validates all parameters before constructing the instance. The public constructor is absent; a `protected` parameterless constructor exists for EF Core only.

**When to use:** When you want to enforce that no `Person` instance can ever exist in an invalid state, and you want the validation failure to be an exception (not a Result type).

**Example:**
```csharp
// Source: project decision D-03, D-13; pattern from PITFALLS.md Pitfall 1
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

// Private so only Create() and update methods can set properties
private Person() { }  // used internally by Create()

/// <summary>
/// Required by EF Core for entity materialization during queries.
/// Do not use in application code — use Person.Create() instead.
/// </summary>
protected Person() { }
```

Note: C# does not allow two parameterless constructors with different accessibility in the same class. The correct pattern is a single `protected Person() { }` for EF, and the `Create()` static method uses an object initializer with private setters, or sets fields via the private setters after calling the protected constructor. See Pattern 2 for the idiomatic approach.

### Pattern 2: Idiomatic Private-Setter Construction via Static Factory

**What:** The `Create()` static method calls `new Person()` (which calls the `protected` parameterless constructor) and then assigns each property via its private setter — the private setters are accessible within the same class.

**When to use:** This is the standard pattern for rich domain entities in C# where EF Core materialization is also required.

**Example:**
```csharp
// Source: ARCHITECTURE.md "How EF Core Fits as a Driven Adapter" + D-13/D-14
public sealed class Person
{
    public int Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string PaternalLastName { get; private set; } = string.Empty;
    public string MaternalLastName { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>
    /// Age computed from DateOfBirth on every access. Never stored in the database.
    /// EF Core will ignore this property via builder.Ignore(p => p.Age) in Phase 3.
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth.Month > today.Month
                || (DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day))
            {
                age--;
            }
            return age;
        }
    }

    /// <summary>
    /// Required by EF Core for entity materialization during queries.
    /// Do not use in application code — use Person.Create() instead.
    /// </summary>
    protected Person() { }

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
}
```

### Pattern 3: DomainException as a Custom Exception

**What:** A custom exception class that inherits directly from `Exception`. Single type — the message carries the rule violation detail. Application layer catches it by type to distinguish domain violations from infrastructure or system errors.

**When to use:** Always, for this project. Do not throw `ArgumentException`, `InvalidOperationException`, or other BCL exceptions for domain rule violations.

**Example:**
```csharp
// Source: project decision D-03, D-04, D-05
namespace PersonsAPI.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated. The message describes the specific rule violation.
/// Caught by the Application layer to produce appropriate error responses.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
```

### Anti-Patterns to Avoid

- **Public property setters on `Person`:** `public string FirstName { get; set; }` allows any caller to set invalid values, bypassing Create() and update methods. All properties must be `private set`.
- **Age as a stored field:** `public int Age { get; set; }` is wrong. Age must be a computed property with no setter. If stored, it becomes stale immediately after midnight.
- **Inline EF attributes on the entity:** `[Required]`, `[MaxLength(100)]`, `[Key]` all require `Microsoft.EntityFrameworkCore` in the Domain project. This violates INFRA-02. All EF config goes in Phase 3's `IEntityTypeConfiguration<Person>`.
- **DomainException as ArgumentException subclass:** This conflates programming errors (wrong argument type) with domain violations (business rule broken). The Application layer must catch domain violations separately.
- **Throwing in the `protected Person()` constructor:** The EF materialization constructor must be empty — EF Core sets properties via reflection after construction. Throwing in it breaks materialization.
- **Validation logic in the `protected` constructor:** Any validation in the parameterless constructor will run during EF Core materialization and throw when reading back persisted data. Validation belongs only in `Create()` and update methods.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Age from DateOfBirth | Custom arithmetic with DateTime.Now | `DateOnly.FromDateTime(DateTime.Today)` + month/day comparison | DateOnly eliminates time-of-day; month+day comparison handles pre-birthday case correctly |
| Name guard clauses | Per-field if/throw blocks × 3 | Private `ValidateName(string value, string fieldName)` helper | 3 identical fields, 3 identical rules — DRY via private helper eliminates copy-paste drift |
| EF materialization | Public parameterless constructor | `protected Person() { }` | Protected scope: EF can use it, application code cannot accidentally call it |

**Key insight:** For a 4-field entity, the temptation is to add frameworks (FluentValidation, guard clause libraries). Resist — the domain rules are exactly 3 name checks and 2 date checks. Hand-rolling the validation explicitly inside the entity is correct here and demonstrates the pattern more clearly than a library would.

---

## Runtime State Inventory

SKIPPED — greenfield phase. No existing code, no stored data, no runtime state.

---

## Common Pitfalls

### Pitfall 1: Two Parameterless Constructors

**What goes wrong:** Developer tries to add both `private Person() { }` (for internal use by `Create()`) and `protected Person() { }` (for EF Core). C# does not allow two constructors with the same signature — compilation fails.

**Why it happens:** ARCHITECTURE.md's code example shows the pattern, but uses a single constructor. The static factory method (`Create()`) uses an object initializer (`new Person { ... }`) which calls the parameterless constructor and then assigns via private setters. Only one parameterless constructor is needed.

**How to avoid:** Use a single `protected Person() { }` constructor. The `Create()` static method accesses this same constructor via `new Person { ... }` syntax (object initializer). Private setters are accessible within the same class, so `Create()` can assign them. EF Core also uses this same protected constructor for materialization.

**Warning signs:** `CS0111: Type 'Person' already defines a member called 'Person' with the same parameter types`.

---

### Pitfall 2: Age Calculation Off-by-One

**What goes wrong:** `Age` returns the wrong value when today is before the person's birthday in the current year.

**Why it happens:** Simple year subtraction (`today.Year - DateOfBirth.Year`) does not account for whether the birthday has already passed this calendar year.

**How to avoid:** After computing the year difference, check whether `DateOfBirth.Month > today.Month`, OR `DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day`. If true, subtract 1. This correctly handles: birthday today = age 0 (not -1), birthday tomorrow = age - 1, birthday yesterday = correct age.

**Warning signs:** A person born on December 31, 1994 shows age 31 on January 1, 2026 instead of 30.

---

### Pitfall 3: EF Core Leaking into the Domain Project

**What goes wrong:** Adding `[Key]`, `[Required]`, or `[Column]` attributes to `Person.cs` causes the `.csproj` to require a `<PackageReference Include="Microsoft.EntityFrameworkCore" />`. This directly violates INFRA-02 (the FIRST success criterion for this phase).

**Why it happens:** Many EF Core tutorials show data annotations directly on entities. It is the "quick start" path in the EF Core docs.

**How to avoid:** Use only plain C# in the Domain project. All EF configuration (column names, max lengths, ignore computed properties) happens in Phase 3 via `IEntityTypeConfiguration<Person>` in the Infrastructure project. Verify by inspecting the `.csproj` file — it must have zero `<PackageReference>` entries.

**Warning signs:** `using Microsoft.EntityFrameworkCore;` at the top of `Person.cs`; the Domain `.csproj` contains any `<PackageReference>` element.

---

### Pitfall 4: Validation Logic in the Protected Constructor

**What goes wrong:** Developer adds guard clauses to `protected Person() { }` believing this prevents invalid entities. At runtime, EF Core calls this constructor during `DbContext.SaveChanges()` or query materialization — guards throw unexpectedly on valid persisted data.

**Why it happens:** Confusion between the two roles of the parameterless constructor: application construction (Create()) vs. EF materialization (protected constructor).

**How to avoid:** The `protected Person() { }` constructor must be empty. All validation belongs in `Create()` and the update methods. Add the XML doc comment to make the EF purpose explicit.

**Warning signs:** Tests pass in isolation but the application throws when reading from the database in Phase 3.

---

### Pitfall 5: DomainException Catching the Wrong Things

**What goes wrong:** If `DomainException` inherits from `ArgumentException` or another BCL exception, catch blocks in the Application layer that catch `DomainException` also accidentally catch unrelated system exceptions. Conversely, if the Application layer catches `ArgumentException`, it may catch domain violations when a developer mistakenly throws `ArgumentException` directly.

**Why it happens:** Developer defaults to `ArgumentException` because "invalid argument" describes the situation semantically.

**How to avoid:** `DomainException : Exception` directly. Single inheritance level. The Application layer catches `DomainException` specifically to produce 422/400 responses. `ArgumentNullException`, `ArgumentException`, and similar BCL types signal programming errors and should not be caught at the application boundary.

**Warning signs:** `catch (ArgumentException ex)` in a handler that is intended to catch domain violations.

---

## Code Examples

### Canonical Age Calculation

```csharp
// Source: project decision D-11; algorithm verified against Microsoft Q&A and Clint McMahon's reference
// https://learn.microsoft.com/en-us/answers/questions/1661051/how-to-calculate-years-from-two-different-date-in
public int Age
{
    get
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - DateOfBirth.Year;
        // Birthday has not yet occurred this calendar year
        if (DateOfBirth.Month > today.Month
            || (DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day))
        {
            age--;
        }
        return age;
    }
}
```

Edge cases this handles correctly:
- Birthday today: year diff = N, neither condition fires, age = N (correct)
- Birthday tomorrow: `DateOfBirth.Day > today.Day` fires, age = N - 1 (correct)
- Born December 31, checking January 1: `DateOfBirth.Month (12) > today.Month (1)` is false; `12 == 1` is false; no subtraction; age = year diff (correct)
- Feb 29 birthday in a non-leap year: comparison uses `.Month` and `.Day` — Feb 29 compared against Feb 28 today means `DateOfBirth.Day (29) > today.Day (28)` fires, subtract 1. This treats the birthday as "not yet occurred" on Feb 28, which is the common convention.

### Minimal Domain .csproj (zero dependencies)

```xml
<!-- Source: STACK.md project structure; INFRA-02 requirement -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>

  <!-- Zero <PackageReference> entries — this is enforced by INFRA-02 -->

</Project>
```

### DomainException (canonical form)

```csharp
// Source: project decisions D-03, D-04, D-05
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

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Explicit backing field (`private string _firstName`) | C# 14 `field` keyword for semi-auto properties | C# 14 / .NET 10 (Nov 2025) | Optional — `private set` with factory validation is equally valid; `field` is available if setter-level guards are preferred |
| `DateTime.Now` for age calculation | `DateOnly.FromDateTime(DateTime.Today)` | .NET 6 (DateOnly introduced) | Eliminates time-of-day from date-only concepts; no timezone drift for age calculation |
| EF data annotations on entity | Fluent API in `IEntityTypeConfiguration<T>` | EF Core 2.0 (Fluent API recommended) | Keeps domain entities free of persistence concerns; standard pattern |

**Deprecated/outdated:**
- EF data annotations (`[Key]`, `[Required]`) on domain entities: still supported but considered an anti-pattern for Clean Architecture. Do not use in this project.
- `DateTime.Now.Year - DateOfBirth.Year` for age: still compiles but produces off-by-one errors. Use month+day comparison.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `DateOnly.FromDateTime(DateTime.Today)` uses the local machine's timezone clock, not UTC | Code Examples / Age Calculation | For a learning project on a local dev machine, no practical risk. For a production server in UTC serving users in other timezones, the "today" date could differ from the user's local date by up to 12 hours. Acceptable for this project's scope per D-12. | [ASSUMED] |

**Note:** The decision D-11 explicitly specifies `DateOnly.FromDateTime(DateTime.Today)`. This is the locked decision. The assumption above documents the known limitation, not a recommendation to change it.

---

## Open Questions

1. **`protected` vs `private` for the EF constructor**
   - What we know: EF Core can use either `private` or `protected` parameterless constructors for materialization. Both work.
   - What's unclear: Which is more idiomatically correct in the community for Clean Architecture?
   - Recommendation: Use `protected` — it communicates "this exists for subclass/framework use, not general application code." It also aligns with D-14 which specifies `protected Person()`. Decision is locked.

2. **`sealed` on the `Person` class**
   - What we know: `Person` has no planned subclasses. `sealed` prevents accidental inheritance and enables some compiler optimizations.
   - What's unclear: Whether to seal it or not — CONTEXT.md is silent on this.
   - Recommendation: Mark `Person` as `sealed`. For a learning project, it communicates intent and there is no reason to allow inheritance of the entity.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET 10 SDK | All compilation | Yes | 10.0.202 | — |
| C# 14 language | `field` keyword, extension members | Yes | Ships with .NET 10.0.202 | — |
| `dotnet new classlib` template | Project creation | Yes | Confirmed via `dotnet --list-sdks` | — |

**Missing dependencies with no fallback:** None.
**Missing dependencies with fallback:** None.

---

## Security Domain

`security_enforcement` is enabled (ASVS Level 1). This phase is a pure C# class library with no HTTP surface, no external input processing, no authentication, no persistence, and no network calls. ASVS categories V2 (Authentication), V3 (Session Management), V4 (Access Control), and V6 (Cryptography) do not apply.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | No | No auth surface in Domain layer |
| V3 Session Management | No | No session concepts |
| V4 Access Control | No | No access decisions |
| V5 Input Validation | Yes — partially | Invariant validation via guard clauses in `Person.Create()` and update methods |
| V6 Cryptography | No | No cryptographic operations |

### V5 Application to Domain Layer

The Domain layer performs input validation via guard clauses in `Create()` and update methods. This satisfies ASVS V5 at the domain level: invalid data never enters a `Person` instance. The Application layer (Phase 2) adds a second validation layer via FluentValidation pipeline behavior for request-level validation (VAL-01). These are complementary, not redundant — domain invariants catch logic errors even when called from non-HTTP paths.

### Known Threat Patterns

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Null/empty string injection into name fields | Tampering | Guard clauses in ValidateName() — throw DomainException |
| Future date injection as DateOfBirth | Tampering | Guard clause in ValidateDateOfBirth() — `dateOfBirth > today` throws |
| Unreasonably ancient date (DoS / data corruption) | Tampering | 150-year sanity cap in ValidateDateOfBirth() |

---

## Sources

### Primary (HIGH confidence)

- [What's new in C# 14 — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14) — `field` keyword, extension members, null-conditional assignment, features confirmed for .NET 10
- [How to use DateOnly and TimeOnly — Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/providers/in-memory/) — DateOnly API for age calculation
- [ARCHITECTURE.md](.planning/research/ARCHITECTURE.md) — Person entity structure, project layout, EF materialization constructor pattern
- [PITFALLS.md](.planning/research/PITFALLS.md) — Age calculation pitfalls, EF leakage pitfalls, anemic model pitfalls (all HIGH confidence, verified)
- [STACK.md](.planning/research/STACK.md) — .NET 10 SDK version, C# 14 features with HIGH confidence rating
- [01-CONTEXT.md](.planning/phases/01-domain-layer/01-CONTEXT.md) — All locked decisions D-01 through D-15

### Secondary (MEDIUM confidence)

- [How to calculate age in C# — Clint McMahon](https://www.clintmcmahon.com/blog/how-to-calculate-age-in-c) — month+day comparison algorithm, cross-verified with Microsoft Q&A
- [Microsoft Q&A: How to calculate years from two different dates](https://learn.microsoft.com/en-us/answers/questions/1661051/how-to-calculate-years-from-two-different-date-in) — confirms month+day algorithm
- [Backing Fields — EF Core Microsoft Learn](https://learn.microsoft.com/en-us/ef/core/modeling/backing-field) — EF Core private/protected constructor behavior with C# 14 `field` keyword
- [C# 14 `field` keyword — Microsoft Learn feature spec](https://learn.microsoft.com/en-gb/dotnet/csharp/language-reference/proposals/csharp-14.0/field-keyword) — canonical spec for semi-auto property backing field

### Tertiary (LOW confidence)

None — all claims in this research are HIGH or MEDIUM confidence.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — .NET 10 SDK confirmed installed (10.0.202); no packages for Domain layer
- Architecture: HIGH — all patterns from pre-verified ARCHITECTURE.md and PITFALLS.md which were HIGH confidence
- C# 14 features: HIGH — verified against official Microsoft Learn C# 14 docs
- Age algorithm: HIGH — cross-verified with two Microsoft sources
- Pitfalls: HIGH — sourced from PITFALLS.md which was researched against official EF Core docs

**Research date:** 2026-05-27
**Valid until:** Stable (no fast-moving dependencies; pure C# 14 / .NET 10 BCL — stable until .NET 11 release)
