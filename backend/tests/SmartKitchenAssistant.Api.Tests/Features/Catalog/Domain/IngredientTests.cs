using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Catalog.Domain;

public sealed class IngredientTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsEmptyName(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Ingredient(name!, QuantityDimension.Mass));
    }

    [Fact]
    public void ConstructorRejectsUnknownQuantityDimension()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Ingredient("Un", (QuantityDimension)999));
    }

    [Fact]
    public void ConstructorCreatesActiveIngredientWithDimension()
    {
        var ingredient = new Ingredient("Un", QuantityDimension.Mass);

        Assert.Equal("Un", ingredient.Name);
        Assert.Equal(QuantityDimension.Mass, ingredient.QuantityDimension);
        Assert.True(ingredient.IsActive);
    }
}
