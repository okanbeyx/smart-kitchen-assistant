namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal interface IPantryConsumptionRepository
{
    Task<PantryConsumptionResult> ConsumeAsync(
        string userId,
        long recipeId,
        string idempotencyKey,
        CancellationToken cancellationToken);
}
