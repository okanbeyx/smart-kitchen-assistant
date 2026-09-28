using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartKitchenAssistant.Api.Infrastructure.Authentication;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Authentication;

public sealed class AuthenticationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddApiAuthenticationRejectsMissingAuthority()
    {
        using var provider = BuildProvider(
            authority: null,
            audience: "smart-kitchen-assistant-tests");

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtAuthenticationOptions>>().Value);
    }

    [Fact]
    public void AddApiAuthenticationRejectsNonHttpsAuthority()
    {
        using var provider = BuildProvider(
            authority: "http://issuer.example.test",
            audience: "smart-kitchen-assistant-tests");

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtAuthenticationOptions>>().Value);
    }

    [Fact]
    public void AddApiAuthenticationRejectsMissingAudience()
    {
        using var provider = BuildProvider(
            authority: "https://issuer.example.test",
            audience: null);

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<JwtAuthenticationOptions>>().Value);
    }

    [Fact]
    public void AddApiAuthenticationConfiguresJwtBearerValidation()
    {
        using var provider = BuildProvider();
        var options = provider
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.Equal("https://issuer.example.test", options.Authority);
        Assert.Equal("smart-kitchen-assistant-tests", options.Audience);
        Assert.True(options.RequireHttpsMetadata);
        Assert.False(options.MapInboundClaims);
        Assert.False(options.SaveToken);
        Assert.False(options.IncludeErrorDetails);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.Equal("sub", options.TokenValidationParameters.NameClaimType);
    }

    [Fact]
    public void AddApiAuthenticationUsesJwtBearerAsDefaultScheme()
    {
        using var provider = BuildProvider();
        var options = provider
            .GetRequiredService<IOptions<AuthenticationOptions>>()
            .Value;

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, options.DefaultScheme);
    }

    [Fact]
    public async Task FallbackPolicyRequiresAuthenticatedValidSubject()
    {
        using var provider = BuildProvider();
        var policy = provider
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value
            .FallbackPolicy;
        var authorizationService =
            provider.GetRequiredService<IAuthorizationService>();

        Assert.NotNull(policy);

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var missingSubject = CreateAuthenticatedPrincipal();
        var valid = CreateAuthenticatedPrincipal("user-1");

        Assert.False((await authorizationService.AuthorizeAsync(
            anonymous,
            resource: null,
            policy)).Succeeded);
        Assert.False((await authorizationService.AuthorizeAsync(
            missingSubject,
            resource: null,
            policy)).Succeeded);
        Assert.True((await authorizationService.AuthorizeAsync(
            valid,
            resource: null,
            policy)).Succeeded);
    }

    [Fact]
    public async Task FallbackPolicyRejectsSubjectFromUnauthenticatedIdentity()
    {
        using var provider = BuildProvider();
        var policy = provider
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value
            .FallbackPolicy;
        var authorizationService =
            provider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(
        [
            CreateIdentity(isAuthenticated: true),
            CreateIdentity(isAuthenticated: false, "untrusted-user")
        ]);

        Assert.NotNull(policy);
        Assert.False((await authorizationService.AuthorizeAsync(
            principal,
            resource: null,
            policy)).Succeeded);
    }

    private static ServiceProvider BuildProvider(
        string? authority = "https://issuer.example.test",
        string? audience = "smart-kitchen-assistant-tests")
    {
        var values = new Dictionary<string, string?>();

        if (authority is not null)
        {
            values["Authentication:Jwt:Authority"] = authority;
        }

        if (audience is not null)
        {
            values["Authentication:Jwt:Audience"] = audience;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApiAuthentication(configuration);

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static ClaimsPrincipal CreateAuthenticatedPrincipal(
        params string[] subjects)
    {
        return new ClaimsPrincipal(CreateIdentity(isAuthenticated: true, subjects));
    }

    private static ClaimsIdentity CreateIdentity(
        bool isAuthenticated,
        params string[] subjects)
    {
        var claims = subjects.Select(subject => new Claim("sub", subject));

        return new ClaimsIdentity(claims, isAuthenticated ? "Test" : null);
    }
}
