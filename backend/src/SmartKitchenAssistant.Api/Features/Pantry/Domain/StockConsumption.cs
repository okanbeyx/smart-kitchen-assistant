namespace SmartKitchenAssistant.Api.Features.Pantry.Domain;

public sealed class StockConsumption
{
    public const int MaximumIdempotencyKeyLength = 128;

    private readonly List<StockConsumptionItem> _items = [];

    public StockConsumption(string userId, string idempotencyKey, long recipeId)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            userId.Length > UserPantryItem.MaximumUserIdLength)
        {
            throw new ArgumentException("User identifier is invalid.", nameof(userId));
        }

        if (!IsValidIdempotencyKey(idempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency key must contain 1 to 128 visible ASCII characters.",
                nameof(idempotencyKey));
        }

        if (recipeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recipeId));
        }

        UserId = userId;
        IdempotencyKey = idempotencyKey;
        RecipeId = recipeId;
    }

    public long Id { get; private set; }

    public string UserId { get; private set; }

    public string IdempotencyKey { get; private set; }

    public long RecipeId { get; private set; }

    public IReadOnlyCollection<StockConsumptionItem> Items => _items;

    public void AddItem(StockConsumptionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_items.Any(existing => existing.IngredientId == item.IngredientId))
        {
            throw new InvalidOperationException(
                "A consumed ingredient may appear only once per operation.");
        }

        _items.Add(item);
    }

    internal static bool IsValidIdempotencyKey(string? value) =>
        value is { Length: >= 1 and <= MaximumIdempotencyKeyLength } &&
        value.All(character => character is >= '!' and <= '~');
}
