using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Features.Catalog.Api;

[Collection(SqlServerContainerCollection.Name)]
public sealed class IngredientEndpointTests(SqlServerContainerFixture fixture)
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
    public async Task IngredientsAreAnonymousWhilePantryRemainsAuthenticated()
    {
        using var ingredientsResponse = await Client.GetAsync("/api/ingredients");
        using var pantryResponse = await Client.GetAsync("/api/pantry");

        Assert.Equal(HttpStatusCode.OK, ingredientsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, pantryResponse.StatusCode);
    }

    [Fact]
    public async Task ListReturnsOnlyActiveIngredientsInNameThenIdOrderWithExactContract()
    {
        await AddIngredientAsync("Aardvark", QuantityDimension.Count, isActive: false);
        var apple = await AddIngredientAsync("Apple", QuantityDimension.Count);
        var firstMilk = await AddIngredientAsync("Milk", QuantityDimension.Volume);
        var secondMilk = await AddIngredientAsync("Milk", QuantityDimension.Mass);
        _commandInterceptor.Clear();

        using var response = await Client.GetAsync("/api/ingredients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ingredients = document.RootElement.EnumerateArray().ToArray();
        Assert.Equal(3, ingredients.Length);
        AssertIngredient(ingredients[0], apple.Id, "Apple", "Count");
        AssertIngredient(ingredients[1], firstMilk.Id, "Milk", "Volume");
        AssertIngredient(ingredients[2], secondMilk.Id, "Milk", "Mass");
        Assert.Single(_commandInterceptor.SelectCommands);
    }

    [Fact]
    public async Task SearchFiltersActiveIngredientsBeforeMaterialization()
    {
        var chocolate = await AddIngredientAsync(
            "milk chocolate",
            QuantityDimension.Mass);
        var powder = await AddIngredientAsync("milk powder", QuantityDimension.Mass);
        await AddIngredientAsync("milk secret", QuantityDimension.Mass, isActive: false);
        await AddIngredientAsync("bread", QuantityDimension.Count);

        using var response = await Client.GetAsync("/api/ingredients?search=milk");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            [chocolate.Id, powder.Id],
            document.RootElement.EnumerateArray()
                .Select(ingredient => ingredient.GetProperty("id").GetInt64())
                .ToArray());
    }

    [Fact]
    public async Task EmptyAndWhitespaceSearchBehaveLikeNoFilter()
    {
        var bread = await AddIngredientAsync("bread", QuantityDimension.Count);
        var milk = await AddIngredientAsync("milk", QuantityDimension.Volume);
        long[] expected = [bread.Id, milk.Id];

        foreach (var path in new[]
                 {
                     "/api/ingredients",
                     "/api/ingredients?search=",
                     "/api/ingredients?search=%20%20%20"
                 })
        {
            using var response = await Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, await ReadIdsAsync(response));
        }
    }

    [Fact]
    public async Task NonEmptySearchIsTrimmedAndNoMatchReturnsEmptyArray()
    {
        var milk = await AddIngredientAsync("milk", QuantityDimension.Volume);
        await AddIngredientAsync("bread", QuantityDimension.Count);

        using var trimmedResponse = await Client.GetAsync(
            "/api/ingredients?search=%20%20milk%20%20");
        using var missingResponse = await Client.GetAsync(
            "/api/ingredients?search=missing");

        Assert.Equal(HttpStatusCode.OK, trimmedResponse.StatusCode);
        Assert.Equal([milk.Id], await ReadIdsAsync(trimmedResponse));
        Assert.Equal(HttpStatusCode.OK, missingResponse.StatusCode);
        using var document = JsonDocument.Parse(
            await missingResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Theory]
    [InlineData("%", "100% cocoa", "100 cocoa")]
    [InlineData("_", "grade_a egg", "grade-a egg")]
    public async Task SqlWildcardCharactersAreTreatedAsLiteralSearchText(
        string search,
        string matchingName,
        string nonMatchingName)
    {
        var match = await AddIngredientAsync(matchingName, QuantityDimension.Count);
        await AddIngredientAsync(nonMatchingName, QuantityDimension.Count);
        var encodedSearch = Uri.EscapeDataString(search);

        using var response = await Client.GetAsync(
            $"/api/ingredients?search={encodedSearch}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([match.Id], await ReadIdsAsync(response));
    }

    [Fact]
    public async Task SearchUsesTheEffectiveSqlServerCollation()
    {
        const string storedName = "MilkCaseProbe";
        const string search = "milkcaseprobe";
        var ingredient = await AddIngredientAsync(storedName, QuantityDimension.Volume);
        var sqlServerMatches = await SqlServerContainsAsync(storedName, search);

        using var response = await Client.GetAsync(
            $"/api/ingredients?search={search}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ids = await ReadIdsAsync(response);
        Assert.Equal(sqlServerMatches ? [ingredient.Id] : [], ids);
    }

    [Fact]
    public async Task SearchExecutesOneParameterizedSelectWithSqlFiltersAndOrdering()
    {
        await AddIngredientAsync("hay needle stack", QuantityDimension.Mass);
        await AddIngredientAsync("needle inactive", QuantityDimension.Mass, isActive: false);
        await AddIngredientAsync("unrelated", QuantityDimension.Mass);
        _commandInterceptor.Clear();

        using var response = await Client.GetAsync(
            "/api/ingredients?search=%20needle%20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var command = Assert.Single(_commandInterceptor.SelectCommands);
        Assert.Contains("[catalog].[Ingredients]", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("WHERE", command.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[IsActive]", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("[Name]", command.CommandText, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", command.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("needle", command.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            command.ParameterValues,
            value => value is string parameter &&
                     parameter.Contains("needle", StringComparison.Ordinal));
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

    private async Task<bool> SqlServerContainsAsync(string name, string search)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT CASE WHEN CHARINDEX(@search, @name) > 0 " +
            "THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;";
        command.Parameters.Add(new SqlParameter("@search", SqlDbType.NVarChar, 200)
        {
            Value = search
        });
        command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 200)
        {
            Value = name
        });

        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<long[]> ReadIdsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.EnumerateArray()
            .Select(ingredient => ingredient.GetProperty("id").GetInt64())
            .ToArray();
    }

    private static void AssertIngredient(
        JsonElement ingredient,
        long id,
        string name,
        string quantityDimension)
    {
        Assert.Equal(id, ingredient.GetProperty("id").GetInt64());
        Assert.Equal(name, ingredient.GetProperty("name").GetString());
        Assert.Equal(
            quantityDimension,
            ingredient.GetProperty("quantityDimension").GetString());
        Assert.Equal(
            new[] { "id", "name", "quantityDimension" }.Order(StringComparer.Ordinal),
            ingredient.EnumerateObject()
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
                .Select(parameter => parameter.Value)
                .ToArray();
            _commands.Enqueue(new CapturedCommand(command.CommandText, parameterValues));
        }
    }

    private sealed record CapturedCommand(
        string CommandText,
        IReadOnlyList<object?> ParameterValues);
}
