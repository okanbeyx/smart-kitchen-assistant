namespace SmartKitchenAssistant.Api.Features.Recipes.Domain;

public sealed class Recipe
{
    public Recipe(string title, int baseServings)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Recipe title cannot be empty.", nameof(title));
        }

        if (baseServings <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseServings),
                baseServings,
                "Base servings must be greater than zero.");
        }

        Title = title;
        BaseServings = baseServings;
        Status = RecipeStatus.Draft;
    }

    public long Id { get; private set; }

    public string Title { get; private set; }

    public int BaseServings { get; private set; }

    public RecipeStatus Status { get; private set; }
}
