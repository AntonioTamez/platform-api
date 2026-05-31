using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core fluent configuration for the <see cref="Person"/> entity.
/// Sealed to prevent unintended inheritance.
///
/// <para>
/// <b>Critical:</b> <see cref="Person.Age"/> is excluded via <c>builder.Ignore(p =&gt; p.Age)</c>
/// because it is a computed domain property (Domain D-08) — it is derived from
/// <see cref="Person.DateOfBirth"/> at runtime and must never be stored in the database.
/// Without this call, EF Core would attempt to map the getter-only property and throw a
/// runtime <see cref="System.InvalidOperationException"/> during model building.
/// </para>
///
/// <para>
/// No explicit <c>Property()</c> calls are needed for <see cref="Person.FirstName"/>,
/// <see cref="Person.PaternalLastName"/>, <see cref="Person.MaternalLastName"/>, or
/// <see cref="Person.DateOfBirth"/> — EF Core convention maps <c>private set</c> properties
/// via reflection without additional <c>UsePropertyAccessMode</c> configuration.
/// </para>
/// </summary>
public sealed class PersonEntityConfiguration : IEntityTypeConfiguration<Person>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Ignore(p => p.Age);
    }
}
