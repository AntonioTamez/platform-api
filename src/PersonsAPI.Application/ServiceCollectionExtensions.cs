using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Application.Behaviors;

namespace PersonsAPI.Application;

/// <summary>
/// Composition root for the Application layer.
///
/// <para>Plan 03 of Phase 2 — <see cref="AddApplication"/> registers the three
/// FluentValidation validators discovered from the Application assembly and exposes
/// the <see cref="ValidationBehavior{TMessage, TResponse}"/> open generic so Phase 4
/// can wire it into Mediator's pipeline.</para>
///
/// <para><b>Open Question 1 fallback (RESEARCH.md):</b> This method deliberately does
/// <em>not</em> call <c>AddMediator()</c>. That source-generated extension method lives
/// only in <c>PersonsAPI.Api</c> (which has <c>Mediator.SourceGenerator</c> installed).
/// Calling it here would break isolated builds of the Application project (Pitfall 4).
/// Phase 4's <c>Program.cs</c> is responsible for calling
/// <c>AddMediator(options =&gt; { options.PipelineBehaviors = [typeof(ValidationBehavior&lt;,&gt;)]; ... })</c>
/// before or after <c>AddApplication()</c>.</para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all FluentValidation validators from the Application assembly and the
    /// <see cref="ValidationBehavior{TMessage, TResponse}"/> open-generic pipeline behavior.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="FluentValidationServiceCollectionExtensions.AddValidatorsFromAssembly"/>
    ///       scans <c>typeof(IApplicationMarker).Assembly</c> and registers
    ///       <c>CreatePersonCommandValidator</c>, <c>UpdatePersonCommandValidator</c>, and
    ///       <c>PatchPersonCommandValidator</c> with <see cref="ServiceLifetime.Scoped"/>
    ///       lifetime (D-10).
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       The open-generic <c>IPipelineBehavior&lt;,&gt;</c> registration lets DI construct
    ///       closed <see cref="ValidationBehavior{TMessage, TResponse}"/> instances per
    ///       (TMessage, TResponse) pair when Phase 4 wires it through <c>AddMediator</c>'s
    ///       <c>options.PipelineBehaviors</c> list.
    ///     </description>
    ///   </item>
    /// </list>
    ///
    /// <para><b>Phase 4 responsibility:</b> <c>Program.cs</c> must call
    /// <c>AddMediator(options =&gt; { options.PipelineBehaviors = [typeof(ValidationBehavior&lt;,&gt;)]; })</c>
    /// and then <c>AddApplication()</c>. Phase 4 must also register exception middleware
    /// that catches <see cref="FluentValidation.ValidationException"/> for 400 responses
    /// and <c>PersonsAPI.Application.Exceptions.PersonNotFoundException</c> for 404
    /// responses.</para>
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register all three FluentValidation validators from the Application assembly
        // with Scoped lifetime. AddValidatorsFromAssembly discovers:
        //   - CreatePersonCommandValidator
        //   - UpdatePersonCommandValidator
        //   - PatchPersonCommandValidator
        // Read queries and DeletePersonCommand intentionally have no validator (D-08).
        services.AddValidatorsFromAssembly(
            typeof(IApplicationMarker).Assembly,
            ServiceLifetime.Scoped);

        // Register the open-generic ValidationBehavior so DI can resolve closed instances
        // per (TMessage, TResponse) pair when Phase 4 supplies it to Mediator's pipeline.
        // This registration makes the behavior available to the DI container;
        // Mediator's AddMediator() call in Phase 4 wires it into the dispatch pipeline.
        services.AddScoped(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        return services;
    }
}
