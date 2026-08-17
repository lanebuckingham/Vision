using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Vision.SecurityOperationsService.API.Auth;

public static class VisionAuthExtensions
{
    public const string CognitoGroupsClaim = "cognito:groups";

    public static class Policies
    {
        public const string SecurityOperationsManager = "SecurityOperationsManager";
    }

    public static IServiceCollection AddVisionAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var userPoolId = configuration["Cognito:UserPoolId"];
        var region = configuration["Cognito:Region"];
        var clientId = configuration["Cognito:ClientId"];

        var cognitoConfigured = !string.IsNullOrWhiteSpace(userPoolId)
                             && !string.IsNullOrWhiteSpace(region)
                             && !string.IsNullOrWhiteSpace(clientId);

        if (cognitoConfigured)
        {
            var authority = $"https://cognito-idp.{region}.amazonaws.com/{userPoolId}";

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = authority,
                        ValidateAudience = false, // Cognito access tokens use client_id, not aud
                        ValidateLifetime = true,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var tokenClientId = context.Principal?.FindFirstValue("client_id");
                            if (tokenClientId != clientId)
                            {
                                context.Fail("Token was not issued for the expected app client.");
                                return Task.CompletedTask;
                            }

                            var tokenUse = context.Principal?.FindFirstValue("token_use");
                            if (tokenUse != "access")
                            {
                                context.Fail("Only access tokens are accepted.");
                                return Task.CompletedTask;
                            }

                            return Task.CompletedTask;
                        }
                    };
                });
        }
        else
        {
            // No Cognito config — fail closed. All protected requests return 401.
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                    };
                });
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.SecurityOperationsManager, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireAssertion(ctx => HasGroup(ctx.User, "SecurityManager")));

        return services;
    }

    private static bool HasGroup(ClaimsPrincipal user, string group)
    {
        return user.HasClaim(CognitoGroupsClaim, group) || user.IsInRole(group);
    }
}
