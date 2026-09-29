using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Api;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Features.Pantry.Api;

[Collection(SqlServerContainerCollection.Name)]
public sealed class PantryEndpointTests(SqlServerContainerFixture fixture)
    : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

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
    public async Task PantryEndpointWithoutCredentialsReturnsUnauthorized()
    {
        using var response = await Client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListReturnsOnlyCurrentUsersItemsWithoutInternalFields()
    {
        var ingredient = await AddIngredientAsync("Flour", QuantityDimension.Mass);
        var secondIngredient = await AddIngredientAsync("Sugar", QuantityDimension.Mass);
        var firstItem = await AddPantryItemAsync(
            "User-A",
            secondIngredient.Id,
            500m,
            Unit.Gram);
        await AddPantryItemAsync("user-a", ingredient.Id, 250m, Unit.Gram);
        var secondItem = await AddPantryItemAsync(
            "User-A",
            ingredient.Id,
            1500m,
            Unit.Kilogram);

        using var response = await SendAsync(HttpMethod.Get, "/api/pantry", "User-A");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<PantryItemResponse[]>(json, JsonOptions);
        Assert.NotNull(items);
        Assert.Equal(
            [firstItem.Id, secondItem.Id],
            items.Select(item => item.Id).ToArray());
        Assert.Equal("Sugar", items[0].IngredientName);
        Assert.Equal("Flour", items[1].IngredientName);
        Assert.Equal(1.5m, items[1].Quantity);
        Assert.Equal("kg", items[1].Unit);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("normalizedQuantity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User-A", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingItemRemainsUsableAfterIngredientBecomesInactive()
    {
        var ingredient = await AddIngredientAsync("Milk", QuantityDimension.Volume);

        using var createResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            new { ingredientId = ingredient.Id, quantity = 1.5m, unit = "l" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<PantryItemResponse>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal($"/api/pantry/{created.Id}", createResponse.Headers.Location?.OriginalString);
        await SetIngredientActiveAsync(ingredient.Id, isActive: false);

        using var getResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/pantry/{created.Id}",
            "user-1");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var updateResponse = await SendJsonAsync(
            HttpMethod.Put,
            $"/api/pantry/{created.Id}",
            "user-1",
            new { quantity = 750m, unit = "ml" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<PantryItemResponse>(JsonOptions);
        Assert.Equal(ingredient.Id, updated!.IngredientId);
        Assert.Equal(750m, updated.Quantity);
        Assert.Equal("ml", updated.Unit);

        await using (var context = CreateContext())
        {
            var stored = await context.UserPantryItems.AsNoTracking().SingleAsync();
            Assert.Equal(ingredient.Id, stored.IngredientId);
            Assert.Equal(750m, stored.NormalizedQuantity);
        }

        using var deleteResponse = await SendAsync(
            HttpMethod.Delete,
            $"/api/pantry/{created.Id}",
            "user-1");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var secondDeleteResponse = await SendAsync(
            HttpMethod.Delete,
            $"/api/pantry/{created.Id}",
            "user-1");
        Assert.Equal(HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);
    }

    [Fact]
    public async Task MissingAndForeignItemsHaveIndistinguishableNotFoundBehavior()
    {
        var ingredient = await AddIngredientAsync("Rice", QuantityDimension.Mass);
        var foreign = await AddPantryItemAsync(
            "owner",
            ingredient.Id,
            1000m,
            Unit.Gram);
        const long missingId = long.MaxValue;

        await AssertSameNotFoundAsync(
            await SendAsync(HttpMethod.Get, $"/api/pantry/{missingId}", "attacker"),
            await SendAsync(HttpMethod.Get, $"/api/pantry/{foreign.Id}", "attacker"));
        await AssertSameNotFoundAsync(
            await SendJsonAsync(
                HttpMethod.Put,
                $"/api/pantry/{missingId}",
                "attacker",
                new { quantity = 2m, unit = "kg" }),
            await SendJsonAsync(
                HttpMethod.Put,
                $"/api/pantry/{foreign.Id}",
                "attacker",
                new { quantity = 2m, unit = "kg" }));
        await AssertSameNotFoundAsync(
            await SendAsync(HttpMethod.Delete, $"/api/pantry/{missingId}", "attacker"),
            await SendAsync(HttpMethod.Delete, $"/api/pantry/{foreign.Id}", "attacker"));

        await using var context = CreateContext();
        var unchanged = await context.UserPantryItems.AsNoTracking().SingleAsync();
        Assert.Equal("owner", unchanged.UserId);
        Assert.Equal(1000m, unchanged.NormalizedQuantity);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task SingleItemReadsUseIdAndExactUserIdInTheSameSqlCommand(
        string method)
    {
        const string owner = "owner";
        const string requester = "Requesting-User";
        var ingredient = await AddIngredientAsync("Scoped", QuantityDimension.Mass);
        var foreign = await AddPantryItemAsync(owner, ingredient.Id, 1m, Unit.Gram);

        using var response = method switch
        {
            "GET" => await SendAsync(
                HttpMethod.Get,
                $"/api/pantry/{foreign.Id}",
                requester),
            "PUT" => await SendJsonAsync(
                HttpMethod.Put,
                $"/api/pantry/{foreign.Id}",
                requester,
                new { quantity = 2m, unit = "g" }),
            "DELETE" => await SendAsync(
                HttpMethod.Delete,
                $"/api/pantry/{foreign.Id}",
                requester),
            _ => throw new InvalidOperationException("Unsupported HTTP method.")
        };

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var expectedUserId = Encoding.Unicode.GetBytes(requester);
        var scopedCommand = Assert.Single(_commandInterceptor.Commands, command =>
            command.CommandText.Contains(
                "[pantry].[UserPantryItems]",
                StringComparison.Ordinal) &&
            command.ParameterValues.Any(value =>
                value is long itemId && itemId == foreign.Id) &&
            command.ParameterValues.Any(value =>
                value is byte[] bytes && bytes.SequenceEqual(expectedUserId)));
        Assert.Contains("[Id]", scopedCommand.CommandText, StringComparison.Ordinal);
        Assert.Contains("[UserId]", scopedCommand.CommandText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostDistinguishesMissingAndInactiveIngredients()
    {
        var inactive = await AddIngredientAsync(
            "Inactive ingredient",
            QuantityDimension.Mass,
            isActive: false);

        using var missingResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            new { ingredientId = long.MaxValue, quantity = 1m, unit = "g" });
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
        using var missingDocument = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());
        Assert.True(missingDocument.RootElement
            .GetProperty("errors")
            .TryGetProperty("ingredientId", out _));

        using var inactiveResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            new { ingredientId = inactive.Id, quantity = 1m, unit = "g" });
        await AssertConflictCodeAsync(inactiveResponse, "ingredient_inactive");

        await using var context = CreateContext();
        Assert.Equal(0, await context.UserPantryItems.CountAsync());
    }

    [Fact]
    public async Task ExactDuplicateConflictsButDifferentUsersMayUseSameIngredient()
    {
        var ingredient = await AddIngredientAsync("Salt", QuantityDimension.Mass);

        using var first = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "exact-user",
            new { ingredientId = ingredient.Id, quantity = 1m, unit = "g" });
        using var duplicate = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "exact-user",
            new { ingredientId = ingredient.Id, quantity = 2m, unit = "g" });
        using var otherUser = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "Exact-User",
            new { ingredientId = ingredient.Id, quantity = 3m, unit = "g" });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await AssertConflictCodeAsync(duplicate, "pantry_item_duplicate");
        Assert.Equal(HttpStatusCode.Created, otherUser.StatusCode);
    }

    [Fact]
    public async Task ConcurrentDuplicatePostsProduceCreatedAndConflict()
    {
        var ingredient = await AddIngredientAsync("Pepper", QuantityDimension.Mass);

        var firstTask = SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "race-user",
            new { ingredientId = ingredient.Id, quantity = 1m, unit = "g" });
        var secondTask = SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "race-user",
            new { ingredientId = ingredient.Id, quantity = 2m, unit = "g" });
        var responses = await Task.WhenAll(firstTask, secondTask);

        try
        {
            Assert.Equal(
                [HttpStatusCode.Created, HttpStatusCode.Conflict],
                responses.Select(response => response.StatusCode).Order().ToArray());
            var conflict = responses.Single(response =>
                response.StatusCode == HttpStatusCode.Conflict);
            await AssertConflictCodeAsync(conflict, "pantry_item_duplicate");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task UnknownPropertiesAreRejectedAndIngredientRemainsImmutable()
    {
        var firstIngredient = await AddIngredientAsync("First", QuantityDimension.Mass);
        var secondIngredient = await AddIngredientAsync("Second", QuantityDimension.Mass);

        using var spoofedCreate = await SendRawJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            $$"""
            {"ingredientId":{{firstIngredient.Id}},"quantity":1,"unit":"g","userId":"victim"}
            """);
        Assert.Equal(HttpStatusCode.BadRequest, spoofedCreate.StatusCode);

        var item = await AddPantryItemAsync(
            "user-1",
            firstIngredient.Id,
            1m,
            Unit.Gram);
        using var spoofedUpdate = await SendRawJsonAsync(
            HttpMethod.Put,
            $"/api/pantry/{item.Id}",
            "user-1",
            $$"""
            {"quantity":2,"unit":"g","ingredientId":{{secondIngredient.Id}}}
            """);
        Assert.Equal(HttpStatusCode.BadRequest, spoofedUpdate.StatusCode);

        await using var context = CreateContext();
        var unchanged = await context.UserPantryItems.AsNoTracking().SingleAsync();
        Assert.Equal(firstIngredient.Id, unchanged.IngredientId);
        Assert.Equal(1m, unchanged.NormalizedQuantity);
    }

    [Theory]
    [InlineData("0.1234567", "g")]
    [InlineData("1000000000000", "g")]
    [InlineData("1000000000", "kg")]
    public async Task InvalidDecimalPrecisionOrRangeIsRejectedBeforePersistence(
        string quantity,
        string unit)
    {
        var ingredient = await AddIngredientAsync("Bounded", QuantityDimension.Mass);
        using var response = await SendRawJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            $$"""
            {"ingredientId":{{ingredient.Id}},"quantity":{{quantity}},"unit":"{{unit}}"}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = CreateContext();
        Assert.Equal(0, await context.UserPantryItems.CountAsync());
    }

    [Theory]
    [InlineData("0.1234567", "g")]
    [InlineData("1000000000000", "g")]
    [InlineData("1", "l")]
    public async Task PutRejectsInvalidQuantityOrDimensionWithoutChangingTheItem(
        string quantity,
        string unit)
    {
        var ingredient = await AddIngredientAsync("Update bounded", QuantityDimension.Mass);
        var item = await AddPantryItemAsync(
            "user-1",
            ingredient.Id,
            100m,
            Unit.Gram);
        using var response = await SendRawJsonAsync(
            HttpMethod.Put,
            $"/api/pantry/{item.Id}",
            "user-1",
            $$"""
            {"quantity":{{quantity}},"unit":"{{unit}}"}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = CreateContext();
        var unchanged = await context.UserPantryItems.AsNoTracking().SingleAsync();
        Assert.Equal(100m, unchanged.NormalizedQuantity);
        Assert.Equal(Unit.Gram, unchanged.DisplayUnit);
    }

    [Fact]
    public async Task Decimal18Scale6MaximumRoundTripsExactly()
    {
        var ingredient = await AddIngredientAsync("Boundary", QuantityDimension.Mass);
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            new
            {
                ingredientId = ingredient.Id,
                quantity = 999_999_999_999.999999m,
                unit = "g"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var context = CreateContext();
        Assert.Equal(
            999_999_999_999.999999m,
            (await context.UserPantryItems.AsNoTracking().SingleAsync()).NormalizedQuantity);
    }

    [Fact]
    public async Task CrossDimensionUnitIsRejectedWithoutGuessedConversion()
    {
        var ingredient = await AddIngredientAsync("Oil", QuantityDimension.Volume);
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "user-1",
            new { ingredientId = ingredient.Id, quantity = 1m, unit = "kg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var context = CreateContext();
        Assert.Equal(0, await context.UserPantryItems.CountAsync());
    }

    [Fact]
    public async Task UnrelatedUniqueIndexViolationIsNotMislabeledAsPantryDuplicate()
    {
        var firstIngredient = await AddIngredientAsync("First unique", QuantityDimension.Mass);
        var secondIngredient = await AddIngredientAsync("Second unique", QuantityDimension.Mass);
        await AddPantryItemAsync("first-user", firstIngredient.Id, 1m, Unit.Gram);

        await using (var context = CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                "CREATE UNIQUE INDEX [UX_Test_UserPantryItems_DisplayUnit] " +
                "ON [pantry].[UserPantryItems] ([DisplayUnit]);");
        }

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "second-user",
            new { ingredientId = secondIngredient.Id, quantity = 2m, unit = "g" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("pantry_item_duplicate", body, StringComparison.Ordinal);
        Assert.DoesNotContain("UX_Test_UserPantryItems_DisplayUnit", body, StringComparison.Ordinal);
        Assert.DoesNotContain("2601", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SupersetUniqueIndexNameIsNotMislabeledAsPantryDuplicate()
    {
        var firstIngredient = await AddIngredientAsync("First superset", QuantityDimension.Mass);
        var secondIngredient = await AddIngredientAsync("Second superset", QuantityDimension.Mass);
        await AddPantryItemAsync("first-user", firstIngredient.Id, 1m, Unit.Gram);
        const string indexName =
            "IX_UserPantryItems_UserId_IngredientId_Secondary";

        await using (var context = CreateContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                $"CREATE UNIQUE INDEX [{indexName}] " +
                "ON [pantry].[UserPantryItems] ([DisplayUnit]);");
        }

        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/pantry",
            "second-user",
            new { ingredientId = secondIngredient.Id, quantity = 2m, unit = "g" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("pantry_item_duplicate", body, StringComparison.Ordinal);
        Assert.DoesNotContain(indexName, body, StringComparison.Ordinal);
        Assert.DoesNotContain("2601", body, StringComparison.Ordinal);
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

    private async Task<UserPantryItem> AddPantryItemAsync(
        string userId,
        long ingredientId,
        decimal normalizedQuantity,
        Unit displayUnit)
    {
        await using var context = CreateContext();
        var item = new UserPantryItem(
            userId,
            ingredientId,
            normalizedQuantity,
            displayUnit);
        context.UserPantryItems.Add(item);
        await context.SaveChangesAsync();
        return item;
    }

    private async Task SetIngredientActiveAsync(long ingredientId, bool isActive)
    {
        await using var context = CreateContext();
        await context.Ingredients
            .Where(ingredient => ingredient.Id == ingredientId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                ingredient => ingredient.IsActive,
                isActive));
    }

    private Task<HttpResponseMessage> SendJsonAsync<T>(
        HttpMethod method,
        string path,
        string userId,
        T body) =>
        SendAsync(method, path, userId, JsonContent.Create(body));

    private Task<HttpResponseMessage> SendRawJsonAsync(
        HttpMethod method,
        string path,
        string userId,
        string body) =>
        SendAsync(
            method,
            path,
            userId,
            new StringContent(body, Encoding.UTF8, "application/json"));

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string userId,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        request.Headers.TryAddWithoutValidation(
            TestAuthenticationHandler.UserIdHeaderName,
            userId);
        return await Client.SendAsync(request);
    }

    private static async Task AssertSameNotFoundAsync(
        HttpResponseMessage missing,
        HttpResponseMessage foreign)
    {
        using (missing)
        using (foreign)
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            using var missingDocument = JsonDocument.Parse(
                await missing.Content.ReadAsStringAsync());
            using var foreignDocument = JsonDocument.Parse(
                await foreign.Content.ReadAsStringAsync());

            foreach (var property in new[] { "status", "title", "detail" })
            {
                Assert.Equal(
                    missingDocument.RootElement.GetProperty(property).ToString(),
                    foreignDocument.RootElement.GetProperty(property).ToString());
            }
        }
    }

    private static async Task AssertConflictCodeAsync(
        HttpResponseMessage response,
        string expectedCode)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    private sealed class CommandCaptureInterceptor : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<CapturedCommand> _commands = new();

        internal IReadOnlyCollection<CapturedCommand> Commands =>
            _commands.ToArray();

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
