using PersonsAPI.Domain.Exceptions;

namespace PersonsAPI.Domain.Entities;

/// <summary>
/// Rich domain entity representing a person.
/// Encapsulates all business invariants — the only valid construction path is <see cref="Create"/>.
/// </summary>
public sealed class Person
{
    // ---------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------

    /// <summary>Auto-increment identifier assigned by the Infrastructure layer (EF Core). Zero before first persist.</summary>
    public int Id { get; private set; }

    /// <summary>Person's first (given) name. Minimum 2, maximum 100 characters.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Person's paternal (father's) last name. Minimum 2, maximum 100 characters.</summary>
    public string PaternalLastName { get; private set; } = string.Empty;

    /// <summary>Person's maternal (mother's) last name. Minimum 2, maximum 100 characters.</summary>
    public string MaternalLastName { get; private set; } = string.Empty;

    /// <summary>Date of birth. Cannot be in the future; cannot be more than 150 years in the past.</summary>
    public DateOnly DateOfBirth { get; private set; }

    /// <summary>
    /// Age computed from <see cref="DateOfBirth"/> on every access using a month-and-day-aware algorithm.
    /// Never stored in the database — EF Core ignores this property via <c>builder.Ignore(p => p.Age)</c> in Phase 3.
    /// </summary>
    public int Age
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - DateOfBirth.Year;
            // Subtract 1 when the birthday has not yet occurred this calendar year
            if (DateOfBirth.Month > today.Month
                || (DateOfBirth.Month == today.Month && DateOfBirth.Day > today.Day))
            {
                age--;
            }
            return age;
        }
    }

    // ---------------------------------------------------------------------------
    // Constructors
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Required by EF Core for entity materialization during queries.
    /// Do not use in application code — use <see cref="Create"/> instead.
    /// </summary>
    protected Person() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="Person"/> after validating all invariants.
    /// This is the only valid construction path — no public constructor exists.
    /// </summary>
    /// <param name="firstName">Person's first (given) name.</param>
    /// <param name="paternalLastName">Person's paternal last name.</param>
    /// <param name="maternalLastName">Person's maternal last name.</param>
    /// <param name="dateOfBirth">Date of birth (DateOnly, no time component).</param>
    /// <returns>A fully valid <see cref="Person"/> instance with <see cref="Id"/> equal to zero.</returns>
    /// <exception cref="DomainException">Thrown when any invariant is violated.</exception>
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

    // ---------------------------------------------------------------------------
    // Update methods
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Updates all three name fields after re-running the same invariant checks.
    /// Invalid arguments throw <see cref="DomainException"/> and leave the prior state unchanged.
    /// </summary>
    public void UpdateName(string firstName, string paternalLastName, string maternalLastName)
    {
        ValidateName(firstName, nameof(firstName));
        ValidateName(paternalLastName, nameof(paternalLastName));
        ValidateName(maternalLastName, nameof(maternalLastName));

        FirstName = firstName;
        PaternalLastName = paternalLastName;
        MaternalLastName = maternalLastName;
    }

    /// <summary>
    /// Updates the date of birth after re-running the same invariant check.
    /// An invalid date throws <see cref="DomainException"/> and leaves the prior state unchanged.
    /// </summary>
    public void UpdateDateOfBirth(DateOnly dateOfBirth)
    {
        ValidateDateOfBirth(dateOfBirth);
        DateOfBirth = dateOfBirth;
    }

    // ---------------------------------------------------------------------------
    // Private guard helpers
    // ---------------------------------------------------------------------------

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
