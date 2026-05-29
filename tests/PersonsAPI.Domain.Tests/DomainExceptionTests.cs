using PersonsAPI.Domain.Exceptions;

namespace PersonsAPI.Domain.Tests;

public class DomainExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var exception = new DomainException("rule broken");

        Assert.Equal("rule broken", exception.Message);
    }

    [Fact]
    public void DomainException_InheritsDirectlyFromException()
    {
        Assert.True(typeof(DomainException).BaseType == typeof(Exception));
    }
}
