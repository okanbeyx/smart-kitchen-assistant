using SmartKitchenAssistant.Api.Application.Identity;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Application;

internal sealed class PantryService(
    ICurrentUser currentUser,
    IPantryRepository repository)
{
    public Task<IReadOnlyList<PantryItemView>> ListAsync(
        CancellationToken cancellationToken) =>
        repository.ListAsync(currentUser.UserId, cancellationToken);

    public async Task<PantryResult<PantryItemView>> GetAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return PantryResult<PantryItemView>.NotFound();
        }

        var item = await repository.GetAsync(
            id,
            currentUser.UserId,
            cancellationToken);

        return item is null
            ? PantryResult<PantryItemView>.NotFound()
            : PantryResult<PantryItemView>.Success(item);
    }

    public async Task<PantryResult<PantryItemView>> CreateAsync(
        long ingredientId,
        decimal quantity,
        string? unitValue,
        CancellationToken cancellationToken)
    {
        var input = ValidateInput(ingredientId, quantity, unitValue);

        if (!input.IsValid)
        {
            return PantryResult<PantryItemView>.Validation(input.Errors);
        }

        var ingredient = await repository.GetIngredientAsync(
            ingredientId,
            cancellationToken);

        if (ingredient is null)
        {
            return PantryResult<PantryItemView>.Validation(
                new Dictionary<string, string[]>
                {
                    ["ingredientId"] = ["The specified ingredient does not exist."]
                });
        }

        if (!ingredient.IsActive)
        {
            return PantryResult<PantryItemView>.IngredientInactive();
        }

        if (input.Unit.GetDimension() != ingredient.QuantityDimension)
        {
            return DimensionMismatch<PantryItemView>();
        }

        var userId = currentUser.UserId;

        if (await repository.ExistsAsync(userId, ingredientId, cancellationToken))
        {
            return PantryResult<PantryItemView>.Duplicate();
        }

        var item = new UserPantryItem(
            userId,
            ingredientId,
            input.NormalizedQuantity,
            input.Unit);
        repository.Add(item);

        if (await repository.SaveChangesAsync(cancellationToken) ==
            PantrySaveResult.DuplicateUserIngredient)
        {
            return PantryResult<PantryItemView>.Duplicate();
        }

        return PantryResult<PantryItemView>.Success(
            ToView(item, ingredient.Name));
    }

    public async Task<PantryResult<PantryItemView>> UpdateAsync(
        long id,
        decimal quantity,
        string? unitValue,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return PantryResult<PantryItemView>.NotFound();
        }

        var item = await repository.GetTrackedAsync(
            id,
            currentUser.UserId,
            cancellationToken);

        if (item is null)
        {
            return PantryResult<PantryItemView>.NotFound();
        }

        var input = ValidateQuantity(quantity, unitValue);

        if (!input.IsValid)
        {
            return PantryResult<PantryItemView>.Validation(input.Errors);
        }

        var ingredient = await repository.GetIngredientAsync(
            item.IngredientId,
            cancellationToken) ?? throw new InvalidOperationException(
                "A pantry item references a missing ingredient.");

        if (input.Unit.GetDimension() != ingredient.QuantityDimension)
        {
            return DimensionMismatch<PantryItemView>();
        }

        item.UpdateQuantity(input.NormalizedQuantity, input.Unit);
        await repository.SaveChangesAsync(cancellationToken);

        return PantryResult<PantryItemView>.Success(
            ToView(item, ingredient.Name));
    }

    public async Task<PantryResult<object?>> DeleteAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return PantryResult<object?>.NotFound();
        }

        var item = await repository.GetTrackedAsync(
            id,
            currentUser.UserId,
            cancellationToken);

        if (item is null)
        {
            return PantryResult<object?>.NotFound();
        }

        repository.Remove(item);
        await repository.SaveChangesAsync(cancellationToken);

        return PantryResult<object?>.Success(null);
    }

    private static PantryInputValidation ValidateInput(
        long ingredientId,
        decimal quantity,
        string? unitValue)
    {
        if (ingredientId <= 0)
        {
            return PantryInputValidation.Invalid(
                "ingredientId",
                "Ingredient identifier must be greater than zero.");
        }

        return ValidateQuantity(quantity, unitValue);
    }

    private static PantryInputValidation ValidateQuantity(
        decimal quantity,
        string? unitValue)
    {
        if (!PantryQuantityValidator.TryParseUnit(unitValue, out var unit))
        {
            return PantryInputValidation.Invalid(
                "unit",
                "Unit must be one of: g, kg, ml, l, adet.");
        }

        if (!PantryQuantityValidator.TryNormalize(
                quantity,
                unit,
                out var normalizedQuantity,
                out var error))
        {
            return PantryInputValidation.Invalid("quantity", error!);
        }

        return PantryInputValidation.Valid(unit, normalizedQuantity);
    }

    private static PantryResult<T> DimensionMismatch<T>() =>
        PantryResult<T>.Validation(
            new Dictionary<string, string[]>
            {
                ["unit"] = ["Unit dimension does not match the ingredient dimension."]
            });

    private static PantryItemView ToView(UserPantryItem item, string ingredientName) =>
        new(
            item.Id,
            item.IngredientId,
            ingredientName,
            item.NormalizedQuantity,
            item.DisplayUnit);
}

internal sealed record PantryInputValidation(
    bool IsValid,
    Unit Unit,
    decimal NormalizedQuantity,
    IReadOnlyDictionary<string, string[]> Errors)
{
    internal static PantryInputValidation Valid(Unit unit, decimal normalizedQuantity) =>
        new(true, unit, normalizedQuantity, new Dictionary<string, string[]>());

    internal static PantryInputValidation Invalid(string field, string error) =>
        new(
            false,
            default,
            default,
            new Dictionary<string, string[]> { [field] = [error] });
}

internal enum PantryOutcome
{
    Success,
    NotFound,
    ValidationFailed,
    Duplicate,
    IngredientInactive
}

internal sealed record PantryResult<T>(
    PantryOutcome Outcome,
    T? Value,
    IReadOnlyDictionary<string, string[]>? Errors)
{
    internal static PantryResult<T> Success(T? value) =>
        new(PantryOutcome.Success, value, null);

    internal static PantryResult<T> NotFound() =>
        new(PantryOutcome.NotFound, default, null);

    internal static PantryResult<T> Validation(
        IReadOnlyDictionary<string, string[]> errors) =>
        new(PantryOutcome.ValidationFailed, default, errors);

    internal static PantryResult<T> Duplicate() =>
        new(PantryOutcome.Duplicate, default, null);

    internal static PantryResult<T> IngredientInactive() =>
        new(PantryOutcome.IngredientInactive, default, null);
}
