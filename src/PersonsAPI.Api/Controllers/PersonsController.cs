using Mediator;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using PersonsAPI.Application.Commands;
using PersonsAPI.Application.DTOs;
using PersonsAPI.Application.Queries;

namespace PersonsAPI.Api.Controllers;

/// <summary>
/// HTTP controller for the <c>/api/persons</c> resource.
/// Exposes six endpoints that dispatch to Mediator handlers:
/// GET (all), GET (by id), POST (create), PUT (update), PATCH (partial update), DELETE.
///
/// <para><b>PATCH (D-07/D-08/D-09):</b> Constructs a fresh empty <see cref="UpdatePersonDto"/>,
/// applies the <c>JsonPatchDocument&lt;UpdatePersonDto&gt;</c> to it with ModelState validation,
/// returns <c>ValidationProblem(ModelState)</c> on structural failure, then dispatches
/// <see cref="PatchPersonCommand"/>.</para>
///
/// <para><b>POST (Pitfall 7):</b> Returns 201 Created with a Location header via
/// <c>CreatedAtAction(nameof(GetById), ...)</c> — never 200.</para>
///
/// <para>All exception-to-HTTP mapping is handled centrally by the
/// <see cref="PersonNotFoundExceptionHandler"/> (404) and
/// <see cref="ValidationExceptionHandler"/> (400) registered in Program.cs.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PersonsController(IMediator mediator) : ControllerBase
{
    /// <summary>Returns all persons.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllPersonsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single person by id. Throws PersonNotFoundException for unknown ids.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPersonByIdQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Creates a new person and returns 201 Created with Location header (Pitfall 7).</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePersonRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreatePersonCommand(
                request.FirstName,
                request.PaternalLastName,
                request.MaternalLastName,
                request.DateOfBirth),
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Replaces all fields of an existing person.</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePersonRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdatePersonCommand(id, request), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Applies a JSON Patch document to a person (D-07/D-08/D-09).
    /// Constructs a fresh empty <see cref="UpdatePersonDto"/> (all props null),
    /// applies the patch with ModelState binding, and returns <c>ValidationProblem</c>
    /// on structural failure before dispatching.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch(int id, [FromBody] JsonPatchDocument<UpdatePersonDto> patchDoc, CancellationToken cancellationToken)
    {
        var dto = new UpdatePersonDto();         // D-08: fresh empty DTO, all props null
        patchDoc.ApplyTo(dto, error =>           // D-09: STJ package uses Action<JsonPatchError> overload
            ModelState.AddModelError(error.Operation.path, error.ErrorMessage));
        if (!ModelState.IsValid)                 // D-09: structural JsonPatch errors
            return ValidationProblem(ModelState);
        var result = await mediator.Send(new PatchPersonCommand(id, dto), cancellationToken);
        return Ok(result);
    }

    /// <summary>Deletes a person. Returns 204 No Content on success.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePersonCommand(id), cancellationToken);
        return NoContent();
    }
}
