using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal sealed class RecipeReadService(IRecipeReadRepository repository)
{
    public Task<IReadOnlyList<RecipeSummaryView>> ListAsync(
        CancellationToken cancellationToken) =>
        repository.ListPublishedAsync(cancellationToken);

    public async Task<RecipeDetailView?> GetAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return null;
        }

        var recipe = await repository.GetPublishedAsync(id, cancellationToken);

        if (recipe is null)
        {
            return null;
        }

        return new RecipeDetailView(
            recipe.Id,
            recipe.Title,
            recipe.BaseServings,
            recipe.Ingredients.Select(ToView).ToArray(),
            recipe.Steps);
    }

    private static RecipeIngredientView ToView(RecipeIngredientData ingredient)
    {
        if (ingredient.NormalizedQuantity.HasValue != ingredient.DisplayUnit.HasValue)
        {
            throw InvalidPublishedIngredient();
        }

        if (!ingredient.NormalizedQuantity.HasValue)
        {
            if (!ingredient.IsOptional)
            {
                throw InvalidPublishedIngredient();
            }

            return new RecipeIngredientView(
                ingredient.IngredientId,
                ingredient.Name,
                null,
                null,
                ingredient.IsOptional);
        }

        var unit = ingredient.DisplayUnit!.Value;

        if (unit.GetDimension() != ingredient.QuantityDimension)
        {
            throw InvalidPublishedIngredient();
        }

        return new RecipeIngredientView(
            ingredient.IngredientId,
            ingredient.Name,
            unit.ConvertFromBase(ingredient.NormalizedQuantity.Value),
            unit.GetSymbol(),
            ingredient.IsOptional);
    }

    private static InvalidOperationException InvalidPublishedIngredient() =>
        new("Published recipe ingredient data is invalid.");
}

internal sealed record RecipeSummaryView(
    long Id,
    string Title,
    int BaseServings);

internal sealed record RecipeDetailView(
    long Id,
    string Title,
    int BaseServings,
    IReadOnlyList<RecipeIngredientView> Ingredients,
    IReadOnlyList<RecipeStepView> Steps);

internal sealed record RecipeIngredientView(
    long IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    bool IsOptional);

internal sealed record RecipeStepView(
    string Instruction,
    int? TimerSeconds);
