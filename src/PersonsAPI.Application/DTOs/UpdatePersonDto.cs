namespace PersonsAPI.Application.DTOs;

/// <summary>
/// DTO for PATCH /api/persons/{id}.
/// The controller (Phase 4) applies a <c>JsonPatchDocument&lt;UpdatePersonDto&gt;</c> to a fresh
/// instance of this type, then dispatches <c>PatchPersonCommand</c> with the result.
/// Only non-null fields are applied to the domain entity by the handler.
/// </summary>
public record UpdatePersonDto(
    string? FirstName,
    string? PaternalLastName,
    string? MaternalLastName,
    DateOnly? DateOfBirth);
