using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

public sealed class PersistenceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddPersistenceThrowsWhenConnectionStringIsMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPersistence(configuration));

        Assert.Contains(
            "ConnectionStrings:SmartKitchen",
            exception.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AddPersistenceThrowsWhenConnectionStringIsWhitespace()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SmartKitchen"] = "   "
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddPersistence(configuration));

        Assert.Contains(
            "ConnectionStrings:SmartKitchen",
            exception.Message,
            StringComparison.Ordinal);
    }
}
