using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Tests.Infrastructure.Persistence;

public sealed class SmartKitchenDbContextModelTests
{
    private readonly IModel _model;

    public SmartKitchenDbContextModelTests()
    {
        var options = new DbContextOptionsBuilder<SmartKitchenDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=SmartKitchenAssistantModelTests;" +
                "Integrated Security=True;TrustServerCertificate=True;")
            .Options;

        using var context = new SmartKitchenDbContext(options);
        _model = context.GetService<IDesignTimeModel>().Model;
    }

    [Fact]
    public void IngredientMappingMatchesPersistenceContract()
    {
        var entity = GetEntity<Ingredient>("Ingredients", "catalog");

        AssertPrimaryKeyGeneratedOnAdd(entity, nameof(Ingredient.Id));
        Assert.Equal(200, entity.FindProperty(nameof(Ingredient.Name))!.GetMaxLength());
        AssertEnumStringProperty<QuantityDimension>(
            entity,
            nameof(Ingredient.QuantityDimension),
            isNullable: false);
        Assert.Contains(
            "CK_Ingredients_QuantityDimension",
            entity.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.Null(entity.FindProperty("NormalizedName"));
    }

    [Fact]
    public void RecipeMappingMatchesPersistenceContract()
    {
        var entity = GetEntity<Recipe>("Recipes", "recipes");

        AssertPrimaryKeyGeneratedOnAdd(entity, nameof(Recipe.Id));
        Assert.Equal(200, entity.FindProperty(nameof(Recipe.Title))!.GetMaxLength());
        AssertEnumStringProperty<RecipeStatus>(
            entity,
            nameof(Recipe.Status),
            isNullable: false);
        AssertCheckConstraints(
            entity,
            "CK_Recipes_BaseServings_Positive",
            "CK_Recipes_Status");
    }

    [Fact]
    public void RecipeIngredientMappingMatchesPersistenceContract()
    {
        var entity = GetEntity<RecipeIngredient>("RecipeIngredients", "recipes");

        AssertPrimaryKeyGeneratedOnAdd(entity, nameof(RecipeIngredient.Id));
        AssertDecimalProperty(
            entity,
            nameof(RecipeIngredient.NormalizedQuantity),
            isNullable: true);
        AssertEnumStringProperty<Unit>(
            entity,
            nameof(RecipeIngredient.DisplayUnit),
            isNullable: true);
        AssertUniqueIndex(
            entity,
            nameof(RecipeIngredient.RecipeId),
            nameof(RecipeIngredient.Sequence));
        AssertForeignKey<Recipe>(
            entity,
            nameof(RecipeIngredient.RecipeId),
            DeleteBehavior.Cascade);
        AssertForeignKey<Ingredient>(
            entity,
            nameof(RecipeIngredient.IngredientId),
            DeleteBehavior.NoAction);
        AssertCheckConstraints(
            entity,
            "CK_RecipeIngredients_Sequence_Positive",
            "CK_RecipeIngredients_NormalizedQuantity_Positive",
            "CK_RecipeIngredients_QuantityAndUnit_Paired",
            "CK_RecipeIngredients_DisplayUnit");
    }

    [Fact]
    public void RecipeStepMappingMatchesPersistenceContract()
    {
        var entity = GetEntity<RecipeStep>("RecipeSteps", "recipes");

        AssertPrimaryKeyGeneratedOnAdd(entity, nameof(RecipeStep.Id));
        Assert.Equal(
            4000,
            entity.FindProperty(nameof(RecipeStep.Instruction))!.GetMaxLength());
        Assert.True(entity.FindProperty(nameof(RecipeStep.TimerSeconds))!.IsNullable);
        AssertUniqueIndex(
            entity,
            nameof(RecipeStep.RecipeId),
            nameof(RecipeStep.Sequence));
        AssertForeignKey<Recipe>(
            entity,
            nameof(RecipeStep.RecipeId),
            DeleteBehavior.Cascade);
        AssertCheckConstraints(
            entity,
            "CK_RecipeSteps_Sequence_Positive",
            "CK_RecipeSteps_TimerSeconds_Positive");
    }

    [Fact]
    public void UserPantryItemMappingMatchesPersistenceContract()
    {
        var entity = GetEntity<UserPantryItem>("UserPantryItems", "pantry");

        AssertPrimaryKeyGeneratedOnAdd(entity, nameof(UserPantryItem.Id));
        var userId = entity.FindProperty(nameof(UserPantryItem.UserId))!;
        Assert.False(userId.IsNullable);
        var converter = userId.GetTypeMapping().Converter;
        Assert.NotNull(converter);
        Assert.Equal(typeof(byte[]), converter.ProviderClrType);
        Assert.Equal(512, userId.GetMaxLength());
        Assert.Equal("varbinary(512)", userId.GetColumnType());

        const string exactValue = " Aü\ud800 ";
        var providerValue = Assert.IsType<byte[]>(converter.ConvertToProvider(exactValue));
        Assert.Equal(
            [0x20, 0x00, 0x41, 0x00, 0xFC, 0x00, 0x00, 0xD8, 0x20, 0x00],
            providerValue);
        Assert.Equal(exactValue, converter.ConvertFromProvider(providerValue));
        AssertDecimalProperty(
            entity,
            nameof(UserPantryItem.NormalizedQuantity),
            isNullable: false);
        AssertEnumStringProperty<Unit>(
            entity,
            nameof(UserPantryItem.DisplayUnit),
            isNullable: false);
        AssertUniqueIndex(
            entity,
            nameof(UserPantryItem.UserId),
            nameof(UserPantryItem.IngredientId));
        AssertForeignKey<Ingredient>(
            entity,
            nameof(UserPantryItem.IngredientId),
            DeleteBehavior.NoAction);
        AssertCheckConstraints(
            entity,
            "CK_UserPantryItems_NormalizedQuantity_Positive",
            "CK_UserPantryItems_DisplayUnit");
    }

    [Fact]
    public void ModelDoesNotContainDeferredOrValueObjectEntities()
    {
        Assert.Null(_model.FindEntityType(typeof(Unit)));
        Assert.DoesNotContain(
            _model.GetEntityTypes(),
            entity => entity.ClrType.Name.Contains(
                "StockConsumption",
                StringComparison.Ordinal));
        Assert.All(
            _model.GetEntityTypes().SelectMany(entity => entity.GetProperties()),
            property => Assert.False(property.IsConcurrencyToken));
    }

    private IEntityType GetEntity<TEntity>(string tableName, string schema)
    {
        var entity = _model.FindEntityType(typeof(TEntity));

        Assert.NotNull(entity);
        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(schema, entity.GetSchema());

        return entity;
    }

    private static void AssertPrimaryKeyGeneratedOnAdd(
        IEntityType entity,
        string propertyName)
    {
        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        var property = Assert.Single(primaryKey.Properties);
        Assert.Equal(propertyName, property.Name);
        Assert.Equal(ValueGenerated.OnAdd, property.ValueGenerated);
    }

    private static void AssertDecimalProperty(
        IEntityType entity,
        string propertyName,
        bool isNullable)
    {
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(isNullable, property.IsNullable);
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(6, property.GetScale());
    }

    private static void AssertEnumStringProperty<TEnum>(
        IEntityType entity,
        string propertyName,
        bool isNullable)
        where TEnum : struct, Enum
    {
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(isNullable, property.IsNullable);
        Assert.Equal(typeof(string), property.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Equal(16, property.GetMaxLength());
    }

    private static void AssertUniqueIndex(
        IEntityType entity,
        params string[] propertyNames)
    {
        var index = entity.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));

        Assert.True(index.IsUnique);
    }

    private static void AssertForeignKey<TPrincipal>(
        IEntityType entity,
        string propertyName,
        DeleteBehavior deleteBehavior)
    {
        var foreignKey = entity.GetForeignKeys().Single(foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual([propertyName]));

        Assert.Equal(typeof(TPrincipal), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
    }

    private static void AssertCheckConstraints(
        IEntityType entity,
        params string[] constraintNames)
    {
        var actualNames = entity.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(constraintNames, name => Assert.Contains(name, actualNames));
    }
}
