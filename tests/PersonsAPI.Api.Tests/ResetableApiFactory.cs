using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
    // Unique database name per factory instance — frozen at factory construction time
    // so that all scopes within the same fixture share the same isolated store.
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // D-11: suppress Serilog JSON output in test runs (UseSerilog unavailable on IWebHostBuilder in v9)
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.None);
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL PersonDbContext-related registrations added by AddInfrastructure().
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<PersonDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(PersonDbContext))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            // Re-register with the fixed unique database name so that all scopes within
            // this factory instance share the same isolated in-memory store.
            services.AddDbContext<PersonDbContext>(opt =>
                opt.UseInMemoryDatabase(_databaseName));
        });
    }
}
