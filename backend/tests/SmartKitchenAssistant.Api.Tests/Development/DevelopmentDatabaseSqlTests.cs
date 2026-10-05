using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;
using SmartKitchenAssistant.DevDb;

namespace SmartKitchenAssistant.Api.Tests.Development;

[Collection(SqlServerContainerCollection.Name)]
public sealed class DevelopmentDatabaseSqlTests(SqlServerContainerFixture fixture) : IAsyncLifetime
{
    private readonly string _connectionString = fixture.CreateDatabaseConnectionString();

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await DevelopmentDatabase.AddPropertyAsync(context, DevelopmentDatabase.MarkerName, DevelopmentDatabase.MarkerValue, default);
    }

    public async Task DisposeAsync()
    {
        // Only the existing Testcontainers fixture's unique database is used here.
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task SeedUsesRealSqlConstraintsAndPublishedGraphWithNormalizedQuantities()
    {
        await using var context = CreateContext();
        await DevelopmentDatabase.CheckMigrationHistoryAsync(context, false, default);
        Assert.True(await DevelopmentSeed.SeedAsync(context));
        var manifest = await DevelopmentSeed.VerifyAsync(context);
        Assert.Equal(7, manifest.Ingredients.Count);
        Assert.Equal(3, manifest.Recipes.Count);
        Assert.Equal(6, await context.Ingredients.CountAsync(value => value.IsActive));
        Assert.Equal(2, await context.Recipes.CountAsync(value => value.Status == RecipeStatus.Published));
        Assert.Empty(await context.UserPantryItems.ToArrayAsync());
        Assert.Empty(await context.StockConsumptions.ToArrayAsync());
        var tomato = await context.RecipeIngredients.AsNoTracking().SingleAsync(value => value.Id == manifest.Lines["tomato-eggs/1"]);
        Assert.Equal(250m, tomato.NormalizedQuantity);
        Assert.Equal(Unit.Kilogram, tomato.DisplayUnit);
        var milk = await context.RecipeIngredients.AsNoTracking().SingleAsync(value => value.Id == manifest.Lines["lentil-soup/2"]);
        Assert.Equal(500m, milk.NormalizedQuantity);
        Assert.Equal(Unit.Liter, milk.DisplayUnit);
        Assert.Equal(5, await context.RecipeSteps.CountAsync());

        await using var factory = new TestWebApplicationFactory(_connectionString);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/ingredients?search=Domates")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/recipes/{manifest.Recipes["tomato-eggs"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{manifest.Recipes["draft"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pantry")).StatusCode);
    }

    [Fact]
    public async Task RepeatedSeedIsNoOpAndCoexistsWithUnrelatedDataAndReferences()
    {
        await using var context = CreateContext();
        var tomato1 = new Ingredient("Domates", QuantityDimension.Mass);
        var tomato2 = new Ingredient("Domates", QuantityDimension.Mass);
        var recipe = new Recipe("Domatesli Yumurta", 3);
        context.AddRange(tomato1, tomato2, recipe);
        await context.SaveChangesAsync();
        Assert.True(await DevelopmentSeed.SeedAsync(context));
        var manifest = await DevelopmentSeed.VerifyAsync(context);
        Assert.NotEqual(1, manifest.Ingredients["tomato"]);
        context.RecipeIngredients.Add(new RecipeIngredient(recipe.Id, manifest.Ingredients["tomato"], 1, false, 50m, Unit.Gram));
        context.UserPantryItems.Add(new UserPantryItem("fixture-user", tomato1.Id, 100m, Unit.Gram));
        context.Recipes.Add(new Recipe("Another developer recipe", 1));
        await context.SaveChangesAsync();
        var before = await SnapshotAsync(context);
        await using (var second = CreateContext())
            Assert.False(await DevelopmentSeed.SeedAsync(second));
        Assert.Equal(before, await SnapshotAsync(context));
    }

    [Theory]
    [InlineData("rename")]
    [InlineData("delete-step")]
    [InlineData("quantity")]
    [InlineData("duplicate")]
    [InlineData("lowercase-prefix")]
    [InlineData("extra-step")]
    [InlineData("malformed-manifest")]
    [InlineData("missing-manifest")]
    [InlineData("wrong-version")]
    [InlineData("duplicate-id")]
    public async Task SeedDriftFailsWithoutChangingAnyData(string mutation)
    {
        await using var context = CreateContext();
        await DevelopmentSeed.SeedAsync(context);
        var manifest = await DevelopmentSeed.VerifyAsync(context);
        switch (mutation)
        {
            case "rename":
                await context.Ingredients.Where(value => value.Id == manifest.Ingredients["tomato"])
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Name, "Developer renamed the seed"));
                break;
            case "delete-step":
                await context.RecipeSteps.Where(value => value.Id == manifest.Steps["tomato-eggs/1"]).ExecuteDeleteAsync();
                break;
            case "quantity":
                await context.RecipeIngredients.Where(value => value.Id == manifest.Lines["tomato-eggs/1"])
                    .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.NormalizedQuantity, 123m));
                break;
            case "duplicate":
            case "lowercase-prefix":
                context.Ingredients.Add(new Ingredient(
                    mutation == "duplicate" ? "[SKA-DEV-SEED] Domates" : "[ska-dev-seed] Domates", QuantityDimension.Mass));
                await context.SaveChangesAsync();
                break;
            case "extra-step":
                context.RecipeSteps.Add(new RecipeStep(manifest.Recipes["tomato-eggs"], 99, "Unapproved seed edit"));
                await context.SaveChangesAsync();
                break;
            case "missing-manifest":
                await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_dropextendedproperty @name={DevelopmentSeed.ManifestName};");
                break;
            default:
                var json = "not-json";
                if (mutation == "wrong-version") json = JsonSerializer.Serialize(manifest with { Version = 99 });
                if (mutation == "duplicate-id")
                {
                    manifest.Ingredients["egg"] = manifest.Ingredients["tomato"];
                    json = JsonSerializer.Serialize(manifest);
                }
                await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_updateextendedproperty @name={DevelopmentSeed.ManifestName}, @value={json};");
                break;
        }
        var before = await SnapshotAsync(context);
        await using (var retry = CreateContext())
            await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => DevelopmentSeed.SeedAsync(retry));
        Assert.Equal(before, await SnapshotAsync(context));
    }

    [Fact]
    public async Task FirstSeedRefusesToAdoptReservedNameWithoutManifest()
    {
        await using var context = CreateContext();
        context.Ingredients.Add(new Ingredient("[SKA-DEV-SEED] Domates", QuantityDimension.Mass));
        await context.SaveChangesAsync();
        var before = await SnapshotAsync(context);
        await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => DevelopmentSeed.SeedAsync(context));
        Assert.Equal(before, await SnapshotAsync(context));
    }

    [Fact]
    public async Task MarkerlessDatabaseIsNotAdopted()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_dropextendedproperty @name={DevelopmentDatabase.MarkerName};");
        var before = await SnapshotAsync(context);
        await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => DevelopmentDatabase.RequireMarkerAsync(context, default));
        await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => DevelopmentSeed.SeedAsync(context));
        Assert.Equal(before, await SnapshotAsync(context));
        Assert.Null(await DevelopmentDatabase.ReadPropertyAsync(context, DevelopmentDatabase.MarkerName, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedPublicationOrMetadataWriteRollsBackEntireSeed(bool failAfterMetadata)
    {
        await using var baseline = CreateContext();
        baseline.Ingredients.Add(new Ingredient("Unrelated ingredient", QuantityDimension.Count));
        await baseline.SaveChangesAsync();
        var before = await SnapshotAsync(baseline);
        var interceptor = new SeedFailureInterceptor(failAfterMetadata);
        await using (var failing = CreateContext(interceptor))
        {
            if (failAfterMetadata)
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => DevelopmentSeed.SeedAsync(failing));
                Assert.Same(interceptor.MetadataFailure, exception);
            }
            else
                await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => DevelopmentSeed.SeedAsync(failing));
            Assert.True(interceptor.FailureTriggered);
        }
        Assert.Equal(before, await SnapshotAsync(baseline));
        Assert.True(await DevelopmentSeed.SeedAsync(baseline));
        await DevelopmentSeed.VerifyAsync(baseline);
    }

    [Fact]
    public async Task ConcurrentSeedsCannotCommitDuplicateGraphs()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var outcomes = await Task.WhenAll(AttemptAsync(), AttemptAsync());
        Assert.Single(outcomes, created => created);
        await using var context = CreateContext();
        await DevelopmentSeed.VerifyAsync(context);
        Assert.Equal(7, await context.Ingredients.CountAsync());
        Assert.Equal(3, await context.Recipes.CountAsync());
        Assert.Equal(8, await context.RecipeIngredients.CountAsync());
        Assert.Equal(5, await context.RecipeSteps.CountAsync());
        var before = await SnapshotAsync(context);
        await using (var subsequent = CreateContext())
            Assert.False(await DevelopmentSeed.SeedAsync(subsequent));
        Assert.Equal(before, await SnapshotAsync(context));

        async Task<bool> AttemptAsync()
        {
            await using var attempt = CreateContext();
            try { return await DevelopmentSeed.SeedAsync(attempt, timeout.Token); }
            // EF may wrap a deadlock in InvalidOperationException/DbUpdateException.
            // Only SQL Server's rolled-back deadlock victim is an accepted failure.
            catch (Exception exception) when (exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 1205 })
            {
                return false;
            }
        }
    }

    [Fact]
    public async Task RecreatedFixtureDatabaseCanBeMigratedAndReseededWithoutStableIds()
    {
        await using (var first = CreateContext())
        {
            await DevelopmentSeed.SeedAsync(first);
            await first.Database.EnsureDeletedAsync();
        }
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await DevelopmentDatabase.AddPropertyAsync(context, DevelopmentDatabase.MarkerName, DevelopmentDatabase.MarkerValue, default);
        context.Ingredients.Add(new Ingredient("Developer created first", QuantityDimension.Count));
        await context.SaveChangesAsync();
        await DevelopmentSeed.SeedAsync(context);
        var manifest = await DevelopmentSeed.VerifyAsync(context);
        Assert.NotEqual(1, manifest.Ingredients["tomato"]);
        await context.Database.MigrateAsync();
        await DevelopmentDatabase.CheckMigrationHistoryAsync(context, false, default);
        Assert.False(await DevelopmentSeed.SeedAsync(context));
    }

    private SmartKitchenDbContext CreateContext(DbCommandInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<SmartKitchenDbContext>().UseSqlServer(_connectionString);
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new SmartKitchenDbContext(options.Options);
    }

    private static async Task<string> SnapshotAsync(SmartKitchenDbContext context) => JsonSerializer.Serialize(new
    {
        Ingredients = await context.Ingredients.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(),
        Recipes = await context.Recipes.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(),
        Lines = await context.RecipeIngredients.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(),
        Steps = await context.RecipeSteps.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(),
        Pantry = await context.UserPantryItems.AsNoTracking().OrderBy(value => value.Id).ToArrayAsync(),
        Manifest = await DevelopmentDatabase.ReadPropertyAsync(context, DevelopmentSeed.ManifestName, default)
    });

    private sealed class SeedFailureInterceptor(bool failAfterMetadata) : DbCommandInterceptor
    {
        public bool FailureTriggered { get; private set; }
        public InvalidOperationException MetadataFailure { get; } = new("Injected failure after manifest write.");

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!failAfterMetadata && command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal))
            {
                FailureTriggered = true;
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
            }
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (failAfterMetadata && command.CommandText.Contains("sp_addextendedproperty", StringComparison.Ordinal))
            {
                FailureTriggered = true;
                throw MetadataFailure;
            }
            return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
        }
    }
}
