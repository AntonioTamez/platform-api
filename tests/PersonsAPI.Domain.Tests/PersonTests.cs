using PersonsAPI.Domain.Entities;
using PersonsAPI.Domain.Exceptions;

namespace PersonsAPI.Domain.Tests;

public class PersonTests
{
    // ---------------------------------------------------------------------------
    // Helpers — valid defaults
    // ---------------------------------------------------------------------------

    private static readonly DateOnly ValidDateOfBirth = DateOnly.FromDateTime(DateTime.Today).AddYears(-30);
    private const string ValidFirst = "John";
    private const string ValidPaternal = "Smith";
    private const string ValidMaternal = "Doe";

    // ---------------------------------------------------------------------------
    // Construction — valid data
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithValidData_SetsAllFieldsAndIdZero()
    {
        var dateOfBirth = ValidDateOfBirth;

        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, dateOfBirth);

        Assert.Equal(ValidFirst, person.FirstName);
        Assert.Equal(ValidPaternal, person.PaternalLastName);
        Assert.Equal(ValidMaternal, person.MaternalLastName);
        Assert.Equal(dateOfBirth, person.DateOfBirth);
        Assert.Equal(0, person.Id);
    }

    // ---------------------------------------------------------------------------
    // Name invariants — null / empty / whitespace
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyOrWhitespaceFirstName_Throws(string invalidName)
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(invalidName, ValidPaternal, ValidMaternal, ValidDateOfBirth));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyOrWhitespacePaternalLastName_Throws(string invalidName)
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, invalidName, ValidMaternal, ValidDateOfBirth));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyOrWhitespaceMaternalLastName_Throws(string invalidName)
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, invalidName, ValidDateOfBirth));
    }

    // ---------------------------------------------------------------------------
    // Name invariants — minimum length (< 2 chars)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithFirstNameShorterThanTwoChars_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create("A", ValidPaternal, ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_WithPaternalLastNameShorterThanTwoChars_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, "B", ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_WithMaternalLastNameShorterThanTwoChars_Throws()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, "C", ValidDateOfBirth));
    }

    // ---------------------------------------------------------------------------
    // Name invariants — maximum length (> 100 chars)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithFirstNameLongerThan100Chars_Throws()
    {
        var longName = new string('A', 101);
        Assert.Throws<DomainException>(() =>
            Person.Create(longName, ValidPaternal, ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_WithPaternalLastNameLongerThan100Chars_Throws()
    {
        var longName = new string('B', 101);
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, longName, ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_WithMaternalLastNameLongerThan100Chars_Throws()
    {
        var longName = new string('C', 101);
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, longName, ValidDateOfBirth));
    }

    // ---------------------------------------------------------------------------
    // Name invariants — boundary: exactly 2 and exactly 100 chars are valid
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithNameAtExactly2Chars_Succeeds()
    {
        var name2 = "Jo";
        var person = Person.Create(name2, name2, name2, ValidDateOfBirth);

        Assert.Equal(name2, person.FirstName);
        Assert.Equal(name2, person.PaternalLastName);
        Assert.Equal(name2, person.MaternalLastName);
    }

    [Fact]
    public void Create_WithNameAtExactly100Chars_Succeeds()
    {
        var name100 = new string('Z', 100);
        var person = Person.Create(name100, name100, name100, ValidDateOfBirth);

        Assert.Equal(name100, person.FirstName);
        Assert.Equal(name100, person.PaternalLastName);
        Assert.Equal(name100, person.MaternalLastName);
    }

    // ---------------------------------------------------------------------------
    // Name invariants — all three fields covered (invalid value rotated)
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_AppliesNameRulesToAllThreeFields_FirstNameInvalid()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create("A", ValidPaternal, ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_AppliesNameRulesToAllThreeFields_PaternalLastNameInvalid()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, "A", ValidMaternal, ValidDateOfBirth));
    }

    [Fact]
    public void Create_AppliesNameRulesToAllThreeFields_MaternalLastNameInvalid()
    {
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, "A", ValidDateOfBirth));
    }

    // ---------------------------------------------------------------------------
    // DateOfBirth invariants
    // ---------------------------------------------------------------------------

    [Fact]
    public void Create_WithFutureDateOfBirth_Throws()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, ValidMaternal, tomorrow));
    }

    [Fact]
    public void Create_WithDateOfBirthOlderThan150Years_Throws()
    {
        var tooOld = DateOnly.FromDateTime(DateTime.Today).AddYears(-151);
        Assert.Throws<DomainException>(() =>
            Person.Create(ValidFirst, ValidPaternal, ValidMaternal, tooOld));
    }

    [Fact]
    public void Create_WithDateOfBirthToday_Succeeds()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, today);

        Assert.Equal(today, person.DateOfBirth);
    }

    [Fact]
    public void Create_WithThirtyYearsAgo_Succeeds()
    {
        var thirtyYearsAgo = DateOnly.FromDateTime(DateTime.Today).AddYears(-30);
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, thirtyYearsAgo);

        Assert.Equal(thirtyYearsAgo, person.DateOfBirth);
    }

    // ---------------------------------------------------------------------------
    // Age computation — month/day-aware
    // ---------------------------------------------------------------------------

    [Fact]
    public void Age_WhenBirthdayIsToday_ReturnsCorrectAge()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Born exactly 30 years ago on today's month/day
        var birthDate = today.AddYears(-30);
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, birthDate);

        Assert.Equal(30, person.Age);
    }

    [Fact]
    public void Age_WhenBirthdayIsYesterday_ReturnsCorrectAge()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var birthDate = today.AddYears(-25).AddDays(-1);
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, birthDate);

        // Birthday was yesterday: the age should be exactly 25
        Assert.Equal(25, person.Age);
    }

    [Fact]
    public void Age_WhenBirthdayIsTomorrow_ReturnsAgeMinusOne()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        // Birthday falls tomorrow — not yet 20 this year
        var birthDate = today.AddYears(-20).AddDays(1);
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, birthDate);

        Assert.Equal(19, person.Age);
    }

    // ---------------------------------------------------------------------------
    // Encapsulation — no public property setters
    // ---------------------------------------------------------------------------

    [Fact]
    public void Person_HasNoPublicPropertySetters()
    {
        var personType = typeof(Person);

        // All mutable properties must have no publicly accessible setter
        Assert.Null(personType.GetProperty(nameof(Person.FirstName))!.GetSetMethod(nonPublic: false));
        Assert.Null(personType.GetProperty(nameof(Person.PaternalLastName))!.GetSetMethod(nonPublic: false));
        Assert.Null(personType.GetProperty(nameof(Person.MaternalLastName))!.GetSetMethod(nonPublic: false));
        Assert.Null(personType.GetProperty(nameof(Person.DateOfBirth))!.GetSetMethod(nonPublic: false));
        Assert.Null(personType.GetProperty(nameof(Person.Id))!.GetSetMethod(nonPublic: false));

        // Age must have no setter at all (computed property, never stored)
        Assert.Null(personType.GetProperty(nameof(Person.Age))!.SetMethod);
    }

    // ---------------------------------------------------------------------------
    // UpdateName — valid mutation
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateName_WithValidNames_MutatesFields()
    {
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, ValidDateOfBirth);
        var newFirst = "Jane";
        var newPaternal = "Brown";
        var newMaternal = "Green";

        person.UpdateName(newFirst, newPaternal, newMaternal);

        Assert.Equal(newFirst, person.FirstName);
        Assert.Equal(newPaternal, person.PaternalLastName);
        Assert.Equal(newMaternal, person.MaternalLastName);
    }

    [Fact]
    public void UpdateName_WithInvalidName_ThrowsAndLeavesStateUnchanged()
    {
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, ValidDateOfBirth);
        var originalFirst = person.FirstName;
        var originalPaternal = person.PaternalLastName;
        var originalMaternal = person.MaternalLastName;

        Assert.Throws<DomainException>(() =>
            person.UpdateName("A", ValidPaternal, ValidMaternal));

        // State must be unchanged after a failed update
        Assert.Equal(originalFirst, person.FirstName);
        Assert.Equal(originalPaternal, person.PaternalLastName);
        Assert.Equal(originalMaternal, person.MaternalLastName);
    }

    // ---------------------------------------------------------------------------
    // UpdateDateOfBirth — valid mutation
    // ---------------------------------------------------------------------------

    [Fact]
    public void UpdateDateOfBirth_WithValidDate_Mutates()
    {
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, ValidDateOfBirth);
        var newDate = DateOnly.FromDateTime(DateTime.Today).AddYears(-25);

        person.UpdateDateOfBirth(newDate);

        Assert.Equal(newDate, person.DateOfBirth);
    }

    [Fact]
    public void UpdateDateOfBirth_WithFutureDate_ThrowsAndLeavesStateUnchanged()
    {
        var person = Person.Create(ValidFirst, ValidPaternal, ValidMaternal, ValidDateOfBirth);
        var originalDate = person.DateOfBirth;
        var futureDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        Assert.Throws<DomainException>(() => person.UpdateDateOfBirth(futureDate));

        Assert.Equal(originalDate, person.DateOfBirth);
    }
}
