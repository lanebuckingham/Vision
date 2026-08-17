using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vision.WorkOrderService.Infrastructure.Messaging;
using Vision.WorkOrderService.Infrastructure.Persistence;
using Vision.WorkOrderService.Infrastructure.Persistence.Seeding;

namespace Vision.WorkOrderService.Tests.Authorization;

/// <summary>
/// Shared test identity store for authorization tests.
/// </summary>
public class AuthTestIdentityStore
{
    public AuthTestIdentity? Current { get; set; }
}

public class AuthTestIdentity
{
    public string Subject { get; init; } = "test-user";
    public string[] Roles { get; init; } = [];
}

/// <summary>
/// Auth handler that reads identity from the DI-registered AuthTestIdentityStore.
/// </summary>
public class AuthTestHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthTestIdentityStore _store;

    public AuthTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, AuthTestIdentityStore store)
        : base(options, logger, encoder) { _store = store; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = _store.Current;
        if (id is null) return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.Subject),
            new("sub", id.Subject),
            new("token_use", "access"),
            new("client_id", "test-client"),
        };
        foreach (var r in id.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, r));
            claims.Add(new Claim("cognito:groups", r));
        }
        var ci = new ClaimsIdentity(claims, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(ci), "TestScheme")));
    }
}

/// <summary>
/// WebApplicationFactory for WorkOrderService authorization tests.
/// Uses WorkOrderDbContext as assembly marker since Program is ambiguous.
/// </summary>
public class WorkOrderAuthFactory : WebApplicationFactory<WorkOrderDbContext>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<WorkOrderDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Stop the SQS consumer from polling during authorization tests.
            // The IAmazonSQS registration stays in place because other messaging
            // services depend on it.
            var consumers = services
                .Where(d => d.ImplementationType == typeof(IncidentCreatedConsumer))
                .ToList();
            foreach (var consumer in consumers) services.Remove(consumer);

            services.AddDbContext<WorkOrderDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_wo_auth_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<AuthTestIdentityStore>();
            services.PostConfigureAll<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "TestScheme";
                o.DefaultChallengeScheme = "TestScheme";
                o.DefaultScheme = "TestScheme";
            });
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AuthTestHandler>("TestScheme", null);
        });
    }

    /// <summary>
    /// Applies migrations and guarantees the demo seed rows exist, so ownership
    /// assertions can rely on the known Technician / WorkOrder assignments.
    /// </summary>
    public async Task EnsureDatabaseReady()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkOrderDbContext>();
        await db.Database.MigrateAsync();
        await WorkOrderSeeder.SeedAsync(db);
    }

    public void SetIdentity(string subject, params string[] roles)
    {
        var store = Services.GetRequiredService<AuthTestIdentityStore>();
        store.Current = new AuthTestIdentity { Subject = subject, Roles = roles };
    }

    public void ClearIdentity()
    {
        var store = Services.GetRequiredService<AuthTestIdentityStore>();
        store.Current = null;
    }
}
