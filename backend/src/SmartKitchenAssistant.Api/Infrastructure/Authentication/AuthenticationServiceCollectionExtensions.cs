using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using SmartKitchenAssistant.Api.Application.Identity;

namespace SmartKitchenAssistant.Api.Infrastructure.Authentication;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtAuthenticationOptions.SectionName);
        var authority = jwtSection[nameof(JwtAuthenticationOptions.Authority)];
        var audience = jwtSection[nameof(JwtAuthenticationOptions.Audience)];

        services.AddOptions<JwtAuthenticationOptions>()
            .Bind(jwtSection)
            .Validate(
                options => IsSecureAuthority(options.Authority),
                $"'{JwtAuthenticationOptions.SectionName}:Authority' must be an absolute HTTPS URI.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                $"'{JwtAuthenticationOptions.SectionName}:Audience' is required.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.RequireHttpsMetadata = true;
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = AuthenticatedUserIdResolver.UserIdClaimType
                };
            });

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    AuthenticatedUserIdResolver.TryResolve(context.User, out _))
                .Build();
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        return services;
    }

    private static bool IsSecureAuthority(string? authority)
    {
        return Uri.TryCreate(authority, UriKind.Absolute, out var uri) &&
               uri.Scheme == Uri.UriSchemeHttps &&
               string.IsNullOrEmpty(uri.UserInfo) &&
               string.IsNullOrEmpty(uri.Query) &&
               string.IsNullOrEmpty(uri.Fragment);
    }
}
