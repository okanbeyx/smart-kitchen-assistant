using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence.ValueConverters;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal sealed class StockConsumptionConfiguration
    : IEntityTypeConfiguration<StockConsumption>
{
    internal const string UserIdempotencyKeyUniqueIndexName =
        "IX_StockConsumptions_UserId_IdempotencyKey";

    public void Configure(EntityTypeBuilder<StockConsumption> builder)
    {
        builder.ToTable("StockConsumptions", "pantry");

        builder.HasKey(consumption => consumption.Id);

        builder.Property(consumption => consumption.Id)
            .UseIdentityColumn();

        builder.Property(consumption => consumption.UserId)
            .HasConversion<Utf16LittleEndianStringToBytesConverter>()
            .HasColumnType("varbinary(512)")
            .HasMaxLength(Utf16LittleEndianStringToBytesConverter.MaximumByteLength)
            .IsRequired();

        var asciiConverter = new ValueConverter<string, byte[]>(
            value => Encoding.ASCII.GetBytes(value),
            value => Encoding.ASCII.GetString(value));

        builder.Property(consumption => consumption.IdempotencyKey)
            .HasConversion(asciiConverter)
            .HasColumnType("varbinary(128)")
            .HasMaxLength(StockConsumption.MaximumIdempotencyKeyLength)
            .IsRequired();

        builder.Property(consumption => consumption.RecipeId)
            .IsRequired();

        builder.HasIndex(consumption => new
            {
                consumption.UserId,
                consumption.IdempotencyKey
            })
            .HasDatabaseName(UserIdempotencyKeyUniqueIndexName)
            .IsUnique();

        builder.HasOne<Recipe>()
            .WithMany()
            .HasForeignKey(consumption => consumption.RecipeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(consumption => consumption.Items)
            .WithOne()
            .HasForeignKey(item => item.StockConsumptionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
