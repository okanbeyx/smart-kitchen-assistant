using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Pantry.Application;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal sealed class PantryRepository(SmartKitchenDbContext dbContext)
    : IPantryRepository
{
    public async Task<IReadOnlyList<PantryItemView>> ListAsync(
        string userId,
        CancellationToken cancellationToken) =>
        await (
            from item in dbContext.UserPantryItems.AsNoTracking()
            join ingredient in dbContext.Ingredients.AsNoTracking()
                on item.IngredientId equals ingredient.Id
            where item.UserId == userId
            orderby item.Id
            select new PantryItemView(
                item.Id,
                item.IngredientId,
                ingredient.Name,
                item.NormalizedQuantity,
                item.DisplayUnit))
            .ToArrayAsync(cancellationToken);

    public Task<PantryItemView?> GetAsync(
        long id,
        string userId,
        CancellationToken cancellationToken) =>
        (
            from item in dbContext.UserPantryItems.AsNoTracking()
            join ingredient in dbContext.Ingredients.AsNoTracking()
                on item.IngredientId equals ingredient.Id
            where item.Id == id && item.UserId == userId
            select new PantryItemView(
                item.Id,
                item.IngredientId,
                ingredient.Name,
                item.NormalizedQuantity,
                item.DisplayUnit))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<UserPantryItem?> GetTrackedAsync(
        long id,
        string userId,
        CancellationToken cancellationToken) =>
        dbContext.UserPantryItems.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId,
            cancellationToken);

    public Task<IngredientView?> GetIngredientAsync(
        long ingredientId,
        CancellationToken cancellationToken) =>
        dbContext.Ingredients
            .AsNoTracking()
            .Where(ingredient => ingredient.Id == ingredientId)
            .Select(ingredient => new IngredientView(
                ingredient.Id,
                ingredient.Name,
                ingredient.QuantityDimension,
                ingredient.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        string userId,
        long ingredientId,
        CancellationToken cancellationToken) =>
        dbContext.UserPantryItems.AnyAsync(
            item => item.UserId == userId && item.IngredientId == ingredientId,
            cancellationToken);

    public void Add(UserPantryItem item) =>
        dbContext.UserPantryItems.Add(item);

    public void Remove(UserPantryItem item) =>
        dbContext.UserPantryItems.Remove(item);

    public async Task<PantrySaveResult> SaveChangesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return PantrySaveResult.Saved;
        }
        catch (DbUpdateException exception)
            when (SqlServerPantryErrorClassifier.IsDuplicateUserIngredient(exception))
        {
            return PantrySaveResult.DuplicateUserIngredient;
        }
    }

}
