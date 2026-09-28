using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

[Collection(SqlServerContainerCollection.Name)]
public sealed class UserPantryItemMigrationTests(SqlServerContainerFixture fixture)
{
    private const string InitialMigration = "20260928113758_InitialDomainModel";

    [Fact]
    public async Task UpAndDownPreserveExactUserIdentifierValues()
    {
        var connectionString = fixture.CreateDatabaseConnectionString();
        string[] expectedUserIds =
        [
            "user",
            "User",
            "user ",
            " user",
            "üser",
            "ı-user",
            "I-user",
            "é-user",
            "e\u0301-user"
        ];

        try
        {
            await using (var context = CreateContext(connectionString))
            {
                var migrator = context.GetService<IMigrator>();
                await migrator.MigrateAsync(InitialMigration);
                await InsertInitialModelRowsAsync(connectionString, expectedUserIds);

                await migrator.MigrateAsync();

                var afterUp = await context.UserPantryItems
                    .AsNoTracking()
                    .OrderBy(item => item.Id)
                    .Select(item => item.UserId)
                    .ToArrayAsync();

                Assert.Equal(expectedUserIds, afterUp);

                await migrator.MigrateAsync(InitialMigration);
            }

            var afterDown = await ReadTextUserIdsAsync(connectionString);
            Assert.Equal(expectedUserIds, afterDown);
        }
        finally
        {
            await DeleteDatabaseAsync(connectionString);
        }
    }

    private static async Task InsertInitialModelRowsAsync(
        string connectionString,
        IEnumerable<string> userIds)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        var sequence = 0;

        foreach (var userId in userIds)
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO [catalog].[Ingredients]
                    ([Name], [QuantityDimension], [IsActive])
                VALUES
                    (@name, N'Mass', CAST(1 AS bit));

                DECLARE @ingredientId bigint = SCOPE_IDENTITY();

                INSERT INTO [pantry].[UserPantryItems]
                    ([UserId], [IngredientId], [NormalizedQuantity], [DisplayUnit])
                VALUES
                    (@userId, @ingredientId, 1, N'Gram');
                """;
            command.Parameters.Add(
                new SqlParameter("@name", SqlDbType.NVarChar, 200)
                {
                    Value = $"Migration ingredient {sequence++}"
                });
            command.Parameters.Add(
                new SqlParameter("@userId", SqlDbType.NVarChar, 256)
                {
                    Value = userId
                });

            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string[]> ReadTextUserIdsAsync(string connectionString)
    {
        var userIds = new List<string>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT [UserId] FROM [pantry].[UserPantryItems] ORDER BY [Id];";
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            userIds.Add(reader.GetString(0));
        }

        return [.. userIds];
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
