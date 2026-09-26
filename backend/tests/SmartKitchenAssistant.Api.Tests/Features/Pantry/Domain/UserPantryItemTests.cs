using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Pantry.Domain;

public sealed class UserPantryItemTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsEmptyUserIdentifier(string? userId)
    {
        Assert.Throws<ArgumentException>(() =>
            new UserPantryItem(userId!, 1, 1000m, Unit.Gram));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPantryItem("user-1", 1, quantity, Unit.Gram));
    }

    [Fact]
    public void ConstructorRejectsUnknownDisplayUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPantryItem("user-1", 1, 1000m, (Unit)999));
    }

    [Fact]
    public void ConstructorCreatesPantryItemWithRequiredValues()
    {
        var item = new UserPantryItem("user-1", 1, 1000m, Unit.Kilogram);

        Assert.Equal("user-1", item.UserId);
        Assert.Equal(1, item.IngredientId);
        Assert.Equal(1000m, item.NormalizedQuantity);
        Assert.Equal(Unit.Kilogram, item.DisplayUnit);
    }
}
