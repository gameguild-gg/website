using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.UnitTests.TestSupport;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using MockQueryable.Moq;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Commands;

/// <summary>
///     Tests for <see cref="TransferOwnershipCommand"/> (issue #372): the transfer
///     authorization matrix (owner / tenant admin / system admin / everyone else),
///     guarded + versioned + audited mutation semantics, the soft-delete parity hook
///     (the previous owner row is soft-revoked, never hard-deleted) and the durable
///     restore point audit entry.
/// </summary>
public class TransferOwnershipCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _dbContext = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IPermissionAuditService> _auditService = new();
    private readonly ActorContextAccessor _actorAccessor = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _newOwnerId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    private readonly ResourceUserPermission _ownerRow;

    public TransferOwnershipCommandHandlerTests()
    {
        _ownerRow = new ResourceUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            UserId = _ownerId,
            ResourceType = "Project",
            ResourceId = "p-1",
            Permissions = ["read", "write", "share"],
            GrantedByUserId = _ownerId
        };
    }

    private TransferOwnershipCommandHandler CreateSut(List<ResourceUserPermission> rows)
    {
        var set = rows.AsQueryable().BuildMockDbSet();
        // MockQueryable's Add is a plain mock call; mirror the tracked-state behavior so
        // assertions can see rows the handler appends.
        set.Setup(s => s.Add(It.IsAny<ResourceUserPermission>()))
            .Callback<ResourceUserPermission>(rows.Add);
        _dbContext.Setup(c => c.Set<ResourceUserPermission>()).Returns(set.Object);
        _dbContext
            .Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(rows.Count + 1);
        _versionStore
            .Setup(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L);
        _auditService
            .Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());

        return new TransferOwnershipCommandHandler(
            _dbContext.Object,
            _actorAccessor,
            _versionStore.Object,
            _auditService.Object,
            NullLogger<TransferOwnershipCommandHandler>.Instance);
    }

    private TransferOwnershipCommand Command(Guid? newOwner = null)
        => new()
        {
            TenantId = _tenantId,
            ResourceType = "Project",
            ResourceId = "p-1",
            NewOwnerUserId = newOwner ?? _newOwnerId,
            Reason = "handover"
        };

    private void ActAs(Guid? userId, Guid? tenantId, params string[] roles)
    {
        var actor = new ActorContext
        {
            ActorKind = userId is null ? ActorKind.Anonymous : ActorKind.User,
            SubjectId = userId?.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            IsAuthenticated = userId is not null
        };
        _actorAccessor.SetActorContext(actor);
    }

    // ── Guard matrix ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AnonymousActor_IsRejected()
    {
        ActAs(null, null);
        var sut = CreateSut([_ownerRow]);

        var act = () => sut.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnrelatedAuthenticatedUser_IsRejected()
    {
        ActAs(_actorId, _tenantId);
        var sut = CreateSut([_ownerRow]);

        var act = () => sut.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CrossTenantTenantAdmin_IsRejected()
    {
        ActAs(_actorId, Guid.NewGuid(), "TenantAdmin");
        var sut = CreateSut([_ownerRow]);

        var act = () => sut.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData("SystemAdmin", null)]
    [InlineData("TenantAdmin", true)]
    [InlineData("Admin", true)]
    public async Task Handle_AdminActors_CanTransfer(string role, bool? sameTenant)
    {
        ActAs(_actorId, sameTenant == true ? _tenantId : Guid.NewGuid(), role);
        var sut = CreateSut([_ownerRow]);

        var result = await sut.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CurrentOwner_CanTransfer()
    {
        ActAs(_ownerId, _tenantId);
        var sut = CreateSut([_ownerRow]);

        var result = await sut.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_OwnerFromAnotherTenant_IsRejected()
    {
        // The owner row lives in _tenantId; the actor's tenant context is different.
        ActAs(_ownerId, Guid.NewGuid());
        var sut = CreateSut([_ownerRow]);

        var act = () => sut.Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Mutation semantics: guarded + versioned + audited, soft delete only ──

    [Fact]
    public async Task Handle_ValidTransfer_SoftRevokesPreviousOwner_AndNeverHardDeletes()
    {
        ActAs(_ownerId, _tenantId);
        var rows = new List<ResourceUserPermission> { _ownerRow };
        var sut = CreateSut(rows);

        var result = await sut.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.PreviousOwnerUserId.Should().Be(_ownerId);
        result.NewOwnerUserId.Should().Be(_newOwnerId);
        result.TenantId.Should().Be(_tenantId);

        // Soft-delete parity: the previous owner row is revoked, data intact.
        _ownerRow.RevokedAt.Should().NotBeNull();
        _ownerRow.RevokedByUserId.Should().Be(_ownerId);
        _ownerRow.Permissions.Should().NotBeEmpty();
        _ownerRow.RevocationReason.Should().Be("handover");

        // The incoming owner row exists with the owner permission set and the owner flag.
        var incoming = rows.Should().ContainSingle(r =>
            r.UserId == _newOwnerId && r.IsOwner && r.RevokedAt == null).Subject;
        incoming.Permissions.Should().BeEquivalentTo(_ownerRow.Permissions);
    }

    [Fact]
    public async Task Handle_ValidTransfer_IsVersioned_AndAuditedWithRestorePoint()
    {
        ActAs(_ownerId, _tenantId);
        var auditLogId = Guid.NewGuid();
        _auditService.Setup(a => a.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog { Id = auditLogId });
        var sut = CreateSut([_ownerRow]);

        var result = await sut.Handle(Command(), CancellationToken.None);

        // Versioned: the tenant security version was bumped for the resource's tenant.
        _versionStore.Verify(v => v.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);

        // Audited: a durable TransferOwnership entry carrying the before/after snapshots.
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.TransferOwnership,
            _newOwnerId,
            _ownerId,
            _tenantId,
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.Is<string?>(oldValue => oldValue!.Contains(_ownerId.ToString())),
            It.Is<string?>(newValue => newValue!.Contains(_newOwnerId.ToString())),
            "handover",
            true,
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        result.RestorePointAuditLogId.Should().Be(auditLogId);
    }

    [Fact]
    public async Task Handle_SameOwnerTransfer_IsAnIdempotentNoOp()
    {
        ActAs(_ownerId, _tenantId);
        var sut = CreateSut([_ownerRow]);

        var result = await sut.Handle(Command(newOwner: _ownerId), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Message.Should().Contain("already owns");
        _ownerRow.RevokedAt.Should().BeNull();
        _dbContext.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditService.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoRecordedOwner_FailsClosed()
    {
        ActAs(_actorId, _tenantId, "TenantAdmin");
        var sut = CreateSut([]);

        var act = () => sut.Handle(Command(), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no recorded owner*");
    }

    [Fact]
    public async Task Handle_MultipleActiveOwnerRows_AllSoftRevoked_SingleOwnerInvariant()
    {
        ActAs(_actorId, _tenantId, "TenantAdmin");
        var secondOwnerRow = new ResourceUserPermission
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            UserId = Guid.NewGuid(),
            ResourceType = "Project",
            ResourceId = "p-1",
            Permissions = ["read"],
            GrantedByUserId = _ownerId,
            IsOwner = true
        };
        var rows = new List<ResourceUserPermission> { _ownerRow, secondOwnerRow };
        var sut = CreateSut(rows);

        var result = await sut.Handle(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        _ownerRow.RevokedAt.Should().NotBeNull();
        secondOwnerRow.RevokedAt.Should().NotBeNull();
        rows.Should().ContainSingle(r => r.IsOwner && r.RevokedAt == null && r.UserId == _newOwnerId);
    }

    [Fact]
    public async Task Handle_InvalidCommandArguments_FailClosed()
    {
        ActAs(_actorId, null, "SystemAdmin");
        var sut = CreateSut([_ownerRow]);

        var act = () => sut.Handle(new TransferOwnershipCommand
        {
            TenantId = _tenantId,
            ResourceType = "Project",
            ResourceId = "p-1",
            NewOwnerUserId = Guid.Empty
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
