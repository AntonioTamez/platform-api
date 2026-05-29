using FluentValidation;
using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Ports;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.Commands;

/// <summary>
/// Creates a new person. Returns the created <see cref="PersonResponse"/>.
/// </summary>
public record CreatePersonCommand(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth) : ICommand<PersonResponse>;

/// <summary>
/// Validates <see cref="CreatePersonCommand"/> inputs before the handler runs.
/// Mirrors domain invariants (D-09): Application validates for field-level 400 detail;
/// Domain is the second line of defense.
/// </summary>
public sealed class CreatePersonCommandValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.PaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.MaternalLastName)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.DateOfBirth)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("DateOfBirth cannot be in the future.");
    }
}

/// <summary>
/// Handles <see cref="CreatePersonCommand"/>.
/// Delegates entity creation to <see cref="Person.Create"/> — never sets properties directly.
/// </summary>
public sealed class CreatePersonHandler(IPersonRepository repository)
    : ICommandHandler<CreatePersonCommand, PersonResponse>
{
    /// <inheritdoc/>
    public async ValueTask<PersonResponse> Handle(
        CreatePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = Person.Create(
            command.FirstName,
            command.PaternalLastName,
            command.MaternalLastName,
            command.DateOfBirth);

        await repository.AddAsync(person, cancellationToken);
        return PersonResponse.FromDomain(person);
    }
}
