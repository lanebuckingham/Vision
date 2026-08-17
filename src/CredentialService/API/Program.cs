using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.API.Auth;
using Vision.CredentialService.API.Endpoints;
using Vision.CredentialService.API.ExceptionHandling;
using Vision.CredentialService.API.Observability;
using Vision.CredentialService.Application.Common;
using Vision.CredentialService.Infrastructure.Persistence;
using Vision.CredentialService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Observability — tracing + trace-correlated logging.
builder.AddVisionObservability();

// Persistence
builder.Services.AddDbContext<CredentialDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CredentialDb"))
           .UseSnakeCaseNamingConvention());

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<CredentialDbContext>("database", tags: ["ready"]);

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<CredentialDbContext>());

// FluentValidation — register all validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<CredentialDbContext>();

// MediatR pipeline — validation runs before handlers
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Problem Details
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Authentication & Authorization
builder.Services.AddVisionAuth(builder.Configuration, builder.Environment);

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
    await db.Database.MigrateAsync();
    await CredentialSeeder.SeedAsync(db);
}

// Only redirect to HTTPS when an HTTPS endpoint is actually configured (e.g. the
// local "https" launch profile). Skipping this in HTTP-only environments — plain
// local Docker Compose today, and an Azure Container Apps ingress that already
// terminates TLS in Phase 7 — avoids a "Failed to determine the https port for
// redirect" warning logged on every single request.
if (!string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_HTTPS_PORT"])
    || !string.IsNullOrEmpty(builder.Configuration["HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapVisionHealthEndpoints("CredentialService");

app.MapPeopleEndpoints();
app.MapCredentialEndpoints();
app.MapCredentialIssuanceEndpoints();

app.Run();

// Make the implicit Program class accessible to the test project
public partial class Program { }
