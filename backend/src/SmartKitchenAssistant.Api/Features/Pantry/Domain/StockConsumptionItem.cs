using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Domain;

public sealed class StockConsumptionItem
{
    private const decimal MaximumNormalizedQuantity =
        999_999_999_999.999999m;

    public StockConsumptionItem(
        long ingredientId,
        decimal consumedNormalizedQuantity,
        QuantityDimension quantityDimension)
    {
        if (ingredientId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ingredientId));
        }

        if (consumedNormalizedQuantity <= 0m ||
            consumedNormalizedQuantity > MaximumNormalizedQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(consumedNormalizedQuantity));
        }

        if (!Enum.IsDefined(quantityDimension))
        {
            throw new ArgumentOutOfRangeException(nameof(quantityDimension));
        }

        IngredientId = ingredientId;
        ConsumedNormalizedQuantity = consumedNormalizedQuantity;
        QuantityDimension = quantityDimension;
    }

    public long StockConsumptionId { get; private set; }

    public long IngredientId { get; private set; }

    public decimal ConsumedNormalizedQuantity { get; private set; }

    public QuantityDimension QuantityDimension { get; private set; }
}
