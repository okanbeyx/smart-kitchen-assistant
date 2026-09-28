using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace SmartKitchenAssistant.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__SmartKitchen";
    private const string AuthorityEnvironmentVariable =
        "Authentication__Jwt__Authority";
    private const string AudienceEnvironmentVariable =
        "Authentication__Jwt__Audience";

    private readonly string? _previousConnectionString;
    private readonly string? _previousAuthority;
    private readonly string? _previousAudience;

    public TestWebApplicationFactory()
    {
        _previousConnectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);
        _previousAuthority = Environment.GetEnvironmentVariable(
            AuthorityEnvironmentVariable);
        _previousAudience = Environment.GetEnvironmentVariable(
            AudienceEnvironmentVariable);

        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            "Server=localhost;Database=SmartKitchenAssistantTests;" +
            "Integrated Security=True;TrustServerCertificate=True;");
        Environment.SetEnvironmentVariable(
            AuthorityEnvironmentVariable,
            "https://issuer.example.test");
        Environment.SetEnvironmentVariable(
            AudienceEnvironmentVariable,
            "smart-kitchen-assistant-tests");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultChallengeScheme =
                        TestAuthenticationHandler.SchemeName;
                    options.DefaultForbidScheme =
                        TestAuthenticationHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    _ => { });
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
            Environment.SetEnvironmentVariable(
                ConnectionStringEnvironmentVariable,
                _previousConnectionString);
            Environment.SetEnvironmentVariable(
                AuthorityEnvironmentVariable,
                _previousAuthority);
            Environment.SetEnvironmentVariable(
                AudienceEnvironmentVariable,
                _previousAudience);
        }
    }
}
