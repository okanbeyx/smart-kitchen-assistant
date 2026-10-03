using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartKitchenAssistant.Api.Features.Pantry.Application;
using SmartKitchenAssistant.Api.Features.Pantry.Domain;
using SmartKitchenAssistant.Api.Features.Recipes.Application;
using SmartKitchenAssistant.Api.Features.Recipes.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Features.Pantry.Infrastructure.Persistence;

internal sealed class PantryConsumptionRepository(SmartKitchenDbContext dbContext)
    : IPantryConsumptionRepository
{
    public async Task<PantryConsumptionResult> ConsumeAsync(
        string userId,
        long recipeId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var completed = await FindCompletedAsync(
            userId,
            idempotencyKey,
            cancellationToken);

        if (completed is not null)
        {
            return ReplayOrConflict(completed, recipeId);
        }

        var attempt = await ConsumeInTransactionAsync(
            userId,
            recipeId,
            idempotencyKey,
            cancellationToken);

        if (attempt.Outcome != TransactionAttemptOutcome.IdempotencyRace)
        {
            return attempt.Result!;
        }

        dbContext.ChangeTracker.Clear();
        completed = await FindCompletedAsync(
            userId,
            idempotencyKey,
            cancellationToken);

        return completed is null
            ? PantryConsumptionResult.From(PantryConsumptionOutcome.PantryStockChanged)
            : ReplayOrConflict(completed, recipeId);
    }

    private async Task<TransactionAttempt> ConsumeInTransactionAsync(
        string userId,
        long recipeId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var isPublished = await dbContext.Recipes
                .AsNoTracking()
                .AnyAsync(
                    recipe => recipe.Id == recipeId &&
                        recipe.Status == RecipeStatus.Published,
                    cancellationToken);

            if (!isPublished)
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionAttempt.Completed(PantryConsumptionResult.From(
                    PantryConsumptionOutcome.RecipeNotFound));
            }

            var sources = await (
                from recipeIngredient in dbContext.RecipeIngredients.AsNoTracking()
                join ingredient in dbContext.Ingredients.AsNoTracking()
                    on recipeIngredient.IngredientId equals ingredient.Id
                where recipeIngredient.RecipeId == recipeId &&
                    !recipeIngredient.IsOptional
                orderby recipeIngredient.IngredientId, recipeIngredient.Sequence
                select new RequiredIngredientSource(
                    recipeIngredient.IngredientId,
                    ingredient.QuantityDimension,
                    recipeIngredient.NormalizedQuantity,
                    recipeIngredient.DisplayUnit))
                .ToArrayAsync(cancellationToken);

            if (!TryAggregateRequirements(sources, out var requirements))
            {
                await transaction.RollbackAsync(cancellationToken);
                return TransactionAttempt.Completed(PantryConsumptionResult.From(
                    PantryConsumptionOutcome.RecipeNotConsumable));
            }

            var consumption = new StockConsumption(
                userId,
                idempotencyKey,
                recipeId);
            dbContext.StockConsumptions.Add(consumption);
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var requirement in requirements)
            {
                var deleted = await dbContext.UserPantryItems
                    .Where(item =>
                        item.UserId == userId &&
                        item.IngredientId == requirement.IngredientId &&
                        item.NormalizedQuantity == requirement.NormalizedQuantity)
                    .ExecuteDeleteAsync(cancellationToken);

                var updated = 0;

                if (deleted == 0)
                {
                    updated = await dbContext.UserPantryItems
                        .Where(item =>
                            item.UserId == userId &&
                            item.IngredientId == requirement.IngredientId &&
                            item.NormalizedQuantity > requirement.NormalizedQuantity)
                        .ExecuteUpdateAsync(
                            setters => setters.SetProperty(
                                item => item.NormalizedQuantity,
                                item => item.NormalizedQuantity -
                                    requirement.NormalizedQuantity),
                            cancellationToken);
                }

                if (deleted + updated != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    dbContext.ChangeTracker.Clear();
                    return TransactionAttempt.Completed(PantryConsumptionResult.From(
                        PantryConsumptionOutcome.InsufficientStock));
                }

                consumption.AddItem(new StockConsumptionItem(
                    requirement.IngredientId,
                    requirement.NormalizedQuantity,
                    requirement.QuantityDimension));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return TransactionAttempt.Completed(PantryConsumptionResult.Success(
                ToView(consumption)));
        }
        catch (DbUpdateException exception)
            when (SqlServerPantryConsumptionErrorClassifier.IsIdempotencyRace(exception))
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            return TransactionAttempt.IdempotencyRace();
        }
        catch (Exception exception)
            when (SqlServerPantryConsumptionErrorClassifier.IsDeadlock(exception))
        {
            await RollbackSafelyAsync(transaction, cancellationToken);
            dbContext.ChangeTracker.Clear();
            return TransactionAttempt.Completed(PantryConsumptionResult.From(
                PantryConsumptionOutcome.PantryStockChanged));
        }
    }

    private async Task<StockConsumption?> FindCompletedAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await dbContext.StockConsumptions
            .AsNoTracking()
            .Include(consumption => consumption.Items)
            .SingleOrDefaultAsync(
                consumption => consumption.UserId == userId &&
                    consumption.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private static bool TryAggregateRequirements(
        IReadOnlyList<RequiredIngredientSource> sources,
        out IReadOnlyList<RequiredIngredientRequirement> requirements)
    {
        if (sources.Count == 0)
        {
            requirements = [];
            return false;
        }

        var aggregated = new List<RequiredIngredientRequirement>();

        foreach (var group in sources.GroupBy(source => source.IngredientId))
        {
            if (!RequiredIngredientAggregator.TryAggregate(
                    group.ToArray(),
                    out var requirement,
                    out _))
            {
                requirements = [];
                return false;
            }

            aggregated.Add(requirement);
        }

        requirements = aggregated
            .OrderBy(requirement => requirement.IngredientId)
            .ToArray();
        return true;
    }

    private static PantryConsumptionResult ReplayOrConflict(
        StockConsumption consumption,
        long recipeId) =>
        consumption.RecipeId == recipeId
            ? PantryConsumptionResult.Success(ToView(consumption))
            : PantryConsumptionResult.From(
                PantryConsumptionOutcome.IdempotencyKeyReused);

    private static PantryConsumptionView ToView(StockConsumption consumption) =>
        new(
            consumption.Id,
            consumption.RecipeId,
            consumption.Items
                .OrderBy(item => item.IngredientId)
                .Select(item => new ConsumedIngredientView(
                    item.IngredientId,
                    item.ConsumedNormalizedQuantity,
                    item.QuantityDimension))
                .ToArray());

    private static async Task RollbackSafelyAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // The original SQL Server failure is already the authoritative outcome.
        }
    }

    private enum TransactionAttemptOutcome
    {
        Completed,
        IdempotencyRace
    }

    private sealed record TransactionAttempt(
        TransactionAttemptOutcome Outcome,
        PantryConsumptionResult? Result)
    {
        internal static TransactionAttempt Completed(PantryConsumptionResult result) =>
            new(TransactionAttemptOutcome.Completed, result);

        internal static TransactionAttempt IdempotencyRace() =>
            new(TransactionAttemptOutcome.IdempotencyRace, null);
    }
}
