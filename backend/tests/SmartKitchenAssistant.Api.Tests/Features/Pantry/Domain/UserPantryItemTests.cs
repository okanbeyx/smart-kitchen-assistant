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

    [Fact]
    public void ConstructorRejectsUserIdentifierLongerThanStorageContract()
    {
        var userId = new string('a', UserPantryItem.MaximumUserIdLength + 1);

        Assert.Throws<ArgumentException>(() =>
            new UserPantryItem(userId, 1, 1000m, Unit.Gram));
    }

    [Fact]
    public void ConstructorAcceptsUserIdentifierAtStorageContractLimit()
    {
        var userId = new string('ü', UserPantryItem.MaximumUserIdLength);

        var item = new UserPantryItem(userId, 1, 1000m, Unit.Gram);

        Assert.Equal(userId, item.UserId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveIngredientIdentifier(long ingredientId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new UserPantryItem("user-1", ingredientId, 1000m, Unit.Gram));
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

    [Fact]
    public void UpdateQuantityChangesOnlyQuantityAndDisplayUnit()
    {
        var item = new UserPantryItem("user-1", 17, 1000m, Unit.Gram);

        item.UpdateQuantity(2m, Unit.Kilogram);

        Assert.Equal("user-1", item.UserId);
        Assert.Equal(17, item.IngredientId);
        Assert.Equal(2m, item.NormalizedQuantity);
        Assert.Equal(Unit.Kilogram, item.DisplayUnit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void UpdateQuantityRejectsNonPositiveQuantity(int quantity)
    {
        var item = new UserPantryItem("user-1", 1, 1m, Unit.Gram);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            item.UpdateQuantity(quantity, Unit.Gram));
    }

    [Fact]
    public void UpdateQuantityRejectsUnknownDisplayUnit()
    {
        var item = new UserPantryItem("user-1", 1, 1m, Unit.Gram);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            item.UpdateQuantity(1m, (Unit)999));
    }
}
