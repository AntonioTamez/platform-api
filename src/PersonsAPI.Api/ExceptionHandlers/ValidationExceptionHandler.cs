using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace PersonsAPI.Api.ExceptionHandlers;

/// <summary>
/// Maps <see cref="FluentValidation.ValidationException"/> thrown by <c>ValidationBehavior&lt;,&gt;</c>
/// to a 400 Bad Request RFC 9457 Problem Details response with a field-keyed errors dictionary.
///
/// <para><b>D-01:</b> This handler is registered SECOND via <c>AddExceptionHandler&lt;ValidationExceptionHandler&gt;()</c>,
/// after <see cref="PersonNotFoundExceptionHandler"/>.</para>
///
/// <para><b>D-04:</b> The <c>extensions["errors"]</c> dictionary groups validation failures by
/// <c>PropertyName</c> with <c>string[]</c> of <c>ErrorMessage</c> values.</para>
///
/// <para><b>D-05:</b> The <c>Detail</c> field is the fixed string
/// "One or more validation errors occurred."</para>
///
/// <para><b>ERR-01 / ERR-02:</b> Emits <c>application/problem+json</c> with
/// <c>Type="about:blank"</c>, <c>Title="Validation Failed"</c>, <c>Status=400</c>.</para>
/// </summary>
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        // D-04: group by PropertyName, values as string arrays
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Title = "Validation Failed",
            Status = StatusCodes.Status400BadRequest,
            Detail = "One or more validation errors occurred."  // D-05
        };
        // Serialise to JsonElement first — this guarantees the value is a well-formed
        // JSON object regardless of the ProblemDetails serialiser pipeline in use,
        // including AOT/source-generation mode where Dictionary<string, string[]>
        // stored as object? may not be registered (CR-02).
        problemDetails.Extensions["errors"] = JsonSerializer.SerializeToElement(errors);

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
