using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Features.Recipes.Api;

internal static class RecipeEndpoints
{
    internal static IEndpointRouteBuilder MapRecipeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes").WithTags("Recipes");

        group.MapGet("/", ListAsync).AllowAnonymous();
        group.MapGet("/{id:long}", GetAsync).AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        RecipeReadService service,
        CancellationToken cancellationToken)
    {
        var recipes = await service.ListAsync(cancellationToken);
        return Results.Ok(recipes.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        long id,
        RecipeReadService service,
        CancellationToken cancellationToken)
    {
        var recipe = await service.GetAsync(id, cancellationToken);
        return recipe is null
            ? NotFound()
            : Results.Ok(ToResponse(recipe));
    }

    private static RecipeSummaryResponse ToResponse(RecipeSummaryView recipe) =>
        new(recipe.Id, recipe.Title, recipe.BaseServings);

    private static RecipeDetailResponse ToResponse(RecipeDetailView recipe) =>
        new(
            recipe.Id,
            recipe.Title,
            recipe.BaseServings,
            recipe.Ingredients
                .Select(ingredient => new RecipeIngredientResponse(
                    ingredient.IngredientId,
                    ingredient.Name,
                    ingredient.Quantity,
                    ingredient.Unit,
                    ingredient.IsOptional))
                .ToArray(),
            recipe.Steps
                .Select(step => new RecipeStepResponse(
                    step.Instruction,
                    step.TimerSeconds))
                .ToArray());

    private static IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Recipe not found.",
            detail: "The requested recipe was not found.");
}
