namespace SmartKitchenAssistant.Api.Features.Pantry.Api;

public sealed record PantryConsumptionResponse(
    long ConsumptionId,
    long RecipeId,
    string Status,
    IReadOnlyList<ConsumedIngredientResponse> ConsumedIngredients);

public sealed record ConsumedIngredientResponse(
    long IngredientId,
    decimal Quantity,
    string QuantityDimension);
