using SmartKitchenAssistant.Api.Application.Identity;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Application;

public sealed class RecipeSuitabilityServiceTests
{
    [Fact]
    public async Task AuthenticatedUserIdentifierIsForwardedExactly()
    {
        const string userId = " Exact-Üser ";
        var repository = new StubRecipeSuitabilityRepository();
        var service = CreateService(userId, repository);

        await service.ListAsync(CancellationToken.None);

        Assert.Equal(userId, repository.UserId);
    }

    [Fact]
    public async Task CancellationTokenIsForwarded()
    {
        var repository = new StubRecipeSuitabilityRepository();
        var service = CreateService("user", repository);
        using var cancellation = new CancellationTokenSource();

        await service.ListAsync(cancellation.Token);

        Assert.Equal(cancellation.Token, repository.CancellationToken);
    }

    [Fact]
    public async Task ResultsUseDeterministicStructuredRanking()
    {
        var repository = new StubRecipeSuitabilityRepository
        {
            Data = new RecipeSuitabilityData(
                [Recipe(1), Recipe(2), Recipe(3), Recipe(4)],
                [
                    Required(2, 20, 100m),
                    Required(3, 30, 100m),
                    Required(4, 40, 100m)
                ],
                [
                    Pantry(30, 100m),
                    Pantry(40, 50m)
                ])
        };
        var service = CreateService("user", repository);

        var results = await service.ListAsync(CancellationToken.None);

        Assert.Equal(
            [3L, 4L, 2L, 1L],
            results.Select(result => result.RecipeId).ToArray());
    }

    [Fact]
    public async Task EqualResultsUseLowerRecipeIdAsFinalTieBreak()
    {
        var repository = new StubRecipeSuitabilityRepository
        {
            Data = new RecipeSuitabilityData(
                [Recipe(2), Recipe(1)],
                [Required(2, 20, 1m), Required(1, 10, 1m)],
                [Pantry(10, 1m), Pantry(20, 1m)])
        };
        var service = CreateService("user", repository);

        var results = await service.ListAsync(CancellationToken.None);

        Assert.Equal([1L, 2L], results.Select(result => result.RecipeId).ToArray());
    }

    private static RecipeSuitabilityService CreateService(
        string userId,
        IRecipeSuitabilityRepository repository) =>
        new(
            new StubCurrentUser(userId),
            repository,
            new RecipeSuitabilityCalculator());

    private static RecipeSuitabilityRecipeData Recipe(long id) =>
        new(id, $"Recipe {id}", 4);

    private static RecipeSuitabilityIngredientData Required(
        long recipeId,
        long ingredientId,
        decimal quantity) =>
        new(
            recipeId,
            ingredientId,
            1,
            $"Ingredient {ingredientId}",
            QuantityDimension.Mass,
            true,
            false,
            quantity,
            Unit.Gram);

    private static RecipeSuitabilityPantryItemData Pantry(
        long ingredientId,
        decimal quantity) =>
        new(ingredientId, quantity, Unit.Gram, QuantityDimension.Mass);

    private sealed class StubCurrentUser(string userId) : ICurrentUser
    {
        public string UserId { get; } = userId;
    }

    private sealed class StubRecipeSuitabilityRepository : IRecipeSuitabilityRepository
    {
        internal RecipeSuitabilityData Data { get; init; } = new([], [], []);

        internal string? UserId { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        public Task<RecipeSuitabilityData> LoadAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            UserId = userId;
            CancellationToken = cancellationToken;
            return Task.FromResult(Data);
        }
    }
}
