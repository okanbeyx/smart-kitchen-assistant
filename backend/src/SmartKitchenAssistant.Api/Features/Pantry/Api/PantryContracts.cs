using System.Text.Json.Serialization;

namespace SmartKitchenAssistant.Api.Features.Pantry.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePantryItemRequest(
    long IngredientId,
    decimal Quantity,
    string? Unit);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdatePantryItemRequest(
    decimal Quantity,
    string? Unit);

public sealed record PantryItemResponse(
    long Id,
    long IngredientId,
    string IngredientName,
    decimal Quantity,
    string Unit);
