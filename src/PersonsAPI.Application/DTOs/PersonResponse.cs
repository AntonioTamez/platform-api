using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.DTOs;

/// <summary>
/// Response DTO returned by all Person handlers.
/// <see cref="Age"/> is read from the domain entity's computed property — never stored.
/// Use <see cref="FromDomain"/> to produce instances; do not construct directly from handler code.
/// </summary>
public record PersonResponse(
    int Id,
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth,
    int Age)
{
    /// <summary>
    /// Maps a domain <see cref="Person"/> entity to a <see cref="PersonResponse"/>.
    /// Age is read from the domain entity's computed property — never stored.
    /// </summary>
    public static PersonResponse FromDomain(Person person) => new(
        person.Id,
        person.FirstName,
        person.PaternalLastName,
        person.MaternalLastName,
        person.DateOfBirth,
        person.Age);
}
