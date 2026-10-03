using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Api;

[Collection(SqlServerContainerCollection.Name)]
public sealed class RecipeSuitabilityEndpointTests(SqlServerContainerFixture fixture)
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
    public async Task SuitabilityIsProtectedWhileExistingCatalogReadsRemainPublic()
    {
        using var anonymousSuitability = await Client.GetAsync(
            "/api/recipes/suitability");
        using var authenticatedSuitability = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "user-1");
        using var recipes = await Client.GetAsync("/api/recipes");
        using var ingredients = await Client.GetAsync("/api/ingredients");
        using var pantry = await Client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousSuitability.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticatedSuitability.StatusCode);
        Assert.Equal(HttpStatusCode.OK, recipes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ingredients.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pantry.StatusCode);
    }

    [Fact]
    public async Task OnlyPublishedRecipesAreReturnedWithExactPublicContract()
    {
        var ingredient = await AddIngredientAsync(
            "Inactive flour",
            QuantityDimension.Mass,
            isActive: false);
        var optionalIngredient = await AddIngredientAsync(
            "Milk",
            QuantityDimension.Volume);
        var published = await AddRecipeAsync("Published", 4, published: true);
        var draft = await AddRecipeAsync("Draft", 2, published: false);
        await AddRecipeIngredientAsync(
            published.Id,
            ingredient.Id,
            1,
            isOptional: false,
            100m,
            Unit.Gram);
        await AddRecipeIngredientAsync(
            draft.Id,
            ingredient.Id,
            1,
            isOptional: false,
            normalizedQuantity: null,
            displayUnit: null);
        await AddRecipeIngredientAsync(
            published.Id,
            optionalIngredient.Id,
            2,
            isOptional: true,
            50m,
            Unit.Liter);
        await AddPantryItemAsync("user-1", ingredient.Id, 100m, Unit.Kilogram);

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "user-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var result = Assert.Single(document.RootElement.EnumerateArray());
        AssertJsonProperties(
            result,
            "recipeId",
            "title",
            "baseServings",
            "classification",
            "isCookable",
            "requiredIngredientCount",
            "satisfiedRequiredIngredientCount",
            "requiredCoverage",
            "missingRequiredIngredients",
            "insufficientRequiredIngredients",
            "optionalIngredientCount",
            "availableOptionalIngredientCount",
            "optionalIssues",
            "unevaluableIngredients",
            "reasonCodes");
        Assert.Equal(published.Id, result.GetProperty("recipeId").GetInt64());
        Assert.Equal("Published", result.GetProperty("title").GetString());
        Assert.Equal(4, result.GetProperty("baseServings").GetInt32());
        Assert.Equal("Cookable", result.GetProperty("classification").GetString());
        Assert.True(result.GetProperty("isCookable").GetBoolean());
        Assert.Equal(1m, result.GetProperty("requiredCoverage").GetDecimal());
        Assert.Equal(1, result.GetProperty("optionalIngredientCount").GetInt32());
        Assert.Equal(0, result.GetProperty("availableOptionalIngredientCount").GetInt32());
        var optionalIssue = Assert.Single(
            result.GetProperty("optionalIssues").EnumerateArray());
        AssertJsonProperties(
            optionalIssue,
            "ingredientId",
            "name",
            "kind",
            "requiredQuantity",
            "availableQuantity",
            "shortfall",
            "unit",
            "reasonCode");
        Assert.Equal("Missing", optionalIssue.GetProperty("kind").GetString());
        Assert.Equal("ml", optionalIssue.GetProperty("unit").GetString());
        Assert.DoesNotContain("Draft", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("user-1", body, StringComparison.Ordinal);
        Assert.DoesNotContain("normalizedQuantity", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("displayUnit", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isActive", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OtherUsersAbundantStockCannotAffectCurrentUserSuitability()
    {
        var ingredient = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync("Bread", 4, published: true);
        await AddRecipeIngredientAsync(
            recipe.Id,
            ingredient.Id,
            1,
            isOptional: false,
            100m,
            Unit.Gram);
        await AddPantryItemAsync("current-user", ingredient.Id, 40m, Unit.Gram);
        await AddPantryItemAsync("other-user", ingredient.Id, 1000m, Unit.Gram);
        _commandInterceptor.Clear();

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "current-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(
            "InsufficientRequired",
            result.GetProperty("classification").GetString());
        var insufficient = Assert.Single(
            result.GetProperty("insufficientRequiredIngredients").EnumerateArray());
        AssertJsonProperties(
            insufficient,
            "ingredientId",
            "name",
            "requiredQuantity",
            "availableQuantity",
            "shortfall",
            "unit");
        Assert.Equal(100m, insufficient.GetProperty("requiredQuantity").GetDecimal());
        Assert.Equal(40m, insufficient.GetProperty("availableQuantity").GetDecimal());
        Assert.Equal(60m, insufficient.GetProperty("shortfall").GetDecimal());
        Assert.Equal("g", insufficient.GetProperty("unit").GetString());

        var pantryCommand = Assert.Single(
            _commandInterceptor.SelectCommands,
            command => command.CommandText.Contains(
                "[pantry].[UserPantryItems]",
                StringComparison.Ordinal));
        Assert.Contains("[UserId]", pantryCommand.CommandText, StringComparison.Ordinal);
        Assert.Contains(
            pantryCommand.ParameterValues,
            value => value is byte[] bytes &&
                     bytes.SequenceEqual(Encoding.Unicode.GetBytes("current-user")));
        Assert.DoesNotContain("current-user", pantryCommand.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatedRequiredRowsAreAggregatedAgainstPantryOnce()
    {
        var ingredient = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync("Bread", 4, published: true);
        await AddRecipeIngredientAsync(
            recipe.Id, ingredient.Id, 1, false, 60m, Unit.Gram);
        await AddRecipeIngredientAsync(
            recipe.Id, ingredient.Id, 2, false, 60m, Unit.Gram);
        await AddPantryItemAsync("user-1", ingredient.Id, 100m, Unit.Gram);

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "user-1");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal(1, result.GetProperty("requiredIngredientCount").GetInt32());
        Assert.Equal(
            "InsufficientRequired",
            result.GetProperty("classification").GetString());
        var insufficient = Assert.Single(
            result.GetProperty("insufficientRequiredIngredients").EnumerateArray());
        Assert.Equal(120m, insufficient.GetProperty("requiredQuantity").GetDecimal());
    }

    [Fact]
    public async Task MalformedPublishedDimensionReturnsSafeUnevaluableResult()
    {
        var ingredient = await AddIngredientAsync("Milk", QuantityDimension.Volume);
        var recipe = await AddRecipeAsync("Invalid soup", 4, published: true);
        await AddRecipeIngredientAsync(
            recipe.Id,
            ingredient.Id,
            1,
            isOptional: false,
            100m,
            Unit.Gram);
        await AddPantryItemAsync("user-1", ingredient.Id, 100m, Unit.Liter);

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "user-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var result = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("Unevaluable", result.GetProperty("classification").GetString());
        Assert.False(result.GetProperty("isCookable").GetBoolean());
        Assert.Equal(
            JsonValueKind.Null,
            result.GetProperty("requiredCoverage").ValueKind);
        var issue = Assert.Single(
            result.GetProperty("unevaluableIngredients").EnumerateArray());
        AssertJsonProperties(issue, "ingredientId", "name", "reasonCode");
        Assert.Equal(
            "quantity_dimension_mismatch",
            issue.GetProperty("reasonCode").GetString());
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RecipeIngredients", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleRecipesUseThreeFixedFilteredSelectsWithoutNPlusOne()
    {
        var ingredient = await AddIngredientAsync("Rice", QuantityDimension.Mass);

        for (var index = 1; index <= 5; index++)
        {
            var recipe = await AddRecipeAsync($"Recipe {index}", 2, published: true);
            await AddRecipeIngredientAsync(
                recipe.Id,
                ingredient.Id,
                1,
                isOptional: false,
                index,
                Unit.Gram);
        }

        await AddPantryItemAsync("exact-user", ingredient.Id, 10m, Unit.Gram);
        _commandInterceptor.Clear();

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "exact-user");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(5, document.RootElement.GetArrayLength());
        Assert.Equal(3, _commandInterceptor.SelectCommands.Count);
        Assert.Equal(
            2,
            _commandInterceptor.SelectCommands.Count(command =>
                command.CommandText.Contains("[recipes]", StringComparison.Ordinal) &&
                command.CommandText.Contains("[Status]", StringComparison.Ordinal)));
        Assert.Single(
            _commandInterceptor.SelectCommands,
            command => command.CommandText.Contains(
                "[pantry].[UserPantryItems]",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            _commandInterceptor.SelectCommands,
            command => command.CommandText.Contains(
                "RecipeSteps",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoPublishedRecipesShortCircuitsAfterHeaderSelect()
    {
        await AddRecipeAsync("Draft", 2, published: false);
        _commandInterceptor.Clear();

        using var response = await SendAuthenticatedAsync(
            "/api/recipes/suitability",
            "user-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.EnumerateArray());
        Assert.Single(_commandInterceptor.SelectCommands);
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

    private async Task AddPantryItemAsync(
        string userId,
        long ingredientId,
        decimal normalizedQuantity,
        Unit displayUnit)
    {
        await using var context = CreateContext();
        context.UserPantryItems.Add(new UserPantryItem(
            userId,
            ingredientId,
            normalizedQuantity,
            displayUnit));
        await context.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        string path,
        string userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.UserIdHeaderName,
            userId);
        return await Client.SendAsync(request);
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

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> _commands = new();

        internal IReadOnlyList<CapturedCommand> SelectCommands =>
            _commands
                .Where(command => command.CommandText.TrimStart().StartsWith(
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
            Capture(command);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Capture(command);
            return base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        private void Capture(DbCommand command)
        {
            var parameterValues = command.Parameters
                .Cast<DbParameter>()
                .Select(parameter => parameter.Value is byte[] bytes
                    ? (object)bytes.ToArray()
                    : parameter.Value)
                .ToArray();
            _commands.Enqueue(new CapturedCommand(command.CommandText, parameterValues));
        }
    }

    private sealed record CapturedCommand(
        string CommandText,
        IReadOnlyList<object?> ParameterValues);
}
