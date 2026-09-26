using Microsoft.AspNetCore.Mvc.Testing;

namespace SmartKitchenAssistant.Api.Tests;

public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringEnvironmentVariable =
        "ConnectionStrings__SmartKitchen";

    private readonly string? _previousConnectionString;

    public TestWebApplicationFactory()
    {
        _previousConnectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        Environment.SetEnvironmentVariable(
            ConnectionStringEnvironmentVariable,
            "Server=localhost;Database=SmartKitchenAssistantTests;" +
            "Integrated Security=True;TrustServerCertificate=True;");
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
        }
    }
}
