using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Domain;

public sealed class RecipeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsEmptyTitle(string? title)
    {
        Assert.Throws<ArgumentException>(() => new Recipe(title!, 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveBaseServings(int baseServings)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Recipe("Mercimek Çorbası", baseServings));
    }

    [Fact]
    public void ConstructorCreatesDraftRecipe()
    {
        var recipe = new Recipe("Mercimek Çorbası", 4);

        Assert.Equal("Mercimek Çorbası", recipe.Title);
        Assert.Equal(4, recipe.BaseServings);
        Assert.Equal(RecipeStatus.Draft, recipe.Status);
    }
}
