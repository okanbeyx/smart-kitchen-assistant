using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Application;

namespace SmartKitchenAssistant.Api.Features.Pantry.Api;

internal static class PantryEndpoints
{
    internal static IEndpointRouteBuilder MapPantryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/pantry").WithTags("Pantry");

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:long}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:long}", UpdateAsync);
        group.MapDelete("/{id:long}", DeleteAsync);

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        PantryService service,
        CancellationToken cancellationToken)
    {
        var items = await service.ListAsync(cancellationToken);
        return Results.Ok(items.Select(ToResponse));
    }

    private static async Task<IResult> GetAsync(
        long id,
        PantryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> CreateAsync(
        CreatePantryItemRequest request,
        PantryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(
            request.IngredientId,
            request.Quantity,
            request.Unit,
            cancellationToken);

        if (result.Outcome != PantryOutcome.Success)
        {
            return ToHttpResult(result);
        }

        var response = ToResponse(result.Value!);
        return Results.Created($"/api/pantry/{response.Id}", response);
    }

    private static async Task<IResult> UpdateAsync(
        long id,
        UpdatePantryItemRequest request,
        PantryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(
            id,
            request.Quantity,
            request.Unit,
            cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> DeleteAsync(
        long id,
        PantryService service,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteAsync(id, cancellationToken);
        return result.Outcome == PantryOutcome.Success
            ? Results.NoContent()
            : NotFound();
    }

    private static IResult ToHttpResult(PantryResult<PantryItemView> result) =>
        result.Outcome switch
        {
            PantryOutcome.Success => Results.Ok(ToResponse(result.Value!)),
            PantryOutcome.NotFound => NotFound(),
            PantryOutcome.ValidationFailed => Results.ValidationProblem(
                result.Errors!.ToDictionary(pair => pair.Key, pair => pair.Value),
                title: "One or more validation errors occurred."),
            PantryOutcome.Duplicate => Conflict(
                "pantry_item_duplicate",
                "A pantry item already exists for this ingredient."),
            PantryOutcome.IngredientInactive => Conflict(
                "ingredient_inactive",
                "The ingredient is inactive and cannot be added to the pantry."),
            _ => throw new InvalidOperationException("Unsupported pantry outcome.")
        };

    private static PantryItemResponse ToResponse(PantryItemView item) =>
        new(
            item.Id,
            item.IngredientId,
            item.IngredientName,
            item.DisplayUnit.ConvertFromBase(item.NormalizedQuantity),
            item.DisplayUnit.GetSymbol());

    private static IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Pantry item not found.",
            detail: "The requested pantry item was not found.");

    private static IResult Conflict(string code, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Pantry request conflicts with the current state.",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
