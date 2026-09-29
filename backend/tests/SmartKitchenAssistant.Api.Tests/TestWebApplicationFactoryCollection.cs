namespace SmartKitchenAssistant.Api.Tests;

// Keep tests that share the same WebApplicationFactory instance serialized so mutable
// in-memory host state cannot leak between requests.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TestWebApplicationFactoryCollection
    : ICollectionFixture<TestWebApplicationFactory>
{
    public const string Name = "Test web application factory collection";
}
