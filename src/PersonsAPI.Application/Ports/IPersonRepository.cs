using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.Ports;

/// <summary>
/// Secondary port: persistence contract for Person aggregates.
/// Implemented in the Infrastructure layer (Phase 3) by PersonRepository using EF Core.
/// </summary>
public interface IPersonRepository
{
    /// <summary>Returns all persons. Never returns null; returns empty list when no records exist.</summary>
    Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the person with the given ID, or null if not found.</summary>
    Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Persists a new person. Id is assigned by the store (EF Core identity).</summary>
    Task AddAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing person (entity already tracked or reattached).</summary>
    Task UpdateAsync(Person person, CancellationToken cancellationToken = default);

    /// <summary>Removes a person from the store.</summary>
    Task DeleteAsync(Person person, CancellationToken cancellationToken = default);
}
