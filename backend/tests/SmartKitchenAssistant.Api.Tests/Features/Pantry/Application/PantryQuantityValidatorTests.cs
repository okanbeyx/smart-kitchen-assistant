using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Pantry.Application;

public sealed class PantryQuantityValidatorTests
{
    [Theory]
    [InlineData("g", Unit.Gram)]
    [InlineData("kg", Unit.Kilogram)]
    [InlineData("ml", Unit.Milliliter)]
    [InlineData("l", Unit.Liter)]
    [InlineData("adet", Unit.Each)]
    public void TryParseUnitAcceptsSupportedExactSymbols(string value, Unit expected)
    {
        Assert.True(PantryQuantityValidator.TryParseUnit(value, out var unit));
        Assert.Equal(expected, unit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("KG")]
    [InlineData("cup")]
    public void TryParseUnitRejectsUnsupportedSymbols(string? value)
    {
        Assert.False(PantryQuantityValidator.TryParseUnit(value, out _));
    }

    [Fact]
    public void TryNormalizeAcceptsSixDecimalPlaces()
    {
        Assert.True(PantryQuantityValidator.TryNormalize(
            0.123456m,
            Unit.Kilogram,
            out var normalized,
            out var error));
        Assert.Equal(123.456000m, normalized);
        Assert.Null(error);
    }

    [Fact]
    public void TryNormalizeRejectsMoreThanSixDecimalPlaces()
    {
        Assert.False(PantryQuantityValidator.TryNormalize(
            0.1234567m,
            Unit.Gram,
            out _,
            out _));
    }

    [Fact]
    public void TryNormalizeAcceptsDecimal18Scale6Maximum()
    {
        Assert.True(PantryQuantityValidator.TryNormalize(
            PantryQuantityValidator.MaximumNormalizedQuantity,
            Unit.Gram,
            out var normalized,
            out _));
        Assert.Equal(PantryQuantityValidator.MaximumNormalizedQuantity, normalized);
    }

    [Fact]
    public void TryNormalizeRejectsDecimal18Scale6Overflow()
    {
        Assert.False(PantryQuantityValidator.TryNormalize(
            1_000_000_000_000m,
            Unit.Gram,
            out _,
            out _));
    }

    [Fact]
    public void TryNormalizeRejectsArithmeticOverflow()
    {
        Assert.False(PantryQuantityValidator.TryNormalize(
            decimal.MaxValue,
            Unit.Kilogram,
            out _,
            out _));
    }
}
