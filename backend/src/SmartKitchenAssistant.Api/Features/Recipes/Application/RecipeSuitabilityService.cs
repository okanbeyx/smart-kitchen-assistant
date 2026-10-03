using SmartKitchenAssistant.Api.Application.Identity;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal sealed class RecipeSuitabilityService(
    ICurrentUser currentUser,
    IRecipeSuitabilityRepository repository,
    RecipeSuitabilityCalculator calculator)
{
    public async Task<IReadOnlyList<RecipeSuitabilityResult>> ListAsync(
        CancellationToken cancellationToken)
    {
        var data = await repository.LoadAsync(
            currentUser.UserId,
            cancellationToken);
        var results = calculator.Calculate(data);
        return RecipeSuitabilityRanking.Order(results).ToArray();
    }
}
