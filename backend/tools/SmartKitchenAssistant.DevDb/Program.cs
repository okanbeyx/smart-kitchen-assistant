using Microsoft.Extensions.Configuration;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.DevDb;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            if (args.Length == 0 || args[0] is not ("status" or "migrate" or "seed" or "verify" or "reset") ||
                (args[0] == "reset" ? args.Length != 2 : args.Length != 1))
            {
                Console.Error.WriteLine("Usage: DevDb status|migrate|seed|verify|reset SmartKitchenAssistantDevelopment");
                return 2;
            }

            DevelopmentDatabase.ValidateEnvironment(
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"),
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            DevelopmentDatabase.RejectConnectionOverrides(Environment.GetEnvironmentVariables().Keys.Cast<string>());
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(SmartKitchenDbContext).Assembly, optional: true)
                .Build();
            using var configurationLifetime = configuration as IDisposable;
            var database = new DevelopmentDatabase(configuration.GetConnectionString("SmartKitchen"));
            var token = cancellation.Token;

            switch (args[0])
            {
                case "status":
                    Console.WriteLine(await database.StatusAsync(token));
                    break;
                case "migrate":
                    await database.MigrateAsync(token);
                    Console.WriteLine("Development migrations applied; no seed was run.");
                    break;
                case "seed":
                    await using (var context = await database.OpenReadyContextAsync(token))
                    {
                        var created = await DevelopmentSeed.SeedAsync(context, token);
                        Console.WriteLine(created ? "Canonical development seed created." : "Canonical seed unchanged (no-op).");
                    }
                    break;
                case "verify":
                    await using (var context = await database.OpenReadyContextAsync(token))
                    {
                        var manifest = await DevelopmentSeed.VerifyAsync(context, token);
                        Console.WriteLine("Database target, migrations and canonical seed verified.");
                        Console.WriteLine($"Draft fixture ID: {manifest.Recipes["draft"]}");
                    }
                    break;
                case "reset":
                    await database.ResetAsync(args[1], token);
                    await database.MigrateAsync(token);
                    await using (var context = await database.OpenReadyContextAsync(token))
                    {
                        await DevelopmentSeed.SeedAsync(context, token);
                        await DevelopmentSeed.VerifyAsync(context, token);
                    }
                    Console.WriteLine("Development database reset, migrated, seeded and verified.");
                    break;
            }

            return 0;
        }
        catch (DevelopmentDatabaseException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled. Run Status/Verify before continuing.");
            return 1;
        }
        catch (Exception)
        {
            // Configuration, SQL and provider exception text can include local secrets.
            Console.Error.WriteLine("Development database operation failed. No automatic repair was attempted. Check local configuration/runtime, then run Status/Verify. Raw exception details are suppressed.");
            return 1;
        }
    }
}
