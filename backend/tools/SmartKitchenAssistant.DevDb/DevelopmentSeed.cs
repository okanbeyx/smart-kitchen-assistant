using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.DevDb;

internal static class DevelopmentSeed
{
    internal const string Prefix = "[SKA-DEV-SEED] ";
    internal const string ManifestName = "SmartKitchenAssistant.DevelopmentSeed";
    private const string ReservedPrefix = "[SKA-DEV-SEED]";
    private const int Version = 1;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };

    // Original, presentation-only development fixtures; no external recipe content or user data.
    private static readonly IngredientDefinition[] Ingredients =
    [
        new("tomato", "Domates", QuantityDimension.Mass),
        new("egg", "Yumurta", QuantityDimension.Count),
        new("lentil", "Mercimek", QuantityDimension.Mass),
        new("milk", "Süt", QuantityDimension.Volume),
        new("oil", "Zeytinyağı", QuantityDimension.Volume),
        new("salt", "Tuz", QuantityDimension.Mass),
        new("inactive", "Pasif test malzemesi", QuantityDimension.Count, false)
    ];

    private static readonly RecipeDefinition[] Recipes =
    [
        new("tomato-eggs", "Domatesli Yumurta", 2, true,
            [new("tomato", 0.25m, Unit.Kilogram), new("egg", 2m, Unit.Each),
             new("oil", 10m, Unit.Milliliter), new("salt", 2m, Unit.Gram, true)],
            [new("Domatesleri doğrayıp zeytinyağı ile tavaya alın.", 120),
             new("Yumurtaları ekleyip pişirin; isteğe göre tuz ekleyin.", 180)]),
        new("lentil-soup", "Mercimek Çorbası", 4, true,
            [new("lentil", 0.2m, Unit.Kilogram), new("milk", 0.5m, Unit.Liter),
             new("salt", 5m, Unit.Gram)],
            [new("Yıkanmış mercimeği yeterli su ile yumuşayana kadar pişirin.", 1200),
             new("Süt ve tuzu ekleyip karıştırın; kıvamı su ile ayarlayın.", 120)]),
        new("draft", "Taslak tarif", 1, false,
            [new("salt", null, null)], [new("Bu kayıt yalnız Draft görünürlüğünü doğrulamak içindir.")])
    ];

    internal sealed record Manifest(
        int Version,
        Dictionary<string, long> Ingredients,
        Dictionary<string, long> Recipes,
        Dictionary<string, long> Lines,
        Dictionary<string, long> Steps);

    internal static async Task<bool> SeedAsync(SmartKitchenDbContext context, CancellationToken token = default)
    {
        ValidateDefinitions();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await DevelopmentDatabase.RequireMarkerAsync(context, token);
        var existing = await DevelopmentDatabase.ReadPropertyAsync(context, ManifestName, token);
        if (existing is not null)
        {
            await VerifyContentsAsync(context, ParseManifest(existing), token);
            await transaction.CommitAsync(token);
            return false;
        }

        // Serializable prefix reads protect the first seed from concurrent fixture insertion.
        // A competing writer may get a deadlock/conflict; it must rollback, never silently duplicate.
        if ((await ReservedIngredientsAsync(context, token)).Length != 0 ||
            (await ReservedRecipesAsync(context, token)).Length != 0)
            throw Drift();

        var manifest = new Manifest(Version, [], [], [], []);
        foreach (var definition in Ingredients)
        {
            var ingredient = new Ingredient(Prefix + definition.Name, definition.Dimension, definition.Active);
            context.Ingredients.Add(ingredient);
            await context.SaveChangesAsync(token);
            manifest.Ingredients.Add(definition.Key, ingredient.Id);
        }

        foreach (var definition in Recipes)
        {
            var recipe = new Recipe(Prefix + definition.Title, definition.Servings);
            context.Recipes.Add(recipe);
            await context.SaveChangesAsync(token);
            manifest.Recipes.Add(definition.Key, recipe.Id);
            for (var index = 0; index < definition.Lines.Length; index++)
            {
                var line = definition.Lines[index];
                var row = new RecipeIngredient(recipe.Id, manifest.Ingredients[line.Ingredient], index + 1,
                    line.Optional, Normalize(line), line.Unit);
                context.RecipeIngredients.Add(row);
                await context.SaveChangesAsync(token);
                manifest.Lines.Add(ChildKey(definition.Key, index), row.Id);
            }
            for (var index = 0; index < definition.Steps.Length; index++)
            {
                var step = definition.Steps[index];
                var row = new RecipeStep(recipe.Id, index + 1, step.Instruction, step.Seconds);
                context.RecipeSteps.Add(row);
                await context.SaveChangesAsync(token);
                manifest.Steps.Add(ChildKey(definition.Key, index), row.Id);
            }
        }

        // This ID set exists only for the graph created above, in this transaction.
        // There is intentionally no production publishing behavior or generic publish command.
        var newlyCreatedPublishedIds = Recipes.Where(recipe => recipe.Published)
            .Select(recipe => manifest.Recipes[recipe.Key]).ToArray();
        var updated = await context.Recipes
            .Where(recipe => newlyCreatedPublishedIds.Contains(recipe.Id) && recipe.Status == RecipeStatus.Draft)
            .ExecuteUpdateAsync(setters => setters.SetProperty(recipe => recipe.Status, RecipeStatus.Published), token);
        if (updated != newlyCreatedPublishedIds.Length) throw Drift();

        await VerifyContentsAsync(context, manifest, token); // AsNoTracking: bulk update bypasses tracked state.
        await DevelopmentDatabase.AddPropertyAsync(context, ManifestName, JsonSerializer.Serialize(manifest), token);
        await transaction.CommitAsync(token);
        return true;
    }

    internal static async Task<Manifest> VerifyAsync(SmartKitchenDbContext context, CancellationToken token = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await DevelopmentDatabase.RequireMarkerAsync(context, token);
        var json = await DevelopmentDatabase.ReadPropertyAsync(context, ManifestName, token);
        if (json is null)
            throw new DevelopmentDatabaseException("Seed manifest absent. Run Seed explicitly; existing reserved records will not be adopted.");
        var manifest = ParseManifest(json);
        await VerifyContentsAsync(context, manifest, token);
        await transaction.CommitAsync(token);
        return manifest;
    }

    private static Manifest ParseManifest(string json)
    {
        try
        {
            // System.Text.Json normally accepts duplicate JSON properties. Metadata must not.
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            RejectDuplicateProperties(document.RootElement);
            var manifest = JsonSerializer.Deserialize<Manifest>(json, JsonOptions);
            if (manifest is null || manifest.Version != Version ||
                !ValidMap(manifest.Ingredients, Ingredients.Select(value => value.Key)) ||
                !ValidMap(manifest.Recipes, Recipes.Select(value => value.Key)) ||
                !ValidMap(manifest.Lines, Recipes.SelectMany(value => value.Lines.Select((_, index) => ChildKey(value.Key, index)))) ||
                !ValidMap(manifest.Steps, Recipes.SelectMany(value => value.Steps.Select((_, index) => ChildKey(value.Key, index)))))
                throw Drift();
            return manifest;
        }
        catch (JsonException)
        {
            throw Drift();
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!keys.Add(property.Name)) throw Drift();
            RejectDuplicateProperties(property.Value);
        }
    }

    private static bool ValidMap(Dictionary<string, long>? map, IEnumerable<string> keys) =>
        map is not null && map.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(keys) &&
        map.Values.All(value => value > 0) && map.Values.Distinct().Count() == map.Count;

    private static async Task VerifyContentsAsync(SmartKitchenDbContext context, Manifest manifest, CancellationToken token)
    {
        var ingredientIds = manifest.Ingredients.Values.ToArray();
        var recipeIds = manifest.Recipes.Values.ToArray();
        var ingredients = await context.Ingredients.AsNoTracking().Where(value => ingredientIds.Contains(value.Id)).ToArrayAsync(token);
        var recipes = await context.Recipes.AsNoTracking().Where(value => recipeIds.Contains(value.Id)).ToArrayAsync(token);
        if (ingredients.Length != Ingredients.Length || recipes.Length != Recipes.Length ||
            !(await ReservedIngredientsAsync(context, token)).Select(value => value.Id).ToHashSet().SetEquals(ingredientIds) ||
            !(await ReservedRecipesAsync(context, token)).Select(value => value.Id).ToHashSet().SetEquals(recipeIds))
            throw Drift();

        foreach (var expected in Ingredients)
        {
            var actual = ingredients.Single(value => value.Id == manifest.Ingredients[expected.Key]);
            if (!string.Equals(actual.Name, Prefix + expected.Name, StringComparison.Ordinal) ||
                actual.QuantityDimension != expected.Dimension || actual.IsActive != expected.Active)
                throw Drift();
        }

        // Rows attached to a seed recipe are part of its canonical graph. Other recipes may
        // legitimately reference seed ingredients; those relationships are not seed-owned.
        var lines = await context.RecipeIngredients.AsNoTracking().Where(value => recipeIds.Contains(value.RecipeId)).ToArrayAsync(token);
        var steps = await context.RecipeSteps.AsNoTracking().Where(value => recipeIds.Contains(value.RecipeId)).ToArrayAsync(token);
        if (!lines.Select(value => value.Id).ToHashSet().SetEquals(manifest.Lines.Values) ||
            !steps.Select(value => value.Id).ToHashSet().SetEquals(manifest.Steps.Values))
            throw Drift();

        foreach (var expected in Recipes)
        {
            var actual = recipes.Single(value => value.Id == manifest.Recipes[expected.Key]);
            if (!string.Equals(actual.Title, Prefix + expected.Title, StringComparison.Ordinal) ||
                actual.BaseServings != expected.Servings ||
                actual.Status != (expected.Published ? RecipeStatus.Published : RecipeStatus.Draft))
                throw Drift();
            for (var index = 0; index < expected.Lines.Length; index++)
            {
                var definition = expected.Lines[index];
                var line = lines.Single(value => value.Id == manifest.Lines[ChildKey(expected.Key, index)]);
                if (line.RecipeId != actual.Id || line.Sequence != index + 1 ||
                    line.IngredientId != manifest.Ingredients[definition.Ingredient] ||
                    line.IsOptional != definition.Optional || line.NormalizedQuantity != Normalize(definition) ||
                    line.DisplayUnit != definition.Unit)
                    throw Drift();
            }
            for (var index = 0; index < expected.Steps.Length; index++)
            {
                var definition = expected.Steps[index];
                var step = steps.Single(value => value.Id == manifest.Steps[ChildKey(expected.Key, index)]);
                if (step.RecipeId != actual.Id || step.Sequence != index + 1 ||
                    !string.Equals(step.Instruction, definition.Instruction, StringComparison.Ordinal) ||
                    step.TimerSeconds != definition.Seconds)
                    throw Drift();
            }
        }
    }

    private static Task<Ingredient[]> ReservedIngredientsAsync(SmartKitchenDbContext context, CancellationToken token) =>
        context.Ingredients.AsNoTracking()
            .Where(value => EF.Functions.Collate(value.Name, "Latin1_General_100_CI_AS").StartsWith(ReservedPrefix))
            .ToArrayAsync(token);

    private static Task<Recipe[]> ReservedRecipesAsync(SmartKitchenDbContext context, CancellationToken token) =>
        context.Recipes.AsNoTracking()
            .Where(value => EF.Functions.Collate(value.Title, "Latin1_General_100_CI_AS").StartsWith(ReservedPrefix))
            .ToArrayAsync(token);

    private static decimal? Normalize(LineDefinition line) => line.Quantity.HasValue
        ? line.Unit!.Value.NormalizeToBase(line.Quantity.Value)
        : null;

    private static string ChildKey(string recipe, int index) => $"{recipe}/{index + 1}";

    private static void ValidateDefinitions()
    {
        foreach (var ingredient in Ingredients)
        {
            _ = new Ingredient(Prefix + ingredient.Name, ingredient.Dimension, ingredient.Active);
            if ((Prefix + ingredient.Name).Length > 200) throw Drift();
        }
        foreach (var recipe in Recipes)
        {
            _ = new Recipe(Prefix + recipe.Title, recipe.Servings);
            if ((Prefix + recipe.Title).Length > 200 || (recipe.Published && recipe.Steps.Length == 0)) throw Drift();
            foreach (var line in recipe.Lines)
            {
                var ingredient = Ingredients.Single(value => value.Key == line.Ingredient);
                if (line.Quantity.HasValue != line.Unit.HasValue ||
                    (recipe.Published && !line.Optional && !line.Quantity.HasValue) ||
                    (line.Unit.HasValue && line.Unit.Value.GetDimension() != ingredient.Dimension))
                    throw Drift();
                var normalized = Normalize(line);
                if (normalized.HasValue &&
                    (normalized > 999_999_999_999.999999m || decimal.Round(normalized.Value, 6) != normalized.Value))
                    throw Drift();
                _ = new RecipeIngredient(1, 1, 1, line.Optional, normalized, line.Unit);
            }
            foreach (var step in recipe.Steps)
            {
                _ = new RecipeStep(1, 1, step.Instruction, step.Seconds);
                if (step.Instruction.Length > 4000) throw Drift();
            }
        }
    }

    private static DevelopmentDatabaseException Drift() => new(
        "Canonical seed collision, drift or unsupported manifest detected. No repair/adoption was performed; unrelated data is preserved. Review the fixture data before considering an explicit full Reset.");

    private sealed record IngredientDefinition(string Key, string Name, QuantityDimension Dimension, bool Active = true);
    private sealed record RecipeDefinition(string Key, string Title, int Servings, bool Published, LineDefinition[] Lines, StepDefinition[] Steps);
    private sealed record LineDefinition(string Ingredient, decimal? Quantity, Unit? Unit, bool Optional = false);
    private sealed record StepDefinition(string Instruction, int? Seconds = null);
}
