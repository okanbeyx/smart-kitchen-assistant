using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Catalog.Application;

internal interface IIngredientReadRepository
{
    Task<IReadOnlyList<IngredientSummaryView>> ListActiveAsync(
        string? search,
        CancellationToken cancellationToken);
}

internal sealed record IngredientSummaryView(
    long Id,
    string Name,
    QuantityDimension QuantityDimension);
