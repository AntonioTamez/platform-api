using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Application.Ports;
using PersonsAPI.Infrastructure.Persistence;
using PersonsAPI.Infrastructure.Repositories;

namespace PersonsAPI.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer.
///
/// <para>Plan 01 of Phase 3 — <see cref="AddInfrastructure"/> registers
/// <see cref="PersonDbContext"/> (EF Core InMemory, scoped) and
/// <see cref="PersonRepository"/> as the implementation of <see cref="IPersonRepository"/> (scoped).</para>
///
/// <para><b>Intentionally not registered here (D-06):</b> <c>DataSeeder</c> is a static class with
/// an extension method on <see cref="IServiceProvider"/>. It is a startup initialization step,
/// not a DI service. Phase 4's <c>Program.cs</c> calls <c>await app.Services.SeedAsync()</c>
/// directly — no DI registration is needed or appropriate.</para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core InMemory persistence adapter and repository implementation.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <description>
    ///       <see cref="EntityFrameworkServiceCollectionExtensions.AddDbContext{TContext}(IServiceCollection, Action{DbContextOptionsBuilder}?, ServiceLifetime, ServiceLifetime)"/>
    ///       registers <see cref="PersonDbContext"/> as <see cref="ServiceLifetime.Scoped"/> (default)
    ///       using <c>UseInMemoryDatabase("PersonsDb")</c>. The fixed name <c>"PersonsDb"</c> is for the
    ///       running application; infrastructure tests use <c>Guid.NewGuid().ToString()</c> names for isolation.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <c>IPersonRepository</c> → <see cref="PersonRepository"/> is registered as
    ///       <see cref="ServiceLifetime.Scoped"/> to match the <see cref="PersonDbContext"/> lifetime.
    ///       Both resolve within the same HTTP request scope, preventing context lifetime mismatches.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <description>
    ///       <b>DataSeeder is NOT registered here</b> (D-06): it is a static class with an extension
    ///       method on <see cref="IServiceProvider"/>. Phase 4's <c>Program.cs</c> calls
    ///       <c>await app.Services.SeedAsync()</c> before <c>app.Run()</c>.
    ///     </description>
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="services">The application's service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for fluent chaining.</returns>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<PersonDbContext>(options =>
            options.UseInMemoryDatabase("PersonsDb"));

        services.AddScoped<IPersonRepository, PersonRepository>();

        return services;
    }
}
