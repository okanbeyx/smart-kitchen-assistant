using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Domain;

public sealed class RecipeIngredient
{
    public RecipeIngredient(
        long recipeId,
        long ingredientId,
        int sequence,
        bool isOptional,
        decimal? normalizedQuantity,
        Unit? displayUnit)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "Sequence must be greater than zero.");
        }

        if (normalizedQuantity.HasValue != displayUnit.HasValue)
        {
            throw new ArgumentException(
                "Normalized quantity and display unit must either both be provided or both be omitted.");
        }

        if (normalizedQuantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedQuantity),
                normalizedQuantity,
                "Normalized quantity must be greater than zero when provided.");
        }

        if (displayUnit.HasValue && !Enum.IsDefined(displayUnit.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayUnit),
                displayUnit,
                "Unsupported display unit.");
        }

        RecipeId = recipeId;
        IngredientId = ingredientId;
        Sequence = sequence;
        IsOptional = isOptional;
        NormalizedQuantity = normalizedQuantity;
        DisplayUnit = displayUnit;
    }

    public long Id { get; private set; }

    public long RecipeId { get; private set; }

    public long IngredientId { get; private set; }

    public int Sequence { get; private set; }

    public bool IsOptional { get; private set; }

    public decimal? NormalizedQuantity { get; private set; }

    public Unit? DisplayUnit { get; private set; }
}
