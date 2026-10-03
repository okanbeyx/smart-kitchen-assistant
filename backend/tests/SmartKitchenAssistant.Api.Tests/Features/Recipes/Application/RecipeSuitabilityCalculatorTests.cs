using System.Text.Json;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Application;

public sealed class RecipeSuitabilityCalculatorTests
{
    private readonly RecipeSuitabilityCalculator _calculator = new();

    [Fact]
    public void AllRequiredIngredientsSufficientIsCookable()
    {
        var result = Calculate(
            [Required(1, 500m), Required(2, 1000m, QuantityDimension.Volume, Unit.Liter)],
            [Pantry(1, 500m), Pantry(2, 1200m, QuantityDimension.Volume, Unit.Liter)]);

        Assert.Equal(RecipeSuitabilityClassification.Cookable, result.Classification);
        Assert.True(result.IsCookable);
        Assert.Equal(2, result.SatisfiedRequiredIngredientCount);
        Assert.Equal(1m, result.RequiredCoverage);
    }

    [Fact]
    public void MissingRequiredIngredientIsReported()
    {
        var result = Calculate([Required(1, 500m)], []);

        Assert.Equal(RecipeSuitabilityClassification.MissingRequired, result.Classification);
        Assert.Single(result.MissingRequiredIngredients);
        Assert.Equal(0m, result.RequiredCoverage);
    }

    [Fact]
    public void InsufficientRequiredIngredientReportsExactShortfall()
    {
        var result = Calculate([Required(1, 500m)], [Pantry(1, 125m)]);

        Assert.Equal(
            RecipeSuitabilityClassification.InsufficientRequired,
            result.Classification);
        var insufficient = Assert.Single(result.InsufficientRequiredIngredients);
        Assert.Equal(375m, insufficient.ShortfallNormalizedQuantity);
        Assert.Equal(0.25m, result.RequiredCoverage);
    }

    [Fact]
    public void MissingRequiredDominatesInsufficientRequired()
    {
        var result = Calculate(
            [Required(1, 100m), Required(2, 100m)],
            [Pantry(1, 50m)]);

        Assert.Equal(RecipeSuitabilityClassification.MissingRequired, result.Classification);
        Assert.Single(result.MissingRequiredIngredients);
        Assert.Single(result.InsufficientRequiredIngredients);
    }

    [Fact]
    public void UnevaluableRequiredDominatesMissingRequired()
    {
        var result = Calculate(
            [RequiredWithoutQuantity(1), Required(2, 100m)],
            []);

        Assert.Equal(RecipeSuitabilityClassification.Unevaluable, result.Classification);
        Assert.False(result.IsCookable);
        Assert.Single(result.UnevaluableIngredients);
        Assert.Single(result.MissingRequiredIngredients);
        Assert.Null(result.RequiredCoverage);
    }

    [Fact]
    public void RecipeWithoutRequiredGroupsIsUnevaluable()
    {
        var result = Calculate([Optional(1, 10m)], [Pantry(1, 10m)]);

        Assert.Equal(RecipeSuitabilityClassification.Unevaluable, result.Classification);
        Assert.False(result.IsCookable);
        Assert.Null(result.RequiredCoverage);
        Assert.Contains(SuitabilityReasonCodes.NoRequiredIngredients, result.ReasonCodes);
    }

    [Fact]
    public void MissingOptionalIngredientDoesNotAffectCookableClassification()
    {
        var result = Calculate([Required(1, 10m), Optional(2, 5m)], [Pantry(1, 10m)]);

        Assert.Equal(RecipeSuitabilityClassification.Cookable, result.Classification);
        Assert.Equal(OptionalIngredientIssueKind.Missing, Assert.Single(result.OptionalIssues).Kind);
    }

    [Fact]
    public void SufficientOptionalIngredientIsAvailable()
    {
        var result = Calculate(
            [Required(1, 10m), Optional(2, 5m)],
            [Pantry(1, 10m), Pantry(2, 5m)]);

        Assert.Equal(1, result.AvailableOptionalIngredientCount);
        Assert.Empty(result.OptionalIssues);
        Assert.Equal(1m, result.OptionalAvailabilityRatio);
    }

    [Fact]
    public void InsufficientOptionalIngredientIsReportedWithoutChangingClassification()
    {
        var result = Calculate(
            [Required(1, 10m), Optional(2, 5m)],
            [Pantry(1, 10m), Pantry(2, 2m)]);

        Assert.Equal(RecipeSuitabilityClassification.Cookable, result.Classification);
        var issue = Assert.Single(result.OptionalIssues);
        Assert.Equal(OptionalIngredientIssueKind.Insufficient, issue.Kind);
        Assert.Equal(3m, issue.ShortfallNormalizedQuantity);
    }

    [Fact]
    public void QuantitylessOptionalIngredientUsesPresenceOnlySemantics()
    {
        var result = Calculate(
            [Required(1, 10m), OptionalWithoutQuantity(2)],
            [Pantry(1, 10m), Pantry(2, 1m)]);

        Assert.Equal(1, result.AvailableOptionalIngredientCount);
        Assert.Empty(result.OptionalIssues);
    }

    [Fact]
    public void MixedOptionalQuantityModesAreUnevaluable()
    {
        var quantified = Optional(2, 5m) with { Sequence = 2 };
        var presenceOnly = OptionalWithoutQuantity(2) with { Sequence = 3 };
        var result = Calculate(
            [Required(1, 10m), quantified, presenceOnly],
            [Pantry(1, 10m), Pantry(2, 10m)]);

        var issue = Assert.Single(result.OptionalIssues);
        Assert.Equal(OptionalIngredientIssueKind.Unevaluable, issue.Kind);
        Assert.Equal(SuitabilityReasonCodes.MixedOptionalQuantityMode, issue.ReasonCode);
        Assert.Equal(0, result.AvailableOptionalIngredientCount);
    }

    [Fact]
    public void QuantitylessRequiredIngredientIsUnevaluable()
    {
        var result = Calculate([RequiredWithoutQuantity(1)], [Pantry(1, 10m)]);

        Assert.Equal(RecipeSuitabilityClassification.Unevaluable, result.Classification);
        Assert.Equal(
            SuitabilityReasonCodes.MissingRequiredQuantity,
            Assert.Single(result.UnevaluableIngredients).ReasonCode);
    }

    [Fact]
    public void RecipeUnitDimensionMismatchIsUnevaluable()
    {
        var requirement = Required(1, 10m, QuantityDimension.Volume, Unit.Gram);

        var result = Calculate(
            [requirement],
            [Pantry(1, 10m, QuantityDimension.Volume, Unit.Liter)]);

        Assert.Equal(RecipeSuitabilityClassification.Unevaluable, result.Classification);
        Assert.Equal(
            SuitabilityReasonCodes.QuantityDimensionMismatch,
            Assert.Single(result.UnevaluableIngredients).ReasonCode);
    }

    [Fact]
    public void PantryUnitDimensionMismatchIsUnevaluable()
    {
        var result = Calculate(
            [Required(1, 10m, QuantityDimension.Volume, Unit.Liter)],
            [Pantry(1, 10m, QuantityDimension.Volume, Unit.Gram)]);

        Assert.Equal(RecipeSuitabilityClassification.Unevaluable, result.Classification);
        Assert.Equal(
            SuitabilityReasonCodes.PantryQuantityDimensionMismatch,
            Assert.Single(result.UnevaluableIngredients).ReasonCode);
    }

    [Fact]
    public void NormalizedQuantitiesAreComparedWithoutDisplayUnitReconversion()
    {
        var result = Calculate(
            [Required(1, 1000m, QuantityDimension.Mass, Unit.Gram)],
            [Pantry(1, 1.5m, QuantityDimension.Mass, Unit.Kilogram)]);

        Assert.Equal(
            RecipeSuitabilityClassification.InsufficientRequired,
            result.Classification);
        Assert.Equal(1.5m, Assert.Single(result.InsufficientRequiredIngredients)
            .AvailableNormalizedQuantity);
    }

    [Fact]
    public void RepeatedRequiredIngredientQuantitiesAreAggregated()
    {
        var first = Required(1, 60m) with { Sequence = 1 };
        var second = Required(1, 60m) with { Sequence = 2 };

        var result = Calculate([first, second], [Pantry(1, 100m)]);

        Assert.Equal(1, result.RequiredIngredientCount);
        Assert.Equal(120m, Assert.Single(result.InsufficientRequiredIngredients)
            .RequiredNormalizedQuantity);
    }

    [Fact]
    public void RepeatedOptionalIngredientQuantitiesAreAggregated()
    {
        var first = Optional(2, 60m) with { Sequence = 2 };
        var second = Optional(2, 60m) with { Sequence = 3 };

        var result = Calculate(
            [Required(1, 10m), first, second],
            [Pantry(1, 10m), Pantry(2, 100m)]);

        Assert.Equal(1, result.OptionalIngredientCount);
        Assert.Equal(120m, Assert.Single(result.OptionalIssues).RequiredNormalizedQuantity);
    }

    [Fact]
    public void RequiredAllocationIsRemovedBeforeEvaluatingOptionalQuantity()
    {
        var result = Calculate(
            [Required(1, 60m), Optional(1, 50m) with { Sequence = 2 }],
            [Pantry(1, 100m)]);

        Assert.Equal(RecipeSuitabilityClassification.Cookable, result.Classification);
        var issue = Assert.Single(result.OptionalIssues);
        Assert.Equal(OptionalIngredientIssueKind.Insufficient, issue.Kind);
        Assert.Equal(40m, issue.AvailableNormalizedQuantity);
    }

    [Fact]
    public void PresenceOnlyOptionalSharingRequiredIngredientNeedsRemainingStock()
    {
        var ingredients = new[]
        {
            Required(1, 100m),
            OptionalWithoutQuantity(1) with { Sequence = 2 }
        };

        var noRemaining = Calculate(ingredients, [Pantry(1, 100m)]);
        var remaining = Calculate(ingredients, [Pantry(1, 101m)]);

        Assert.Equal(0, noRemaining.AvailableOptionalIngredientCount);
        Assert.Single(noRemaining.OptionalIssues);
        Assert.Equal(1, remaining.AvailableOptionalIngredientCount);
        Assert.Empty(remaining.OptionalIssues);
    }

    [Fact]
    public void InactiveIngredientDoesNotInvalidateExistingRequirement()
    {
        var inactive = Required(1, 10m) with { IsActive = false };

        var result = Calculate([inactive], [Pantry(1, 10m)]);

        Assert.Equal(RecipeSuitabilityClassification.Cookable, result.Classification);
    }

    [Fact]
    public void IdenticalInputProducesIdenticalResult()
    {
        var data = Data(
            [Required(1, 3m), Optional(2, 2m)],
            [Pantry(1, 2m), Pantry(2, 1m)]);

        var first = _calculator.Calculate(data);
        var second = _calculator.Calculate(data);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    [Fact]
    public void EqualRankUsesLowerRecipeIdAsFinalTieBreak()
    {
        var recipes = new[] { Recipe(2), Recipe(1) };
        var ingredients = new[]
        {
            Required(1, 10m) with { RecipeId = 1 },
            Required(1, 10m) with { RecipeId = 2 }
        };
        var results = _calculator.Calculate(
            new RecipeSuitabilityData(recipes, ingredients, [Pantry(1, 10m)]));

        Assert.Equal(
            [1L, 2L],
            RecipeSuitabilityRanking.Order(results)
                .Select(result => result.RecipeId)
                .ToArray());
    }

    [Fact]
    public void RequiredCoverageUsesUnroundedDecimalArithmetic()
    {
        var result = Calculate(
            [Required(1, 3m), Required(2, 3m)],
            [Pantry(1, 1m), Pantry(2, 2m)]);

        Assert.Equal((1m / 3m + 2m / 3m) / 2m, result.RequiredCoverage);
    }

    [Fact]
    public void OptionalRatioIsZeroWhenRecipeHasNoOptionalGroups()
    {
        var result = Calculate([Required(1, 1m)], [Pantry(1, 1m)]);

        Assert.Equal(0m, result.OptionalAvailabilityRatio);
    }

    private RecipeSuitabilityResult Calculate(
        IReadOnlyList<RecipeSuitabilityIngredientData> ingredients,
        IReadOnlyList<RecipeSuitabilityPantryItemData> pantryItems) =>
        Assert.Single(_calculator.Calculate(Data(ingredients, pantryItems)));

    private static RecipeSuitabilityData Data(
        IReadOnlyList<RecipeSuitabilityIngredientData> ingredients,
        IReadOnlyList<RecipeSuitabilityPantryItemData> pantryItems) =>
        new([Recipe(1)], ingredients, pantryItems);

    private static RecipeSuitabilityRecipeData Recipe(long id) =>
        new(id, $"Recipe {id}", 4);

    private static RecipeSuitabilityIngredientData Required(
        long ingredientId,
        decimal quantity,
        QuantityDimension dimension = QuantityDimension.Mass,
        Unit unit = Unit.Gram) =>
        Ingredient(ingredientId, isOptional: false, quantity, unit, dimension);

    private static RecipeSuitabilityIngredientData RequiredWithoutQuantity(
        long ingredientId) =>
        Ingredient(
            ingredientId,
            isOptional: false,
            null,
            null,
            QuantityDimension.Mass);

    private static RecipeSuitabilityIngredientData Optional(
        long ingredientId,
        decimal quantity) =>
        Ingredient(
            ingredientId,
            isOptional: true,
            quantity,
            Unit.Gram,
            QuantityDimension.Mass);

    private static RecipeSuitabilityIngredientData OptionalWithoutQuantity(
        long ingredientId) =>
        Ingredient(
            ingredientId,
            isOptional: true,
            null,
            null,
            QuantityDimension.Mass);

    private static RecipeSuitabilityIngredientData Ingredient(
        long ingredientId,
        bool isOptional,
        decimal? quantity,
        Unit? unit,
        QuantityDimension dimension) =>
        new(
            1,
            ingredientId,
            1,
            $"Ingredient {ingredientId}",
            dimension,
            true,
            isOptional,
            quantity,
            unit);

    private static RecipeSuitabilityPantryItemData Pantry(
        long ingredientId,
        decimal quantity,
        QuantityDimension dimension = QuantityDimension.Mass,
        Unit unit = Unit.Gram) =>
        new(ingredientId, quantity, unit, dimension);
}
