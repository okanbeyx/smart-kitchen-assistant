using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;

internal sealed class RecipeConfiguration : IEntityTypeConfiguration<Recipe>
{
    public void Configure(EntityTypeBuilder<Recipe> builder)
    {
        builder.ToTable("Recipes", "recipes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_Recipes_BaseServings_Positive",
                "[BaseServings] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_Recipes_Status",
                "[Status] IN (N'Draft', N'Published')");
        });

        builder.HasKey(recipe => recipe.Id);

        builder.Property(recipe => recipe.Id)
            .UseIdentityColumn();

        builder.Property(recipe => recipe.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(recipe => recipe.BaseServings)
            .IsRequired();

        builder.Property(recipe => recipe.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();
    }
}
