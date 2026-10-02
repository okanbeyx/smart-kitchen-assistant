namespace SmartKitchenAssistant.Api.Features.Catalog.Application;

internal sealed class IngredientReadService(IIngredientReadRepository repository)
{
    public Task<IReadOnlyList<IngredientSummaryView>> ListAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        var normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim();

        return repository.ListActiveAsync(normalizedSearch, cancellationToken);
    }
}
