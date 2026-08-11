using Microsoft.EntityFrameworkCore;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WorkOrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkOrderDb"))
           .UseSnakeCaseNamingConvention());

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

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "WorkOrderService" }))
    .WithName("HealthCheck");

app.Run();
