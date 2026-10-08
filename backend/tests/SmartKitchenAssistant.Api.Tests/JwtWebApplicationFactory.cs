using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartKitchenAssistant.Api.Tests;

// Issue #32: independent host using the production Bearer handler, not the Test scheme.
public sealed class JwtWebApplicationFactory : WebApplicationFactory<Program>
{
    internal const string Issuer = "https://issuer.example.test/";
    internal const string Audience = "smart-kitchen-assistant-tests";
    internal const string SigningKeyId = "ephemeral-test-key";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RsaSecurityKey _signingKey;
    private readonly RejectMetadataRequestsHandler _metadataHandler = new();
    private readonly HttpClient _backchannel;
    private int _resourcesDisposed;

    public JwtWebApplicationFactory()
    {
        _signingKey = new RsaSecurityKey(_rsa)
        {
            KeyId = SigningKeyId,
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        _backchannel = new HttpClient(_metadataHandler);
    }

    internal int MetadataRequestCount => _metadataHandler.RequestCount;

    internal string CreateToken(
        IEnumerable<string>? subjects = null,
        string issuer = Issuer,
        string audience = Audience,
        DateTime? notBefore = null,
        DateTime? expires = null,
        SecurityKey? signingKey = null)
    {
        var now = DateTime.UtcNow;
        var claims = (subjects ?? ["auth0|fixture-user"])
            .Select(subject => new Claim("sub", subject));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore ?? now.AddMinutes(-1),
            expires: expires ?? now.AddHours(1),
            signingCredentials: new SigningCredentials(
                signingKey ?? _signingKey,
                SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // These routes do not access SQL; only satisfy production startup configuration.
        builder.UseSetting("ConnectionStrings:SmartKitchen",
            "Server=localhost;Database=SmartKitchenAssistantJwtTests;" +
            "Integrated Security=True;TrustServerCertificate=True;");
        builder.UseSetting("Authentication:Jwt:Authority", Issuer);
        builder.UseSetting("Authentication:Jwt:Audience", Audience);

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                // Validation receives only the public key. No real Auth0 keys or metadata.
                configuration.SigningKeys.Add(new RsaSecurityKey(_rsa.ExportParameters(false))
                {
                    KeyId = SigningKeyId,
                    CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
                });
                options.ConfigurationManager =
                    new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                // Fail closed if a future change accidentally attempts OIDC/JWKS HTTP.
                options.Backchannel = _backchannel;
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            if (disposing) DisposeResources();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            DisposeResources();
        }
    }

    private void DisposeResources()
    {
        if (Interlocked.Exchange(ref _resourcesDisposed, 1) == 0)
        {
            _backchannel.Dispose();
            _rsa.Dispose();
        }
    }

    private sealed class RejectMetadataRequestsHandler : HttpMessageHandler
    {
        private int _requestCount;

        internal int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            throw new InvalidOperationException("JWT integration tests must not request external metadata.");
        }
    }
}
