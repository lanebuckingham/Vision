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
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Api;

/// <summary>
/// Factory for CredentialService API tests.
/// Uses TestScheme with TestIdentityStore for controllable auth.
/// </summary>
public class CredentialServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CredentialDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<CredentialDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_credential_api_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TestIdentityStore>();
            services.PostConfigureAll<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "TestScheme";
                o.DefaultChallengeScheme = "TestScheme";
                o.DefaultScheme = "TestScheme";
            });
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestHeaderAuthHandler>("TestScheme", null);
        });
    }

    public async Task EnsureDatabaseReady()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        await db.Database.MigrateAsync();
    }

    public void SetIdentity(params string[] roles)
    {
        var store = Services.GetRequiredService<TestIdentityStore>();
        store.Current = roles.Length > 0 ? new TestIdentity { Roles = roles } : null;
    }
}

/// <summary>
/// Factory for authorization tests — same as CredentialServiceFactory but uses a separate DB.
/// </summary>
public class CredentialAuthServiceFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<CredentialDbContext>));
            if (descriptor != null) services.Remove(descriptor);
            services.AddDbContext<CredentialDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_credential_auth_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TestIdentityStore>();
            services.PostConfigureAll<AuthenticationOptions>(o =>
            {
                o.DefaultAuthenticateScheme = "TestScheme";
                o.DefaultChallengeScheme = "TestScheme";
                o.DefaultScheme = "TestScheme";
            });
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestHeaderAuthHandler>("TestScheme", null);
        });
    }

    public async Task EnsureDatabaseReady()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        await db.Database.MigrateAsync();
    }

    public void SetIdentity(params string[] roles)
    {
        var store = Services.GetRequiredService<TestIdentityStore>();
        store.Current = roles.Length > 0 ? new TestIdentity { Roles = roles } : null;
    }

    public void ClearIdentity()
    {
        var store = Services.GetRequiredService<TestIdentityStore>();
        store.Current = null;
    }
}

public class TestHeaderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TestIdentityStore _store;
    public TestHeaderAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, TestIdentityStore store)
        : base(options, logger, encoder) { _store = store; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = _store.Current;
        if (id is null) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id.Subject), new("sub", id.Subject),
            new("token_use", "access"), new("client_id", "test-client"),
        };
        foreach (var r in id.Roles) { claims.Add(new Claim(ClaimTypes.Role, r)); claims.Add(new Claim("cognito:groups", r)); }
        var ci = new ClaimsIdentity(claims, "TestScheme");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(ci), "TestScheme")));
    }
}

public class TestIdentityStore { public TestIdentity? Current { get; set; } }
public class TestIdentity { public string Subject { get; init; } = "test-user"; public string[] Roles { get; init; } = []; }
