using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Economy.Bounties;
using GameGuild.Economy.Contracts;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyBountiesControllerContractTests
{
    [Fact]
    public void SelfServiceBodiesExposeOnlyBusinessIntent()
    {
        typeof(CreateMyBountyRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "Currency",
                "AmountUnits",
                "RequiresPrerequisite",
                "MinimumReputation",
                "RequiresInstructorVerification",
                "ExpiresAt",
                "IdempotencyKey"
            ]);
        typeof(CompleteMyBountyRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["IdempotencyKey"]);
    }

    [Theory]
    [InlineData(EconomyProtectedOperationState.Denied, 403)]
    [InlineData(EconomyProtectedOperationState.ReviewRequired, 409)]
    [InlineData(EconomyProtectedOperationState.Hold, 409)]
    [InlineData(EconomyProtectedOperationState.ComplianceUnavailable, 503)]
    public async Task ProtectedOperationStatesAreReturnedAsStructuredResponses(
        EconomyProtectedOperationState state,
        int expectedStatus)
    {
        var actor = new ActorContextAccessor();
        actor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        Guid? reviewId = state == EconomyProtectedOperationState.ReviewRequired ? Guid.NewGuid() : null;
        var service = new ThrowingBountyService(state, reviewId);
        var sender = EconomyHandlerSenders.Public(bounties: service);
        var controller = new EconomyBountiesController(sender, service, actor, TimeProvider.System);

        var result = await controller.Create(new CreateMyBountyRequest(
            CurrencyCode.HardCoin,
            25,
            false,
            0,
            false,
            DateTimeOffset.UtcNow.AddDays(1),
            "bounty-1"), CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new BountyProtectedOperationFailureResponse(
            state, reviewId, ["not-ready"]));
    }

    private sealed class ThrowingBountyService(
        EconomyProtectedOperationState state,
        Guid? reviewId) : IDurableBountyApplicationService
    {
        public ValueTask<DurableBountyView> CreateAsync(
            CreateDurableBountyRequest request,
            CancellationToken cancellationToken = default) => ValueTask.FromException<DurableBountyView>(
            new EconomyProtectedOperationException(state, reviewId, ["not-ready"]));

        public ValueTask<DurableBountyView> ClaimAsync(
            ClaimDurableBountyRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<DurableBountyView> ReclaimAsync(
            ReclaimDurableBountyRequest request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<DurableBountyView?> FindAsync(
            Guid tenantId,
            BountyId bountyId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<DurableBountyView>> ListAsync(
            Guid tenantId,
            BountyStatus? status,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
