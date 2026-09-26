using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Domain;

public sealed class RecipeIngredientTests
{
    [Fact]
    public void ConstructorAllowsQuantityAndUnitToBeOmittedTogether()
    {
        var ingredient = new RecipeIngredient(1, 2, 1, true, null, null);

        Assert.Null(ingredient.NormalizedQuantity);
        Assert.Null(ingredient.DisplayUnit);
        Assert.True(ingredient.IsOptional);
    }

    [Fact]
    public void ConstructorAllowsQuantityAndUnitToBeProvidedTogether()
    {
        var ingredient = new RecipeIngredient(1, 2, 1, false, 1000m, Unit.Gram);

        Assert.Equal(1000m, ingredient.NormalizedQuantity);
        Assert.Equal(Unit.Gram, ingredient.DisplayUnit);
        Assert.False(ingredient.IsOptional);
    }

    [Fact]
    public void ConstructorRejectsQuantityWithoutUnit()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecipeIngredient(1, 2, 1, false, 1000m, null));
    }

    [Fact]
    public void ConstructorRejectsUnitWithoutQuantity()
    {
        Assert.Throws<ArgumentException>(() =>
            new RecipeIngredient(1, 2, 1, false, null, Unit.Gram));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecipeIngredient(1, 2, 1, false, quantity, Unit.Gram));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveSequence(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecipeIngredient(1, 2, sequence, false, 1000m, Unit.Gram));
    }
}
