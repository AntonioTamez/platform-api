using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Exceptions;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Queries;

/// <summary>
/// Returns a single person by ID. Throws <see cref="PersonNotFoundException"/> when not found (D-03/D-04).
/// No body input — no validator registered (D-08).
/// </summary>
public record GetPersonByIdQuery(int Id) : IQuery<PersonResponse>;

/// <summary>
/// Handles <see cref="GetPersonByIdQuery"/>.
/// Throws <see cref="PersonNotFoundException"/> when <see cref="IPersonRepository.GetByIdAsync"/> returns null.
/// </summary>
public sealed class GetPersonByIdHandler(IPersonRepository repository)
    : IQueryHandler<GetPersonByIdQuery, PersonResponse>
{
    /// <inheritdoc/>
    public async ValueTask<PersonResponse> Handle(
        GetPersonByIdQuery query,
        CancellationToken cancellationToken)
    {
        var person = await repository.GetByIdAsync(query.Id, cancellationToken)
            ?? throw new PersonNotFoundException(query.Id);

        return PersonResponse.FromDomain(person);
    }
}
