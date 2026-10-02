using Microsoft.EntityFrameworkCore;
using SmartKitchenAssistant.Api.Features.Catalog.Application;
using SmartKitchenAssistant.Api.Features.Catalog.Domain;
using SmartKitchenAssistant.Api.Infrastructure.Persistence;

namespace SmartKitchenAssistant.Api.Features.Catalog.Infrastructure.Persistence;

internal sealed class IngredientReadRepository(SmartKitchenDbContext dbContext)
    : IIngredientReadRepository
{
    public async Task<IReadOnlyList<IngredientSummaryView>> ListActiveAsync(
        string? search,
        CancellationToken cancellationToken)
    {
        IQueryable<Ingredient> query = dbContext.Ingredients
            .AsNoTracking()
            .Where(ingredient => ingredient.IsActive);

        if (search is not null)
        {
            query = query.Where(ingredient => ingredient.Name.Contains(search));
        }

        return await query
            .OrderBy(ingredient => ingredient.Name)
            .ThenBy(ingredient => ingredient.Id)
            .Select(ingredient => new IngredientSummaryView(
                ingredient.Id,
                ingredient.Name,
                ingredient.QuantityDimension))
            .ToArrayAsync(cancellationToken);
    }
}
