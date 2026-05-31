using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PersonsAPI.Application.Exceptions;

namespace PersonsAPI.Api.ExceptionHandlers;

/// <summary>
/// Maps <see cref="PersonNotFoundException"/> to a 404 Not Found RFC 9457 Problem Details response.
///
/// <para><b>D-01:</b> This handler is registered FIRST via <c>AddExceptionHandler&lt;PersonNotFoundExceptionHandler&gt;()</c>
/// so it runs before the <see cref="ValidationExceptionHandler"/>. The framework tries handlers in
/// registration order.</para>
///
/// <para><b>D-03:</b> The <c>Detail</c> field is set to <c>exception.Message</c> verbatim — the
/// <see cref="PersonNotFoundException"/> constructor already formats the message as
/// "Person with ID {id} was not found."</para>
///
/// <para><b>ERR-01 / ERR-03:</b> Emits <c>application/problem+json</c> with
/// <c>Type="about:blank"</c>, <c>Title="Not Found"</c>, <c>Status=404</c>.</para>
/// </summary>
public sealed class PersonNotFoundExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not PersonNotFoundException notFound)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "about:blank",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = notFound.Message  // D-03: "Person with ID {id} was not found."
            }
        });
    }
}
