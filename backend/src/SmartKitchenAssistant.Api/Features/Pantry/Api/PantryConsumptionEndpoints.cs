using SmartKitchenAssistant.Api.Features.Pantry.Application;

namespace SmartKitchenAssistant.Api.Features.Pantry.Api;

internal static class PantryConsumptionEndpoints
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";

    internal static IEndpointRouteBuilder MapPantryConsumptionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/recipes/{id:long}/consume", ConsumeAsync)
            .WithTags("Recipes");

        return endpoints;
    }

    private static async Task<IResult> ConsumeAsync(
        long id,
        HttpRequest request,
        PantryConsumptionService service,
        CancellationToken cancellationToken)
    {
        var headerValues = request.Headers[IdempotencyKeyHeaderName];
        var key = headerValues.Count == 1 ? headerValues[0] : null;
        var result = await service.ConsumeAsync(id, key, cancellationToken);

        return result.Outcome switch
        {
            PantryConsumptionOutcome.Success => Results.Ok(ToResponse(result.Value!)),
            PantryConsumptionOutcome.InvalidIdempotencyKey => Problem(
                StatusCodes.Status400BadRequest,
                "invalid_idempotency_key",
                "A single Idempotency-Key containing 1 to 128 visible ASCII characters is required."),
            PantryConsumptionOutcome.RecipeNotFound => Problem(
                StatusCodes.Status404NotFound,
                "recipe_not_found",
                "The requested recipe was not found."),
            PantryConsumptionOutcome.RecipeNotConsumable => Problem(
                StatusCodes.Status409Conflict,
                "recipe_not_consumable",
                "The published recipe does not contain valid consumable requirements."),
            PantryConsumptionOutcome.InsufficientStock => Problem(
                StatusCodes.Status409Conflict,
                "insufficient_stock",
                "The pantry does not contain enough stock for this recipe."),
            PantryConsumptionOutcome.IdempotencyKeyReused => Problem(
                StatusCodes.Status409Conflict,
                "idempotency_key_reused",
                "The idempotency key has already been used for another recipe."),
            PantryConsumptionOutcome.PantryStockChanged => Problem(
                StatusCodes.Status409Conflict,
                "pantry_stock_changed",
                "The pantry changed while the recipe was being consumed. Retry with the same idempotency key."),
            _ => throw new InvalidOperationException(
                "Unsupported pantry consumption outcome.")
        };
    }

    private static PantryConsumptionResponse ToResponse(PantryConsumptionView view) =>
        new(
            view.ConsumptionId,
            view.RecipeId,
            "Completed",
            view.ConsumedIngredients
                .Select(item => new ConsumedIngredientResponse(
                    item.IngredientId,
                    item.NormalizedQuantity,
                    item.QuantityDimension.ToString()))
                .ToArray());

    private static IResult Problem(int statusCode, string code, string detail) =>
        Results.Problem(
            statusCode: statusCode,
            title: statusCode == StatusCodes.Status400BadRequest
                ? "The pantry consumption request is invalid."
                : statusCode == StatusCodes.Status404NotFound
                    ? "Recipe not found."
                    : "Pantry consumption conflicts with the current state.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
