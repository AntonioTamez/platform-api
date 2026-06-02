---
phase: 01-domain-layer
reviewed: 2026-05-29T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - src/PersonsAPI.Domain/PersonsAPI.Domain.csproj
  - src/PersonsAPI.Domain/Exceptions/DomainException.cs
  - src/PersonsAPI.Domain/Entities/Person.cs
  - tests/PersonsAPI.Domain.Tests/PersonsAPI.Domain.Tests.csproj
  - tests/PersonsAPI.Domain.Tests/DomainExceptionTests.cs
  - tests/PersonsAPI.Domain.Tests/PersonTests.cs
findings:
  critical: 1
  warning: 4
  info: 3
  total: 8
status: issues_found
---

# Phase 01: Domain Layer — Code Review Report

**Reviewed:** 2026-05-29T00:00:00Z
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Reviewed the full domain layer: `Person` entity, `DomainException`, their project files, and both test files. The overall design is sound — the entity is rich, the factory pattern is correctly enforced, and invariants are validated before state mutation. Several issues were found ranging from a compiler-warning-level correctness problem on the sealed class constructor, through missing test coverage, to a subtle `DateTime.Today` timezone assumption that will silently produce wrong results in cloud deployments.

---

## Critical Issues

### CR-01: `protected` Constructor on `sealed` Class Generates Compiler Warning (CS0628) and Signals Incorrect Intent

**File:** `src/PersonsAPI.Domain/Entities/Person.cs:58`
**Issue:** `Person` is declared `sealed`, making `protected` access on its parameterless constructor meaningless — no subclass can ever exist to use it. The C# compiler emits warning CS0628 ("new protected member declared in sealed class"). Depending on project `<TreatWarningsAsErrors>` settings this can become a build error; even when it does not, a compiler warning in domain code is a BLOCKER for a production-quality project. EF Core's entity materialization does not require `protected` — it uses reflection and works with `private` constructors.

**Fix:**
```csharp
// Change:
protected Person() { }

// To:
private Person() { }
```

---

## Warnings

### WR-01: `DateTime.Today` Uses Server Local Time Zone — Silent Wrong Results in UTC-Deployed Environments

**File:** `src/PersonsAPI.Domain/Entities/Person.cs:38,139`
**Issue:** Both `Age` getter (line 38) and `ValidateDateOfBirth` (line 139) call `DateTime.Today`, which resolves to the server's local date. Cloud hosts (Azure App Service, AWS Lambda, containers) typically run in UTC. A person born on "today" in a UTC−5 zone would report `DateOfBirth` as tomorrow from the server's UTC perspective, causing the validation to throw `DomainException("DateOfBirth cannot be in the future.")` even though the date is valid from the user's local perspective. More critically, the `Age` property will silently return an incorrect value near midnight UTC when the local date differs from the server date.

**Fix:** Use `DateOnly.FromDateTime(DateTime.UtcNow)` consistently and document that the domain treats all dates as UTC calendar dates, or inject a clock abstraction (`ISystemClock` / `TimeProvider`) so behavior is deterministic in tests and across time zones.

```csharp
// Replace all occurrences of:
var today = DateOnly.FromDateTime(DateTime.Today);

// With:
var today = DateOnly.FromDateTime(DateTime.UtcNow);
```

### WR-02: Error Messages Expose Internal Parameter Names Rather Than Domain Field Names

**File:** `src/PersonsAPI.Domain/Entities/Person.cs:130,132,134`
**Issue:** `ValidateName` is called with `nameof(firstName)`, `nameof(paternalLastName)`, `nameof(maternalLastName)` — the camelCase local parameter names from the `Create` and `UpdateName` call sites. These resolve to `"firstName"`, `"paternalLastName"`, `"maternalLastName"`. When the Application layer surfaces these messages to API consumers, the casing is inconsistent with .NET conventions (PascalCase property names), and the messages change if the parameter is ever renamed. The domain error message should use stable, user-facing field names.

**Fix:**
```csharp
// Instead of passing nameof(firstName), define constants or use fixed strings:
private static void ValidateName(string value, string fieldName)
{
    if (string.IsNullOrWhiteSpace(value))
        throw new DomainException($"{fieldName} cannot be null, empty, or whitespace.");
    if (value.Length < 2)
        throw new DomainException($"{fieldName} must be at least 2 characters.");
    if (value.Length > 100)
        throw new DomainException($"{fieldName} cannot exceed 100 characters.");
}

// Call with PascalCase field names:
ValidateName(firstName,        "FirstName");
ValidateName(paternalLastName, "PaternalLastName");
ValidateName(maternalLastName, "MaternalLastName");
```

### WR-03: `DomainExceptionTests` Does Not Test the Two-Argument Constructor

**File:** `tests/PersonsAPI.Domain.Tests/DomainExceptionTests.cs:1-20`
**Issue:** `DomainException` exposes two constructors. Only `DomainException(string message)` is tested. The `DomainException(string message, Exception innerException)` constructor is never exercised — specifically, no test verifies that `InnerException` is forwarded to `Exception.InnerException`. If the second constructor were accidentally broken (e.g., `base(message)` instead of `base(message, innerException)`), the test suite would not catch it.

**Fix:**
```csharp
[Fact]
public void Constructor_WithMessageAndInnerException_SetsMessageAndInnerException()
{
    var inner = new InvalidOperationException("root cause");
    var exception = new DomainException("rule broken", inner);

    Assert.Equal("rule broken", exception.Message);
    Assert.Same(inner, exception.InnerException);
}
```

### WR-04: Null Input for Name Parameters Is Not Tested

**File:** `tests/PersonsAPI.Domain.Tests/PersonTests.cs:39-64`
**Issue:** The test project has `<Nullable>enable</Nullable>`, but `Person.Create` and `Person.UpdateName` accept `string` (non-nullable). In practice, null can arrive from deserialization, Application-layer mapping, or reflection. `string.IsNullOrWhiteSpace(null)` returns true, so null does trigger the domain exception correctly — but this behavior is never verified by a test. If the validation logic were refactored (e.g., to add a null-specific guard before `IsNullOrWhiteSpace`), there is no safety net to catch a regression.

**Fix:**
```csharp
[Fact]
public void Create_WithNullFirstName_Throws()
{
    Assert.Throws<DomainException>(() =>
        Person.Create(null!, ValidPaternal, ValidMaternal, ValidDateOfBirth));
}
```
Add equivalent tests for `paternalLastName` and `maternalLastName`.

---

## Info

### IN-01: Duplicate Test Cases — Short-Name Rotation Tests Repeat Min-Length Tests

**File:** `tests/PersonsAPI.Domain.Tests/PersonTests.cs:149-168`
**Issue:** The block `Create_AppliesNameRulesToAllThreeFields_*` (lines 149-168) tests passing `"A"` in each of the three name positions. This is functionally identical to `Create_WithFirstNameShorterThanTwoChars_Throws`, `Create_WithPaternalLastNameShorterThanTwoChars_Throws`, and `Create_WithMaternalLastNameShorterThanTwoChars_Throws` (lines 70-89), which also pass `"A"` (1 char) in each position. The duplication adds noise without increasing coverage.

**Fix:** Remove the `Create_AppliesNameRulesToAllThreeFields_*` block; the behavior is already proven by the min-length tests. If the intent was to verify that all three fields are independently checked, document that intent in a comment on the existing min-length tests.

### IN-02: Test Project Missing `<LangVersion>` Declaration

**File:** `tests/PersonsAPI.Domain.Tests/PersonsAPI.Domain.Tests.csproj:1-24`
**Issue:** The production project (`PersonsAPI.Domain.csproj`) explicitly declares `<LangVersion>14</LangVersion>`. The test project does not declare a `LangVersion` at all, relying on the SDK default for .NET 10. While the SDK default for .NET 10 is C# 14, the omission creates an inconsistency — if the SDK default ever changes in a patch, or if someone adds a `<LangVersion>` to a `Directory.Build.props` that overrides tests differently, the test project silently diverges.

**Fix:**
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <ImplicitUsings>enable</ImplicitUsings>
  <Nullable>enable</Nullable>
  <LangVersion>14</LangVersion>
  <IsPackable>false</IsPackable>
</PropertyGroup>
```

### IN-03: `ValidateName` Has a Dead Code Path for Single-Character Whitespace Strings

**File:** `src/PersonsAPI.Domain/Entities/Person.cs:129-133`
**Issue:** When `value` is a single whitespace character (e.g., `" "`), `IsNullOrWhiteSpace` fires on line 129 and the method throws immediately. The `Length < 2` check on line 131 is therefore unreachable for any string that is whitespace-only and has length 1. This is not a behavior bug, but it creates a false impression that both checks independently contribute to guarding single-whitespace input. A reader could mistakenly believe removing the `IsNullOrWhiteSpace` check would be safe because the length check "covers" it — it does not cover multi-character whitespace-only strings (e.g., `"  "`).

**Fix:** Add an XML comment on `ValidateName` clarifying the guard ordering, or restructure to make the intent explicit:
```csharp
private static void ValidateName(string value, string fieldName)
{
    // IsNullOrWhiteSpace covers: null, "", " ", "   " — all invalid regardless of length.
    if (string.IsNullOrWhiteSpace(value))
        throw new DomainException($"{fieldName} cannot be null, empty, or whitespace.");
    // Length checks apply only to non-whitespace strings that passed the above guard.
    if (value.Length < 2)
        throw new DomainException($"{fieldName} must be at least 2 characters.");
    if (value.Length > 100)
        throw new DomainException($"{fieldName} cannot exceed 100 characters.");
}
```

---

_Reviewed: 2026-05-29T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
