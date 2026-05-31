namespace PersonsAPI.Application.DTOs;

/// <summary>
/// Mutable PATCH target DTO for PATCH /api/persons/{id}.
///
/// <para><b>D-06 / D-08:</b> The controller creates a fresh <c>new UpdatePersonDto()</c>
/// (all properties null), applies <c>JsonPatchDocument&lt;UpdatePersonDto&gt;.ApplyTo(dto, ModelState)</c>,
/// then dispatches <c>PatchPersonCommand(id, dto)</c>. Only non-null fields are applied to the
/// domain entity by the handler.</para>
///
/// <para><b>Critical Fix (RESEARCH.md):</b> Must be a <c>class</c> with <c>{ get; set; }</c>
/// properties. <c>JsonPatchDocument&lt;T&gt;.ApplyTo()</c> mutates the target object in place via
/// reflection. A positional record with init-only properties throws at runtime because the
/// setter is not publicly writable.</para>
/// </summary>
public class UpdatePersonDto
{
    public string? FirstName { get; set; }
    public string? PaternalLastName { get; set; }
    public string? MaternalLastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
}
