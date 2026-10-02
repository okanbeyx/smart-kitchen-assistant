using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Api;

[Collection(SqlServerContainerCollection.Name)]
public sealed class RecipeEndpointTests(SqlServerContainerFixture fixture)
    : IAsyncLifetime
{
    private readonly string _connectionString = fixture.CreateDatabaseConnectionString();
    private readonly CommandCaptureInterceptor _commandInterceptor = new();
    private TestWebApplicationFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();

        _factory = new TestWebApplicationFactory(
            _connectionString,
            _commandInterceptor);
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        try
        {
            _client?.Dispose();
            _factory?.Dispose();
        }
        finally
        {
            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task ListReturnsOnlyPublishedRecipesInIdOrderWithExactContract()
    {
        var first = await AddRecipeAsync("First published", 2, published: true);
        await AddRecipeAsync("Hidden draft", 3, published: false);
        var second = await AddRecipeAsync("Second published", 4, published: true);
        _commandInterceptor.Clear();

        using var response = await Client.GetAsync("/api/recipes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var recipes = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, recipes.Length);
        Assert.Equal(first.Id, recipes[0].GetProperty("id").GetInt64());
        Assert.Equal("First published", recipes[0].GetProperty("title").GetString());
        Assert.Equal(2, recipes[0].GetProperty("baseServings").GetInt32());
        Assert.Equal(second.Id, recipes[1].GetProperty("id").GetInt64());
        Assert.Equal("Second published", recipes[1].GetProperty("title").GetString());
        Assert.Equal(4, recipes[1].GetProperty("baseServings").GetInt32());
        AssertJsonProperties(recipes[0], "id", "title", "baseServings");
        AssertJsonProperties(recipes[1], "id", "title", "baseServings");
        Assert.Single(_commandInterceptor.SelectCommands);
    }

    [Fact]
    public async Task EmptyPublishedCatalogReturnsEmptyArray()
    {
        await AddRecipeAsync("Draft", 2, published: false);

        using var response = await Client.GetAsync("/api/recipes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task DetailUsesCatalogNamesExactConversionsAndSequenceOrdering()
    {
        var recipe = await AddRecipeAsync("Soup", 4, published: true);
        var flour = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        var milk = await AddIngredientAsync("Milk", QuantityDimension.Volume, isActive: false);
        var salt = await AddIngredientAsync("Salt", QuantityDimension.Mass);
        await AddRecipeIngredientAsync(recipe.Id, flour.Id, 2, false, 1000m, Unit.Gram);
        await AddRecipeIngredientAsync(recipe.Id, milk.Id, 1, false, 500m, Unit.Liter);
        await AddRecipeIngredientAsync(recipe.Id, salt.Id, 3, true, null, null);
        await AddRecipeStepAsync(recipe.Id, 2, "Serve.", null);
        await AddRecipeStepAsync(recipe.Id, 1, "Cook.", 600);
        _commandInterceptor.Clear();

        using var response = await Client.GetAsync($"/api/recipes/{recipe.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        AssertJsonProperties(root, "id", "title", "baseServings", "ingredients", "steps");
        Assert.Equal(recipe.Id, root.GetProperty("id").GetInt64());
        Assert.Equal("Soup", root.GetProperty("title").GetString());
        Assert.Equal(4, root.GetProperty("baseServings").GetInt32());

        var ingredients = root.GetProperty("ingredients").EnumerateArray().ToArray();
        Assert.Equal(3, ingredients.Length);
        Assert.Equal(milk.Id, ingredients[0].GetProperty("ingredientId").GetInt64());
        Assert.Equal("Milk", ingredients[0].GetProperty("name").GetString());
        Assert.Equal(0.5m, ingredients[0].GetProperty("quantity").GetDecimal());
        Assert.Equal("l", ingredients[0].GetProperty("unit").GetString());
        Assert.False(ingredients[0].GetProperty("isOptional").GetBoolean());
        Assert.Equal(flour.Id, ingredients[1].GetProperty("ingredientId").GetInt64());
        Assert.Equal("Flour", ingredients[1].GetProperty("name").GetString());
        Assert.Equal(1000m, ingredients[1].GetProperty("quantity").GetDecimal());
        Assert.Equal("g", ingredients[1].GetProperty("unit").GetString());
        Assert.False(ingredients[1].GetProperty("isOptional").GetBoolean());
        Assert.Equal(salt.Id, ingredients[2].GetProperty("ingredientId").GetInt64());
        Assert.Equal("Salt", ingredients[2].GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, ingredients[2].GetProperty("quantity").ValueKind);
        Assert.Equal(JsonValueKind.Null, ingredients[2].GetProperty("unit").ValueKind);
        Assert.True(ingredients[2].GetProperty("isOptional").GetBoolean());
        foreach (var ingredient in ingredients)
        {
            AssertJsonProperties(
                ingredient,
                "ingredientId",
                "name",
                "quantity",
                "unit",
                "isOptional");
        }

        var steps = root.GetProperty("steps").EnumerateArray().ToArray();
        Assert.Equal(2, steps.Length);
        Assert.Equal("Cook.", steps[0].GetProperty("instruction").GetString());
        Assert.Equal(600, steps[0].GetProperty("timerSeconds").GetInt32());
        Assert.Equal("Serve.", steps[1].GetProperty("instruction").GetString());
        Assert.Equal(JsonValueKind.Null, steps[1].GetProperty("timerSeconds").ValueKind);
        AssertJsonProperties(steps[0], "instruction", "timerSeconds");
        AssertJsonProperties(steps[1], "instruction", "timerSeconds");
        Assert.Equal(3, _commandInterceptor.SelectCommands.Count);
    }

    [Fact]
    public async Task RequiredIngredientWithoutQuantityReturnsSafeGenericError()
    {
        var recipe = await AddRecipeAsync("Invalid required quantity", 2, published: true);
        var ingredient = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        await AddRecipeIngredientAsync(recipe.Id, ingredient.Id, 1, false, null, null);

        using var response = await Client.GetAsync($"/api/recipes/{recipe.Id}");

        await AssertSafeGenericErrorAsync(response);
    }

    [Fact]
    public async Task MismatchedIngredientDimensionReturnsSafeGenericError()
    {
        var recipe = await AddRecipeAsync("Invalid dimension", 2, published: true);
        var ingredient = await AddIngredientAsync("Milk", QuantityDimension.Volume);
        await AddRecipeIngredientAsync(recipe.Id, ingredient.Id, 1, false, 100m, Unit.Gram);

        using var response = await Client.GetAsync($"/api/recipes/{recipe.Id}");

        await AssertSafeGenericErrorAsync(response);
    }

    [Fact]
    public async Task DraftAndMissingRecipesHaveIndistinguishableNotFoundResponses()
    {
        var draft = await AddRecipeAsync("Draft", 2, published: false);

        using var draftResponse = await Client.GetAsync($"/api/recipes/{draft.Id}");
        using var missingResponse = await Client.GetAsync("/api/recipes/9223372036854775807");

        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        using var draftDocument = JsonDocument.Parse(
            await draftResponse.Content.ReadAsStringAsync());
        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());
        AssertStableProblemDetailsEqual(
            draftDocument.RootElement,
            missingDocument.RootElement);
        AssertNotFoundDoesNotLeakRecipeDetails(
            draftDocument.RootElement,
            draft.Id);
        AssertNotFoundDoesNotLeakRecipeDetails(
            missingDocument.RootElement,
            long.MaxValue);
    }

    [Fact]
    public async Task RecipeEndpointsAreAnonymousWhilePantryRemainsAuthenticated()
    {
        var recipe = await AddRecipeAsync("Public", 2, published: true);

        using var listResponse = await Client.GetAsync("/api/recipes");
        using var detailResponse = await Client.GetAsync($"/api/recipes/{recipe.Id}");
        using var pantryResponse = await Client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pantryResponse.StatusCode);
    }

    private HttpClient Client =>
        _client ?? throw new InvalidOperationException("Test client is not initialized.");

    private SmartKitchenDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SmartKitchenDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new SmartKitchenDbContext(options);
    }

    private async Task<Recipe> AddRecipeAsync(
        string title,
        int baseServings,
        bool published)
    {
        await using var context = CreateContext();
        var recipe = new Recipe(title, baseServings);
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();

        if (published)
        {
            await context.Recipes
                .Where(candidate => candidate.Id == recipe.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    candidate => candidate.Status,
                    RecipeStatus.Published));
        }

        return recipe;
    }

    private async Task<Ingredient> AddIngredientAsync(
        string name,
        QuantityDimension dimension,
        bool isActive = true)
    {
        await using var context = CreateContext();
        var ingredient = new Ingredient(name, dimension, isActive);
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();
        return ingredient;
    }

    private async Task AddRecipeIngredientAsync(
        long recipeId,
        long ingredientId,
        int sequence,
        bool isOptional,
        decimal? normalizedQuantity,
        Unit? displayUnit)
    {
        await using var context = CreateContext();
        context.RecipeIngredients.Add(new RecipeIngredient(
            recipeId,
            ingredientId,
            sequence,
            isOptional,
            normalizedQuantity,
            displayUnit));
        await context.SaveChangesAsync();
    }

    private async Task AddRecipeStepAsync(
        long recipeId,
        int sequence,
        string instruction,
        int? timerSeconds)
    {
        await using var context = CreateContext();
        context.RecipeSteps.Add(new RecipeStep(
            recipeId,
            sequence,
            instruction,
            timerSeconds));
        await context.SaveChangesAsync();
    }

    private static void AssertJsonProperties(
        JsonElement element,
        params string[] expectedProperties)
    {
        Assert.Equal(
            expectedProperties.Order(StringComparer.Ordinal),
            element.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
    }

    private static async Task AssertSafeGenericErrorAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        Assert.Equal(500, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            "An unexpected error occurred.",
            document.RootElement.GetProperty("title").GetString());
        Assert.DoesNotContain("Published recipe ingredient", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NormalizedQuantity", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DisplayUnit", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("QuantityDimension", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recipes.", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertStableProblemDetailsEqual(
        JsonElement first,
        JsonElement second)
    {
        foreach (var propertyName in new[] { "status", "title", "detail" })
        {
            Assert.Equal(
                first.GetProperty(propertyName).ToString(),
                second.GetProperty(propertyName).ToString());
        }

        var firstHasType = first.TryGetProperty("type", out var firstType);
        var secondHasType = second.TryGetProperty("type", out var secondType);
        Assert.Equal(firstHasType, secondHasType);

        if (firstHasType)
        {
            Assert.Equal(firstType.ToString(), secondType.ToString());
        }
    }

    private static void AssertNotFoundDoesNotLeakRecipeDetails(
        JsonElement problem,
        long requestedId)
    {
        Assert.DoesNotContain(
            problem.EnumerateObject(),
            property => property.Name.Equals("id", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("recipeId", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("requestedId", StringComparison.OrdinalIgnoreCase));

        var publicMessage = string.Join(
            " ",
            problem.GetProperty("title").GetString(),
            problem.GetProperty("detail").GetString());
        var nonTracingContent = string.Join(
            " ",
            problem.EnumerateObject()
                .Where(property => !property.Name.Equals(
                    "traceId",
                    StringComparison.OrdinalIgnoreCase))
                .Select(property => $"{property.Name}:{property.Value}"));
        Assert.DoesNotContain(requestedId.ToString(), publicMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft", nonTracingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Published", nonTracingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SQL", nonTracingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", nonTracingContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", nonTracingContent, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _commands = new();

        internal IReadOnlyList<string> SelectCommands =>
            _commands
                .Where(command => command.TrimStart().StartsWith(
                    "SELECT",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();

        internal void Clear()
        {
            while (_commands.TryDequeue(out _))
            {
            }
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            _commands.Enqueue(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _commands.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
