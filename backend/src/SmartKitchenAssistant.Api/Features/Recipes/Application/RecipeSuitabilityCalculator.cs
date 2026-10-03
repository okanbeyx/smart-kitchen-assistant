using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Application;

internal sealed class RecipeSuitabilityCalculator
{
    internal IReadOnlyList<RecipeSuitabilityResult> Calculate(
        RecipeSuitabilityData data)
    {
        var pantryByIngredient = data.PantryItems.ToDictionary(
            item => item.IngredientId);
        var ingredientsByRecipe = data.Ingredients
            .GroupBy(ingredient => ingredient.RecipeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(ingredient => ingredient.Sequence).ToArray());

        return data.Recipes
            .Select(recipe => CalculateRecipe(
                recipe,
                ingredientsByRecipe.GetValueOrDefault(recipe.Id) ?? [],
                pantryByIngredient))
            .ToArray();
    }

    private static RecipeSuitabilityResult CalculateRecipe(
        RecipeSuitabilityRecipeData recipe,
        IReadOnlyList<RecipeSuitabilityIngredientData> ingredients,
        IReadOnlyDictionary<long, RecipeSuitabilityPantryItemData> pantry)
    {
        var requiredGroups = CreateGroups(ingredients, isOptional: false);
        var optionalGroups = CreateGroups(ingredients, isOptional: true);
        var missingRequired = new List<MissingRequiredIngredient>();
        var insufficientRequired = new List<InsufficientRequiredIngredient>();
        var unevaluableRequired = new List<UnevaluableIngredient>();
        var reasonCodes = new List<string>();
        var requiredCoverageParts = new List<decimal>();
        var requiredAllocations = new Dictionary<long, RequiredAllocation>();
        var satisfiedRequiredCount = 0;

        foreach (var group in requiredGroups)
        {
            var first = group.Items[0];

            if (!TryGetRequiredQuantifiedRequirement(
                    group.Items,
                    out var requiredQuantity,
                    out var reasonCode))
            {
                AddUnevaluable(
                    unevaluableRequired,
                    reasonCodes,
                    first,
                    reasonCode!);
                requiredAllocations[first.IngredientId] =
                    RequiredAllocation.Unevaluable();
                continue;
            }

            requiredAllocations[first.IngredientId] =
                RequiredAllocation.Evaluable(requiredQuantity);

            if (!pantry.TryGetValue(first.IngredientId, out var pantryItem))
            {
                missingRequired.Add(new MissingRequiredIngredient(
                    first.IngredientId,
                    first.Name,
                    requiredQuantity,
                    first.QuantityDimension));
                requiredCoverageParts.Add(0m);
                continue;
            }

            if (!TryValidatePantry(
                    pantryItem,
                    first.QuantityDimension,
                    out reasonCode))
            {
                AddUnevaluable(
                    unevaluableRequired,
                    reasonCodes,
                    first,
                    reasonCode!);
                requiredAllocations[first.IngredientId] =
                    RequiredAllocation.Unevaluable();
                continue;
            }

            if (pantryItem.NormalizedQuantity < requiredQuantity)
            {
                insufficientRequired.Add(new InsufficientRequiredIngredient(
                    first.IngredientId,
                    first.Name,
                    requiredQuantity,
                    pantryItem.NormalizedQuantity,
                    requiredQuantity - pantryItem.NormalizedQuantity,
                    first.QuantityDimension));
                requiredCoverageParts.Add(
                    pantryItem.NormalizedQuantity / requiredQuantity);
                continue;
            }

            satisfiedRequiredCount++;
            requiredCoverageParts.Add(1m);
        }

        if (requiredGroups.Count == 0)
        {
            reasonCodes.Add(SuitabilityReasonCodes.NoRequiredIngredients);
        }

        var optionalIssues = new List<OptionalIngredientIssue>();
        var availableOptionalCount = 0;

        foreach (var group in optionalGroups)
        {
            var first = group.Items[0];
            var quantityMode = GetOptionalQuantityMode(group.Items);

            if (quantityMode == OptionalQuantityMode.Mixed)
            {
                optionalIssues.Add(UnevaluableOptional(
                    first,
                    SuitabilityReasonCodes.MixedOptionalQuantityMode));
                continue;
            }

            decimal? requiredQuantity = null;

            if (quantityMode == OptionalQuantityMode.Quantified)
            {
                if (!TryGetOptionalQuantifiedRequirement(
                        group.Items,
                        out var quantity,
                        out var reasonCode))
                {
                    optionalIssues.Add(UnevaluableOptional(first, reasonCode!));
                    continue;
                }

                requiredQuantity = quantity;
            }
            else if (!TryValidatePresenceOnlyRequirement(
                         group.Items,
                         out var presenceReasonCode))
            {
                optionalIssues.Add(UnevaluableOptional(first, presenceReasonCode!));
                continue;
            }

            if (!pantry.TryGetValue(first.IngredientId, out var pantryItem))
            {
                optionalIssues.Add(new OptionalIngredientIssue(
                    first.IngredientId,
                    first.Name,
                    OptionalIngredientIssueKind.Missing,
                    requiredQuantity,
                    null,
                    null,
                    first.QuantityDimension,
                    null));
                continue;
            }

            if (!TryValidatePantry(
                    pantryItem,
                    first.QuantityDimension,
                    out var pantryReasonCode))
            {
                optionalIssues.Add(UnevaluableOptional(first, pantryReasonCode!));
                continue;
            }

            var availableQuantity = pantryItem.NormalizedQuantity;

            if (requiredAllocations.TryGetValue(
                    first.IngredientId,
                    out var requiredAllocation))
            {
                if (!requiredAllocation.IsEvaluable)
                {
                    optionalIssues.Add(UnevaluableOptional(
                        first,
                        SuitabilityReasonCodes.RequiredAllocationUnevaluable));
                    continue;
                }

                availableQuantity = Math.Max(
                    availableQuantity - requiredAllocation.RequiredQuantity,
                    0m);
            }

            if (!requiredQuantity.HasValue)
            {
                if (availableQuantity > 0m)
                {
                    availableOptionalCount++;
                }
                else
                {
                    optionalIssues.Add(new OptionalIngredientIssue(
                        first.IngredientId,
                        first.Name,
                        OptionalIngredientIssueKind.Missing,
                        null,
                        0m,
                        null,
                        first.QuantityDimension,
                        null));
                }

                continue;
            }

            if (availableQuantity >= requiredQuantity.Value)
            {
                availableOptionalCount++;
                continue;
            }

            optionalIssues.Add(new OptionalIngredientIssue(
                first.IngredientId,
                first.Name,
                OptionalIngredientIssueKind.Insufficient,
                requiredQuantity,
                availableQuantity,
                requiredQuantity.Value - availableQuantity,
                first.QuantityDimension,
                null));
        }

        var classification = Classify(
            requiredGroups.Count,
            unevaluableRequired.Count,
            missingRequired.Count,
            insufficientRequired.Count);
        decimal? requiredCoverage = classification == RecipeSuitabilityClassification.Unevaluable
            ? null
            : requiredCoverageParts.Sum() / requiredGroups.Count;
        var optionalAvailabilityRatio = optionalGroups.Count == 0
            ? 0m
            : (decimal)availableOptionalCount / optionalGroups.Count;

        return new RecipeSuitabilityResult(
            recipe.Id,
            recipe.Title,
            recipe.BaseServings,
            classification,
            classification == RecipeSuitabilityClassification.Cookable,
            requiredGroups.Count,
            satisfiedRequiredCount,
            requiredCoverage,
            missingRequired,
            insufficientRequired,
            optionalGroups.Count,
            availableOptionalCount,
            optionalAvailabilityRatio,
            optionalIssues,
            unevaluableRequired,
            reasonCodes.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<RequirementGroup> CreateGroups(
        IReadOnlyList<RecipeSuitabilityIngredientData> ingredients,
        bool isOptional) =>
        ingredients
            .Where(ingredient => ingredient.IsOptional == isOptional)
            .GroupBy(ingredient => ingredient.IngredientId)
            .Select(group => new RequirementGroup(
                group.OrderBy(ingredient => ingredient.Sequence).ToArray()))
            .OrderBy(group => group.Items[0].Sequence)
            .ToArray();

    private static bool TryGetRequiredQuantifiedRequirement(
        IReadOnlyList<RecipeSuitabilityIngredientData> items,
        out decimal totalQuantity,
        out string? reasonCode)
    {
        var sources = items
            .Select(item => new RequiredIngredientSource(
                item.IngredientId,
                item.QuantityDimension,
                item.NormalizedQuantity,
                item.DisplayUnit))
            .ToArray();

        if (RequiredIngredientAggregator.TryAggregate(
                sources,
                out var requirement,
                out var error))
        {
            totalQuantity = requirement.NormalizedQuantity;
            reasonCode = null;
            return true;
        }

        totalQuantity = 0m;
        reasonCode = error switch
        {
            RequiredIngredientValidationError.MissingQuantity =>
                SuitabilityReasonCodes.MissingRequiredQuantity,
            RequiredIngredientValidationError.InvalidQuantity =>
                SuitabilityReasonCodes.InvalidQuantity,
            RequiredIngredientValidationError.QuantityDimensionMismatch =>
                SuitabilityReasonCodes.QuantityDimensionMismatch,
            RequiredIngredientValidationError.QuantityOverflow =>
                SuitabilityReasonCodes.QuantityOverflow,
            _ => throw new InvalidOperationException(
                "Required ingredient validation did not provide an error.")
        };
        return false;
    }

    private static bool TryGetOptionalQuantifiedRequirement(
        IReadOnlyList<RecipeSuitabilityIngredientData> items,
        out decimal totalQuantity,
        out string? reasonCode)
    {
        totalQuantity = 0m;
        reasonCode = null;
        var dimension = items[0].QuantityDimension;

        foreach (var item in items)
        {
            if (!item.NormalizedQuantity.HasValue || !item.DisplayUnit.HasValue)
            {
                reasonCode = SuitabilityReasonCodes.MissingRequiredQuantity;
                return false;
            }

            if (item.NormalizedQuantity.Value <= 0m ||
                !Enum.IsDefined(item.QuantityDimension) ||
                !Enum.IsDefined(item.DisplayUnit.Value))
            {
                reasonCode = SuitabilityReasonCodes.InvalidQuantity;
                return false;
            }

            if (item.QuantityDimension != dimension ||
                item.DisplayUnit.Value.GetDimension() != item.QuantityDimension)
            {
                reasonCode = SuitabilityReasonCodes.QuantityDimensionMismatch;
                return false;
            }

            try
            {
                totalQuantity = checked(totalQuantity + item.NormalizedQuantity.Value);
            }
            catch (OverflowException)
            {
                reasonCode = SuitabilityReasonCodes.QuantityOverflow;
                return false;
            }
        }

        return true;
    }

    private static bool TryValidatePresenceOnlyRequirement(
        IReadOnlyList<RecipeSuitabilityIngredientData> items,
        out string? reasonCode)
    {
        reasonCode = null;
        var dimension = items[0].QuantityDimension;

        if (!Enum.IsDefined(dimension) ||
            items.Any(item =>
                item.QuantityDimension != dimension ||
                item.NormalizedQuantity.HasValue ||
                item.DisplayUnit.HasValue))
        {
            reasonCode = SuitabilityReasonCodes.QuantityDimensionMismatch;
            return false;
        }

        return true;
    }

    private static bool TryValidatePantry(
        RecipeSuitabilityPantryItemData pantryItem,
        QuantityDimension expectedDimension,
        out string? reasonCode)
    {
        reasonCode = null;

        if (pantryItem.NormalizedQuantity <= 0m ||
            !Enum.IsDefined(pantryItem.DisplayUnit) ||
            !Enum.IsDefined(pantryItem.QuantityDimension))
        {
            reasonCode = SuitabilityReasonCodes.InvalidQuantity;
            return false;
        }

        if (pantryItem.QuantityDimension != expectedDimension ||
            pantryItem.DisplayUnit.GetDimension() != expectedDimension)
        {
            reasonCode = SuitabilityReasonCodes.PantryQuantityDimensionMismatch;
            return false;
        }

        return true;
    }

    private static OptionalQuantityMode GetOptionalQuantityMode(
        IReadOnlyList<RecipeSuitabilityIngredientData> items)
    {
        var quantifiedCount = items.Count(item =>
            item.NormalizedQuantity.HasValue && item.DisplayUnit.HasValue);
        var presenceOnlyCount = items.Count(item =>
            !item.NormalizedQuantity.HasValue && !item.DisplayUnit.HasValue);

        if (quantifiedCount == items.Count)
        {
            return OptionalQuantityMode.Quantified;
        }

        if (presenceOnlyCount == items.Count)
        {
            return OptionalQuantityMode.PresenceOnly;
        }

        return OptionalQuantityMode.Mixed;
    }

    private static void AddUnevaluable(
        ICollection<UnevaluableIngredient> unevaluableIngredients,
        ICollection<string> reasonCodes,
        RecipeSuitabilityIngredientData ingredient,
        string reasonCode)
    {
        unevaluableIngredients.Add(new UnevaluableIngredient(
            ingredient.IngredientId,
            ingredient.Name,
            reasonCode));
        reasonCodes.Add(reasonCode);
    }

    private static OptionalIngredientIssue UnevaluableOptional(
        RecipeSuitabilityIngredientData ingredient,
        string reasonCode) =>
        new(
            ingredient.IngredientId,
            ingredient.Name,
            OptionalIngredientIssueKind.Unevaluable,
            null,
            null,
            null,
            ingredient.QuantityDimension,
            reasonCode);

    private static RecipeSuitabilityClassification Classify(
        int requiredCount,
        int unevaluableCount,
        int missingCount,
        int insufficientCount)
    {
        if (requiredCount == 0 || unevaluableCount > 0)
        {
            return RecipeSuitabilityClassification.Unevaluable;
        }

        if (missingCount > 0)
        {
            return RecipeSuitabilityClassification.MissingRequired;
        }

        return insufficientCount > 0
            ? RecipeSuitabilityClassification.InsufficientRequired
            : RecipeSuitabilityClassification.Cookable;
    }

    private sealed record RequirementGroup(
        IReadOnlyList<RecipeSuitabilityIngredientData> Items);

    private sealed record RequiredAllocation(
        bool IsEvaluable,
        decimal RequiredQuantity)
    {
        internal static RequiredAllocation Evaluable(decimal requiredQuantity) =>
            new(true, requiredQuantity);

        internal static RequiredAllocation Unevaluable() =>
            new(false, 0m);
    }

    private enum OptionalQuantityMode
    {
        Quantified,
        PresenceOnly,
        Mixed
    }
}
