namespace PersonsAPI.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated.
/// The message describes the specific business rule violation in plain English.
/// Caught by the Application layer to produce appropriate HTTP error responses.
/// Do not catch this in the Domain layer — let it propagate.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
