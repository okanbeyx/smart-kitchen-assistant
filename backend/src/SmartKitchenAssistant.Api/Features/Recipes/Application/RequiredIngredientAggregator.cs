using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal static class RequiredIngredientAggregator
{
    internal const decimal MaximumNormalizedQuantity =
        999_999_999_999.999999m;

    internal static bool TryAggregate(
        IReadOnlyList<RequiredIngredientSource> items,
        out RequiredIngredientRequirement requirement,
        out RequiredIngredientValidationError? error)
    {
        ArgumentOutOfRangeException.ThrowIfZero(items.Count);

        var first = items[0];
        var total = 0m;

        foreach (var item in items)
        {
            if (item.IngredientId != first.IngredientId ||
                !item.NormalizedQuantity.HasValue ||
                !item.DisplayUnit.HasValue)
            {
                requirement = default!;
                error = RequiredIngredientValidationError.MissingQuantity;
                return false;
            }

            if (item.NormalizedQuantity.Value <= 0m ||
                !Enum.IsDefined(item.QuantityDimension) ||
                !Enum.IsDefined(item.DisplayUnit.Value))
            {
                requirement = default!;
                error = RequiredIngredientValidationError.InvalidQuantity;
                return false;
            }

            if (item.QuantityDimension != first.QuantityDimension ||
                item.DisplayUnit.Value.GetDimension() != item.QuantityDimension)
            {
                requirement = default!;
                error = RequiredIngredientValidationError.QuantityDimensionMismatch;
                return false;
            }

            try
            {
                total = checked(total + item.NormalizedQuantity.Value);
            }
            catch (OverflowException)
            {
                requirement = default!;
                error = RequiredIngredientValidationError.QuantityOverflow;
                return false;
            }

            if (total > MaximumNormalizedQuantity)
            {
                requirement = default!;
                error = RequiredIngredientValidationError.QuantityOverflow;
                return false;
            }
        }

        requirement = new RequiredIngredientRequirement(
            first.IngredientId,
            total,
            first.QuantityDimension);
        error = null;
        return true;
    }
}

internal sealed record RequiredIngredientSource(
    long IngredientId,
    QuantityDimension QuantityDimension,
    decimal? NormalizedQuantity,
    Unit? DisplayUnit);

internal sealed record RequiredIngredientRequirement(
    long IngredientId,
    decimal NormalizedQuantity,
    QuantityDimension QuantityDimension);

internal enum RequiredIngredientValidationError
{
    MissingQuantity,
    InvalidQuantity,
    QuantityDimensionMismatch,
    QuantityOverflow
}
