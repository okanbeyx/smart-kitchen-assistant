using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Recipes.Application;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;

internal sealed class RecipeSuitabilityRepository(SmartKitchenDbContext dbContext)
    : IRecipeSuitabilityRepository
{
    public async Task<RecipeSuitabilityData> LoadAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var recipes = await dbContext.Recipes
            .AsNoTracking()
            .Where(recipe => recipe.Status == RecipeStatus.Published)
            .OrderBy(recipe => recipe.Id)
            .Select(recipe => new RecipeSuitabilityRecipeData(
                recipe.Id,
                recipe.Title,
                recipe.BaseServings))
            .ToArrayAsync(cancellationToken);

        if (recipes.Length == 0)
        {
            return new RecipeSuitabilityData(recipes, [], []);
        }

        var ingredients = await (
            from recipeIngredient in dbContext.RecipeIngredients.AsNoTracking()
            join recipe in dbContext.Recipes.AsNoTracking()
                on recipeIngredient.RecipeId equals recipe.Id
            join ingredient in dbContext.Ingredients.AsNoTracking()
                on recipeIngredient.IngredientId equals ingredient.Id
            where recipe.Status == RecipeStatus.Published
            orderby recipeIngredient.RecipeId, recipeIngredient.Sequence
            select new RecipeSuitabilityIngredientData(
                recipeIngredient.RecipeId,
                recipeIngredient.IngredientId,
                recipeIngredient.Sequence,
                ingredient.Name,
                ingredient.QuantityDimension,
                ingredient.IsActive,
                recipeIngredient.IsOptional,
                recipeIngredient.NormalizedQuantity,
                recipeIngredient.DisplayUnit))
            .ToArrayAsync(cancellationToken);

        var pantryItems = await (
            from pantryItem in dbContext.UserPantryItems.AsNoTracking()
            join ingredient in dbContext.Ingredients.AsNoTracking()
                on pantryItem.IngredientId equals ingredient.Id
            where pantryItem.UserId == userId
            orderby pantryItem.Id
            select new RecipeSuitabilityPantryItemData(
                pantryItem.IngredientId,
                pantryItem.NormalizedQuantity,
                pantryItem.DisplayUnit,
                ingredient.QuantityDimension))
            .ToArrayAsync(cancellationToken);

        return new RecipeSuitabilityData(recipes, ingredients, pantryItems);
    }
}
