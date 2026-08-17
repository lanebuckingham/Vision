using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Vision.CredentialService.Infrastructure.Persistence;

namespace Vision.CredentialService.Tests.Api;

/// <summary>
/// Exercises the production JwtBearer validation pipeline.
///
/// The factory enables the real Cognito code path (Authority, issuer validation,
/// lifetime validation, client_id check, token_use check) and injects a test RSA
/// signing key so tokens can be minted locally instead of calling AWS.
///
/// The positive test must pass for the negative tests to be meaningful:
/// if no token could ever authenticate, every negative test would pass trivially.
/// </summary>
public class JwtValidationTests : IAsyncLifetime
{
    private const string TestIssuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_TestPool";
    private const string TestClientId = "test-client-123";
    private const string TestKeyId = "vision-test-key";

    private static readonly RSA SigningKey = RSA.Create(2048);

    private readonly JwtValidationFactory _factory = new(SigningKey, TestKeyId, TestIssuer, TestClientId);

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CredentialDbContext>();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // === POSITIVE PATH — proves the pipeline can authenticate a good token ===

    [Fact]
    public async Task ValidAccessToken_SecurityManager_Returns200()
    {
        var token = CreateToken(roles: ["SecurityManager"]);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ValidAccessToken_CredentialAdministrator_Returns200()
    {
        var token = CreateToken(roles: ["CredentialAdministrator"]);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // === AUTHENTICATED BUT UNAUTHORIZED — must be 403, not 401 ===

    [Fact]
    public async Task ValidAccessToken_Technician_Returns403()
    {
        var token = CreateToken(roles: ["Technician"]);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ValidAccessToken_NoGroups_Returns403()
    {
        var token = CreateToken(roles: []);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // === NEGATIVE PATH — authentication failures must be 401 ===

    [Fact]
    public async Task NoToken_Returns401()
    {
        var response = await Get("/api/v1/credentials/summary", token: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedToken_Returns401()
    {
        var response = await Get("/api/v1/credentials/summary", "not.a.valid.jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignature_Returns401()
    {
        using var attackerKey = RSA.Create(2048);
        var token = CreateToken(roles: ["SecurityManager"], signingKey: attackerKey);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var token = CreateToken(roles: ["SecurityManager"], expired: true);
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_Returns401()
    {
        var token = CreateToken(
            roles: ["SecurityManager"],
            issuer: "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_OtherPool");
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongClientId_Returns401()
    {
        var token = CreateToken(roles: ["SecurityManager"], clientId: "unapproved-client");
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IdTokenInsteadOfAccessToken_Returns401()
    {
        var token = CreateToken(roles: ["SecurityManager"], tokenUse: "id");
        var response = await Get("/api/v1/credentials/summary", token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthFailureResponse_DoesNotLeakSecrets()
    {
        var response = await Get("/api/v1/credentials/summary", "not.a.valid.jwt");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vision_dev", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEGIN RSA", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Npgsql", body, StringComparison.OrdinalIgnoreCase);
    }

    // === Helpers ===

    private async Task<HttpResponseMessage> Get(string path, string? token)
    {
        using var client = _factory.CreateDefaultClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.GetAsync(path);
    }

    private static string CreateToken(
        string[] roles,
        RSA? signingKey = null,
        string issuer = TestIssuer,
        string clientId = TestClientId,
        string tokenUse = "access",
        bool expired = false)
    {
        var key = new RsaSecurityKey(signingKey ?? SigningKey) { KeyId = TestKeyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("token_use", tokenUse),
            new("client_id", clientId),
            new("username", "integration-test-user"),
        };

        foreach (var role in roles)
            claims.Add(new Claim("cognito:groups", role));

        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Subject = new ClaimsIdentity(claims),
            NotBefore = expired ? now.AddHours(-2) : now.AddMinutes(-1),
            Expires = expired ? now.AddHours(-1) : now.AddHours(1),
            SigningCredentials = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }
}

/// <summary>
/// Enables the production Cognito JwtBearer branch via host settings, then replaces
/// only the OIDC metadata/signing-key discovery with a locally supplied test key.
///
/// Everything else (issuer validation, lifetime validation, client_id check,
/// token_use check, authorization policies) is the real production configuration.
/// </summary>
public class JwtValidationFactory(RSA key, string keyId, string issuer, string clientId)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting flows into the app's IConfiguration for the minimal hosting model,
        // which makes VisionAuthExtensions take the real Cognito code path.
        builder.UseSetting("Cognito:UserPoolId", "us-east-1_TestPool");
        builder.UseSetting("Cognito:Region", "us-east-1");
        builder.UseSetting("Cognito:ClientId", clientId);

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<CredentialDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<CredentialDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=vision_credential_jwt_test;Username=vision;Password=vision_dev")
                       .UseSnakeCaseNamingConvention());
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace only metadata discovery. Validation rules stay as configured in production.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var signingKey = new RsaSecurityKey(key) { KeyId = keyId };

                options.RequireHttpsMetadata = false;
                options.ConfigurationManager = null;

                var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
                configuration.SigningKeys.Add(signingKey);
                options.Configuration = configuration;

                options.TokenValidationParameters.IssuerSigningKey = signingKey;
                options.TokenValidationParameters.IssuerSigningKeys = [signingKey];
            });
        });
    }
}
