using Mediator;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Commands;

/// <summary>
/// Deletes a person by ID. Returns <see cref="Unit"/> (no content).
/// Throws <see cref="PersonNotFoundException"/> if not found.
/// No validator registered — the only input is a route ID (D-08).
/// </summary>
public record DeletePersonCommand(int Id) : ICommand<Unit>;

/// <summary>
/// Handles <see cref="DeletePersonCommand"/>.
/// Fetches the entity first (EF Core tracked-entity pattern), then deletes it.
/// </summary>
public sealed class DeletePersonHandler(IPersonRepository repository)
    : ICommandHandler<DeletePersonCommand, Unit>
{
    /// <inheritdoc/>
    public async ValueTask<Unit> Handle(
        DeletePersonCommand command,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new PersonNotFoundException(command.Id);

        await repository.DeleteAsync(person, cancellationToken);
        return Unit.Value;
    }
}
