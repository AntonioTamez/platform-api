using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using PersonsAPI.Application.DTOs;

namespace PersonsAPI.Api.Tests.Integration;

/// <summary>
/// Integration tests covering all six PersonsController endpoints.
/// Uses <see cref="WebApplicationFactory{T}"/> to boot the real ASP.NET Core host
/// with the InMemory store seeded by <c>DataSeeder</c> (via Program.cs startup).
/// </summary>
public sealed class PersonsEndpointsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient() => factory.CreateClient();

    [Fact]
    public async Task GetAll_ReturnsThreeSeededPersons()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/persons");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var persons = await response.Content.ReadFromJsonAsync<PersonResponse[]>();
        Assert.NotNull(persons);
        Assert.True(persons.Length >= 3, $"Expected at least 3 seeded persons, got {persons.Length}");
        Assert.Contains(persons, p => p.FirstName == "María");
        Assert.Contains(persons, p => p.FirstName == "Carlos");
        Assert.Contains(persons, p => p.FirstName == "Ana");
    }

    [Fact]
    public async Task GetById_KnownId_Returns200WithPerson()
    {
        var client = CreateClient();

        // Discover a known id from the seeded data
        var all = await client.GetFromJsonAsync<PersonResponse[]>("/api/persons");
        Assert.NotNull(all);
        Assert.True(all.Length > 0, "Expected at least one seeded person");
        var knownId = all[0].Id;

        var response = await client.GetAsync($"/api/persons/{knownId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(person);
        Assert.Equal(knownId, person.Id);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404ProblemDetails()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/persons/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Not Found", problem.Title);
        Assert.Equal(404, problem.Status);
    }

    [Fact]
    public async Task Post_ValidBody_Returns201WithLocation()
    {
        var client = CreateClient();
        var request = new CreatePersonRequest("TestFn", "TestPa", "TestMa", new DateOnly(1990, 1, 1));

        var response = await client.PostAsJsonAsync("/api/persons", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("/api/persons/", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        var person = await response.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(person);
        Assert.NotEqual(0, person.Id);
        Assert.Equal("TestFn", person.FirstName);
        Assert.Equal("TestPa", person.PaternalLastName);
        Assert.Equal("TestMa", person.MaternalLastName);
    }

    [Fact]
    public async Task Patch_ReplaceFirstName_Returns200WithUpdatedName()
    {
        var client = CreateClient();

        // Create a person to patch
        var created = await client.PostAsJsonAsync("/api/persons",
            new CreatePersonRequest("Original", "PatchPa", "PatchMa", new DateOnly(1985, 6, 15)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var person = await created.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(person);
        var newId = person.Id;

        // PATCH: replace firstName
        var patchBody = """[{"op":"replace","path":"/firstName","value":"Patched"}]""";
        var content = new StringContent(patchBody, Encoding.UTF8, "application/json-patch+json");
        var patchResponse = await client.PatchAsync($"/api/persons/{newId}", content);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var patched = await patchResponse.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(patched);
        Assert.Equal("Patched", patched.FirstName);
    }

    [Fact]
    public async Task Delete_KnownId_Returns204()
    {
        var client = CreateClient();

        // Create a person to delete
        var created = await client.PostAsJsonAsync("/api/persons",
            new CreatePersonRequest("DeleteMe", "DelPa", "DelMa", new DateOnly(2000, 3, 10)));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var person = await created.Content.ReadFromJsonAsync<PersonResponse>();
        Assert.NotNull(person);
        var deleteId = person.Id;

        // DELETE
        var deleteResponse = await client.DeleteAsync($"/api/persons/{deleteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify 404 on subsequent GET
        var getResponse = await client.GetAsync($"/api/persons/{deleteId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
