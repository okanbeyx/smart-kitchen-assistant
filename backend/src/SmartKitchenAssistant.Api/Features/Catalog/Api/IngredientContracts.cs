namespace SmartKitchenAssistant.Api.Features.Catalog.Api;

public sealed record IngredientResponse(
    long Id,
    string Name,
    string QuantityDimension);
