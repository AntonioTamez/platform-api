using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace PersonsAPI.Api.Tests.Integration;

/// <summary>
/// Integration tests proving RFC 9457 Problem Details and documentation endpoint behavior.
///
/// <para>Covers requirements: ERR-01 (application/problem+json content type),
/// ERR-02 (400 with errors dictionary), ERR-03 (404 for unknown person id),
/// DOC-01 (OpenAPI document at /openapi/v1.json), DOC-02 (Scalar UI at /scalar/v1).</para>
/// </summary>
public sealed class ProblemDetailsTests(ResetableApiFactory factory)
    : IClassFixture<ResetableApiFactory>
{
    [Fact]
    public async Task Get_UnknownPerson_Returns404WithRfc9457ProblemDetails()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/persons/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        Assert.Equal("about:blank", root.GetProperty("type").GetString());
        Assert.Equal("Not Found", root.GetProperty("title").GetString());
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Contains("Person with ID 999999", root.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Post_EmptyBody_Returns400WithErrorsDictionary()
    {
        var client = factory.CreateClient();
        // Send all fields present but with invalid values (passes model binding, fails FluentValidation).
        // FirstName "A" fails MinimumLength(2) — triggers ValidationExceptionHandler with title "Validation Failed".
        // Sending {} triggers [ApiController] automatic binding validation (different title); this approach
        // exercises the actual FluentValidation pipeline.
        var content = new StringContent(
            """{"firstName":"A","paternalLastName":"P","maternalLastName":"M","dateOfBirth":"1990-01-01"}""",
            Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/persons", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var root = doc.RootElement;

        Assert.Equal("Validation Failed", root.GetProperty("title").GetString());
        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("One or more validation errors occurred.", root.GetProperty("detail").GetString());

        // D-04: errors extension must be a non-empty object keyed by property name
        Assert.True(root.TryGetProperty("errors", out var errors), "Response must contain 'errors' extension");
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);

        var keys = errors.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FirstName", keys);
        Assert.Contains("PaternalLastName", keys);
        Assert.Contains("MaternalLastName", keys);
    }

    [Fact]
    public async Task Get_OpenApiDocument_Returns200Json()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"openapi\"", body);
        // Route token [controller] preserves casing: PersonsController → /api/Persons
        Assert.Contains("/api/Persons", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_ScalarUi_Returns200Html()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType.MediaType);
    }
}
