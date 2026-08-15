using Amazon.SQS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.SecurityOperationsService.API.Endpoints;
using Vision.SecurityOperationsService.API.Middleware;
using Vision.SecurityOperationsService.Application.Common;
using Vision.SecurityOperationsService.Infrastructure.Messaging;
using Vision.SecurityOperationsService.Infrastructure.Persistence;
using Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Correlation
builder.Services.AddScoped<CorrelationContext>();

// Persistence
builder.Services.AddDbContext<SecurityOperationsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SecurityOperationsDb"))
           .UseSnakeCaseNamingConvention());

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

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseMiddleware<CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "SecurityOperationsService" }))
    .WithName("HealthCheck");

app.MapAssetEndpoints();
app.MapIncidentEndpoints();
app.MapDashboardEndpoints();

app.Run();
