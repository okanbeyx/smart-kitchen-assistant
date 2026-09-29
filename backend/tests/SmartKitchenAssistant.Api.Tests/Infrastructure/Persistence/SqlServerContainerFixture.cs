using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private const string Image =
        "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";

    private MsSqlContainer? _container;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new MsSqlBuilder(Image).Build();
            await _container.StartAsync();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "SQL Server integration tests require an available Docker daemon.",
                exception);
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    public string CreateDatabaseConnectionString()
    {
        if (_container is null)
        {
            throw new InvalidOperationException("The SQL Server test container is not running.");
        }

        var builder = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"SmartKitchenAssistantTests_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerContainerCollection
    : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SQL Server container collection";
}
