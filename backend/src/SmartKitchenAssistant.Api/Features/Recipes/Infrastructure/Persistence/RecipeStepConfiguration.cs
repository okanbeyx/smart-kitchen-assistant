using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Features.Recipes.Infrastructure.Persistence;

internal sealed class RecipeStepConfiguration : IEntityTypeConfiguration<RecipeStep>
{
    public void Configure(EntityTypeBuilder<RecipeStep> builder)
    {
        builder.ToTable("RecipeSteps", "recipes", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint(
                "CK_RecipeSteps_Sequence_Positive",
                "[Sequence] > 0");
            tableBuilder.HasCheckConstraint(
                "CK_RecipeSteps_TimerSeconds_Positive",
                "[TimerSeconds] IS NULL OR [TimerSeconds] > 0");
        });

        builder.HasKey(recipeStep => recipeStep.Id);

        builder.Property(recipeStep => recipeStep.Id)
            .UseIdentityColumn();

        builder.Property(recipeStep => recipeStep.Instruction)
            .HasMaxLength(4000)
            .IsRequired();

        builder.HasIndex(recipeStep => new
            {
                recipeStep.RecipeId,
                recipeStep.Sequence
            })
            .IsUnique();

        builder.HasOne<Recipe>()
            .WithMany()
            .HasForeignKey(recipeStep => recipeStep.RecipeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
