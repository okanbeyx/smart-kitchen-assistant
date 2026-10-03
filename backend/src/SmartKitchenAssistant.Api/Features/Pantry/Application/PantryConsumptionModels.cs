using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal enum PantryConsumptionOutcome
{
    Success,
    InvalidIdempotencyKey,
    RecipeNotFound,
    RecipeNotConsumable,
    InsufficientStock,
    IdempotencyKeyReused,
    PantryStockChanged
}

internal sealed record PantryConsumptionResult(
    PantryConsumptionOutcome Outcome,
    PantryConsumptionView? Value = null)
{
    internal static PantryConsumptionResult Success(PantryConsumptionView value) =>
        new(PantryConsumptionOutcome.Success, value);

    internal static PantryConsumptionResult From(PantryConsumptionOutcome outcome) =>
        new(outcome);
}

internal sealed record PantryConsumptionView(
    long ConsumptionId,
    long RecipeId,
    IReadOnlyList<ConsumedIngredientView> ConsumedIngredients);

internal sealed record ConsumedIngredientView(
    long IngredientId,
    decimal NormalizedQuantity,
    QuantityDimension QuantityDimension);
