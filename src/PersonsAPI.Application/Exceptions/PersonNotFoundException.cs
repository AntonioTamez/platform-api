namespace PersonsAPI.Application.Exceptions;

/// <summary>
/// Thrown by Application handlers when a Person with the requested ID does not exist in the store.
/// Caught by the API layer (Phase 4) and mapped to 404 Problem Details.
/// The <see cref="PersonId"/> property surfaces the requested ID structurally so Phase 4
/// can build Problem Details without parsing the message string.
/// </summary>
public sealed class PersonNotFoundException : Exception
{
    /// <summary>The ID of the Person that was not found.</summary>
    public int PersonId { get; }

    public PersonNotFoundException(int id)
        : base($"Person with ID {id} was not found.")
    {
        PersonId = id;
    }

    public PersonNotFoundException(int id, Exception innerException)
        : base($"Person with ID {id} was not found.", innerException)
    {
        PersonId = id;
    }
}
