using FluentValidation.TestHelper;
using PersonsAPI.Application.Commands;

namespace PersonsAPI.Application.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="CreatePersonCommandValidator"/>.
/// Covers happy path and each failure mode (D-09 — mirrors domain invariants).
/// </summary>
public class CreatePersonCommandValidatorTests
{
    private readonly CreatePersonCommandValidator _validator = new();

    [Fact]
    public void Validate_HappyPath_HasNoErrors()
    {
        var command = new CreatePersonCommand(
            "Ada",
            "Lovelace",
            "Byron",
            DateOnly.FromDateTime(DateTime.Today).AddYears(-30));

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void Validate_FirstNameEmptyOrTooShort_HasError(string firstName)
    {
        var command = new CreatePersonCommand(
            firstName,
            "Lovelace",
            "Byron",
            DateOnly.FromDateTime(DateTime.Today).AddYears(-30));

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Validate_FirstNameTooLong_HasError()
    {
        var longName = new string('A', 101);
        var command = new CreatePersonCommand(
            longName,
            "Lovelace",
            "Byron",
            DateOnly.FromDateTime(DateTime.Today).AddYears(-30));

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.FirstName);
    }

    [Fact]
    public void Validate_DateOfBirthInFuture_HasError()
    {
        var command = new CreatePersonCommand(
            "Ada",
            "Lovelace",
            "Byron",
            DateOnly.FromDateTime(DateTime.Today).AddDays(1));

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.DateOfBirth)
            .WithErrorMessage("DateOfBirth cannot be in the future.");
    }
}
