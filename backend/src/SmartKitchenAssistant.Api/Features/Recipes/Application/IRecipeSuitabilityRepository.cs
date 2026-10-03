namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal interface IRecipeSuitabilityRepository
{
    Task<RecipeSuitabilityData> LoadAsync(
        string userId,
        CancellationToken cancellationToken);
}
