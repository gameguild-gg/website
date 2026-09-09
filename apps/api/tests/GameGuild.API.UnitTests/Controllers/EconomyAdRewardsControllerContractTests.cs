using FluentAssertions;
using GameGuild.API.Controllers;
using GameGuild.Economy.AdRewards;
using GameGuild.Economy.Contracts;
using GameGuild.Economy.Ledger;
using GameGuild.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GameGuild.API.UnitTests.Controllers;

public sealed class EconomyAdRewardsControllerContractTests
{
    [Fact]
    public void SelfServiceBodiesExposeOnlyBusinessIntent()
    {
        typeof(StartMyAdRewardSessionRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo([
                "Network",
                "CreativeId",
                "RequiredDurationSeconds",
                "IdempotencyKey"
            ]);
        typeof(CompleteMyAdRewardSessionRequest).GetProperties().Select(property => property.Name)
            .Should().BeEquivalentTo(["Token", "Playback", "ProviderProof", "IdempotencyKey"]);
        typeof(EconomyAdRewardsController).GetMethods()
            .Should().NotContain(method => method.Name.Contains("ConfirmDeferred", StringComparison.Ordinal));
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
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var actor = new ActorContextAccessor();
        actor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        Guid? reviewId = state == EconomyProtectedOperationState.ReviewRequired ? Guid.NewGuid() : null;
        var completions = new Mock<IDurableAdRewardCompletionService>(MockBehavior.Strict);
        completions.Setup(service => service.CompleteAsync(
                It.IsAny<CompleteDurableAdRewardSessionRequest>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<DurableAdRewardCompletionResult>(
                new EconomyProtectedOperationException(state, reviewId, ["not-ready"])));
        var controller = new EconomyAdRewardsController(
            EconomyHandlerSenders.Public(adRewardCompletions: completions.Object),
            Mock.Of<IDurableAdRewardSessionReader>(),
            Mock.Of<IEconomyWalletDirectory>(),
            Mock.Of<IAdRewardRequestRiskContextResolver>(),
            actor,
            TimeProvider.System);

        var result = await controller.Complete(
            Guid.NewGuid(),
            new CompleteMyAdRewardSessionRequest(
                "signed-token",
                new AdPlaybackEvidence(
                    DateTimeOffset.UtcNow.AddSeconds(-30),
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromSeconds(30),
                    TimeSpan.Zero,
                    [0, 25, 50, 75, 100]),
                null,
                "completion-1"),
            CancellationToken.None);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(expectedStatus);
        response.Value.Should().BeEquivalentTo(new
        {
            State = state,
            ReviewId = reviewId,
            Diagnostics = new[] { "not-ready" }
        });
    }

    [Fact]
    public async Task StartUsesOnlyServerResolvedRiskContext()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var walletId = new WalletId(Guid.NewGuid());
        var actor = new ActorContextAccessor();
        actor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        var wallets = new Mock<IEconomyWalletDirectory>(MockBehavior.Strict);
        wallets.Setup(directory => directory.GetOwnerWalletAsync(tenantId, actorId, default))
            .ReturnsAsync(new EconomyWalletIdentity(walletId, tenantId, actorId, WalletLifecycleState.Active));
        var risks = new Mock<IAdRewardRequestRiskContextResolver>(MockBehavior.Strict);
        risks.Setup(resolver => resolver.ResolveAsync(tenantId, actorId, default))
            .ReturnsAsync(new AdRewardRequestRiskContext("device", "ip", "asn"));
        var sessions = new Mock<IDurableAdRewardSessionService>(MockBehavior.Strict);
        sessions.Setup(service => service.StartAsync(
                It.Is<StartDurableAdRewardSessionRequest>(request =>
                    request.TenantId == tenantId && request.UserId == actorId && request.WalletId == walletId &&
                    request.Network == "google" && request.CreativeId == "creative" &&
                    request.DeviceRiskHash == "device" && request.IpRiskHash == "ip" &&
                    request.AsnRiskHash == "asn" && request.RequiredDuration == TimeSpan.FromSeconds(30) &&
                    request.IdempotencyKey == new IdempotencyKey("session-1")), default))
            .ReturnsAsync((DurableAdRewardSessionResult)null!);
        var controller = new EconomyAdRewardsController(
            EconomyHandlerSenders.Public(adRewardSessions: sessions.Object),
            Mock.Of<IDurableAdRewardSessionReader>(),
            wallets.Object,
            risks.Object,
            actor,
            TimeProvider.System);

        var result = await controller.Start(
            new StartMyAdRewardSessionRequest("google", "creative", 30, "session-1"), default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(201);
        sessions.VerifyAll();
        risks.VerifyAll();
        wallets.VerifyAll();
    }

    [Fact]
    public async Task StartFailsClosedWhenTrustedRiskContextIsUnavailable()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var actor = new ActorContextAccessor();
        actor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        var wallets = new Mock<IEconomyWalletDirectory>(MockBehavior.Strict);
        wallets.Setup(directory => directory.GetOwnerWalletAsync(tenantId, actorId, default))
            .ReturnsAsync(new EconomyWalletIdentity(
                new WalletId(Guid.NewGuid()), tenantId, actorId, WalletLifecycleState.Active));
        var risks = new Mock<IAdRewardRequestRiskContextResolver>(MockBehavior.Strict);
        risks.Setup(resolver => resolver.ResolveAsync(tenantId, actorId, default))
            .ThrowsAsync(new AdRewardRiskContextUnavailableException("secret internal detail"));
        var sessions = new Mock<IDurableAdRewardSessionService>(MockBehavior.Strict);
        var controller = new EconomyAdRewardsController(
            EconomyHandlerSenders.Public(adRewardSessions: sessions.Object),
            Mock.Of<IDurableAdRewardSessionReader>(),
            wallets.Object,
            risks.Object,
            actor,
            TimeProvider.System);

        var result = await controller.Start(
            new StartMyAdRewardSessionRequest("google", "creative", 30, "session-1"), default);

        var response = result.Should().BeOfType<ObjectResult>().Subject;
        response.StatusCode.Should().Be(503);
        response.Value.Should().BeEquivalentTo(new
        {
            State = "RiskContextUnavailable",
            Message = "Ad reward risk evidence is unavailable."
        });
        response.Value!.ToString().Should().NotContain("secret internal detail");
        sessions.VerifyNoOtherCalls();
    }
}
