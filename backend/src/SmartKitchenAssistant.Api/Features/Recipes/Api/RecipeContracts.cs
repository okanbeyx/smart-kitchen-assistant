namespace SmartKitchenAssistant.Api.Features.Recipes.Api;

public sealed record RecipeSummaryResponse(
    long Id,
    string Title,
    int BaseServings);

public sealed record RecipeDetailResponse(
    long Id,
    string Title,
    int BaseServings,
    IReadOnlyList<RecipeIngredientResponse> Ingredients,
    IReadOnlyList<RecipeStepResponse> Steps);

public sealed record RecipeIngredientResponse(
    long IngredientId,
    string Name,
    decimal? Quantity,
    string? Unit,
    bool IsOptional);

public sealed record RecipeStepResponse(
    string Instruction,
    int? TimerSeconds);

public sealed record RecipeSuitabilityResponse(
    long RecipeId,
    string Title,
    int BaseServings,
    string Classification,
    bool IsCookable,
    int RequiredIngredientCount,
    int SatisfiedRequiredIngredientCount,
    decimal? RequiredCoverage,
    IReadOnlyList<MissingRequiredIngredientResponse> MissingRequiredIngredients,
    IReadOnlyList<InsufficientRequiredIngredientResponse> InsufficientRequiredIngredients,
    int OptionalIngredientCount,
    int AvailableOptionalIngredientCount,
    IReadOnlyList<OptionalIngredientIssueResponse> OptionalIssues,
    IReadOnlyList<UnevaluableIngredientResponse> UnevaluableIngredients,
    IReadOnlyList<string> ReasonCodes);

public sealed record MissingRequiredIngredientResponse(
    long IngredientId,
    string Name,
    decimal RequiredQuantity,
    string Unit);

public sealed record InsufficientRequiredIngredientResponse(
    long IngredientId,
    string Name,
    decimal RequiredQuantity,
    decimal AvailableQuantity,
    decimal Shortfall,
    string Unit);

public sealed record OptionalIngredientIssueResponse(
    long IngredientId,
    string Name,
    string Kind,
    decimal? RequiredQuantity,
    decimal? AvailableQuantity,
    decimal? Shortfall,
    string Unit,
    string? ReasonCode);

public sealed record UnevaluableIngredientResponse(
    long IngredientId,
    string Name,
    string ReasonCode);
