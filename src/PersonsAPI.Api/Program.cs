using Mediator;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using PersonsAPI.Api.ExceptionHandlers;
using PersonsAPI.Application;
using PersonsAPI.Application.Behaviors;
using PersonsAPI.Infrastructure;
using PersonsAPI.Infrastructure.Seeder;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console(new CompactJsonFormatter()));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();  // D-01: NotFound first
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();      // D-01: Validation second
builder.Services.AddOpenApi();                                            // DOC-01
builder.Services.AddMediator(options =>
{
    options.ServiceLifetime = ServiceLifetime.Scoped;                    // handlers share scope with PersonDbContext
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];        // Mediator + ValidationBehavior pipeline
});
builder.Services.AddApplication();                                        // FluentValidation validators
builder.Services.AddInfrastructure();                                     // DbContext + Repository
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();      // NO route argument — activates IExceptionHandler chain (Pitfall 2)
app.UseHttpsRedirection();
app.MapControllers();
app.MapOpenApi();               // /openapi/v1.json (DOC-01)
app.MapScalarApiReference();    // /scalar — MapScalar not UseScalar (Pitfall 8)
app.MapHealthChecks("/health", new HealthCheckOptions             // D-02: JSON body {"status":"Healthy"}
{
    ResponseWriter = (ctx, _) =>
    {
        ctx.Response.ContentType = "application/json; charset=utf-8";
        return ctx.Response.WriteAsync("{\"status\":\"Healthy\"}");
    }
}); // D-03: anonymous — Cloud Run liveness probe calls /health without credentials

await app.Services.SeedAsync(); // BEFORE RunAsync — seeds InMemory store (Pitfall 5)
await app.RunAsync();

/// <summary>
/// Partial declaration enables <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests (Plan 03).
/// </summary>
public partial class Program { }
