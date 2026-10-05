using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;
using SmartKitchenAssistant.DevDb;

namespace SmartKitchenAssistant.Api.Tests.Development;

public sealed class DevelopmentDatabaseGuardTests
{
    private static string LocalConnection => new SqlConnectionStringBuilder
    {
        DataSource = DevelopmentDatabase.Server,
        InitialCatalog = DevelopmentDatabase.DatabaseName,
        UserID = "sa",
        Password = "test-only-placeholder",
        Encrypt = SqlConnectionEncryptOption.Mandatory,
        TrustServerCertificate = true,
        PersistSecurityInfo = false
    }.ConnectionString;

    [Theory]
    [InlineData(null, null)]
    [InlineData("Development", null)]
    [InlineData(null, "Development")]
    [InlineData("Production", "Development")]
    [InlineData("Development", "Production")]
    [InlineData("Staging", "Staging")]
    [InlineData("development", "Development")]
    public void RequiresBothExplicitDevelopmentEnvironments(string? dotnet, string? aspnet)
    {
        Assert.Throws<DevelopmentDatabaseException>(() => DevelopmentDatabase.ValidateEnvironment(dotnet, aspnet));
    }

    [Fact]
    public void AcceptsOnlyDocumentedLocalConfiguration()
    {
        DevelopmentDatabase.ValidateEnvironment("Development", "Development");
        DevelopmentDatabase.ValidateConnectionString(LocalConnection);
        DevelopmentDatabase.ValidateResetConfirmation(DevelopmentDatabase.DatabaseName);
        DevelopmentDatabase.RejectConnectionOverrides(["PATH", "DOTNET_ENVIRONMENT"]);
    }

    [Fact]
    public void DevelopmentToolDoesNotRequireSchemaChanges()
    {
        using var context = new SmartKitchenDbContext(new DbContextOptionsBuilder<SmartKitchenDbContext>()
            .UseSqlServer(LocalConnection).Options);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("Server", "tcp:db.example.invalid,14330")]
    [InlineData("Server", "tcp:127.0.0.1,1433")]
    [InlineData("Server", "localhost,14330")]
    [InlineData("Database", "master")]
    [InlineData("Database", "SmartKitchenAssistantProduction")]
    [InlineData("User ID", "other")]
    [InlineData("Password", "")]
    [InlineData("Encrypt", "False")]
    [InlineData("TrustServerCertificate", "False")]
    [InlineData("Persist Security Info", "True")]
    [InlineData("Failover Partner", "db.example.invalid")]
    [InlineData("AttachDbFilename", "unapproved.mdf")]
    [InlineData("Integrated Security", "True")]
    public void RejectsWrongTargetsAndRoutingOptions(string key, string value)
    {
        var connection = new SqlConnectionStringBuilder(LocalConnection) { [key] = value };
        var exception = Assert.Throws<DevelopmentDatabaseException>(() =>
            DevelopmentDatabase.ValidateConnectionString(connection.ConnectionString));
        Assert.DoesNotContain("test-only-placeholder", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(value == "" ? "Password=" : "db.example.invalid", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-connection-string")]
    public void MissingOrMalformedConfigurationFailsWithoutSql(string? value)
    {
        Assert.Throws<DevelopmentDatabaseException>(() => new DevelopmentDatabase(value));
    }

    [Theory]
    [InlineData("ConnectionStrings__SmartKitchen")]
    [InlineData("connectionstrings:smartkitchen")]
    [InlineData("SQLCONNSTR_SmartKitchen")]
    [InlineData("SQLAZURECONNSTR_SmartKitchen")]
    [InlineData("CUSTOMCONNSTR_SmartKitchen")]
    public void RejectsEnvironmentConnectionOverrides(string name)
    {
        Assert.Throws<DevelopmentDatabaseException>(() => DevelopmentDatabase.RejectConnectionOverrides([name]));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SmartKitchenAssistantDevelopment ")]
    [InlineData("smartkitchenassistantdevelopment")]
    [InlineData("master")]
    public async Task ResetRejectsIncorrectConfirmationBeforeAnyConnection(string? confirmation)
    {
        var database = new DevelopmentDatabase(LocalConnection);
        await Assert.ThrowsAsync<DevelopmentDatabaseException>(() => database.ResetAsync(confirmation!, default));
    }
}
