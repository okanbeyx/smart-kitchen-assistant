using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence.ValueConverters;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal sealed class UserPantryItemConfiguration : IEntityTypeConfiguration<UserPantryItem>
{
    public void Configure(EntityTypeBuilder<UserPantryItem> builder)
    {
        builder.ToTable("UserPantryItems", "pantry", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_UserPantryItems_NormalizedQuantity_Positive",
                "[NormalizedQuantity] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_UserPantryItems_DisplayUnit",
                "[DisplayUnit] IN " +
                "(N'Gram', N'Kilogram', N'Milliliter', N'Liter', N'Each')");
        });

        builder.HasKey(pantryItem => pantryItem.Id);

        builder.Property(pantryItem => pantryItem.Id)
            .UseIdentityColumn();

        builder.Property(pantryItem => pantryItem.UserId)
            .HasConversion<Utf16LittleEndianStringToBytesConverter>()
            .HasColumnType("varbinary(512)")
            .HasMaxLength(Utf16LittleEndianStringToBytesConverter.MaximumByteLength)
            .IsRequired();

        builder.Property(pantryItem => pantryItem.NormalizedQuantity)
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(pantryItem => pantryItem.DisplayUnit)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(pantryItem => new
            {
                pantryItem.UserId,
                pantryItem.IngredientId
            })
            .IsUnique();

        builder.HasOne<Ingredient>()
            .WithMany()
            .HasForeignKey(pantryItem => pantryItem.IngredientId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
