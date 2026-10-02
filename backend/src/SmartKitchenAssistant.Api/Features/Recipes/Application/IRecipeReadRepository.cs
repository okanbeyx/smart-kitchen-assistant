using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal interface IRecipeReadRepository
{
    Task<IReadOnlyList<RecipeSummaryView>> ListPublishedAsync(
        CancellationToken cancellationToken);

    Task<RecipeDetailData?> GetPublishedAsync(
        long id,
        CancellationToken cancellationToken);
}

internal sealed record RecipeDetailData(
    long Id,
    string Title,
    int BaseServings,
    IReadOnlyList<RecipeIngredientData> Ingredients,
    IReadOnlyList<RecipeStepView> Steps);

internal sealed record RecipeIngredientData(
    long IngredientId,
    string Name,
    QuantityDimension QuantityDimension,
    bool IsOptional,
    decimal? NormalizedQuantity,
    Unit? DisplayUnit);
