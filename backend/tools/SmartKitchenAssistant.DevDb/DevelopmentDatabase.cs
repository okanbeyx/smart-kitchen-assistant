using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.DevDb;

internal sealed class DevelopmentDatabase
{
    internal const string DatabaseName = "SmartKitchenAssistantDevelopment";
    internal const string Server = "tcp:127.0.0.1,14330";
    internal const string Hostname = "ska-development-sql";
    internal const string MarkerName = "SmartKitchenAssistant.DevelopmentDatabase";
    internal const string MarkerValue = "issue-30:v1";
    private readonly string _connectionString;

    internal DevelopmentDatabase(string? connectionString)
    {
        ValidateConnectionString(connectionString);
        _connectionString = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 5,
            Pooling = false
        }.ConnectionString;
    }

    internal static void ValidateEnvironment(string? dotnet, string? aspnet)
    {
        if (dotnet != "Development" || aspnet != "Development")
            throw new DevelopmentDatabaseException("Both DOTNET_ENVIRONMENT and ASPNETCORE_ENVIRONMENT must explicitly be Development.");
    }

    internal static void RejectConnectionOverrides(IEnumerable<string> variableNames)
    {
        string[] forbidden = ["ConnectionStrings__SmartKitchen", "ConnectionStrings:SmartKitchen",
            "SQLCONNSTR_SmartKitchen", "SQLAZURECONNSTR_SmartKitchen", "CUSTOMCONNSTR_SmartKitchen"];
        if (variableNames.Any(name => forbidden.Contains(name, StringComparer.OrdinalIgnoreCase)))
            throw new DevelopmentDatabaseException("Remove the connection-string environment override from this shell; this tool uses the API User Secrets only.");
    }

    internal static void ValidateConnectionString(string? value)
    {
        const string error = "A local development SQL connection in API User Secrets is required; only the documented fixed target and options are accepted.";
        if (string.IsNullOrWhiteSpace(value)) throw new DevelopmentDatabaseException(error);
        try
        {
            // Reject routing options (failover/attach/SPN/etc.), rather than trusting a hostname alone.
            var raw = new DbConnectionStringBuilder { ConnectionString = value };
            string[] allowed = ["Server", "Data Source", "Database", "Initial Catalog", "User ID", "UID",
                "Password", "PWD", "Encrypt", "TrustServerCertificate", "Trust Server Certificate",
                "Persist Security Info", "Connect Timeout", "Connection Timeout"];
            if (raw.Keys.Cast<string>().Any(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)))
                throw new DevelopmentDatabaseException(error);
            var parsed = new SqlConnectionStringBuilder(value);
            if (parsed.DataSource != Server || parsed.InitialCatalog != DatabaseName ||
                parsed.UserID != "sa" || string.IsNullOrWhiteSpace(parsed.Password) ||
                parsed.PersistSecurityInfo || !parsed.TrustServerCertificate ||
                parsed.Encrypt != SqlConnectionEncryptOption.Mandatory)
                throw new DevelopmentDatabaseException(error);
        }
        catch (ArgumentException)
        {
            throw new DevelopmentDatabaseException(error);
        }
    }

    internal static void ValidateResetConfirmation(string? confirmation)
    {
        if (confirmation != DatabaseName)
            throw new DevelopmentDatabaseException("Reset requires the exact database name: SmartKitchenAssistantDevelopment. All data in that database will be lost.");
    }

    private SmartKitchenDbContext CreateContext() => new(new DbContextOptionsBuilder<SmartKitchenDbContext>()
        .UseSqlServer(_connectionString, options => options.CommandTimeout(15)).Options);

    private async Task<SqlConnection> OpenMasterAsync(CancellationToken token)
    {
        var connection = new SqlConnection(new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString);
        try
        {
            await connection.OpenAsync(token);
            using var command = connection.CreateCommand();
            command.CommandTimeout = 15;
            command.CommandText = "SELECT CONVERT(nvarchar(128), SERVERPROPERTY('MachineName'));";
            if (await command.ExecuteScalarAsync(token) is not string name || name != Hostname)
                throw new DevelopmentDatabaseException("SQL instance hostname does not match the development container.");
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> ExistsAsync(SqlConnection master, CancellationToken token)
    {
        using var command = master.CreateCommand();
        command.CommandTimeout = 15;
        command.CommandText = "SELECT DB_ID(@name);";
        command.Parameters.Add("@name", SqlDbType.NVarChar, 128).Value = DatabaseName;
        return await command.ExecuteScalarAsync(token) is not (null or DBNull);
    }

    internal async Task<string> StatusAsync(CancellationToken token)
    {
        await using var master = await OpenMasterAsync(token);
        if (!await ExistsAsync(master, token)) return "Development SQL reachable; database absent. Run Migrate explicitly.";
        await using var context = CreateContext();
        await RequireMarkerAsync(context, token);
        var pending = (await context.Database.GetPendingMigrationsAsync(token)).Count();
        var seeded = await ReadPropertyAsync(context, DevelopmentSeed.ManifestName, token) is not null;
        return $"Marked development database present; pending migrations: {pending}; seed manifest present: {seeded}. Use Verify to validate seed contents.";
    }

    internal async Task MigrateAsync(CancellationToken token)
    {
        await using var context = CreateContext();
        if (context.Database.HasPendingModelChanges())
            throw new DevelopmentDatabaseException("Model/snapshot drift detected. Stop; do not add or alter migrations as part of this workflow.");
        await using var master = await OpenMasterAsync(token);
        if (!await ExistsAsync(master, token))
        {
            using var create = master.CreateCommand();
            create.CommandTimeout = 15;
            create.CommandText = "CREATE DATABASE [SmartKitchenAssistantDevelopment];";
            await create.ExecuteNonQueryAsync(token);
            // Only this successful CREATE authorizes marking. Existing markerless databases are never adopted.
            await AddPropertyAsync(context, MarkerName, MarkerValue, token);
        }

        await RequireMarkerAsync(context, token);
        await CheckMigrationHistoryAsync(context, allowPending: true, token);
        await context.Database.MigrateAsync(token);
        await CheckMigrationHistoryAsync(context, allowPending: false, token);
    }

    internal async Task<SmartKitchenDbContext> OpenReadyContextAsync(CancellationToken token)
    {
        await using var master = await OpenMasterAsync(token);
        if (!await ExistsAsync(master, token))
            throw new DevelopmentDatabaseException("Development database absent. Run Migrate explicitly.");
        var context = CreateContext();
        try
        {
            await RequireMarkerAsync(context, token);
            await CheckMigrationHistoryAsync(context, allowPending: false, token);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    internal async Task ResetAsync(string confirmation, CancellationToken token)
    {
        ValidateResetConfirmation(confirmation);
        await using var master = await OpenMasterAsync(token);
        if (!await ExistsAsync(master, token))
            throw new DevelopmentDatabaseException("Reset refuses an absent database. Use Migrate for first creation.");
        await using (var context = CreateContext())
            await RequireMarkerAsync(context, token);

        // Fixed identifier; no force-disconnect and no arbitrary target parameter.
        using var drop = master.CreateCommand();
        drop.CommandTimeout = 15;
        drop.CommandText = """
            IF EXISTS (SELECT 1 FROM [SmartKitchenAssistantDevelopment].sys.extended_properties
                       WHERE class = 0 AND name = N'SmartKitchenAssistant.DevelopmentDatabase'
                       AND CONVERT(nvarchar(128), value) = N'issue-30:v1')
                DROP DATABASE [SmartKitchenAssistantDevelopment];
            ELSE
                THROW 50001, 'Development marker missing.', 1;
            """;
        await drop.ExecuteNonQueryAsync(token);
    }

    internal static async Task CheckMigrationHistoryAsync(SmartKitchenDbContext context, bool allowPending, CancellationToken token)
    {
        if (context.Database.HasPendingModelChanges())
            throw new DevelopmentDatabaseException("Model/snapshot drift detected. Stop and review the schema.");
        var known = context.Database.GetMigrations().ToArray();
        var applied = (await context.Database.GetAppliedMigrationsAsync(token)).ToArray();
        if (!applied.SequenceEqual(known.Take(applied.Length)) || applied.Length > known.Length ||
            (!allowPending && applied.Length != known.Length))
            throw new DevelopmentDatabaseException("Migration history is unknown, out of order or incomplete. Run Migrate only for known pending migrations.");
    }

    internal static async Task RequireMarkerAsync(SmartKitchenDbContext context, CancellationToken token)
    {
        if (await ReadPropertyAsync(context, MarkerName, token) != MarkerValue)
            throw new DevelopmentDatabaseException("Unknown or markerless database: refusing to adopt, seed, migrate or reset it.");
    }

    internal static async Task<string?> ReadPropertyAsync(SmartKitchenDbContext context, string name, CancellationToken token)
    {
        await context.Database.OpenConnectionAsync(token);
        using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandTimeout = 15;
        command.Transaction = context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = "SELECT CONVERT(nvarchar(3750), value) FROM sys.extended_properties WHERE class = 0 AND name = @name;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@name";
        parameter.Value = name;
        command.Parameters.Add(parameter);
        return await command.ExecuteScalarAsync(token) as string;
    }

    internal static async Task AddPropertyAsync(SmartKitchenDbContext context, string name, string value, CancellationToken token)
    {
        if (System.Text.Encoding.Unicode.GetByteCount(value) > 7500)
            throw new DevelopmentDatabaseException("Development metadata exceeds the supported size; no metadata was written.");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sys.sp_addextendedproperty @name={name}, @value={value};", token);
    }
}

internal sealed class DevelopmentDatabaseException(string message) : Exception(message);
