using SmartKitchenAssistant.Api.Application.Identity;
using SmartKitchenAssistant.Api.Features.Pantry.Application;

namespace SmartKitchenAssistant.Api.Tests.Features.Pantry.Application;

public sealed class PantryConsumptionServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("contains space")]
    [InlineData("ünicode")]
    public async Task InvalidKeyIsRejectedBeforePersistence(string? key)
    {
        var repository = new StubRepository();
        var service = new PantryConsumptionService(
            new StubCurrentUser("user-1"),
            repository);

        var result = await service.ConsumeAsync(1, key, CancellationToken.None);

        Assert.Equal(PantryConsumptionOutcome.InvalidIdempotencyKey, result.Outcome);
        Assert.False(repository.WasCalled);
    }

    [Fact]
    public async Task AuthenticatedUserIsPassedToPersistence()
    {
        var repository = new StubRepository();
        var service = new PantryConsumptionService(
            new StubCurrentUser("Exact-User"),
            repository);

        await service.ConsumeAsync(7, "request-1", CancellationToken.None);

        Assert.Equal("Exact-User", repository.UserId);
        Assert.Equal(7, repository.RecipeId);
        Assert.Equal("request-1", repository.IdempotencyKey);
    }

    [Fact]
    public async Task KeyLongerThan128CharactersIsRejected()
    {
        var repository = new StubRepository();
        var service = new PantryConsumptionService(
            new StubCurrentUser("user-1"),
            repository);

        var result = await service.ConsumeAsync(
            1,
            new string('a', 129),
            CancellationToken.None);

        Assert.Equal(PantryConsumptionOutcome.InvalidIdempotencyKey, result.Outcome);
        Assert.False(repository.WasCalled);
    }

    [Fact]
    public async Task VisibleAsciiKeyAt128CharactersIsAcceptedExactly()
    {
        var key = new string('A', 128);
        var repository = new StubRepository();
        var service = new PantryConsumptionService(
            new StubCurrentUser("user-1"),
            repository);

        await service.ConsumeAsync(1, key, CancellationToken.None);

        Assert.Equal(key, repository.IdempotencyKey);
    }

    private sealed class StubCurrentUser(string userId) : ICurrentUser
    {
        public string UserId { get; } = userId;
    }

    private sealed class StubRepository : IPantryConsumptionRepository
    {
        internal bool WasCalled { get; private set; }
        internal string? UserId { get; private set; }
        internal long RecipeId { get; private set; }
        internal string? IdempotencyKey { get; private set; }

        public Task<PantryConsumptionResult> ConsumeAsync(
            string userId,
            long recipeId,
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            UserId = userId;
            RecipeId = recipeId;
            IdempotencyKey = idempotencyKey;
            return Task.FromResult(PantryConsumptionResult.From(
                PantryConsumptionOutcome.InsufficientStock));
        }
    }
}
