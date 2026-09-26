namespace SmartKitchenAssistant.Api.Features.Recipes.Domain;

public sealed class RecipeStep
{
    public RecipeStep(
        long recipeId,
        int sequence,
        string instruction,
        int? timerSeconds = null)
    {
        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                sequence,
                "Sequence must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(instruction))
        {
            throw new ArgumentException("Instruction cannot be empty.", nameof(instruction));
        }

        if (timerSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timerSeconds),
                timerSeconds,
                "Timer seconds must be greater than zero when provided.");
        }

        RecipeId = recipeId;
        Sequence = sequence;
        Instruction = instruction;
        TimerSeconds = timerSeconds;
    }

    public long Id { get; private set; }

    public long RecipeId { get; private set; }

    public int Sequence { get; private set; }

    public string Instruction { get; private set; }

    public int? TimerSeconds { get; private set; }
}
