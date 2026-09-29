using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal interface IPantryRepository
{
    Task<IReadOnlyList<PantryItemView>> ListAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<PantryItemView?> GetAsync(
        long id,
        string userId,
        CancellationToken cancellationToken);

    Task<UserPantryItem?> GetTrackedAsync(
        long id,
        string userId,
        CancellationToken cancellationToken);

    Task<IngredientView?> GetIngredientAsync(
        long ingredientId,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(
        string userId,
        long ingredientId,
        CancellationToken cancellationToken);

    void Add(UserPantryItem item);

    void Remove(UserPantryItem item);

    Task<PantrySaveResult> SaveChangesAsync(CancellationToken cancellationToken);
}

internal sealed record IngredientView(
    long Id,
    string Name,
    QuantityDimension QuantityDimension,
    bool IsActive);

internal sealed record PantryItemView(
    long Id,
    long IngredientId,
    string IngredientName,
    decimal NormalizedQuantity,
    Unit DisplayUnit);

internal enum PantrySaveResult
{
    Saved,
    DuplicateUserIngredient
}
