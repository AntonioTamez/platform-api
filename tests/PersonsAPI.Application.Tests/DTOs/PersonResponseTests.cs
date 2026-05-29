using PersonsAPI.Application.DTOs;
using PersonsAPI.Domain.Entities;

namespace PersonsAPI.Application.Tests.DTOs;

public class PersonResponseTests
{
    [Fact]
    public void FromDomain_MapsAllFieldsIncludingComputedAge()
    {
        // Arrange — create a person born exactly 30 years ago
        var dateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-30));
        var person = Person.Create("Ada", "Lovelace", "Byron", dateOfBirth);

        // Act
        var response = PersonResponse.FromDomain(person);

        // Assert — all fields including the computed Age must map correctly
        Assert.Equal(person.Id, response.Id);
        Assert.Equal(person.FirstName, response.FirstName);
        Assert.Equal(person.PaternalLastName, response.PaternalLastName);
        Assert.Equal(person.MaternalLastName, response.MaternalLastName);
        Assert.Equal(person.DateOfBirth, response.DateOfBirth);
        Assert.Equal(person.Age, response.Age);
        Assert.Equal(30, response.Age);
    }

    [Fact]
    public void PersonResponse_RecordEquality_HoldsByValue()
    {
        // Arrange — two instances built with the same field values
        var dateOfBirth = new DateOnly(1990, 6, 15);
        var a = new PersonResponse(1, "John", "Doe", "Smith", dateOfBirth, 35);
        var b = new PersonResponse(1, "John", "Doe", "Smith", dateOfBirth, 35);

        // Assert — C# records use value equality by default
        Assert.Equal(a, b);
    }
}
