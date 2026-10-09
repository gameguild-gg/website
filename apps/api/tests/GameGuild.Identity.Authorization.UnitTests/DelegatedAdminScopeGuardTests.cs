using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Tests for delegated administration scopes (issue #325):
///     tenant-scoped admin delegation must be guarded (fail closed), versioned
///     (tenant security version) and audited (permission audit log), and the
///     enforcement paths must only honor currently-valid scopes.
/// </summary>
public class DelegatedAdminScopeGuardTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly Guid _adminUserId = Guid.NewGuid();

    private static GrantDelegatedAdminCommand GrantCommand(Guid adminUserId, Guid? tenantId) =>
        new(
            adminUserId,
            tenantId,
            "Helpdesk delegation",
            "Scoped helpdesk admin",
            new[] { "course", "project" },
            new[] { Guid.NewGuid() },
            new[] { "users:read", "users:update" });

    private static (Mock<IDelegatedAdminService> Service,
        Mock<IActorContextAccessor> Accessor,
        Mock<ITenantSecurityVersionStore> VersionStore,
        Mock<IPermissionAuditService> Audit) CreateDeps(ActorContext actor)
    {
        var service = new Mock<IDelegatedAdminService>();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        var audit = new Mock<IPermissionAuditService>();
        return (service, accessor, versionStore, audit);
    }

    private ActorContext ActorFor(Guid? tenantId, params string[] roles) =>
        ActorContextBuilder.Create()
            .WithActorKind(ActorKind.User)
            .WithSubjectId(Guid.NewGuid().ToString())
            .WithTenantId(tenantId)
            .WithRoles(roles)
            .AsAuthenticated()
            .Build();

    // ------------------------------------------------------------------------
    // Grant guard (fail closed)
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Grant_UnauthenticatedActor_IsRejected()
    {
        var actor = ActorContextBuilder.Create().Build(); // anonymous
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(GrantCommand(_adminUserId, _tenantA), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()), Times.Never);
        versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Grant_AuthenticatedNonAdmin_IsRejected()
    {
        var actor = ActorFor(_tenantA, "Learner");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(GrantCommand(_adminUserId, _tenantA), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Grant_TenantAdminOfAnotherTenant_IsRejected()
    {
        var actor = ActorFor(_tenantB, "TenantAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(GrantCommand(_adminUserId, _tenantA), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Grant_TenantAdmin_GlobalScope_IsRejected()
    {
        var actor = ActorFor(_tenantA, "TenantAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(GrantCommand(_adminUserId, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "global (tenant-less) delegation must require a system admin");
        service.Verify(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Grant_TenantAdminOfOwnTenant_Succeeds_Versioned_And_Audited()
    {
        var actor = ActorFor(_tenantA, "TenantAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        DelegatedAdminScope? captured = null;
        service
            .Setup(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()))
            .Callback<DelegatedAdminScope, CancellationToken>((scope, _) => captured = scope)
            .ReturnsAsync((DelegatedAdminScope scope, CancellationToken _) => scope);

        var result = await handler.Handle(GrantCommand(_adminUserId, _tenantA), CancellationToken.None);

        result.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.CreatedBy.Should().Be(actor.SubjectIdAsGuid!.Value, "grant must record the acting user, never payload input");
        captured.TenantId.Should().NotBeNull();
        captured.TenantId!.Value.Value.Should().Be(_tenantA);

        versionStore.Verify(
            v => v.IncrementVersionAsync(_tenantA.ToString(), It.IsAny<CancellationToken>()),
            Times.Once,
            "granting a delegated admin scope is a permission mutation and must bump the tenant security version");

        audit.Verify(
            a => a.LogPermissionChangeAsync(
                PermissionOperationType.Grant,
                _adminUserId,
                actor.SubjectIdAsGuid!.Value,
                It.Is<Guid?>(t => t == _tenantA),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "granting a delegated admin scope must be audited");
    }

    [Fact]
    public async Task Grant_SystemAdmin_GlobalScope_Succeeds_And_BumpsGlobalVersion()
    {
        var actor = ActorFor(null, "SystemAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        service
            .Setup(s => s.GrantDelegatedAdminAsync(It.IsAny<DelegatedAdminScope>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DelegatedAdminScope scope, CancellationToken _) => scope);

        var result = await handler.Handle(GrantCommand(_adminUserId, null), CancellationToken.None);

        result.Should().NotBeNull();
        versionStore.Verify(v => v.IncrementVersionAsync("global", It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(
            a => a.LogPermissionChangeAsync(
                PermissionOperationType.Grant,
                It.IsAny<Guid?>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ------------------------------------------------------------------------
    // Revoke guard (fail closed)
    // ------------------------------------------------------------------------

    private DelegatedAdminScope ScopeFor(Guid? tenantId) =>
        new()
        {
            AdminUserId = _adminUserId,
            TenantId = tenantId,
            Name = "Helpdesk delegation",
            AllowedResourceTypes = "[\"course\"]",
            AllowedUserIds = "[]",
            GrantablePermissions = "[\"users:read\"]",
            IsActive = true,
        };

    [Fact]
    public async Task Revoke_UnknownScope_ReturnsFalse_WithoutSideEffects()
    {
        var actor = ActorFor(_tenantA, "SystemAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        service
            .Setup(s => s.GetScopeByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DelegatedAdminScope?)null);
        var handler = new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var result = await handler.Handle(new RevokeDelegatedAdminCommand(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeFalse("revoking an unknown scope must surface as not-found, not as success");
        service.Verify(s => s.RevokeDelegatedAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        audit.Verify(a => a.LogPermissionChangeAsync(
            It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_UnauthenticatedActor_IsRejected()
    {
        var actor = ActorContextBuilder.Create().Build();
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var handler = new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(new RevokeDelegatedAdminCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(s => s.RevokeDelegatedAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_NonAdmin_IsRejected_EvenForKnownScope()
    {
        var actor = ActorFor(_tenantA, "Learner");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var scope = ScopeFor(_tenantA);
        service
            .Setup(s => s.GetScopeByIdAsync(scope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scope);
        var handler = new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(new RevokeDelegatedAdminCommand(scope.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(s => s.RevokeDelegatedAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_TenantAdminOfAnotherTenant_IsRejected()
    {
        var actor = ActorFor(_tenantB, "TenantAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var scope = ScopeFor(_tenantA);
        service
            .Setup(s => s.GetScopeByIdAsync(scope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scope);
        var handler = new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var act = () => handler.Handle(new RevokeDelegatedAdminCommand(scope.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "a tenant admin must not revoke another tenant's delegation");
        service.Verify(s => s.RevokeDelegatedAdminAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revoke_TenantAdminOfOwnTenant_Succeeds_Versioned_And_Audited()
    {
        var actor = ActorFor(_tenantA, "TenantAdmin");
        var (service, accessor, versionStore, audit) = CreateDeps(actor);
        var scope = ScopeFor(_tenantA);
        service
            .Setup(s => s.GetScopeByIdAsync(scope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scope);
        service
            .Setup(s => s.RevokeDelegatedAdminAsync(scope.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object);

        var result = await handler.Handle(new RevokeDelegatedAdminCommand(scope.Id), CancellationToken.None);

        result.Should().BeTrue();
        versionStore.Verify(v => v.IncrementVersionAsync(_tenantA.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Revoke,
            It.IsAny<Guid?>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ------------------------------------------------------------------------
    // Enforcement: only currently-valid scopes delegate authority
    // ------------------------------------------------------------------------

    [Fact]
    public async Task Enforcement_ExpiredScope_DoesNotDelegateAuthority()
    {
        var targetUser = Guid.NewGuid();
        var repository = new InMemoryDelegatedAdminScopeRepository();
        repository.Seed(new DelegatedAdminScope
        {
            AdminUserId = _adminUserId,
            TenantId = _tenantA,
            Name = "Expired delegation",
            AllowedUserIds = System.Text.Json.JsonSerializer.Serialize(new[] { targetUser }),
            AllowedResourceTypes = System.Text.Json.JsonSerializer.Serialize(new[] { "course" }),
            GrantablePermissions = "[\"users:read\"]",
            IsActive = true,
            StartsAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1), // already expired
        });
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        (await sut.GetManagedUsersAsync(_adminUserId, _tenantA)).Should().BeEmpty();
        (await sut.GetManagedResourceTypesAsync(_adminUserId, _tenantA)).Should().BeEmpty();
        (await sut.CanManageUserAsync(_adminUserId, targetUser, _tenantA)).Should().BeFalse();
        (await sut.CanManageResourceAsync(_adminUserId, "course", _tenantA)).Should().BeFalse();
    }

    [Fact]
    public async Task Enforcement_InactiveScope_DoesNotDelegateAuthority()
    {
        var targetUser = Guid.NewGuid();
        var repository = new InMemoryDelegatedAdminScopeRepository();
        repository.Seed(new DelegatedAdminScope
        {
            AdminUserId = _adminUserId,
            TenantId = _tenantA,
            Name = "Deactivated delegation",
            AllowedUserIds = System.Text.Json.JsonSerializer.Serialize(new[] { targetUser }),
            AllowedResourceTypes = System.Text.Json.JsonSerializer.Serialize(new[] { "course" }),
            GrantablePermissions = "[\"users:read\"]",
            IsActive = false,
        });
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        (await sut.GetManagedUsersAsync(_adminUserId, _tenantA)).Should().BeEmpty();
        (await sut.CanManageUserAsync(_adminUserId, targetUser, _tenantA)).Should().BeFalse();
    }

    [Fact]
    public async Task Enforcement_FutureScope_DoesNotDelegateAuthority()
    {
        var targetUser = Guid.NewGuid();
        var repository = new InMemoryDelegatedAdminScopeRepository();
        repository.Seed(new DelegatedAdminScope
        {
            AdminUserId = _adminUserId,
            TenantId = _tenantA,
            Name = "Scheduled delegation",
            AllowedUserIds = System.Text.Json.JsonSerializer.Serialize(new[] { targetUser }),
            GrantablePermissions = "[\"users:read\"]",
            IsActive = true,
            StartsAt = DateTime.UtcNow.AddHours(1), // not effective yet
        });
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        (await sut.GetManagedUsersAsync(_adminUserId, _tenantA)).Should().BeEmpty();
        (await sut.CanManageUserAsync(_adminUserId, targetUser, _tenantA)).Should().BeFalse();
    }

    [Fact]
    public async Task Enforcement_ValidScope_DelegatesAuthority_WithinTenantOnly()
    {
        var targetUser = Guid.NewGuid();
        var repository = new InMemoryDelegatedAdminScopeRepository();
        repository.Seed(new DelegatedAdminScope
        {
            AdminUserId = _adminUserId,
            TenantId = _tenantA,
            Name = "Active delegation",
            AllowedUserIds = System.Text.Json.JsonSerializer.Serialize(new[] { targetUser }),
            AllowedResourceTypes = System.Text.Json.JsonSerializer.Serialize(new[] { "course", "project" }),
            GrantablePermissions = "[\"users:read\"]",
            IsActive = true,
        });
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        (await sut.GetManagedUsersAsync(_adminUserId, _tenantA)).Should().BeEquivalentTo(new[] { targetUser });
        (await sut.GetManagedResourceTypesAsync(_adminUserId, _tenantA))
            .Should().BeEquivalentTo(new[] { "course", "project" });
        (await sut.CanManageUserAsync(_adminUserId, targetUser, _tenantA)).Should().BeTrue();
        (await sut.CanManageResourceAsync(_adminUserId, "course", _tenantA)).Should().BeTrue();

        // Tenant scoping: the same scope must not authorize in another tenant.
        (await sut.CanManageUserAsync(_adminUserId, targetUser, _tenantB)).Should().BeFalse();
        (await sut.CanManageResourceAsync(_adminUserId, "course", _tenantB)).Should().BeFalse();
    }

    [Fact]
    public async Task Enforcement_MalformedScopeJson_IsIgnored()
    {
        var repository = new InMemoryDelegatedAdminScopeRepository();
        repository.Seed(new DelegatedAdminScope
        {
            AdminUserId = _adminUserId,
            TenantId = _tenantA,
            Name = "Corrupt delegation",
            AllowedUserIds = "{not-json",
            AllowedResourceTypes = "{not-json",
            GrantablePermissions = "[\"users:read\"]",
            IsActive = true,
        });
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        var act = () => sut.GetManagedUsersAsync(_adminUserId, _tenantA);
        await act.Should().NotThrowAsync();
        (await sut.GetManagedUsersAsync(_adminUserId, _tenantA)).Should().BeEmpty();
        (await sut.GetManagedResourceTypesAsync(_adminUserId, _tenantA)).Should().BeEmpty();
    }

    // ------------------------------------------------------------------------
    // Service revoke semantics
    // ------------------------------------------------------------------------

    [Fact]
    public async Task RevokeDelegatedAdminAsync_UnknownId_ReturnsFalse()
    {
        var sut = new DelegatedAdminService(
            new InMemoryDelegatedAdminScopeRepository(),
            NullLogger<DelegatedAdminService>.Instance);

        var result = await sut.RevokeDelegatedAdminAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeDelegatedAdminAsync_KnownId_RemovesScopeAndReturnsTrue()
    {
        var repository = new InMemoryDelegatedAdminScopeRepository();
        var scope = ScopeFor(_tenantA);
        repository.Seed(scope);
        var sut = new DelegatedAdminService(repository, NullLogger<DelegatedAdminService>.Instance);

        var result = await sut.RevokeDelegatedAdminAsync(scope.Id);

        result.Should().BeTrue();
        (await sut.GetScopeByIdAsync(scope.Id)).Should().BeNull();
    }

    /// <summary>
    ///     Minimal in-memory repository for service-level tests. Note: unlike the EF
    ///     repository, <see cref="GetByAdminUserAsync"/> filters by validity via the
    ///     service, matching production behavior for active scopes.
    /// </summary>
    private sealed class InMemoryDelegatedAdminScopeRepository : IDelegatedAdminScopeRepository
    {
        private readonly List<DelegatedAdminScope> _scopes = new();

        public void Seed(DelegatedAdminScope scope) => _scopes.Add(scope);

        public Task<DelegatedAdminScope> CreateAsync(
            DelegatedAdminScope scope,
            CancellationToken cancellationToken = default)
        {
            _scopes.Add(scope);
            return Task.FromResult(scope);
        }

        public Task<DelegatedAdminScope?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_scopes.FirstOrDefault(s => s.Id == id));

        public Task UpdateAsync(DelegatedAdminScope scope, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _scopes.RemoveAll(s => s.Id == id);
            return Task.CompletedTask;
        }

        public Task<List<DelegatedAdminScope>> GetByAdminUserAsync(
            Guid adminUserId,
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            var query = _scopes.Where(s => s.AdminUserId == adminUserId && s.IsActive);
            if (tenantId.HasValue)
                query = query.Where(s => s.TenantId == new TenantId(tenantId.Value));
            return Task.FromResult(query.ToList());
        }

        public Task<List<DelegatedAdminScope>> GetByTenantAsync(
            Guid? tenantId,
            CancellationToken cancellationToken = default)
        {
            var query = tenantId.HasValue
                ? _scopes.Where(s => s.TenantId == new TenantId(tenantId.Value))
                : _scopes.Where(s => s.TenantId == null);
            return Task.FromResult(query.OrderBy(s => s.Name).ToList());
        }
    }
}
