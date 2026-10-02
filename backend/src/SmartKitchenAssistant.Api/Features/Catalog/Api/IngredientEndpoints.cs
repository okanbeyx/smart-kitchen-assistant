using SmartKitchenAssistant.Api.Features.Catalog.Application;

namespace SmartKitchenAssistant.Api.Features.Catalog.Api;

internal static class IngredientEndpoints
{
    internal static IEndpointRouteBuilder MapIngredientEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/ingredients").WithTags("Ingredients");

        group.MapGet("/", ListAsync).AllowAnonymous();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string? search,
        IngredientReadService service,
        CancellationToken cancellationToken)
    {
        var ingredients = await service.ListAsync(search, cancellationToken);
        return Results.Ok(ingredients.Select(ToResponse));
    }

    private static IngredientResponse ToResponse(IngredientSummaryView ingredient) =>
        new(
            ingredient.Id,
            ingredient.Name,
            ingredient.QuantityDimension.ToString());
}
