namespace SmartKitchenAssistant.Api.Features.Catalog.Domain;

public static class UnitConversions
{
    public static string GetSymbol(this Unit unit) => unit switch
    {
        Unit.Gram => "g",
        Unit.Kilogram => "kg",
        Unit.Milliliter => "ml",
        Unit.Liter => "l",
        Unit.Each => "adet",
        _ => throw UnknownUnit(unit)
    };

    public static QuantityDimension GetDimension(this Unit unit) => unit switch
    {
        Unit.Gram or Unit.Kilogram => QuantityDimension.Mass,
        Unit.Milliliter or Unit.Liter => QuantityDimension.Volume,
        Unit.Each => QuantityDimension.Count,
        _ => throw UnknownUnit(unit)
    };

    public static Unit GetBaseUnit(this Unit unit) => unit switch
    {
        Unit.Gram or Unit.Kilogram => Unit.Gram,
        Unit.Milliliter or Unit.Liter => Unit.Milliliter,
        Unit.Each => Unit.Each,
        _ => throw UnknownUnit(unit)
    };

    public static decimal GetBaseUnitConversionFactor(this Unit unit) => unit switch
    {
        Unit.Gram or Unit.Milliliter or Unit.Each => 1m,
        Unit.Kilogram or Unit.Liter => 1000m,
        _ => throw UnknownUnit(unit)
    };

    public static decimal NormalizeToBase(this Unit unit, decimal quantity)
    {
        EnsurePositive(quantity);
        return checked(quantity * unit.GetBaseUnitConversionFactor());
    }

    public static decimal ConvertFromBase(this Unit unit, decimal normalizedQuantity)
    {
        EnsurePositive(normalizedQuantity);
        return normalizedQuantity / unit.GetBaseUnitConversionFactor();
    }

    public static decimal ConvertTo(this Unit sourceUnit, decimal quantity, Unit targetUnit)
    {
        if (sourceUnit.GetDimension() != targetUnit.GetDimension())
        {
            throw new InvalidOperationException(
                $"Cannot convert from {sourceUnit.GetDimension()} to {targetUnit.GetDimension()}.");
        }

        var normalizedQuantity = sourceUnit.NormalizeToBase(quantity);
        return targetUnit.ConvertFromBase(normalizedQuantity);
    }

    private static void EnsurePositive(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Quantity must be greater than zero.");
        }
    }

    private static ArgumentOutOfRangeException UnknownUnit(Unit unit) =>
        new(nameof(unit), unit, "Unsupported unit.");
}
