using FluentValidation;
using Mediator;

namespace PersonsAPI.Application.Behaviors;

/// <summary>
/// Open-generic pipeline behavior that runs all registered <see cref="IValidator{T}"/>
/// instances for <typeparamref name="TMessage"/> before the handler executes.
///
/// <para><b>D-10 short-circuit:</b> When no <see cref="IValidator{TMessage}"/> is registered
/// for the message type, <see cref="Handle"/> immediately invokes <paramref name="next"/>
/// and returns its result without inspecting anything. This means read queries (which
/// have no registered validator) flow through unchanged.</para>
///
/// <para><b>Pitfall 2 guard:</b> <see cref="Handle"/> returns <see cref="ValueTask{TResult}"/>
/// (not <c>Task&lt;TResponse&gt;</c>). Mediator.Abstractions 3.x defines
/// <see cref="IPipelineBehavior{TMessage, TResponse}"/> with <c>ValueTask&lt;TResponse&gt;</c>;
/// using <c>Task&lt;T&gt;</c> would fail to implement the interface.</para>
///
/// <para><b>Pitfall 3 guard:</b> The <paramref name="next"/> delegate is typed as
/// <see cref="MessageHandlerDelegate{TMessage, TResponse}"/>, which accepts
/// <c>(message, cancellationToken)</c> explicitly — NOT MediatR's
/// <c>RequestHandlerDelegate</c> closure form.</para>
///
/// <para><b>Exception contract:</b> On validation failure, throws
/// <see cref="FluentValidation.ValidationException"/> (the library type carrying
/// the <c>Failures</c> collection). Phase 4 catches this exact type to build
/// RFC 9457 Problem Details (ERR-01, ERR-02). Do not create a custom
/// ValidationException class.</para>
/// </summary>
/// <typeparam name="TMessage">The CQRS message type. Must be <see langword="notnull"/> and implement <see cref="IMessage"/>.</typeparam>
/// <typeparam name="TResponse">The response type produced by the handler.</typeparam>
public sealed class ValidationBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    /// <summary>
    /// Runs all registered <see cref="IValidator{TMessage}"/> instances before calling
    /// <paramref name="next"/>. If any validator produces failures, throws
    /// <see cref="FluentValidation.ValidationException"/> with the aggregated failure list —
    /// the handler is never reached.
    /// </summary>
    /// <param name="message">The CQRS message being dispatched.</param>
    /// <param name="next">
    /// The next step in the pipeline (handler or next behavior).
    /// Typed as <see cref="MessageHandlerDelegate{TMessage, TResponse}"/> — Pitfall 3 guard.
    /// Called with explicit <c>(message, cancellationToken)</c> arguments.
    /// </param>
    /// <param name="cancellationToken">Cancellation token propagated through the pipeline.</param>
    /// <returns>The handler's response when all validators pass.</returns>
    /// <exception cref="FluentValidation.ValidationException">
    /// Thrown when at least one validator returns one or more failures.
    /// Contains the full aggregated <c>Errors</c> collection across all validators.
    /// </exception>
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var validatorList = validators.ToList();

        // D-10: graceful short-circuit when no validators are registered for this message type.
        // Read queries (no validator) and DeletePersonCommand pass through unchanged.
        if (validatorList.Count == 0)
            return await next(message, cancellationToken);

        // Run all validators in parallel for this message.
        var context = new ValidationContext<TMessage>(message);
        var results = await Task.WhenAll(
            validatorList.Select(v => v.ValidateAsync(context, cancellationToken)));

        // Aggregate all failures across all validators; filter out any null entries.
        var failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        // Throw FluentValidation.ValidationException — the library type Phase 4 catches.
        // Do NOT throw a custom ApplicationException subclass (T-02-10 anti-pattern guard).
        if (failures.Count > 0)
            throw new ValidationException(failures);

        // All validators passed — forward to the next pipeline step (handler).
        // Pitfall 3: call next with both (message, cancellationToken) arguments.
        return await next(message, cancellationToken);
    }
}
