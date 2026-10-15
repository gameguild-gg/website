using FluentAssertions;
using GameGuild;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

/// <summary>
///     Transactionality and contract tests for the bulk tenant permission commands
/// (issues #302/#310): expiry/reason forwarding, outcome-counted revocation, atomic
/// all-or-nothing mode, and the IsFailure/ICommitOnFailureOutcome recognition by the
/// host's transactional outcome classifier.
/// </summary>
public class BulkTenantPermissionCommandHandlerTests
{
    private readonly Mock<IPermissionBulkService> _bulkService = new();
    private readonly Mock<IPermissionGrantService> _grantService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    private void SetAdminActor(Guid? tenantId = null) =>
        _actorAccessor
            .SetupGet(a => a.ActorContext)
            .Returns(ActorContextBuilder
                .ForUser(Guid.NewGuid())
                .WithTenantId(tenantId ?? _tenantId)
                .WithRoles(["SystemAdmin"])
                .Build());

    [Fact]
    public async Task BulkGrant_ForwardsExpiryAndReason_ToEveryPerUserGrant()
    {
        SetAdminActor();
        var expiresAt = SystemClock.UtcNow.AddHours(2);
        var reason = "incident response elevation";
        DateTime? capturedExpiry = null;
        string? capturedReason = null;
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.IsAny<Guid[]>(), It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid[], Guid, string[], Guid?, DateTime?, string?, CancellationToken>(
                (_, _, _, _, expiry, why, _) =>
                {
                    capturedExpiry = expiry;
                    capturedReason = why;
                })
            .ReturnsAsync([]);

        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object, NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkGrantTenantPermissionsCommand
            {
                UserIds = [Guid.NewGuid()],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read],
                ExpiresAt = expiresAt,
                Reason = reason
            },
            CancellationToken.None);

        capturedExpiry.Should().Be(expiresAt);
        capturedReason.Should().Be(reason);
        result.Successful.Should().Be(1);
    }

    [Fact]
    public async Task BulkGrant_PastExpiry_IsRejected()
    {
        SetAdminActor();

        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object, NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkGrantTenantPermissionsCommand
            {
                UserIds = [Guid.NewGuid()],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read],
                ExpiresAt = SystemClock.UtcNow.AddHours(-1)
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _bulkService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BulkGrant_PerItemMode_ReportsFailuresWithoutAborting()
    {
        SetAdminActor();
        var ok = Guid.NewGuid();
        var failing = Guid.NewGuid();
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.Is<Guid[]>(ids => ids.Contains(ok)), It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.Is<Guid[]>(ids => ids.Contains(failing)), It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object, NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkGrantTenantPermissionsCommand
            {
                UserIds = [ok, failing],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read]
            },
            CancellationToken.None);

        result.Successful.Should().Be(1);
        result.Failed.Should().Be(1);
        result.Failures.Should().ContainSingle().Which.UserId.Should().Be(failing);
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task BulkGrant_AtomicMode_ThrowsOnFirstFailure()
    {
        SetAdminActor();
        var failing = Guid.NewGuid();
        _bulkService
            .Setup(b => b.BulkGrantTenantPermissionAsync(
                It.IsAny<Guid[]>(), It.IsAny<Guid>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("store unavailable"));

        var handler = new BulkGrantTenantPermissionsCommandHandler(
            _bulkService.Object, _actorAccessor.Object, NullLogger<BulkGrantTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkGrantTenantPermissionsCommand
            {
                UserIds = [Guid.NewGuid(), failing],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read],
                Atomic = true
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("Atomic bulk grant aborted", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BulkRevoke_CountsSuccess_ByServiceOutcome()
    {
        SetAdminActor();
        var revoked = Guid.NewGuid();
        var noGrant = Guid.NewGuid();
        _grantService
            .Setup(g => g.RevokeTenantPermissionAsync(revoked, _tenantId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _grantService
            .Setup(g => g.RevokeTenantPermissionAsync(noGrant, _tenantId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new BulkRevokeTenantPermissionsCommandHandler(
            _grantService.Object, _actorAccessor.Object, NullLogger<BulkRevokeTenantPermissionsCommandHandler>.Instance);

        var result = await handler.Handle(
            new BulkRevokeTenantPermissionsCommand
            {
                UserIds = [revoked, noGrant],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read]
            },
            CancellationToken.None);

        result.Successful.Should().Be(1, "a no-op revoke must not count as success (issue #310)");
        result.Failed.Should().Be(1);
        result.Failures.Should().ContainSingle()
            .Which.Error.Should().Be("NoMatchingGrant");
    }

    [Fact]
    public async Task BulkRevoke_AtomicMode_ThrowsOnMissingGrant()
    {
        SetAdminActor();
        var noGrant = Guid.NewGuid();
        _grantService
            .Setup(g => g.RevokeTenantPermissionAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new BulkRevokeTenantPermissionsCommandHandler(
            _grantService.Object, _actorAccessor.Object, NullLogger<BulkRevokeTenantPermissionsCommandHandler>.Instance);

        var act = () => handler.Handle(
            new BulkRevokeTenantPermissionsCommand
            {
                UserIds = [noGrant],
                TenantId = _tenantId,
                Permissions = [PermissionType.Read],
                Atomic = true
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.Contains("no matching grant", StringComparison.Ordinal));
    }

    [Fact]
    public void BulkPermissionResult_PartialFailureIsRecognizedByOutcomeClassifier()
    {
        // Issues #302/#310: the transactional outcome classifier must recognize the
        // partial-failure contract — reported as business failure (IsFailure) while the
        // completed per-user mutations are deliberately retained (no rollback).
        var partial = new TenantBulkPermissionResult { TotalRequested = 2, Successful = 1, Failed = 1 };
        var clean = new TenantBulkPermissionResult { TotalRequested = 2, Successful = 2, Failed = 0 };

        partial.IsFailure.Should().BeTrue();
        clean.IsSuccess.Should().BeTrue();

        CommandOutcome.IsFailure(partial).Should().BeTrue("partial failures are business failures");
        CommandOutcome.ShouldRollback(partial).Should().BeFalse("successful items are deliberately retained");
        CommandOutcome.ShouldRollback(clean).Should().BeFalse();
        partial.Should().BeAssignableTo<ICommitOnFailureOutcome>();
    }
}
