namespace PersonsAPI.Application.DTOs;

/// <summary>Request body for PUT /api/persons/{id}. All four fields are required (full replacement).</summary>
public record UpdatePersonRequest(
    string FirstName,
    string PaternalLastName,
    string MaternalLastName,
    DateOnly DateOfBirth);
