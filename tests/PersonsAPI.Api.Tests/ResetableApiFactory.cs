using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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
        builder.ConfigureServices(services =>
        {
            // D-11: replace Serilog's ILoggerFactory with NullLoggerFactory so test console stays clean.
            // ConfigureLogging.ClearProviders is ineffective because UseSerilog registers a custom
            // ILoggerFactory singleton that bypasses the ILoggerProvider system entirely.
            var loggerFactoryDescriptors = services
                .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory))
                .ToList();
            foreach (var d in loggerFactoryDescriptors)
                services.Remove(d);
            services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);

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
