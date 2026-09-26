namespace SmartKitchenAssistant.Api.Features.Catalog.Domain;

public sealed class Ingredient
{
    public Ingredient(
        string name,
        QuantityDimension quantityDimension,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Ingredient name cannot be empty.", nameof(name));
        }

        if (!Enum.IsDefined(quantityDimension))
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityDimension),
                quantityDimension,
                "Unsupported quantity dimension.");
        }

        Name = name;
        QuantityDimension = quantityDimension;
        IsActive = isActive;
    }

    public long Id { get; private set; }

    public string Name { get; private set; }

    public QuantityDimension QuantityDimension { get; private set; }

    public bool IsActive { get; private set; }
}
