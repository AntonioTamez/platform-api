using FluentValidation.TestHelper;
using PersonsAPI.Application.Commands;
using PersonsAPI.Application.DTOs;

namespace PersonsAPI.Application.Tests.Commands;

/// <summary>
/// Unit tests for <see cref="PatchPersonCommandValidator"/>.
/// Proves the <c>When()</c> conditions work correctly: null fields skip validation (Pitfall 5 guard),
/// and non-null fields are validated to the same bounds as full-replacement commands (D-09).
/// </summary>
public class PatchPersonCommandValidatorTests
{
    private readonly PatchPersonCommandValidator _validator = new();

    [Fact]
    public void Validate_AllFieldsNull_HasNoErrors()
    {
        // Proves null fields skip validation entirely (Pitfall 5 guard).
        var command = new PatchPersonCommand(1, new UpdatePersonDto());
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_OnlyFirstNameProvidedValid_HasNoErrors()
    {
        var command = new PatchPersonCommand(1, new UpdatePersonDto { FirstName = "Ada" });
        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_FirstNameProvidedEmpty_HasError()
    {
        var command = new PatchPersonCommand(1, new UpdatePersonDto { FirstName = "" });
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(c => c.Dto.FirstName);
    }

    [Fact]
    public void Validate_DateOfBirthInFuture_HasError()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        var command = new PatchPersonCommand(1, new UpdatePersonDto { DateOfBirth = futureDate });

        _validator.TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.Dto.DateOfBirth!.Value)
            .WithErrorMessage("DateOfBirth cannot be in the future.");
    }
}
