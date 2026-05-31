using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Domain.Entities;
using PersonsAPI.Infrastructure.Persistence;

namespace PersonsAPI.Infrastructure.Seeder;

/// <summary>
/// Startup-time initializer for the InMemory store.
///
/// <para>This is a static utility class — it is <b>not a DI service</b> and is intentionally
/// omitted from <c>AddInfrastructure()</c> (D-06). Phase 4's <c>Program.cs</c> calls
/// <c>await app.Services.SeedAsync()</c> directly before <c>app.Run()</c>.</para>
///
/// <remarks>
/// <list type="bullet">
///   <item>
///     <description>
///       <b>D-04:</b> <see cref="SeedAsync"/> is an extension method on <see cref="IServiceProvider"/>.
///       It resolves <see cref="PersonDbContext"/> from a dedicated DI scope created internally via
///       <c>services.CreateScope()</c> — never from the root provider, which would throw
///       <see cref="InvalidOperationException"/> for scoped services (RESEARCH.md Pitfall 4).
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>D-05:</b> The seeder is idempotent. If any <see cref="Person"/> row already exists
///       in the store, <see cref="SeedAsync"/> returns immediately without inserting new records.
///       This teaches the production-safe pattern even though InMemory resets on each process restart.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>D-06:</b> <see cref="DataSeeder"/> is a static class with an extension method.
///       It must not be registered with <c>services.Add*()</c> — it has no managed DI lifetime.
///     </description>
///   </item>
/// </list>
/// </remarks>
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Idempotently seeds exactly three <see cref="Person"/> records into the InMemory store
    /// if the store is empty.
    ///
    /// <para><b>Idempotency guarantee (D-05):</b> The method checks <c>!context.Persons.Any()</c>
    /// before inserting. A second call on an already-seeded store returns immediately without
    /// modifying the row count.</para>
    ///
    /// <para><b>Dedicated scope (D-04):</b> <c>services.CreateScope()</c> is called before resolving
    /// <see cref="PersonDbContext"/>. Resolving a scoped <see cref="PersonDbContext"/> directly from
    /// the root <see cref="IServiceProvider"/> would throw <see cref="InvalidOperationException"/>
    /// at startup (RESEARCH.md Pitfall 4).</para>
    ///
    /// <para><b>Seed records (D-01, D-02, D-03):</b> Exactly three persons are inserted via
    /// <see cref="Person.Create"/>, enforcing domain invariants at seed time:
    /// María García López (DOB 1994-06-15 ~32 yrs),
    /// Carlos Ramírez Martínez (DOB 1979-03-22 ~47 yrs),
    /// Ana Flores Mendoza (DOB 1963-11-08 ~62 yrs).</para>
    /// </summary>
    /// <param name="services">The application's root <see cref="IServiceProvider"/>.</param>
    public static async Task SeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PersonDbContext>();

        if (context.Persons.Any()) return;

        context.Persons.AddRange(
            Person.Create("María", "García", "López", new DateOnly(1994, 6, 15)),
            Person.Create("Carlos", "Ramírez", "Martínez", new DateOnly(1979, 3, 22)),
            Person.Create("Ana", "Flores", "Mendoza", new DateOnly(1963, 11, 8))
        );
        await context.SaveChangesAsync();
    }
}
