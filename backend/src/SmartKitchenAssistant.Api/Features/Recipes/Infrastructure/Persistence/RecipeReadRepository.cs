using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Recipes.Application;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;

internal sealed class RecipeReadRepository(SmartKitchenDbContext dbContext)
    : IRecipeReadRepository
{
    public async Task<IReadOnlyList<RecipeSummaryView>> ListPublishedAsync(
        CancellationToken cancellationToken) =>
        await dbContext.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Status == RecipeStatus.Published)
            .OrderBy(recipe => recipe.Id)
            .Select(recipe => new RecipeSummaryView(
                recipe.Id,
                recipe.Title,
                recipe.BaseServings))
            .ToArrayAsync(cancellationToken);

    public async Task<RecipeDetailData?> GetPublishedAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var header = await dbContext.Recipes
            .AsNoTracking()
            .Where(recipe =>
                recipe.Id == id && recipe.Status == RecipeStatus.Published)
            .Select(recipe => new RecipeHeader(
                recipe.Id,
                recipe.Title,
                recipe.BaseServings))
            .SingleOrDefaultAsync(cancellationToken);

        if (header is null)
        {
            return null;
        }

        var ingredients = await (
            from recipeIngredient in dbContext.RecipeIngredients.AsNoTracking()
            join recipe in dbContext.Recipes.AsNoTracking()
                on recipeIngredient.RecipeId equals recipe.Id
            join ingredient in dbContext.Ingredients.AsNoTracking()
                on recipeIngredient.IngredientId equals ingredient.Id
            where recipeIngredient.RecipeId == id &&
                  recipe.Status == RecipeStatus.Published
            orderby recipeIngredient.Sequence
            select new RecipeIngredientData(
                recipeIngredient.IngredientId,
                ingredient.Name,
                ingredient.QuantityDimension,
                recipeIngredient.IsOptional,
                recipeIngredient.NormalizedQuantity,
                recipeIngredient.DisplayUnit))
            .ToArrayAsync(cancellationToken);

        var steps = await (
            from recipeStep in dbContext.RecipeSteps.AsNoTracking()
            join recipe in dbContext.Recipes.AsNoTracking()
                on recipeStep.RecipeId equals recipe.Id
            where recipeStep.RecipeId == id &&
                  recipe.Status == RecipeStatus.Published
            orderby recipeStep.Sequence
            select new RecipeStepView(
                recipeStep.Instruction,
                recipeStep.TimerSeconds))
            .ToArrayAsync(cancellationToken);

        return new RecipeDetailData(
            header.Id,
            header.Title,
            header.BaseServings,
            ingredients,
            steps);
    }

    private sealed record RecipeHeader(
        long Id,
        string Title,
        int BaseServings);
}
