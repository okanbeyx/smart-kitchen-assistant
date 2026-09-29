using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly DbCommandInterceptor? _commandInterceptor;

    public TestWebApplicationFactory()
        : this(
            "Server=localhost;Database=SmartKitchenAssistantTests;" +
            "Integrated Security=True;TrustServerCertificate=True;")
    {
    }

    internal TestWebApplicationFactory(
        string connectionString,
        DbCommandInterceptor? commandInterceptor = null)
    {
        _connectionString = connectionString;
        _commandInterceptor = commandInterceptor;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:SmartKitchen", _connectionString);
        builder.UseSetting(
            "Authentication:Jwt:Authority",
            "https://issuer.example.test");
        builder.UseSetting(
            "Authentication:Jwt:Audience",
            "smart-kitchen-assistant-tests");

        builder.ConfigureTestServices(services =>
        {
            if (_commandInterceptor is not null)
            {
                services.AddDbContext<SmartKitchenDbContext>(options =>
                    options.AddInterceptors(_commandInterceptor));
            }

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

}
