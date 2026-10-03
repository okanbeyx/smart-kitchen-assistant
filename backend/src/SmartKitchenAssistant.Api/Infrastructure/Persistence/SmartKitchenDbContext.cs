using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Infrastructure.Persistence;

public sealed class SmartKitchenDbContext(DbContextOptions<SmartKitchenDbContext> options)
    : DbContext(options)
{
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    public DbSet<UserPantryItem> UserPantryItems => Set<UserPantryItem>();

    public DbSet<StockConsumption> StockConsumptions => Set<StockConsumption>();

    public DbSet<StockConsumptionItem> StockConsumptionItems =>
        Set<StockConsumptionItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartKitchenDbContext).Assembly);
    }
}
