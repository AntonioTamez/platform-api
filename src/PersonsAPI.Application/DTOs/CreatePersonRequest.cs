namespace PersonsAPI.Application.DTOs;

/// <summary>Request body for POST /api/persons. All four fields are required.</summary>
public record CreatePersonRequest(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth);
