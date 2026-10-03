using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal sealed class StockConsumptionItemConfiguration
    : IEntityTypeConfiguration<StockConsumptionItem>
{
    public void Configure(EntityTypeBuilder<StockConsumptionItem> builder)
    {
        builder.ToTable("StockConsumptionItems", "pantry", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_StockConsumptionItems_ConsumedNormalizedQuantity_Positive",
                "[ConsumedNormalizedQuantity] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_StockConsumptionItems_QuantityDimension",
                "[QuantityDimension] IN (N'Mass', N'Volume', N'Count')");
        });

        builder.HasKey(item => new
        {
            item.StockConsumptionId,
            item.IngredientId
        });

        builder.Property(item => item.ConsumedNormalizedQuantity)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(item => item.QuantityDimension)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(item => item.IngredientId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
