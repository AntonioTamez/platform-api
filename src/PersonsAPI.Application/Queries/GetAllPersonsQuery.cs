using Mediator;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Ports;

namespace PersonsAPI.Application.Queries;

/// <summary>
/// Returns all persons as a read-only list. No body input — no validator registered (D-08).
/// </summary>
public record GetAllPersonsQuery : IQuery<IReadOnlyList<PersonResponse>>;

/// <summary>
/// Handles <see cref="GetAllPersonsQuery"/> by delegating to <see cref="IPersonRepository.GetAllAsync"/>
/// and projecting each entity to <see cref="PersonResponse"/> via the static factory.
/// </summary>
public sealed class GetAllPersonsHandler(IPersonRepository repository)
    : IQueryHandler<GetAllPersonsQuery, IReadOnlyList<PersonResponse>>
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<PersonResponse>> Handle(
        GetAllPersonsQuery query,
        CancellationToken cancellationToken)
    {
        var persons = await repository.GetAllAsync(cancellationToken);
        return persons.Select(PersonResponse.FromDomain).ToList().AsReadOnly();
    }
}
