using Mediator;
using PersonsAPI.Api.ExceptionHandlers;
using PersonsAPI.Application;
using PersonsAPI.Application.Behaviors;
using PersonsAPI.Infrastructure;
using PersonsAPI.Infrastructure.Seeder;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PersonNotFoundExceptionHandler>();  // D-01: NotFound first
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();      // D-01: Validation second
builder.Services.AddOpenApi();                                            // DOC-01
builder.Services.AddMediator(options =>
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]);        // Mediator + ValidationBehavior pipeline
builder.Services.AddApplication();                                        // FluentValidation validators
builder.Services.AddInfrastructure();                                     // DbContext + Repository

var app = builder.Build();

app.UseExceptionHandler();      // NO route argument — activates IExceptionHandler chain (Pitfall 2)
app.UseHttpsRedirection();
app.MapControllers();
app.MapOpenApi();               // /openapi/v1.json (DOC-01)
app.MapScalarApiReference();    // /scalar — MapScalar not UseScalar (Pitfall 8)

await app.Services.SeedAsync(); // BEFORE RunAsync — seeds InMemory store (Pitfall 5)
await app.RunAsync();

/// <summary>
/// Partial declaration enables <c>WebApplicationFactory&lt;Program&gt;</c> in integration tests (Plan 03).
/// </summary>
public partial class Program { }
