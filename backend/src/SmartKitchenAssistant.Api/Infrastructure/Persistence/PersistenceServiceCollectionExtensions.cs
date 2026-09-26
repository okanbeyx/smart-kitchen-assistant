using Microsoft.EntityFrameworkCore;

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SmartKitchen");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:SmartKitchen' is required.");
        }

        services.AddDbContext<SmartKitchenDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
