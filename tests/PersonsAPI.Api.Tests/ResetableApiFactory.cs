using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Infrastructure.Persistence;

namespace PersonsAPI.Api.Tests;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the shared InMemory
/// database with a uniquely-named store per factory instance.
///
/// <para>Eliminates cross-test state contamination: every xUnit fixture that uses
/// <see cref="ResetableApiFactory"/> gets an isolated EF Core InMemory store so that
/// POST/PATCH/DELETE tests cannot affect each other's read assertions.</para>
/// </summary>
public sealed class ResetableApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the shared "PersonsDb" DbContext registration added by AddInfrastructure().
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<PersonDbContext>));
            services.Remove(descriptor);

            // Re-register with a uniquely-named InMemory database so each factory instance
            // (and therefore each test class fixture) gets its own isolated in-memory store.
            services.AddDbContext<PersonDbContext>(opt =>
                opt.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        });
    }
}
