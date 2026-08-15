using Amazon.SQS;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.API.Endpoints;
using Vision.WorkOrderService.Application.Common;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

// Persistence
builder.Services.AddDbContext<WorkOrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkOrderDb"))
           .UseSnakeCaseNamingConvention());

// MediatR
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<WorkOrderDbContext>());

// FluentValidation — register all validators from this assembly
builder.Services.AddValidatorsFromAssemblyContaining<WorkOrderDbContext>();

// MediatR pipeline — validation runs before handlers
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Problem Details
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<Vision.WorkOrderService.API.ExceptionHandling.GlobalExceptionHandler>();

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

    builder.Services.AddHostedService<IncidentCreatedConsumer>();
}
builder.Services.AddScoped<IncidentCreatedHandler>();
builder.Services.AddScoped<IncidentCreatedMessageProcessor>();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
    await db.Database.MigrateAsync();
    await WorkOrderSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "WorkOrderService" }))
    .WithName("HealthCheck");

app.MapWorkOrderEndpoints();
app.MapTechnicianEndpoints();

app.Run();
