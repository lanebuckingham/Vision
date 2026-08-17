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

namespace Vision.SecurityOperationsService.Tests.Authorization;

/// <summary>
/// Test host for SecurityOperationsService authorization tests.
/// Replaces the default authentication scheme with a controllable test scheme so
/// each test can assert behavior for a specific Cognito group.
/// Messaging is disabled so tests do not require LocalStack.
/// </summary>
public class SecurityOperationsAuthFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Disable the SQS outbox publisher for tests
        builder.UseSetting("Messaging:IncidentCreated:QueueName", "");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<SecurityOperationsDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<SecurityOperationsDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_secops_auth_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<SecOpsTestIdentityStore>();

            services.PostConfigureAll<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "TestScheme";
                o.DefaultChallengeScheme = "TestScheme";
                o.DefaultScheme = "TestScheme";
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, SecOpsTestAuthHandler>("TestScheme", null);
        });
    }

    public async Task EnsureDatabaseReady()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityOperationsDbContext>();
        await db.Database.MigrateAsync();
    }

    public void SetIdentity(params string[] roles)
    {
        var store = Services.GetRequiredService<SecOpsTestIdentityStore>();
        store.Current = roles.Length > 0
            ? new SecOpsTestIdentity { Roles = roles }
            : null;
    }

    public void ClearIdentity()
    {
        Services.GetRequiredService<SecOpsTestIdentityStore>().Current = null;
    }
}

public class SecOpsTestIdentityStore
{
    public SecOpsTestIdentity? Current { get; set; }
}

public class SecOpsTestIdentity
{
    public string Subject { get; init; } = "secops-test-user";
    public string[] Roles { get; init; } = [];
}

public class SecOpsTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SecOpsTestIdentityStore store)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = store.Current;
        if (identity is null)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identity.Subject),
            new("sub", identity.Subject),
            new("token_use", "access"),
            new("client_id", "test-client"),
        };

        foreach (var role in identity.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("cognito:groups", role));
        }

        var ci = new ClaimsIdentity(claims, "TestScheme");
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(ci), "TestScheme")));
    }
}
