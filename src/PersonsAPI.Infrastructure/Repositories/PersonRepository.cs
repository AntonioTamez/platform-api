using Microsoft.EntityFrameworkCore;
using PersonsAPI.Application.Ports;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;

namespace PersonsAPI.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of the <see cref="IPersonRepository"/> secondary port.
/// Sealed to prevent unintended inheritance; uses a primary constructor for DI.
///
/// <para>
/// This class is the secondary adapter in Hexagonal Architecture — it implements the persistence
/// contract defined in the Application layer using EF Core InMemory as the backing store.
/// </para>
///
/// <para>
/// Read methods (<see cref="GetAllAsync"/> and <see cref="GetByIdAsync"/>) never call
/// <c>SaveChangesAsync</c>. Write methods (<see cref="AddAsync"/>, <see cref="UpdateAsync"/>,
/// <see cref="DeleteAsync"/>) always call <c>SaveChangesAsync</c> after mutating state.
/// </para>
///
/// <para>
/// <see cref="GetAllAsync"/> returns <see cref="IReadOnlyList{T}"/> — never <c>IQueryable&lt;Person&gt;</c>.
/// Callers cannot compose LINQ over the result and cannot bypass the repository contract.
/// </para>
/// </summary>
public sealed class PersonRepository(PersonDbContext context) : IPersonRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<Person>> GetAllAsync(CancellationToken cancellationToken = default)
        => await context.Persons.ToListAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Person?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Persons.FindAsync([id], cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(Person person, CancellationToken cancellationToken = default)
    {
        await context.Persons.AddAsync(person, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Remove(person);
        await context.SaveChangesAsync(cancellationToken);
    }
}
