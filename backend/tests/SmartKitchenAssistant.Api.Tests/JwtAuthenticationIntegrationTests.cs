using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartKitchenAssistant.Api.Tests;

public sealed class JwtAuthenticationIntegrationTests(JwtWebApplicationFactory factory)
    : IClassFixture<JwtWebApplicationFactory>
{
    [Fact]
    public async Task HostUsesProductionBearerSchemeAndPreservesValidation()
    {
        var schemes = factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();
        var authenticate = await schemes.GetDefaultAuthenticateSchemeAsync();
        var challenge = await schemes.GetDefaultChallengeSchemeAsync();
        var forbid = await schemes.GetDefaultForbidSchemeAsync();

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, authenticate?.Name);
        Assert.Equal(typeof(JwtBearerHandler), authenticate?.HandlerType);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, challenge?.Name);
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, forbid?.Name);
        Assert.Null(await schemes.GetSchemeAsync(TestAuthenticationHandler.SchemeName));

        var options = factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(JwtWebApplicationFactory.Issuer, options.Authority);
        Assert.Equal(JwtWebApplicationFactory.Audience, options.Audience);
        Assert.True(options.RequireHttpsMetadata);
        Assert.False(options.MapInboundClaims);
        Assert.False(options.IncludeErrorDetails);
        Assert.False(options.SaveToken);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens);
        Assert.Equal("sub", options.TokenValidationParameters.NameClaimType);
        Assert.IsType<StaticConfigurationManager<OpenIdConnectConfiguration>>(options.ConfigurationManager);
        Assert.Equal(0, factory.MetadataRequestCount);
    }

    [Fact]
    public async Task ValidRs256TokenReturnsOk()
    {
        var token = factory.CreateToken();
        Assert.Equal(SecurityAlgorithms.RsaSha256, new JwtSecurityTokenHandler().ReadJwtToken(token).Header.Alg);

        await AssertStatusAsync("/", token, HttpStatusCode.OK);
    }

    [Fact]
    public Task MissingAuthorizationReturnsUnauthorized() =>
        AssertStatusAsync("/", null, HttpStatusCode.Unauthorized);

    [Fact]
    public Task MalformedBearerTokenReturnsUnauthorized() =>
        AssertStatusAsync("/", "not-a-jwt", HttpStatusCode.Unauthorized);

    [Fact]
    public async Task WrongSigningKeyReturnsUnauthorized()
    {
        using var rsa = RSA.Create(2048);
        // Matching kid ensures the signature itself must be rejected, not just key lookup.
        var key = new RsaSecurityKey(rsa)
        {
            KeyId = JwtWebApplicationFactory.SigningKeyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        await AssertStatusAsync("/", factory.CreateToken(signingKey: key), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public Task WrongIssuerReturnsUnauthorized() =>
        AssertStatusAsync("/", factory.CreateToken(issuer: "https://wrong-issuer.example.test/"),
            HttpStatusCode.Unauthorized);

    [Fact]
    public Task WrongAudienceReturnsUnauthorized() =>
        AssertStatusAsync("/", factory.CreateToken(audience: "another-api"), HttpStatusCode.Unauthorized);

    [Fact]
    public Task ExpiredTokenReturnsUnauthorized()
    {
        var now = DateTime.UtcNow;
        var token = factory.CreateToken(notBefore: now.AddHours(-2), expires: now.AddHours(-1));

        return AssertStatusAsync("/", token, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public Task AuthenticatedTokenWithoutSubjectReturnsForbidden() =>
        AssertStatusAsync("/", factory.CreateToken(subjects: []), HttpStatusCode.Forbidden);

    [Fact]
    public Task AuthenticatedTokenWithWhitespaceSubjectReturnsForbidden() =>
        AssertStatusAsync("/", factory.CreateToken(subjects: ["   "]), HttpStatusCode.Forbidden);

    [Fact]
    // Duplicate sub claims serialize as an array, rejected during real JWT authentication (401).
    // An already-authenticated principal with duplicate sub is separately rejected by the fallback policy (403).
    public Task TokenWithDuplicateSubjectsReturnsUnauthorized() =>
        AssertStatusAsync("/", factory.CreateToken(subjects: ["user-1", "user-2"]), HttpStatusCode.Unauthorized);

    [Fact]
    public Task AuthenticatedTokenWithTooLongSubjectReturnsForbidden() =>
        AssertStatusAsync("/", factory.CreateToken(subjects: [new string('a', 257)]), HttpStatusCode.Forbidden);

    [Fact]
    public Task HealthRemainsAnonymous() =>
        AssertStatusAsync("/health", null, HttpStatusCode.OK);

    [Fact]
    public Task UnsignedTokenReturnsUnauthorized()
    {
        var token = new JwtSecurityToken(
            issuer: JwtWebApplicationFactory.Issuer,
            audience: JwtWebApplicationFactory.Audience,
            claims: [new Claim("sub", "auth0|fixture-user")],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1));

        return AssertStatusAsync("/", new JwtSecurityTokenHandler().WriteToken(token), HttpStatusCode.Unauthorized);
    }

    private async Task AssertStatusAsync(string path, string? token, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
        }
        using var response = await client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(0, factory.MetadataRequestCount);
    }
}
