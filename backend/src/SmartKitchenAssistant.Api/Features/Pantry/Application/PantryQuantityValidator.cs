using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal static class PantryQuantityValidator
{
    internal const decimal MaximumNormalizedQuantity = 999_999_999_999.999999m;

    internal static bool TryParseUnit(string? value, out Unit unit)
    {
        unit = value switch
        {
            "g" => Unit.Gram,
            "kg" => Unit.Kilogram,
            "ml" => Unit.Milliliter,
            "l" => Unit.Liter,
            "adet" => Unit.Each,
            _ => default
        };

        return value is "g" or "kg" or "ml" or "l" or "adet";
    }

    internal static bool TryNormalize(
        decimal quantity,
        Unit unit,
        out decimal normalizedQuantity,
        out string? error)
    {
        normalizedQuantity = default;

        if (quantity <= 0m)
        {
            error = "Quantity must be greater than zero.";
            return false;
        }

        if (GetScale(quantity) > 6)
        {
            error = "Quantity cannot have more than 6 decimal places.";
            return false;
        }

        try
        {
            normalizedQuantity = unit.NormalizeToBase(quantity);
        }
        catch (OverflowException)
        {
            error = "Quantity exceeds the supported decimal(18,6) range.";
            return false;
        }

        if (GetScale(normalizedQuantity) > 6 ||
            normalizedQuantity > MaximumNormalizedQuantity)
        {
            normalizedQuantity = default;
            error = "Quantity exceeds the supported decimal(18,6) range.";
            return false;
        }

        error = null;
        return true;
    }

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0xFF;
}
