using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;

internal sealed class RecipeIngredientConfiguration : IEntityTypeConfiguration<RecipeIngredient>
{
    public void Configure(EntityTypeBuilder<RecipeIngredient> builder)
    {
        builder.ToTable("RecipeIngredients", "recipes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_RecipeIngredients_Sequence_Positive",
                "[Sequence] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_RecipeIngredients_NormalizedQuantity_Positive",
                "[NormalizedQuantity] IS NULL OR [NormalizedQuantity] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_RecipeIngredients_QuantityAndUnit_Paired",
                "([NormalizedQuantity] IS NULL AND [DisplayUnit] IS NULL) OR " +
                "([NormalizedQuantity] IS NOT NULL AND [DisplayUnit] IS NOT NULL)");
            tableBuilder.HasCheckConstraint(
                "CK_RecipeIngredients_DisplayUnit",
                "[DisplayUnit] IS NULL OR [DisplayUnit] IN " +
                "(N'Gram', N'Kilogram', N'Milliliter', N'Liter', N'Each')");
        });

        builder.HasKey(recipeIngredient => recipeIngredient.Id);

        builder.Property(recipeIngredient => recipeIngredient.Id)
            .UseIdentityColumn();

        builder.Property(recipeIngredient => recipeIngredient.NormalizedQuantity)
            .HasPrecision(18, 6);

        builder.Property(recipeIngredient => recipeIngredient.DisplayUnit)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(recipeIngredient => new
            {
                recipeIngredient.RecipeId,
                recipeIngredient.Sequence
            })
            .IsUnique();

        builder.HasOne<Recipe>()
            .WithMany()
            .HasForeignKey(recipeIngredient => recipeIngredient.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(recipeIngredient => recipeIngredient.IngredientId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
