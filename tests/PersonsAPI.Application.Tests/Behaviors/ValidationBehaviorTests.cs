using FluentValidation;
using FluentValidation.Results;
using Mediator;
using PersonsAPI.Application.Behaviors;

namespace PersonsAPI.Application.Tests.Behaviors;

/// <summary>
/// A minimal test message implementing ICommand&lt;string&gt; so TestCommand satisfies
/// the IPipelineBehavior&lt;TMessage, TResponse&gt; constraint: where TMessage : notnull, IMessage.
/// ICommand&lt;T&gt; inherits IMessage in Mediator.Abstractions.
/// </summary>
public record TestCommand(string Name) : ICommand<string>;

/// <summary>
/// Unit tests for <see cref="ValidationBehavior{TMessage, TResponse}"/>:
/// 1. D-10 — graceful short-circuit when no validators are registered.
/// 2. Pass-through when validators return a successful result.
/// 3. FluentValidation.ValidationException is thrown (with all failures) when any validator fails.
///
/// Avoids Moq — invocation tracking via captured flags/counters on inline stub classes.
/// </summary>
public sealed class ValidationBehaviorTests
{
    // -----------------------------------------------------------------------
    // Test 1: D-10 short-circuit — no validators registered
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_NoValidators_CallsNextAndReturnsResult()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>(
            Enumerable.Empty<IValidator<TestCommand>>());

        var nextCalled = false;
        MessageHandlerDelegate<TestCommand, string> next = (msg, ct) =>
        {
            nextCalled = true;
            return ValueTask.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(new TestCommand("x"), next, CancellationToken.None);

        // Assert
        Assert.Equal("ok", result);
        Assert.True(nextCalled, "next must be called when no validators are registered (D-10).");
    }

    // -----------------------------------------------------------------------
    // Test 2: Pass-through — validator returns successful ValidationResult
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ValidatorPasses_CallsNext()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>(
            new IValidator<TestCommand>[] { new PassingValidator() });

        var nextCalled = false;
        MessageHandlerDelegate<TestCommand, string> next = (msg, ct) =>
        {
            nextCalled = true;
            return ValueTask.FromResult("ok");
        };

        // Act
        var result = await behavior.Handle(new TestCommand("valid"), next, CancellationToken.None);

        // Assert
        Assert.Equal("ok", result);
        Assert.True(nextCalled, "next must be called when all validators pass.");
    }

    // -----------------------------------------------------------------------
    // Test 3: Failing validator — throws FluentValidation.ValidationException
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Handle_ValidatorFails_ThrowsValidationException()
    {
        // Arrange
        var behavior = new ValidationBehavior<TestCommand, string>(
            new IValidator<TestCommand>[] { new FailingValidator() });

        var nextCalled = false;
        MessageHandlerDelegate<TestCommand, string> next = (msg, ct) =>
        {
            nextCalled = true;
            return ValueTask.FromResult("should-not-reach");
        };

        // Act & Assert — must be FluentValidation.ValidationException, not a custom type
        var ex = await Assert.ThrowsAsync<ValidationException>(
            async () => await behavior.Handle(new TestCommand("bad"), next, CancellationToken.None));

        Assert.Equal(2, ex.Errors.Count());
        Assert.False(nextCalled, "next must NOT be called when validation fails.");
    }

    // -----------------------------------------------------------------------
    // Stub validators (inline; no Moq dependency)
    // -----------------------------------------------------------------------

    /// <summary>Always returns a successful ValidationResult.</summary>
    private sealed class PassingValidator : AbstractValidator<TestCommand>
    {
        // No rules → every input is valid (FluentValidation default behavior).
    }

    /// <summary>Returns two hard-coded failures — used by Test 3.</summary>
    private sealed class FailingValidator : AbstractValidator<TestCommand>
    {
        public FailingValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name must not be empty")
                .MinimumLength(2)
                .WithMessage("Name must be at least 2 chars");
        }

        // Override ValidateAsync so we always return failures regardless of input,
        // making Test 3 deterministic for any string passed in.
        public override Task<ValidationResult> ValidateAsync(
            ValidationContext<TestCommand> context,
            CancellationToken cancellation = default)
        {
            var failures = new List<ValidationFailure>
            {
                new("Name", "Name must not be empty"),
                new("Name", "Name must be at least 2 chars"),
            };
            return Task.FromResult(new ValidationResult(failures));
        }
    }
}
