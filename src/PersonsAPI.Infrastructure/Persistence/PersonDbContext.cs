using Microsoft.EntityFrameworkCore;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Infrastructure.Persistence;

/// <summary>
/// EF Core database context for the Persons aggregate.
/// Sealed to prevent unintended inheritance; uses a primary constructor for DI compatibility.
/// Entity configurations are applied automatically via <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>
/// — including <c>PersonEntityConfiguration</c> which excludes the computed <see cref="Person.Age"/> property.
/// </summary>
public sealed class PersonDbContext(DbContextOptions<PersonDbContext> options) : DbContext(options)
{
    /// <summary>The set of all <see cref="Person"/> entities tracked by this context.</summary>
    public DbSet<Person> Persons => Set<Person>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonDbContext).Assembly);
    }
}
