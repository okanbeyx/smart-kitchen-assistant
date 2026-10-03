using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Application;

public sealed class RequiredIngredientAggregatorTests
{
    [Fact]
    public void AggregatesDuplicateRowsUsingNormalizedQuantity()
    {
        var success = RequiredIngredientAggregator.TryAggregate(
            [Source(1, 250m), Source(1, 750m, Unit.Kilogram)],
            out var requirement,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.Equal(1, requirement.IngredientId);
        Assert.Equal(1000m, requirement.NormalizedQuantity);
        Assert.Equal(QuantityDimension.Mass, requirement.QuantityDimension);
    }

    [Fact]
    public void RejectsMissingQuantity()
    {
        var success = RequiredIngredientAggregator.TryAggregate(
            [new RequiredIngredientSource(1, QuantityDimension.Mass, null, null)],
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(RequiredIngredientValidationError.MissingQuantity, error);
    }

    [Fact]
    public void RejectsDimensionMismatch()
    {
        var success = RequiredIngredientAggregator.TryAggregate(
            [Source(1, 1m), Source(1, 1m, Unit.Milliliter)],
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(
            RequiredIngredientValidationError.QuantityDimensionMismatch,
            error);
    }

    [Fact]
    public void RejectsAggregateBeyondSqlDecimalRange()
    {
        var success = RequiredIngredientAggregator.TryAggregate(
            [
                Source(1, 999_999_999_999m),
                Source(1, 1m)
            ],
            out _,
            out var error);

        Assert.False(success);
        Assert.Equal(RequiredIngredientValidationError.QuantityOverflow, error);
    }

    private static RequiredIngredientSource Source(
        long ingredientId,
        decimal quantity,
        Unit unit = Unit.Gram) =>
        new(ingredientId, QuantityDimension.Mass, quantity, unit);
}
