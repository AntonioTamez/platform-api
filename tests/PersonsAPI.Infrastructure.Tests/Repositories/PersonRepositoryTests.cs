using Microsoft.EntityFrameworkCore;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;
using PersonsAPI.Infrastructure.Repositories;

namespace PersonsAPI.Infrastructure.Tests.Repositories;

/// <summary>
/// Integration tests for <see cref="PersonRepository"/> against the EF Core InMemory adapter.
/// Each test uses its own Guid-named InMemory database (T-02) to prevent state contamination.
/// No mocking library is used — PersonRepository is instantiated directly with a real DbContext.
/// </summary>
public sealed class PersonRepositoryTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a fully isolated <see cref="PersonDbContext"/> backed by a new InMemory database.
    /// Using <see cref="Guid.NewGuid"/> as the database name guarantees zero state contamination
    /// across tests (T-02, RESEARCH.md Pitfall 3).
    /// </summary>
    private static PersonDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PersonDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>Creates a valid <see cref="Person"/> via the domain factory for use in tests.</summary>
    private static Person CreateValidPerson(
        string firstName = "María",
        string paternalLastName = "García",
        string maternalLastName = "López",
        DateOnly? dateOfBirth = null)
        => Person.Create(firstName, paternalLastName, maternalLastName,
            dateOfBirth ?? new DateOnly(1994, 6, 15));

    // ---------------------------------------------------------------------------
    // GetAllAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetAllAsync_WhenPersonsExist_ReturnsAllPersons()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new PersonRepository(context);
        var person = CreateValidPerson();
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        // Act
        var result = await repo.GetAllAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("María", result[0].FirstName);
    }

    // ---------------------------------------------------------------------------
    // GetByIdAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetByIdAsync_WhenPersonExists_ReturnsPerson()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new PersonRepository(context);
        var person = CreateValidPerson();
        context.Persons.Add(person);
        await context.SaveChangesAsync();
        var savedId = person.Id;

        // Act
        var result = await repo.GetByIdAsync(savedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("García", result!.PaternalLastName);
    }

    // ---------------------------------------------------------------------------
    // AddAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AddAsync_PersistsPersonAndAssignsId()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new PersonRepository(context);
        var person = CreateValidPerson();

        // Act
        await repo.AddAsync(person);

        // Assert
        Assert.True(person.Id > 0);
        Assert.Equal(1, await context.Persons.CountAsync());
    }

    // ---------------------------------------------------------------------------
    // UpdateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_PersistsChangesToPerson()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new PersonRepository(context);
        var person = CreateValidPerson();
        context.Persons.Add(person);
        await context.SaveChangesAsync();
        person.UpdateName("Carlos", "Ramírez", "Martínez");

        // Act
        await repo.UpdateAsync(person);

        // Assert
        var updated = await context.Persons.FindAsync(person.Id);
        Assert.Equal("Carlos", updated!.FirstName);
    }

    // ---------------------------------------------------------------------------
    // DeleteAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_RemovesPersonFromStore()
    {
        // Arrange
        await using var context = CreateContext();
        var repo = new PersonRepository(context);
        var person = CreateValidPerson();
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        // Act
        await repo.DeleteAsync(person);

        // Assert
        Assert.Equal(0, await context.Persons.CountAsync());
    }
}
