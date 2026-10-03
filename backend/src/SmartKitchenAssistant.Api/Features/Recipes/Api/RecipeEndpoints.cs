using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;

namespace SmartKitchenAssistant.Api.Features.Recipes.Api;

internal static class RecipeEndpoints
{
    internal static IEndpointRouteBuilder MapRecipeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/recipes").WithTags("Recipes");

        group.MapGet("/", ListAsync).AllowAnonymous();
        group.MapGet("/suitability", ListSuitabilityAsync);
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

    private static async Task<IResult> ListSuitabilityAsync(
        RecipeSuitabilityService service,
        CancellationToken cancellationToken)
    {
        var results = await service.ListAsync(cancellationToken);
        return Results.Ok(results.Select(ToResponse));
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

    private static RecipeSuitabilityResponse ToResponse(
        RecipeSuitabilityResult result) =>
        new(
            result.RecipeId,
            result.Title,
            result.BaseServings,
            ClassificationName(result.Classification),
            result.IsCookable,
            result.RequiredIngredientCount,
            result.SatisfiedRequiredIngredientCount,
            result.RequiredCoverage,
            result.MissingRequiredIngredients
                .Select(ingredient => new MissingRequiredIngredientResponse(
                    ingredient.IngredientId,
                    ingredient.Name,
                    ingredient.RequiredNormalizedQuantity,
                    BaseUnitSymbol(ingredient.QuantityDimension)))
                .ToArray(),
            result.InsufficientRequiredIngredients
                .Select(ingredient => new InsufficientRequiredIngredientResponse(
                    ingredient.IngredientId,
                    ingredient.Name,
                    ingredient.RequiredNormalizedQuantity,
                    ingredient.AvailableNormalizedQuantity,
                    ingredient.ShortfallNormalizedQuantity,
                    BaseUnitSymbol(ingredient.QuantityDimension)))
                .ToArray(),
            result.OptionalIngredientCount,
            result.AvailableOptionalIngredientCount,
            result.OptionalIssues
                .Select(issue => new OptionalIngredientIssueResponse(
                    issue.IngredientId,
                    issue.Name,
                    OptionalIssueKindName(issue.Kind),
                    issue.RequiredNormalizedQuantity,
                    issue.AvailableNormalizedQuantity,
                    issue.ShortfallNormalizedQuantity,
                    BaseUnitSymbol(issue.QuantityDimension),
                    issue.ReasonCode))
                .ToArray(),
            result.UnevaluableIngredients
                .Select(ingredient => new UnevaluableIngredientResponse(
                    ingredient.IngredientId,
                    ingredient.Name,
                    ingredient.ReasonCode))
                .ToArray(),
            result.ReasonCodes);

    private static string ClassificationName(
        RecipeSuitabilityClassification classification) => classification switch
        {
            RecipeSuitabilityClassification.Cookable => "Cookable",
            RecipeSuitabilityClassification.InsufficientRequired =>
                "InsufficientRequired",
            RecipeSuitabilityClassification.MissingRequired => "MissingRequired",
            RecipeSuitabilityClassification.Unevaluable => "Unevaluable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported suitability classification.")
        };

    private static string OptionalIssueKindName(
        OptionalIngredientIssueKind kind) => kind switch
        {
            OptionalIngredientIssueKind.Missing => "Missing",
            OptionalIngredientIssueKind.Insufficient => "Insufficient",
            OptionalIngredientIssueKind.Unevaluable => "Unevaluable",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported optional ingredient issue kind.")
        };

    private static string BaseUnitSymbol(QuantityDimension dimension) =>
        dimension switch
        {
            QuantityDimension.Mass => Unit.Gram.GetSymbol(),
            QuantityDimension.Volume => Unit.Milliliter.GetSymbol(),
            QuantityDimension.Count => Unit.Each.GetSymbol(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dimension),
                dimension,
                "Unsupported quantity dimension.")
        };

    private static IResult NotFound() =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Recipe not found.",
            detail: "The requested recipe was not found.");
}
