using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Catalog.Domain;

public sealed class UnitConversionTests
{
    public static TheoryData<Unit, string, QuantityDimension, Unit, decimal> UnitDefinitions => new()
    {
        { Unit.Gram, "g", QuantityDimension.Mass, Unit.Gram, 1m },
        { Unit.Kilogram, "kg", QuantityDimension.Mass, Unit.Gram, 1000m },
        { Unit.Milliliter, "ml", QuantityDimension.Volume, Unit.Milliliter, 1m },
        { Unit.Liter, "l", QuantityDimension.Volume, Unit.Milliliter, 1000m },
        { Unit.Each, "adet", QuantityDimension.Count, Unit.Each, 1m }
    };

    [Theory]
    [MemberData(nameof(UnitDefinitions))]
    public void UnitExposesExpectedDefinition(
        Unit unit,
        string symbol,
        QuantityDimension dimension,
        Unit baseUnit,
        decimal conversionFactor)
    {
        Assert.Equal(symbol, unit.GetSymbol());
        Assert.Equal(dimension, unit.GetDimension());
        Assert.Equal(baseUnit, unit.GetBaseUnit());
        Assert.Equal(conversionFactor, unit.GetBaseUnitConversionFactor());
    }

    [Theory]
    [InlineData(Unit.Gram, 2, 2)]
    [InlineData(Unit.Kilogram, 2, 2000)]
    [InlineData(Unit.Milliliter, 2, 2)]
    [InlineData(Unit.Liter, 2, 2000)]
    [InlineData(Unit.Each, 2, 2)]
    public void NormalizeToBaseReturnsExpectedQuantity(Unit unit, int quantity, int expected)
    {
        Assert.Equal((decimal)expected, unit.NormalizeToBase(quantity));
    }

    [Theory]
    [InlineData(Unit.Gram, 2, 2)]
    [InlineData(Unit.Kilogram, 2000, 2)]
    [InlineData(Unit.Milliliter, 2, 2)]
    [InlineData(Unit.Liter, 2000, 2)]
    [InlineData(Unit.Each, 2, 2)]
    public void ConvertFromBaseReturnsExpectedDisplayQuantity(Unit unit, int quantity, int expected)
    {
        Assert.Equal((decimal)expected, unit.ConvertFromBase(quantity));
    }

    [Fact]
    public void HalfKilogramNormalizesToFiveHundredGrams()
    {
        Assert.Equal(500m, Unit.Kilogram.NormalizeToBase(0.5m));
    }

    [Fact]
    public void TwoHundredFiftyGramsConvertsFromBaseToQuarterKilogram()
    {
        Assert.Equal(0.25m, Unit.Kilogram.ConvertFromBase(250m));
    }

    [Fact]
    public void HalfLiterNormalizesToFiveHundredMilliliters()
    {
        Assert.Equal(500m, Unit.Liter.NormalizeToBase(0.5m));
    }

    [Fact]
    public void ConvertToConvertsWithinTheSameDimension()
    {
        Assert.Equal(0.25m, Unit.Gram.ConvertTo(250m, Unit.Kilogram));
    }

    [Fact]
    public void ConvertToRejectsCrossDimensionConversion()
    {
        Assert.Throws<InvalidOperationException>(() => Unit.Kilogram.ConvertTo(1m, Unit.Liter));
    }

    [Fact]
    public void MetadataRejectsUnknownUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((Unit)999).GetDimension());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NormalizeToBaseRejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit.Gram.NormalizeToBase(quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConvertFromBaseRejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Unit.Kilogram.ConvertFromBase(quantity));
    }

    [Fact]
    public void NormalizeToBaseRejectsDecimalOverflow()
    {
        Assert.Throws<OverflowException>(() =>
            Unit.Kilogram.NormalizeToBase(decimal.MaxValue));
    }
}
