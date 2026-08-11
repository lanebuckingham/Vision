using Microsoft.EntityFrameworkCore;
using Vision.CredentialService.Infrastructure.Persistence;
using Vision.CredentialService.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CredentialDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CredentialDb"))
           .UseSnakeCaseNamingConvention());

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

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "CredentialService" }))
    .WithName("HealthCheck");

app.Run();
