using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Application;

public sealed class RecipeReadServiceTests
{
    [Fact]
    public async Task OptionalIngredientWithoutQuantityPreservesNullDisplayValues()
    {
        var service = CreateService(new RecipeIngredientData(
            7,
            "Salt",
            QuantityDimension.Mass,
            true,
            null,
            null));

        var result = await service.GetAsync(1, CancellationToken.None);

        var ingredient = Assert.Single(Assert.IsType<RecipeDetailView>(result).Ingredients);
        Assert.Null(ingredient.Quantity);
        Assert.Null(ingredient.Unit);
        Assert.True(ingredient.IsOptional);
    }

    [Fact]
    public async Task RequiredIngredientWithoutQuantityIsRejectedAsInvalidCatalogData()
    {
        var service = CreateService(new RecipeIngredientData(
            7,
            "Salt",
            QuantityDimension.Mass,
            false,
            null,
            null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task IngredientWithMismatchedUnitDimensionIsRejected()
    {
        var service = CreateService(new RecipeIngredientData(
            7,
            "Milk",
            QuantityDimension.Volume,
            false,
            1000m,
            Unit.Gram));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(1, CancellationToken.None));
    }

    [Fact]
    public async Task DisplayQuantitiesUseExistingExactUnitConversions()
    {
        var repository = new StubRecipeReadRepository
        {
            Detail = new RecipeDetailData(
                1,
                "Recipe",
                4,
                [
                    new RecipeIngredientData(
                        1, "Flour", QuantityDimension.Mass, false, 250m, Unit.Kilogram),
                    new RecipeIngredientData(
                        2, "Milk", QuantityDimension.Volume, false, 1500m, Unit.Liter),
                    new RecipeIngredientData(
                        3, "Egg", QuantityDimension.Count, false, 2m, Unit.Each)
                ],
                [])
        };
        var service = new RecipeReadService(repository);

        var result = await service.GetAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Collection(
            result.Ingredients,
            ingredient =>
            {
                Assert.Equal(0.25m, ingredient.Quantity);
                Assert.Equal("kg", ingredient.Unit);
            },
            ingredient =>
            {
                Assert.Equal(1.5m, ingredient.Quantity);
                Assert.Equal("l", ingredient.Unit);
            },
            ingredient =>
            {
                Assert.Equal(2m, ingredient.Quantity);
                Assert.Equal("adet", ingredient.Unit);
            });
    }

    [Fact]
    public async Task NonPositiveIdReturnsNotFoundWithoutQueryingRepository()
    {
        var repository = new StubRecipeReadRepository();
        var service = new RecipeReadService(repository);

        var result = await service.GetAsync(0, CancellationToken.None);

        Assert.Null(result);
        Assert.False(repository.GetWasCalled);
    }

    private static RecipeReadService CreateService(RecipeIngredientData ingredient) =>
        new(new StubRecipeReadRepository
        {
            Detail = new RecipeDetailData(1, "Recipe", 4, [ingredient], [])
        });

    private sealed class StubRecipeReadRepository : IRecipeReadRepository
    {
        internal RecipeDetailData? Detail { get; init; }

        internal bool GetWasCalled { get; private set; }

        public Task<IReadOnlyList<RecipeSummaryView>> ListPublishedAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecipeSummaryView>>([]);

        public Task<RecipeDetailData?> GetPublishedAsync(
            long id,
            CancellationToken cancellationToken)
        {
            GetWasCalled = true;
            return Task.FromResult(Detail);
        }
    }
}
