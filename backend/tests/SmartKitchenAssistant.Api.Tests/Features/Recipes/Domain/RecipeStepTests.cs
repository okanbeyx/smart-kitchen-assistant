using SmartKitchenAssistant.Api.Features.Recipes.Domain;

namespace SmartKitchenAssistant.Api.Tests.Features.Recipes.Domain;

public sealed class RecipeStepTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsEmptyInstruction(string? instruction)
    {
        Assert.Throws<ArgumentException>(() => new RecipeStep(1, 1, instruction!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveSequence(int sequence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecipeStep(1, sequence, "Karıştır."));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonPositiveTimer(int timerSeconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecipeStep(1, 1, "Kaynat.", timerSeconds));
    }

    [Fact]
    public void ConstructorAllowsTimerToBeOmitted()
    {
        var step = new RecipeStep(1, 1, "Karıştır.");

        Assert.Null(step.TimerSeconds);
    }

    [Fact]
    public void ConstructorAllowsPositiveTimer()
    {
        var step = new RecipeStep(1, 1, "Kaynat.", 300);

        Assert.Equal(300, step.TimerSeconds);
    }
}
