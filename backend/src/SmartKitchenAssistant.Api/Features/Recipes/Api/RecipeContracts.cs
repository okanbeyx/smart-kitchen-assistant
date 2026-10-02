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
