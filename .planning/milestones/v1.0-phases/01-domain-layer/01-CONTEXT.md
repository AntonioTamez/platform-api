# Phase 1: Domain Layer - Context

**Gathered:** 2026-05-27
**Status:** Ready for planning

<domain>
## Phase Boundary

Build the `Person` domain entity in complete isolation: private setters, computed `Age` property, static `Person.Create()` factory, and intention-revealing update methods. The `PersonsAPI.Domain` project must have zero NuGet references to EF Core or any framework. This entity is the foundation — every other layer depends on it. It must be correct before Infrastructure or Application are built.

</domain>

<decisions>
## Implementation Decisions

### Person Identity
- **D-01:** ID type is `int`. Auto-increment sequence assigned by Infrastructure (EF Core). The Domain entity carries an `int Id` property with a private setter — valid until persisted (0 before save is acceptable for a learning context).
- **D-02:** No `PersonId` value object wrapper. Raw `int` is used throughout.

### Domain Error Contract
- **D-03:** `Person.Create()` and all update methods throw a single `DomainException` base class on invariant violation. No `Result<T>` pattern — exceptions are the error contract for this project.
- **D-04:** Exception messages are in English, consistent with the all-English codebase constraint.
- **D-05:** One `DomainException` type (not per-rule exceptions). Application layer catches it by type; message carries the specific violation detail.

### Property Types
- **D-06:** `FirstName`, `PaternalLastName`, and `MaternalLastName` are plain `string` properties — no value object wrapper. Validation rules live in `Person.Create()` and update methods.
- **D-07:** `DateOfBirth` is `DateOnly` — semantically correct, no timezone risk, natively supported in EF Core 6+.
- **D-08:** `Age` is a computed property getter (`public int Age => ...`) — recalculated on every access from `DateOfBirth`. Never stored. EF ignores it via `builder.Ignore(p => p.Age)` in Phase 3.

### Invariant Rules (concrete, implemented in Person.Create() and update methods)
- **D-09:** Name fields (`FirstName`, `PaternalLastName`, `MaternalLastName`): cannot be null/empty/whitespace; minimum 2 characters; maximum 100 characters. Applies to all three fields equally.
- **D-10:** `DateOfBirth`: cannot be in the future (`dateOfBirth > DateOnly.FromDateTime(DateTime.Today)` is invalid); cannot be older than 150 years from today (sanity cap).
- **D-11:** Age calculation algorithm: `today.Year - DateOfBirth.Year`, then subtract 1 if the birthday hasn't occurred yet this year (compare month+day explicitly). Uses `DateOnly.FromDateTime(DateTime.Today)` as "now" — not `DateTime.UtcNow` (avoids timezone issues for a date-only concept).
- **D-12:** Validation strictness: practical — the rules above are sufficient to demonstrate the pattern without over-engineering.

### Rich Model Structure
- **D-13:** `Person.Create(string firstName, string paternalLastName, string maternalLastName, DateOnly dateOfBirth)` is the only way to construct a valid `Person`. No public constructor.
- **D-14:** `protected Person()` parameterless constructor exists solely for EF Core materialization — carries a comment explaining this.
- **D-15:** Update methods: `UpdateName(string firstName, string paternalLastName, string maternalLastName)` and `UpdateDateOfBirth(DateOnly dateOfBirth)` — each re-runs the same invariant checks before mutating state. No public property setters.

### Claude's Discretion
- Internal structure of `Person.cs` (field order, summary comments) — Claude chooses idiomatic C# style.
- Whether to use `ArgumentException` for the `DomainException` base or a fully custom exception class — Claude selects the cleaner option.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Project Requirements
- `.planning/REQUIREMENTS.md` §Domain Model — DOM-01 through DOM-04, VAL-02, INFRA-02 are the 6 requirements for this phase
- `.planning/PROJECT.md` §Constraints — all-English code, rich models, no Minimal API

### Architecture & Research
- `.planning/research/ARCHITECTURE.md` — component boundaries, build order, how Clean + Hexagonal layers map; specifically: "port interfaces in Application, not Domain"
- `.planning/research/PITFALLS.md` — anemic model drift via EF leakage (most critical pitfall for this phase), PATCH/entity mutation risks, Age calculation failure modes
- `.planning/research/STACK.md` — NuGet package versions, C# 14 feature guidance, what NOT to use

### Roadmap
- `.planning/ROADMAP.md` §Phase 1 — success criteria (4 observable criteria this phase must satisfy)

No external specs — requirements fully captured in decisions above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- None — greenfield project. No existing code to reuse.

### Established Patterns
- None yet established — this phase sets the patterns all other phases follow.

### Integration Points
- `PersonsAPI.Domain` is the innermost project. It has NO references to any other project in the solution.
- Phase 2 (Application) will reference Domain to define `IPersonRepository` and handlers.
- Phase 3 (Infrastructure) will reference Application (and transitively Domain) to implement `PersonRepository`.

</code_context>

<specifics>
## Specific Ideas

- The `protected Person()` constructor for EF materialization should be documented with a comment so it's clear it exists for infrastructure reasons, not domain reasons.
- Age calculation must handle the "birthday today" edge case correctly — a person born today has age 0, not -1 or 1.
- All three name fields have identical validation rules — consider a private helper method `ValidateName(string value, string fieldName)` to avoid duplication inside `Person.Create()`.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 1-Domain Layer*
*Context gathered: 2026-05-27*
