using SmartKitchenAssistant.Api.Features.Catalog.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Catalog.Application;

public sealed class IngredientReadServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptySearchIsNormalizedToNoFilter(string? search)
    {
        var repository = new StubIngredientReadRepository();
        var service = new IngredientReadService(repository);

        await service.ListAsync(search, CancellationToken.None);

        Assert.Null(repository.Search);
    }

    [Fact]
    public async Task SearchIsTrimmedBeforeQueryingRepository()
    {
        var repository = new StubIngredientReadRepository();
        var service = new IngredientReadService(repository);

        await service.ListAsync("  milk  ", CancellationToken.None);

        Assert.Equal("milk", repository.Search);
    }

    [Fact]
    public async Task MeaningfulSearchIsForwardedUnchanged()
    {
        var repository = new StubIngredientReadRepository();
        var service = new IngredientReadService(repository);

        await service.ListAsync("oat milk", CancellationToken.None);

        Assert.Equal("oat milk", repository.Search);
    }

    [Fact]
    public async Task CancellationTokenIsForwardedToRepository()
    {
        var repository = new StubIngredientReadRepository();
        var service = new IngredientReadService(repository);
        using var cancellation = new CancellationTokenSource();

        await service.ListAsync(null, cancellation.Token);

        Assert.Equal(cancellation.Token, repository.CancellationToken);
    }

    private sealed class StubIngredientReadRepository : IIngredientReadRepository
    {
        internal string? Search { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        public Task<IReadOnlyList<IngredientSummaryView>> ListActiveAsync(
            string? search,
            CancellationToken cancellationToken)
        {
            Search = search;
            CancellationToken = cancellationToken;
            return Task.FromResult<IReadOnlyList<IngredientSummaryView>>([]);
        }
    }
}
