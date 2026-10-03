using SmartKitchenAssistant.Api.Application.Identity;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal sealed class PantryConsumptionService(
    ICurrentUser currentUser,
    IPantryConsumptionRepository repository)
{
    internal Task<PantryConsumptionResult> ConsumeAsync(
        long recipeId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!StockConsumption.IsValidIdempotencyKey(idempotencyKey))
        {
            return Task.FromResult(PantryConsumptionResult.From(
                PantryConsumptionOutcome.InvalidIdempotencyKey));
        }

        if (recipeId <= 0)
        {
            return Task.FromResult(PantryConsumptionResult.From(
                PantryConsumptionOutcome.RecipeNotFound));
        }

        return repository.ConsumeAsync(
            currentUser.UserId,
            recipeId,
            idempotencyKey!,
            cancellationToken);
    }
}
