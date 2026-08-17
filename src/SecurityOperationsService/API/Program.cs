using Amazon.SQS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.API.Auth;
using Vision.SecurityOperationsService.API.Endpoints;
using Vision.SecurityOperationsService.API.Middleware;
using Vision.SecurityOperationsService.API.Observability;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Observability — tracing + trace-correlated logging. Registered first so it can
// observe as much of startup as practical; never blocks startup on its own.
builder.AddVisionObservability();

// Correlation
builder.Services.AddScoped<CorrelationContext>();

// Persistence
builder.Services.AddDbContext<SecurityOperationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SecurityOperationsDb"))
           .UseSnakeCaseNamingConvention());

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<SecurityOperationsDbContext>("database", tags: ["ready"]);

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<SecurityOperationsDbContext>());

// FluentValidation — register all validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<SecurityOperationsDbContext>();

// MediatR pipeline — validation runs before handlers
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Problem Details
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Vision.SecurityOperationsService.API.ExceptionHandling.GlobalExceptionHandler>();

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

// Messaging
var messagingSection = builder.Configuration.GetSection(MessagingOptions.SectionName);
builder.Services.Configure<MessagingOptions>(messagingSection);

var messagingOptions = messagingSection.Get<MessagingOptions>();
var queueName = messagingOptions?.IncidentCreated.QueueName;

if (!string.IsNullOrWhiteSpace(queueName))
{
    var sqsConfig = new AmazonSQSConfig
    {
        RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
            messagingOptions!.IncidentCreated.Region)
    };

    if (!string.IsNullOrWhiteSpace(messagingOptions.IncidentCreated.ServiceUrl))
    {
        sqsConfig.ServiceURL = messagingOptions.IncidentCreated.ServiceUrl;

        builder.Services.AddSingleton<IAmazonSQS>(
            new AmazonSQSClient(
                new Amazon.Runtime.BasicAWSCredentials("test", "test"),
                sqsConfig));
    }
    else
    {
        builder.Services.AddSingleton<IAmazonSQS>(new AmazonSQSClient(sqsConfig));
    }

    builder.Services.AddHostedService<OutboxPublisher>();
    builder.Services.AddScoped<OutboxBatchProcessor>();
}

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
    await db.Database.MigrateAsync();
    await SecurityOperationsSeeder.SeedAsync(db);
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
app.UseMiddleware<CorrelationMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapVisionHealthEndpoints("SecurityOperationsService");

app.MapAssetEndpoints();
app.MapIncidentEndpoints();
app.MapDashboardEndpoints();

app.Run();

// Make the implicit Program class accessible to the test project
public partial class Program { }
