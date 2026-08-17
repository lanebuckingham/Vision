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
using Vision.SecurityOperationsService.Infrastructure.Persistence;

namespace Vision.SecurityOperationsService.Tests.Application;

/// <summary>
/// Test host for SecurityOperationsService application/API behavior tests
/// (dashboard, incident/asset commands and queries). Every request is
/// authenticated as SecurityManager — authorization itself is covered separately
/// in <see cref="Vision.SecurityOperationsService.Tests.Authorization.SecurityOperationsAuthorizationTests"/>.
/// Uses its own database so it does not race with the authorization test suite.
/// Messaging is disabled so tests do not require LocalStack.
/// </summary>
public class SecurityOperationsApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>When true, overrides Observability:Enabled=false for this instance —
    /// used to verify that disabling telemetry does not disable business processing.</summary>
    public bool DisableObservability { get; set; }

    /// <summary>When set, overrides Observability:SamplingRatio for this instance —
    /// used to verify invalid values fail fast at startup with a clear error.</summary>
    public string? SamplingRatioOverride { get; set; }

    /// <summary>Captures every ILogger.BeginScope(...) call made anywhere in the app
    /// during this factory's lifetime — used to prove CorrelationMiddleware actually
    /// establishes a CorrelationId logging scope.</summary>
    public readonly ScopeCapturingLoggerProvider ScopeCapture = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Messaging:IncidentCreated:QueueName", "");

        if (DisableObservability)
            builder.UseSetting("Observability:Enabled", "false");

        if (SamplingRatioOverride is not null)
            builder.UseSetting("Observability:SamplingRatio", SamplingRatioOverride);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SecurityOperationsDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<SecurityOperationsDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_secops_app_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigureAll<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "TestScheme";
                o.DefaultChallengeScheme = "TestScheme";
                o.DefaultScheme = "TestScheme";
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, SecurityManagerAuthHandler>("TestScheme", null);

            services.AddLogging(logging => logging.AddProvider(ScopeCapture));
        });
    }

    /// <summary>
    /// Recreates the schema from a clean slate and reseeds the baseline demo data
    /// (hospital/buildings/locations/assets/incidents) that <see cref="Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding.SeedDataIds"/>
    /// tests reference. Several tests (dashboard, recent-activity) query unfiltered,
    /// time-ordered results, so leftover rows from a previous run would make results
    /// non-deterministic. Test classes sharing this database are serialized via
    /// <see cref="SecurityOperationsApplicationCollection"/> so this reset is safe.
    /// </summary>
    public async Task EnsureDatabaseReadyAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        await Vision.SecurityOperationsService.Infrastructure.Persistence.Seeding.SecurityOperationsSeeder.SeedAsync(db);
    }
}

/// <summary>
/// Always authenticates the caller as a SecurityManager. Application/API behavior
/// tests are not concerned with authorization boundaries.
/// </summary>
public class SecurityManagerAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "secops-app-test-user"),
            new("sub", "secops-app-test-user"),
            new("token_use", "access"),
            new("client_id", "test-client"),
            new(ClaimTypes.Role, "SecurityManager"),
            new("cognito:groups", "SecurityManager"),
        };

        var identity = new ClaimsIdentity(claims, "TestScheme");
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "TestScheme")));
    }
}
