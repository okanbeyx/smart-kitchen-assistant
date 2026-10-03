using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal sealed record RecipeSuitabilityData(
    IReadOnlyList<RecipeSuitabilityRecipeData> Recipes,
    IReadOnlyList<RecipeSuitabilityIngredientData> Ingredients,
    IReadOnlyList<RecipeSuitabilityPantryItemData> PantryItems);

internal sealed record RecipeSuitabilityRecipeData(
    long Id,
    string Title,
    int BaseServings);

internal sealed record RecipeSuitabilityIngredientData(
    long RecipeId,
    long IngredientId,
    int Sequence,
    string Name,
    QuantityDimension QuantityDimension,
    bool IsActive,
    bool IsOptional,
    decimal? NormalizedQuantity,
    Unit? DisplayUnit);

internal sealed record RecipeSuitabilityPantryItemData(
    long IngredientId,
    decimal NormalizedQuantity,
    Unit DisplayUnit,
    QuantityDimension QuantityDimension);

internal enum RecipeSuitabilityClassification
{
    Unevaluable,
    MissingRequired,
    InsufficientRequired,
    Cookable
}

internal enum OptionalIngredientIssueKind
{
    Missing,
    Insufficient,
    Unevaluable
}

internal sealed record MissingRequiredIngredient(
    long IngredientId,
    string Name,
    decimal RequiredNormalizedQuantity,
    QuantityDimension QuantityDimension);

internal sealed record InsufficientRequiredIngredient(
    long IngredientId,
    string Name,
    decimal RequiredNormalizedQuantity,
    decimal AvailableNormalizedQuantity,
    decimal ShortfallNormalizedQuantity,
    QuantityDimension QuantityDimension);

internal sealed record OptionalIngredientIssue(
    long IngredientId,
    string Name,
    OptionalIngredientIssueKind Kind,
    decimal? RequiredNormalizedQuantity,
    decimal? AvailableNormalizedQuantity,
    decimal? ShortfallNormalizedQuantity,
    QuantityDimension QuantityDimension,
    string? ReasonCode);

internal sealed record UnevaluableIngredient(
    long IngredientId,
    string Name,
    string ReasonCode);

internal sealed record RecipeSuitabilityResult(
    long RecipeId,
    string Title,
    int BaseServings,
    RecipeSuitabilityClassification Classification,
    bool IsCookable,
    int RequiredIngredientCount,
    int SatisfiedRequiredIngredientCount,
    decimal? RequiredCoverage,
    IReadOnlyList<MissingRequiredIngredient> MissingRequiredIngredients,
    IReadOnlyList<InsufficientRequiredIngredient> InsufficientRequiredIngredients,
    int OptionalIngredientCount,
    int AvailableOptionalIngredientCount,
    decimal OptionalAvailabilityRatio,
    IReadOnlyList<OptionalIngredientIssue> OptionalIssues,
    IReadOnlyList<UnevaluableIngredient> UnevaluableIngredients,
    IReadOnlyList<string> ReasonCodes);

internal static class SuitabilityReasonCodes
{
    internal const string NoRequiredIngredients = "no_required_ingredients";
    internal const string MissingRequiredQuantity = "missing_required_quantity";
    internal const string InvalidQuantity = "invalid_quantity";
    internal const string QuantityDimensionMismatch = "quantity_dimension_mismatch";
    internal const string PantryQuantityDimensionMismatch =
        "pantry_quantity_dimension_mismatch";
    internal const string MixedOptionalQuantityMode = "mixed_optional_quantity_mode";
    internal const string RequiredAllocationUnevaluable =
        "required_allocation_unevaluable";
    internal const string QuantityOverflow = "quantity_overflow";
}

internal static class RecipeSuitabilityRanking
{
    internal static IOrderedEnumerable<RecipeSuitabilityResult> Order(
        IEnumerable<RecipeSuitabilityResult> results) =>
        results
            .OrderByDescending(result => ClassificationPriority(result.Classification))
            .ThenBy(result => result.MissingRequiredIngredients.Count)
            .ThenBy(result => result.InsufficientRequiredIngredients.Count)
            .ThenByDescending(result => result.RequiredCoverage ?? -1m)
            .ThenByDescending(result => result.OptionalAvailabilityRatio)
            .ThenByDescending(result => result.AvailableOptionalIngredientCount)
            .ThenBy(result => result.RecipeId);

    private static int ClassificationPriority(
        RecipeSuitabilityClassification classification) => classification switch
        {
            RecipeSuitabilityClassification.Cookable => 3,
            RecipeSuitabilityClassification.InsufficientRequired => 2,
            RecipeSuitabilityClassification.MissingRequired => 1,
            RecipeSuitabilityClassification.Unevaluable => 0,
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported suitability classification.")
        };
}
