using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

[Collection(SqlServerContainerCollection.Name)]
public sealed class UserPantryItemSqlServerTests(SqlServerContainerFixture fixture)
{
    [Fact]
    public async Task ExactUserIdentifiersRemainDistinctAndAreQueriedExactly()
    {
        var connectionString = fixture.CreateDatabaseConnectionString();

        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var ingredient = new Ingredient("Exact identity ingredient", QuantityDimension.Mass);
            context.Ingredients.Add(ingredient);
            await context.SaveChangesAsync();

            string[] userIds =
            [
                "User-A",
                "user-a",
                "user",
                "user ",
                " user",
                "üser",
                "u\u0308ser",
                "ı-user",
                "I-user",
                "é-user",
                "e\u0301-user"
            ];

            context.UserPantryItems.AddRange(userIds.Select(userId =>
                new UserPantryItem(userId, ingredient.Id, 1m, Unit.Gram)));
            await context.SaveChangesAsync();

            foreach (var userId in userIds)
            {
                var matches = await context.UserPantryItems
                    .AsNoTracking()
                    .Where(item => item.UserId == userId)
                    .Select(item => item.UserId)
                    .ToArrayAsync();

                Assert.Equal([userId], matches);
            }
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task SameExactUserAndIngredientViolatesUniqueIndex()
    {
        var connectionString = fixture.CreateDatabaseConnectionString();

        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var ingredient = new Ingredient("Unique identity ingredient", QuantityDimension.Mass);
            context.Ingredients.Add(ingredient);
            await context.SaveChangesAsync();

            context.UserPantryItems.Add(
                new UserPantryItem("exact-user", ingredient.Id, 1m, Unit.Gram));
            await context.SaveChangesAsync();

            context.UserPantryItems.Add(
                new UserPantryItem("exact-user", ingredient.Id, 2m, Unit.Kilogram));

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    [Fact]
    public async Task DifferentExactUsersMayReferenceTheSameIngredient()
    {
        var connectionString = fixture.CreateDatabaseConnectionString();

        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var ingredient = new Ingredient("Shared ingredient", QuantityDimension.Mass);
            context.Ingredients.Add(ingredient);
            await context.SaveChangesAsync();

            context.UserPantryItems.AddRange(
                new UserPantryItem("same", ingredient.Id, 1m, Unit.Gram),
                new UserPantryItem("same ", ingredient.Id, 1m, Unit.Gram));

            await context.SaveChangesAsync();

            Assert.Equal(
                2,
                await context.UserPantryItems.CountAsync(
                    item => item.IngredientId == ingredient.Id));
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static SmartKitchenDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SmartKitchenDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new SmartKitchenDbContext(options);
    }

    private static async Task DeleteDatabaseAsync(string connectionString)
    {
        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }
}
