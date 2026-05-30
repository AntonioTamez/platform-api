using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>
/// Fully replaces a person's data (PUT). Returns the updated <see cref="PersonResponse"/>.
/// </summary>
public record UpdatePersonCommand(int Id, UpdatePersonRequest Dto) : ICommand<PersonResponse>;

/// <summary>
/// Validates <see cref="UpdatePersonCommand"/> inputs before the handler runs.
/// Mirrors domain invariants (D-09): name fields NotEmpty + length 2–100, DateOfBirth not in the future.
/// </summary>
public sealed class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be a positive integer.");

        RuleFor(x => x.Dto.FirstName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.PaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.MaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Dto.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("DateOfBirth cannot be in the future.");
    }
}

/// <summary>
/// Handles <see cref="UpdatePersonCommand"/>.
/// Fetches the entity, throws <see cref="PersonNotFoundException"/> when not found,
/// then calls <see cref="PersonsAPI.Domain.Entities.Person.UpdateName"/> and
/// <see cref="PersonsAPI.Domain.Entities.Person.UpdateDateOfBirth"/> to apply changes.
/// </summary>
public sealed class UpdatePersonHandler(IPersonRepository repository)
    : ICommandHandler<UpdatePersonCommand, PersonResponse>
{
    /// <inheritdoc/>
    public async ValueTask<PersonResponse> Handle(
        UpdatePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        person.UpdateName(
            command.Dto.FirstName,
            command.Dto.PaternalLastName,
            command.Dto.MaternalLastName);

        person.UpdateDateOfBirth(command.Dto.DateOfBirth);

        await repository.UpdateAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
