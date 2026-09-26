using System.Net;

namespace SmartKitchenAssistant.Api.Tests;

[Collection(TestWebApplicationFactoryCollection.Name)]
public sealed class HealthEndpointTests
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealthReturnsOkWithHealthyBody()
    {
        using var response = await _client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
