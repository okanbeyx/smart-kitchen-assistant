using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Api;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Features.Pantry.Api;

[Collection(SqlServerContainerCollection.Name)]
public sealed class PantryConsumptionEndpointTests(SqlServerContainerFixture fixture)
    : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _connectionString = fixture.CreateDatabaseConnectionString();
    private TestWebApplicationFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        _factory = new TestWebApplicationFactory(_connectionString);
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
    public async Task AnonymousRequestIsUnauthorized()
    {
        using var response = await Client.PostAsync(
            "/api/recipes/1/consume",
            content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("contains space")]
    [InlineData("ünicode")]
    public async Task InvalidIdempotencyKeyIsRejected(string? key)
    {
        using var response = await SendConsumeAsync(1, "key-user", key);

        await AssertProblemCodeAsync(
            response,
            HttpStatusCode.BadRequest,
            "invalid_idempotency_key");
    }

    [Fact]
    public async Task MultipleIdempotencyKeyValuesAreRejected()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/recipes/1/consume");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.UserIdHeaderName,
            "multi-header-user");
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            ["first", "second"]);

        using var response = await Client.SendAsync(request);

        await AssertProblemCodeAsync(
            response,
            HttpStatusCode.BadRequest,
            "invalid_idempotency_key");
    }

    [Fact]
    public async Task SuccessfulConsumptionDeletesExactStockAndPersistsStableResult()
    {
        var ingredient = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Bread",
            published: true,
            [Required(ingredient.Id, 1, 250m, Unit.Gram)]);
        await AddPantryItemAsync("user-1", ingredient.Id, 250m, Unit.Gram);

        using var response = await SendConsumeAsync(recipe.Id, "user-1", "consume-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        AssertJsonProperties(
            document.RootElement,
            "consumptionId",
            "recipeId",
            "status",
            "consumedIngredients");
        Assert.Equal("Completed", document.RootElement.GetProperty("status").GetString());
        var consumed = Assert.Single(
            document.RootElement.GetProperty("consumedIngredients").EnumerateArray());
        AssertJsonProperties(consumed, "ingredientId", "quantity", "quantityDimension");
        Assert.Equal("Mass", consumed.GetProperty("quantityDimension").GetString());
        Assert.Equal(250m, consumed.GetProperty("quantity").GetDecimal());

        await using var context = CreateContext();
        Assert.False(await context.UserPantryItems.AnyAsync(item =>
            item.UserId == "user-1" && item.IngredientId == ingredient.Id));
        var operation = await context.StockConsumptions
            .AsNoTracking()
            .Include(consumption => consumption.Items)
            .SingleAsync(consumption => consumption.UserId == "user-1");
        Assert.Equal(recipe.Id, operation.RecipeId);
        Assert.Equal("consume-1", operation.IdempotencyKey);
        Assert.Single(operation.Items);
    }

    [Fact]
    public async Task MultipleIngredientsUpdateAtomicallyAndOptionalIngredientIsUntouched()
    {
        var flour = await AddIngredientAsync("Flour multi", QuantityDimension.Mass);
        var milk = await AddIngredientAsync("Milk multi", QuantityDimension.Volume);
        var salt = await AddIngredientAsync("Salt optional", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Multi",
            published: true,
            [
                Required(flour.Id, 1, 300m, Unit.Gram),
                Required(milk.Id, 2, 200m, Unit.Milliliter),
                Optional(salt.Id, 3, 10m, Unit.Gram)
            ]);
        await AddPantryItemAsync("multi-user", flour.Id, 500m, Unit.Gram);
        await AddPantryItemAsync("multi-user", milk.Id, 200m, Unit.Milliliter);
        await AddPantryItemAsync("multi-user", salt.Id, 10m, Unit.Gram);

        using var response = await SendConsumeAsync(
            recipe.Id,
            "multi-user",
            "multi-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PantryConsumptionResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal([flour.Id, milk.Id], body.ConsumedIngredients
            .Select(item => item.IngredientId));

        await using var context = CreateContext();
        var remaining = await context.UserPantryItems
            .AsNoTracking()
            .Where(item => item.UserId == "multi-user")
            .OrderBy(item => item.IngredientId)
            .ToArrayAsync();
        Assert.Equal(2, remaining.Length);
        Assert.Equal(200m, remaining.Single(item => item.IngredientId == flour.Id)
            .NormalizedQuantity);
        Assert.Equal(10m, remaining.Single(item => item.IngredientId == salt.Id)
            .NormalizedQuantity);
    }

    [Fact]
    public async Task LaterInsufficientIngredientRollsBackEarlierDeductionAndClaim()
    {
        var first = await AddIngredientAsync("Atomic first", QuantityDimension.Mass);
        var second = await AddIngredientAsync("Atomic second", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Atomic failure",
            published: true,
            [
                Required(first.Id, 1, 70m, Unit.Gram),
                Required(second.Id, 2, 70m, Unit.Gram)
            ]);
        await AddPantryItemAsync("atomic-user", first.Id, 100m, Unit.Gram);
        await AddPantryItemAsync("atomic-user", second.Id, 50m, Unit.Gram);

        using var response = await SendConsumeAsync(
            recipe.Id,
            "atomic-user",
            "atomic-key");

        await AssertProblemCodeAsync(
            response,
            HttpStatusCode.Conflict,
            "insufficient_stock");
        await using var context = CreateContext();
        var items = await context.UserPantryItems
            .AsNoTracking()
            .Where(item => item.UserId == "atomic-user")
            .OrderBy(item => item.IngredientId)
            .ToArrayAsync();
        Assert.Equal([100m, 50m], items.Select(item => item.NormalizedQuantity));
        Assert.False(await context.StockConsumptions.AnyAsync(consumption =>
            consumption.UserId == "atomic-user"));
        Assert.False(await context.StockConsumptionItems.AnyAsync());
    }

    [Fact]
    public async Task MissingCurrentUserStockCannotUseAnotherUsersStock()
    {
        var ingredient = await AddIngredientAsync("Private stock", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Private recipe",
            published: true,
            [Required(ingredient.Id, 1, 70m, Unit.Gram)]);
        await AddPantryItemAsync("owner", ingredient.Id, 1000m, Unit.Gram);

        using var response = await SendConsumeAsync(
            recipe.Id,
            "requester",
            "private-key");

        await AssertProblemCodeAsync(
            response,
            HttpStatusCode.Conflict,
            "insufficient_stock");
        await using var context = CreateContext();
        var ownerItem = await context.UserPantryItems.AsNoTracking().SingleAsync();
        Assert.Equal("owner", ownerItem.UserId);
        Assert.Equal(1000m, ownerItem.NormalizedQuantity);
    }

    [Fact]
    public async Task RepeatedRequiredRowsAggregateAndInactiveReferenceRemainsConsumable()
    {
        var ingredient = await AddIngredientAsync(
            "Inactive legacy",
            QuantityDimension.Mass,
            isActive: false);
        var recipe = await AddRecipeAsync(
            "Repeated",
            published: true,
            [
                Required(ingredient.Id, 1, 30m, Unit.Gram),
                Required(ingredient.Id, 2, 40m, Unit.Gram)
            ]);
        await AddPantryItemAsync("repeat-user", ingredient.Id, 100m, Unit.Gram);

        using var response = await SendConsumeAsync(
            recipe.Id,
            "repeat-user",
            "repeat-key");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PantryConsumptionResponse>(JsonOptions);
        var consumed = Assert.Single(body!.ConsumedIngredients);
        Assert.Equal(70m, consumed.Quantity);
        await using var context = CreateContext();
        Assert.Equal(
            30m,
            (await context.UserPantryItems.AsNoTracking().SingleAsync(item =>
                item.UserId == "repeat-user")).NormalizedQuantity);
        Assert.Single(await context.StockConsumptionItems.AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task InvalidRecipeDataAndSqlRangeOverflowFailWithoutDeduction()
    {
        var volume = await AddIngredientAsync("Invalid volume", QuantityDimension.Volume);
        var invalidRecipe = await AddRecipeAsync(
            "Invalid dimension",
            published: true,
            [Required(volume.Id, 1, 1m, Unit.Gram)]);
        await AddPantryItemAsync("invalid-user", volume.Id, 10m, Unit.Milliliter);

        using var invalidResponse = await SendConsumeAsync(
            invalidRecipe.Id,
            "invalid-user",
            "invalid-key");
        await AssertProblemCodeAsync(
            invalidResponse,
            HttpStatusCode.Conflict,
            "recipe_not_consumable");

        var mass = await AddIngredientAsync("Overflow mass", QuantityDimension.Mass);
        var overflowRecipe = await AddRecipeAsync(
            "Overflow",
            published: true,
            [
                Required(mass.Id, 1, 999_999_999_999m, Unit.Gram),
                Required(mass.Id, 2, 1m, Unit.Gram)
            ]);
        await AddPantryItemAsync("overflow-user", mass.Id, 10m, Unit.Gram);

        using var overflowResponse = await SendConsumeAsync(
            overflowRecipe.Id,
            "overflow-user",
            "overflow-key");
        await AssertProblemCodeAsync(
            overflowResponse,
            HttpStatusCode.Conflict,
            "recipe_not_consumable");

        await using var context = CreateContext();
        Assert.Equal(2, await context.UserPantryItems.CountAsync());
        Assert.Equal(0, await context.StockConsumptions.CountAsync());
    }

    [Fact]
    public async Task DraftAndMissingRecipesHaveSameNotFoundBehavior()
    {
        var draft = await AddRecipeAsync("Draft", published: false, []);

        using var draftResponse = await SendConsumeAsync(
            draft.Id,
            "not-found-user",
            "draft-key");
        using var missingResponse = await SendConsumeAsync(
            long.MaxValue,
            "not-found-user",
            "missing-key");

        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);
        using var draftDocument = JsonDocument.Parse(
            await draftResponse.Content.ReadAsStringAsync());
        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            ProblemSignature(draftDocument.RootElement),
            ProblemSignature(missingDocument.RootElement));
    }

    [Fact]
    public async Task SequentialReplayReturnsIdenticalResponseAndDifferentRecipeConflicts()
    {
        var ingredient = await AddIngredientAsync("Replay", QuantityDimension.Mass);
        var firstRecipe = await AddRecipeAsync(
            "Replay first",
            published: true,
            [Required(ingredient.Id, 1, 70m, Unit.Gram)]);
        var secondRecipe = await AddRecipeAsync(
            "Replay second",
            published: true,
            [Required(ingredient.Id, 1, 10m, Unit.Gram)]);
        await AddPantryItemAsync("replay-user", ingredient.Id, 100m, Unit.Gram);

        using var first = await SendConsumeAsync(
            firstRecipe.Id,
            "replay-user",
            "same-key");
        using var replay = await SendConsumeAsync(
            firstRecipe.Id,
            "replay-user",
            "same-key");
        using var conflict = await SendConsumeAsync(
            secondRecipe.Id,
            "replay-user",
            "same-key");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await replay.Content.ReadAsStringAsync());
        await AssertProblemCodeAsync(
            conflict,
            HttpStatusCode.Conflict,
            "idempotency_key_reused");
        await using var context = CreateContext();
        Assert.Equal(30m, (await context.UserPantryItems.AsNoTracking()
            .SingleAsync(item => item.UserId == "replay-user")).NormalizedQuantity);
        Assert.Equal(1, await context.StockConsumptions.CountAsync(consumption =>
            consumption.UserId == "replay-user"));
    }

    [Fact]
    public async Task SameKeyForDifferentUsersCreatesIndependentOperations()
    {
        var ingredient = await AddIngredientAsync("Scoped key", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Scoped key recipe",
            published: true,
            [Required(ingredient.Id, 1, 20m, Unit.Gram)]);
        await AddPantryItemAsync("scope-a", ingredient.Id, 30m, Unit.Gram);
        await AddPantryItemAsync("scope-b", ingredient.Id, 30m, Unit.Gram);

        using var first = await SendConsumeAsync(recipe.Id, "scope-a", "shared-key");
        using var second = await SendConsumeAsync(recipe.Id, "scope-b", "shared-key");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await using var context = CreateContext();
        Assert.Equal(2, await context.StockConsumptions.CountAsync(consumption =>
            consumption.IdempotencyKey == "shared-key"));
    }

    [Fact]
    public async Task ConcurrentSameKeyRequestsProduceOneLogicalConsumption()
    {
        var ingredient = await AddIngredientAsync("Same key race", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Same key race recipe",
            published: true,
            [Required(ingredient.Id, 1, 70m, Unit.Gram)]);
        await AddPantryItemAsync("same-race-user", ingredient.Id, 100m, Unit.Gram);
        var barrier = new CommandBarrierInterceptor("INSERT INTO [pantry].[StockConsumptions]");

        using var factory = new TestWebApplicationFactory(_connectionString, barrier);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(
            SendConsumeAsync(client, recipe.Id, "same-race-user", "race-key"),
            SendConsumeAsync(client, recipe.Id, "same-race-user", "race-key"));

        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            Assert.Equal(
                await responses[0].Content.ReadAsStringAsync(),
                await responses[1].Content.ReadAsStringAsync());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var context = CreateContext();
        Assert.Equal(30m, (await context.UserPantryItems.AsNoTracking()
            .SingleAsync(item => item.UserId == "same-race-user")).NormalizedQuantity);
        Assert.Equal(1, await context.StockConsumptions.CountAsync(consumption =>
            consumption.UserId == "same-race-user"));
    }

    [Fact]
    public async Task ConcurrentDifferentKeysCannotOverConsumeStock()
    {
        var ingredient = await AddIngredientAsync("Stock race", QuantityDimension.Mass);
        var recipe = await AddRecipeAsync(
            "Stock race recipe",
            published: true,
            [Required(ingredient.Id, 1, 70m, Unit.Gram)]);
        await AddPantryItemAsync("stock-race-user", ingredient.Id, 100m, Unit.Gram);
        var barrier = new CommandBarrierInterceptor("DELETE FROM [p]");

        using var factory = new TestWebApplicationFactory(_connectionString, barrier);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(
            SendConsumeAsync(client, recipe.Id, "stock-race-user", "stock-key-1"),
            SendConsumeAsync(client, recipe.Id, "stock-race-user", "stock-key-2"));

        try
        {
            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order().ToArray());
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var context = CreateContext();
        Assert.Equal(30m, (await context.UserPantryItems.AsNoTracking()
            .SingleAsync(item => item.UserId == "stock-race-user")).NormalizedQuantity);
        Assert.Equal(1, await context.StockConsumptions.CountAsync(consumption =>
            consumption.UserId == "stock-race-user"));
    }

    [Fact]
    public async Task OverlappingMultiIngredientRequestsNeverPartiallyCommit()
    {
        var first = await AddIngredientAsync("Overlap first", QuantityDimension.Mass);
        var second = await AddIngredientAsync("Overlap second", QuantityDimension.Mass);
        var larger = await AddRecipeAsync(
            "Overlap larger",
            published: true,
            [Required(first.Id, 1, 70m, Unit.Gram), Required(second.Id, 2, 70m, Unit.Gram)]);
        var smaller = await AddRecipeAsync(
            "Overlap smaller",
            published: true,
            [Required(first.Id, 1, 40m, Unit.Gram), Required(second.Id, 2, 40m, Unit.Gram)]);
        await AddPantryItemAsync("overlap-user", first.Id, 100m, Unit.Gram);
        await AddPantryItemAsync("overlap-user", second.Id, 100m, Unit.Gram);
        var barrier = new CommandBarrierInterceptor("DELETE FROM [p]");

        using var factory = new TestWebApplicationFactory(_connectionString, barrier);
        using var client = factory.CreateClient();
        var responses = await Task.WhenAll(
            SendConsumeAsync(client, larger.Id, "overlap-user", "overlap-1"),
            SendConsumeAsync(client, smaller.Id, "overlap-user", "overlap-2"));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var context = CreateContext();
        var quantities = await context.UserPantryItems
            .AsNoTracking()
            .Where(item => item.UserId == "overlap-user")
            .OrderBy(item => item.IngredientId)
            .Select(item => item.NormalizedQuantity)
            .ToArrayAsync();
        Assert.Equal(2, quantities.Length);
        Assert.True(
            quantities.SequenceEqual([30m, 30m]) ||
            quantities.SequenceEqual([60m, 60m]));
        Assert.All(quantities, quantity => Assert.True(quantity > 0m));
        Assert.Equal(1, await context.StockConsumptions.CountAsync(consumption =>
            consumption.UserId == "overlap-user"));
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

    private async Task<Recipe> AddRecipeAsync(
        string title,
        bool published,
        IReadOnlyList<RecipeIngredientSeed> ingredients)
    {
        await using var context = CreateContext();
        var recipe = new Recipe(title, 4);
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

        context.RecipeIngredients.AddRange(ingredients.Select(ingredient =>
            new RecipeIngredient(
                recipe.Id,
                ingredient.IngredientId,
                ingredient.Sequence,
                ingredient.IsOptional,
                ingredient.NormalizedQuantity,
                ingredient.DisplayUnit)));
        await context.SaveChangesAsync();
        return recipe;
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

    private Task<HttpResponseMessage> SendConsumeAsync(
        long recipeId,
        string userId,
        string? idempotencyKey) =>
        SendConsumeAsync(Client, recipeId, userId, idempotencyKey);

    private static async Task<HttpResponseMessage> SendConsumeAsync(
        HttpClient client,
        long recipeId,
        string userId,
        string? idempotencyKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/recipes/{recipeId}/consume");
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.UserIdHeaderName,
            userId);

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertProblemCodeAsync(
        HttpResponseMessage response,
        HttpStatusCode statusCode,
        string code)
    {
        Assert.Equal(statusCode, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, document.RootElement.GetProperty("code").GetString());
    }

    private static void AssertJsonProperties(
        JsonElement element,
        params string[] expectedProperties) =>
        Assert.Equal(
            expectedProperties.Order(StringComparer.Ordinal),
            element.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));

    private static string[] ProblemSignature(JsonElement problem) =>
    [
        problem.GetProperty("status").GetRawText(),
        problem.GetProperty("title").GetString()!,
        problem.GetProperty("detail").GetString()!,
        problem.GetProperty("code").GetString()!
    ];

    private static RecipeIngredientSeed Required(
        long ingredientId,
        int sequence,
        decimal quantity,
        Unit unit) =>
        new(ingredientId, sequence, false, quantity, unit);

    private static RecipeIngredientSeed Optional(
        long ingredientId,
        int sequence,
        decimal quantity,
        Unit unit) =>
        new(ingredientId, sequence, true, quantity, unit);

    private sealed record RecipeIngredientSeed(
        long IngredientId,
        int Sequence,
        bool IsOptional,
        decimal? NormalizedQuantity,
        Unit? DisplayUnit);

    private sealed class CommandBarrierInterceptor(string commandToken)
        : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await WaitForPeerAsync(command, cancellationToken);
            return await base.NonQueryExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await WaitForPeerAsync(command, cancellationToken);
            return await base.ReaderExecutingAsync(
                command,
                eventData,
                result,
                cancellationToken);
        }

        private async Task WaitForPeerAsync(
            DbCommand command,
            CancellationToken cancellationToken)
        {
            if (!command.CommandText.Contains(commandToken, StringComparison.Ordinal))
            {
                return;
            }

            var arrival = Interlocked.Increment(ref _arrivals);

            if (arrival == 2)
            {
                _release.TrySetResult();
            }

            if (arrival <= 2)
            {
                await _release.Task.WaitAsync(
                    TimeSpan.FromSeconds(15),
                    cancellationToken);
            }
        }
    }
}
