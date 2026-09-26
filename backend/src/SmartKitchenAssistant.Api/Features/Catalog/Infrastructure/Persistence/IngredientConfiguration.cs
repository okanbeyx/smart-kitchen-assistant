using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;

namespace SmartKitchenAssistant.Api.Features.Catalog.Infrastructure.Persistence;

internal sealed class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("Ingredients", "catalog", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_Ingredients_QuantityDimension",
                "[QuantityDimension] IN (N'Mass', N'Volume', N'Count')"));

        builder.HasKey(ingredient => ingredient.Id);

        builder.Property(ingredient => ingredient.Id)
            .UseIdentityColumn();

        builder.Property(ingredient => ingredient.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ingredient => ingredient.QuantityDimension)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(ingredient => ingredient.IsActive)
            .IsRequired();
    }
}
