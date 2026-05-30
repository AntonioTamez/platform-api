using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>
/// Partially updates a person's data (PATCH). Returns the updated <see cref="PersonResponse"/>.
/// The Dto parameter type MUST be <see cref="UpdatePersonDto"/> (nullable fields) — NOT UpdatePersonRequest (Pitfall 5).
/// </summary>
public record PatchPersonCommand(int Id, UpdatePersonDto Dto) : ICommand<PersonResponse>;

/// <summary>
/// Validates non-null fields in <see cref="PatchPersonCommand"/> before the handler runs.
/// Null fields (not patched) are skipped — <c>When()</c> conditions prevent NotEmpty failures on omitted fields (Pitfall 5 guard).
/// </summary>
public sealed class PatchPersonCommandValidator : AbstractValidator<PatchPersonCommand>
{
    public PatchPersonCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be a positive integer.");

        When(x => x.Dto.FirstName is not null, () =>
        {
            RuleFor(x => x.Dto.FirstName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.PaternalLastName is not null, () =>
        {
            RuleFor(x => x.Dto.PaternalLastName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.MaternalLastName is not null, () =>
        {
            RuleFor(x => x.Dto.MaternalLastName!)
                .NotEmpty()
                .MinimumLength(2)
                .MaximumLength(100);
        });

        When(x => x.Dto.DateOfBirth is not null, () =>
        {
            RuleFor(x => x.Dto.DateOfBirth!.Value)
                .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
                .WithMessage("DateOfBirth cannot be in the future.");
        });
    }
}

/// <summary>
/// Handles <see cref="PatchPersonCommand"/>.
/// Applies only non-null DTO fields using the <c>dto.Field ?? person.Field</c> fallback pattern
/// to prevent passing null to <see cref="PersonsAPI.Domain.Entities.Person.UpdateName"/> (Pitfall 6 guard).
/// </summary>
public sealed class PatchPersonHandler(IPersonRepository repository)
    : ICommandHandler<PatchPersonCommand, PersonResponse>
{
    /// <inheritdoc/>
    public async ValueTask<PersonResponse> Handle(
        PatchPersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        var dto = command.Dto;

        // Apply name fields only when at least one is non-null (Pitfall 6 guard).
        // dto.Field ?? person.Field ensures Person.UpdateName never receives null.
        if (dto.FirstName is not null || dto.PaternalLastName is not null || dto.MaternalLastName is not null)
            person.UpdateName(
                dto.FirstName ?? person.FirstName,
                dto.PaternalLastName ?? person.PaternalLastName,
                dto.MaternalLastName ?? person.MaternalLastName);

        if (dto.DateOfBirth is not null)
            person.UpdateDateOfBirth(dto.DateOfBirth.Value);

        await repository.UpdateAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
