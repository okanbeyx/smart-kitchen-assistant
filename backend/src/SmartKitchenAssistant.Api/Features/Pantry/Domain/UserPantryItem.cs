using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Domain;

public sealed class UserPantryItem
{
    public const int MaximumUserIdLength = 256;

    public UserPantryItem(
        string userId,
        long ingredientId,
        decimal normalizedQuantity,
        Unit displayUnit)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User identifier cannot be empty.", nameof(userId));
        }

        if (userId.Length > MaximumUserIdLength)
        {
            throw new ArgumentException(
                $"User identifier cannot exceed {MaximumUserIdLength} UTF-16 code units.",
                nameof(userId));
        }

        if (ingredientId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ingredientId),
                ingredientId,
                "Ingredient identifier must be greater than zero.");
        }

        if (normalizedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedQuantity),
                normalizedQuantity,
                "Normalized quantity must be greater than zero.");
        }

        if (!Enum.IsDefined(displayUnit))
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayUnit),
                displayUnit,
                "Unsupported display unit.");
        }

        UserId = userId;
        IngredientId = ingredientId;
        NormalizedQuantity = normalizedQuantity;
        DisplayUnit = displayUnit;
    }

    public long Id { get; private set; }

    public string UserId { get; private set; }

    public long IngredientId { get; private set; }

    public decimal NormalizedQuantity { get; private set; }

    public Unit DisplayUnit { get; private set; }
}
