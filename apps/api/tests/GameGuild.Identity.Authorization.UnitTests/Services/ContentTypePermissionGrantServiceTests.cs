using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the content-type grant store's guarded + versioned + audited mutation
///     contract (issues #360/#365): grant/deny/revoke flows, payload validation,
///     tenant-security-version bump, audit entries, soft delete on empty payload, and
///     the command-side tenant-admin guards.
/// </summary>
public class ContentTypePermissionGrantServiceTests
{
    private readonly Mock<IContentTypePermissionGrantRepository> _repository = new();
    private readonly Mock<IPermissionAuditService> _audit = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();

    public ContentTypePermissionGrantServiceTests()
    {
        // The actor is available to every handler built directly against the accessor
        // (command-guard tests do not go through CreateSut).
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(AdminActor());
    }

    private ContentTypePermissionGrantService CreateSut(ActorContext? actor = null)
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(actor ?? AdminActor());
        return new ContentTypePermissionGrantService(
            _repository.Object,
            _audit.Object,
            _versionStore.Object,
            _actorAccessor.Object,
            NullLogger<ContentTypePermissionGrantService>.Instance);
    }

    private ActorContext AdminActor()
        => new()
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _adminId.ToString(),
            TenantId = _tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(["TenantAdmin"], StringComparer.OrdinalIgnoreCase)
        };

    [Fact]
    public async Task GrantAsync_NewRow_CreatesAuditsAndBumpsVersion()
    {
        _repository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant?)null);
        _repository
            .Setup(r => r.CreateAsync(It.IsAny<ContentTypePermissionGrant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant grant, CancellationToken _) => grant);
        _versionStore
            .Setup(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);

        var grant = await CreateSut().GrantAsync(_userId, _tenantId, "Course", ["Read", "Write"], _adminId);

        grant.Permissions.Should().Equal("Read", "Write");
        grant.TenantId.Should().Be(_tenantId);
        grant.GrantedBy.Should().Be(_adminId);
        _versionStore.Verify(v => v.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            _userId,
            _adminId,
            _tenantId,
            "ContentType",
            It.IsAny<Guid?>(),
            It.Is<string?>(resourceType => resourceType == "ContentTypePermissionGrant:Course"),
            null,
            "Read,Write",
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GrantAsync_ExistingRow_ExtendsPayloadAndClearsMatchingDenies()
    {
        var existing = new ContentTypePermissionGrant
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentTypeName = "Course",
            Permissions = ["Read"],
            DenyPermissions = ["Write"]
        };
        _repository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _repository
            .Setup(r => r.UpdateAsync(It.IsAny<ContentTypePermissionGrant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant grant, CancellationToken _) => grant);

        var grant = await CreateSut().GrantAsync(_userId, _tenantId, "Course", ["Write"], _adminId);

        grant.Permissions.Should().ContainInOrder("Read", "Write");
        grant.DenyPermissions.Should().BeEmpty("granting clears matching deny entries (allow replaces deny)");
        _repository.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.CreateAsync(It.IsAny<ContentTypePermissionGrant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GrantAsync_BlankContentTypeName_IsRejected(string contentTypeName)
    {
        var act = () => CreateSut().GrantAsync(_userId, _tenantId, contentTypeName, ["Read"], _adminId);
        await act.Should().ThrowAsync<ArgumentException>();
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GrantAsync_EmptyPermissions_IsRejected()
    {
        var act = () => CreateSut().GrantAsync(_userId, _tenantId, "Course", [], _adminId);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GrantAsync_PastExpiration_IsRejected()
    {
        var act = () => CreateSut().GrantAsync(
            _userId, _tenantId, "Course", ["Read"], _adminId, expiresAt: SystemClock.UtcNow.AddSeconds(-1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DenyAsync_CreatesRowWithDenyPayloadAndAudits()
    {
        _repository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant?)null);
        _repository
            .Setup(r => r.CreateAsync(It.IsAny<ContentTypePermissionGrant>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant grant, CancellationToken _) => grant);

        var grant = await CreateSut().DenyAsync(_userId, _tenantId, "Course", ["Write"], _adminId, "policy");

        grant.DenyPermissions.Should().Equal("Write");
        grant.Permissions.Should().BeEmpty();
        _audit.Verify(a => a.LogPermissionChangeAsync(
            PermissionOperationType.Grant,
            _userId,
            _adminId,
            _tenantId,
            "ContentTypeDeny",
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            "Write",
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_RemovesPermissions_AndSoftDeletesEmptyRows()
    {
        var existing = new ContentTypePermissionGrant
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };
        _repository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var revoked = await CreateSut().RevokeAsync(_userId, _tenantId, "Course", ["Read"]);

        revoked.Should().BeTrue();
        _repository.Verify(r => r.DeleteAsync(existing.Id, It.IsAny<CancellationToken>()), Times.Once,
            "an empty payload row is soft-deleted (permission history preservation)");
        _versionStore.Verify(v => v.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_UnknownRow_ReturnsFalse_WithoutSideEffects()
    {
        _repository
            .Setup(r => r.GetBySubjectAndContentTypeAsync(_userId, _tenantId, "Course", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContentTypePermissionGrant?)null);

        var revoked = await CreateSut().RevokeAsync(_userId, _tenantId, "Course", ["Read"]);

        revoked.Should().BeFalse();
        _versionStore.VerifyNoOtherCalls();
        _audit.VerifyNoOtherCalls();
    }

    // ── Command-side guards (tenant-admin / system-admin, fail-closed) ─────

    [Fact]
    public async Task GrantCommand_SameTenantAdmin_Succeeds()
    {
        var grantService = new Mock<IContentTypePermissionGrantService>();
        grantService
            .Setup(g => g.GrantAsync(_userId, _tenantId, "Course", It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentTypePermissionGrant { Id = Guid.NewGuid() });
        var handler = new GrantContentTypePermissionGrantCommandHandler(
            grantService.Object,
            _actorAccessor.Object,
            NullLogger<GrantContentTypePermissionGrantCommandHandler>.Instance);

        var command = new GrantContentTypePermissionGrantCommand
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(_tenantId),
            UserId = _userId,
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };

        var grantId = await handler.Handle(command, CancellationToken.None);

        grantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GrantCommand_CrossTenantAdmin_IsDenied()
    {
        var grantService = new Mock<IContentTypePermissionGrantService>();
        var handler = new GrantContentTypePermissionGrantCommandHandler(
            grantService.Object,
            _actorAccessor.Object,
            NullLogger<GrantContentTypePermissionGrantCommandHandler>.Instance);

        var command = new GrantContentTypePermissionGrantCommand
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid()),
            UserId = _userId,
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        grantService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GrantCommand_NonAdminSameTenant_IsDenied()
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _adminId.ToString(),
            TenantId = _tenantId,
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        });
        var grantService = new Mock<IContentTypePermissionGrantService>();
        var handler = new GrantContentTypePermissionGrantCommandHandler(
            grantService.Object,
            _actorAccessor.Object,
            NullLogger<GrantContentTypePermissionGrantCommandHandler>.Instance);

        var command = new GrantContentTypePermissionGrantCommand
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(_tenantId),
            UserId = _userId,
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        grantService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GrantCommand_SystemAdmin_CrossTenantSucceeds()
    {
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = _adminId.ToString(),
            TenantId = Guid.NewGuid(),
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(["SystemAdmin"], StringComparer.OrdinalIgnoreCase)
        });
        var grantService = new Mock<IContentTypePermissionGrantService>();
        grantService
            .Setup(g => g.GrantAsync(_userId, It.IsAny<Guid>(), "Course", It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentTypePermissionGrant { Id = Guid.NewGuid() });
        var handler = new GrantContentTypePermissionGrantCommandHandler(
            grantService.Object,
            _actorAccessor.Object,
            NullLogger<GrantContentTypePermissionGrantCommandHandler>.Instance);

        var command = new GrantContentTypePermissionGrantCommand
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid()),
            UserId = _userId,
            ContentTypeName = "Course",
            Permissions = ["Read"]
        };

        var grantId = await handler.Handle(command, CancellationToken.None);
        grantId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task DeleteCommand_RowOfAnotherTenant_IsDenied()
    {
        var otherTenant = Guid.NewGuid();
        var repository = new Mock<IContentTypePermissionGrantRepository>();
        repository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentTypePermissionGrant
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                TenantId = otherTenant,
                ContentTypeName = "Course",
                Permissions = ["Read"]
            });
        var handler = new RevokeContentTypePermissionGrantByIdCommandHandler(
            repository.Object,
            Mock.Of<IContentTypePermissionGrantService>(),
            _actorAccessor.Object);

        var command = new RevokeContentTypePermissionGrantByIdCommand
        {
            GrantId = Guid.NewGuid(),
            TenantId = new GameGuild.CQRS.Models.TenantId(_tenantId)
        };

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>("cross-tenant grant deletion is denied");
    }

    [Fact]
    public async Task GrantsQuery_CrossTenantNonSystemAdmin_IsDenied()
    {
        var handler = new GetContentTypePermissionGrantsQueryHandler(
            Mock.Of<IContentTypePermissionGrantService>(),
            _actorAccessor.Object);

        var query = new GetContentTypePermissionGrantsQuery
        {
            TenantId = new GameGuild.CQRS.Models.TenantId(Guid.NewGuid())
        };

        var act = () => handler.Handle(query, CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
