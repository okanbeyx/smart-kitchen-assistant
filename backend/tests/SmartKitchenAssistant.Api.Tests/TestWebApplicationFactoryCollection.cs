namespace SmartKitchenAssistant.Api.Tests;

// The factory sets process-wide configuration before Program runs, so tests using it
// must not overlap other collections that could observe the temporary environment value.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TestWebApplicationFactoryCollection
    : ICollectionFixture<TestWebApplicationFactory>
{
    public const string Name = "Test web application factory collection";
}
