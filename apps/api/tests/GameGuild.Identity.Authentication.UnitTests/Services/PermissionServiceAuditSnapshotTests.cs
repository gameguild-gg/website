using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PermissionServiceAuditSnapshotTests
{
    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    public async Task CreateGrant_RecordsNullBeforeAndPersistedStateAfter(string layer)
    {
        await using var fixture = new Fixture();
        await fixture.GrantAsync(layer, PermissionType.Read);

        var entry = fixture.Entries.Should().ContainSingle().Which;
        entry.Operation.Should().Be(PermissionOperationType.Grant);
        entry.OldValue.Should().BeNull();
        var after = Parse(entry.NewValue);
        AssertIdentity(after, fixture, layer);
        ReadPermissions(after).Should().Equal(Encode(layer, PermissionType.Read));
        after.GetProperty("isActive").GetBoolean().Should().BeTrue();
        after.GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.Null);
        entry.PerformedBy.Should().Be(fixture.ActorId);
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(fixture.TenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    public async Task ExtendGrant_FreezesBeforeStateIncludingActivationAndExpiration(string layer)
    {
        await using var fixture = new Fixture();
        var grant = fixture.Seed(layer, [PermissionType.Read]);
        grant.IsActive = false;
        grant.ExpiresAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await fixture.Db.SaveChangesAsync();

        await fixture.GrantAsync(layer, PermissionType.Edit);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        var before = Parse(entry.OldValue);
        var after = Parse(entry.NewValue);
        ReadPermissions(before).Should().Equal(Encode(layer, PermissionType.Read));
        ReadPermissions(after).Should().Equal(Encode(layer, PermissionType.Read), Encode(layer, PermissionType.Edit));
        before.GetProperty("isActive").GetBoolean().Should().BeFalse();
        before.GetProperty("expiresAt").GetDateTime().Should().Be(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        after.GetProperty("isActive").GetBoolean().Should().BeTrue();
        after.GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.Null);
        before.GetProperty("grantId").GetGuid().Should().Be(after.GetProperty("grantId").GetGuid());
        grant.IsActive = false;
        ReadPermissions(Parse(entry.OldValue)).Should().Equal(Encode(layer, PermissionType.Read));
        Parse(entry.NewValue).GetProperty("isActive").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TenantSnapshot_PreservesDenyAndGrantorButExcludesArbitraryMetadataAndReason()
    {
        await using var fixture = new Fixture();
        var grant = (TenantPermission)fixture.Seed("Tenant", [PermissionType.Read]);
        grant.DenyPermissions = ["restricted:write"];
        grant.GrantedBy = fixture.ActorId;
        grant.Reason = "private free-form reason";
        grant.Metadata = new Dictionary<string, object> { ["private"] = "private metadata" };
        await fixture.Db.SaveChangesAsync();

        await fixture.GrantAsync("Tenant", PermissionType.Edit);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        foreach (var value in new[] { entry.OldValue, entry.NewValue })
        {
            var state = Parse(value);
            state.GetProperty("deniedPermissions").EnumerateArray().Select(item => item.GetString()).Should().Equal("restricted:write");
            state.GetProperty("grantedBy").GetGuid().Should().Be(fixture.ActorId);
            state.TryGetProperty("metadata", out _).Should().BeFalse();
            state.TryGetProperty("reason", out _).Should().BeFalse();
            value.Should().NotContain("private metadata").And.NotContain("private free-form reason");
        }
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    public async Task PartialRevoke_RecordsRemainingPermissions(string layer)
    {
        await using var fixture = new Fixture();
        fixture.Seed(layer, [PermissionType.Read, PermissionType.Edit]);
        await fixture.Db.SaveChangesAsync();

        await fixture.RevokeAsync(layer, PermissionType.Edit);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        entry.Operation.Should().Be(PermissionOperationType.Revoke);
        ReadPermissions(Parse(entry.OldValue)).Should().Equal(Encode(layer, PermissionType.Read), Encode(layer, PermissionType.Edit));
        ReadPermissions(Parse(entry.NewValue)).Should().Equal(Encode(layer, PermissionType.Read));
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    public async Task FullRevoke_RecordsSoftDeletionOrNullAfterPhysicalDeletion(string layer)
    {
        await using var fixture = new Fixture();
        fixture.Seed(layer, [PermissionType.Read]);
        await fixture.Db.SaveChangesAsync();

        await fixture.RevokeAsync(layer, PermissionType.Read);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        ReadPermissions(Parse(entry.OldValue)).Should().Equal(Encode(layer, PermissionType.Read));
        if (layer == "Tenant")
        {
            var after = Parse(entry.NewValue);
            after.GetProperty("isDeleted").GetBoolean().Should().BeTrue();
            after.GetProperty("deletedAt").ValueKind.Should().Be(JsonValueKind.String);
            ReadPermissions(after).Should().BeEmpty();
        }
        else
        {
            entry.NewValue.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    public async Task MissingRevoke_DoesNotPublishMutationOrVersionBump(string layer)
    {
        await using var fixture = new Fixture();
        await fixture.RevokeAsync(layer, PermissionType.Read);
        fixture.Entries.Should().BeEmpty();
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Defaults_RecordCreationAndReplacementInTheirActualScope(bool global)
    {
        await using var fixture = new Fixture();
        if (global)
        {
            await fixture.Service.SetGlobalDefaultPermissionsAsync([PermissionType.Read]);
            await fixture.Service.SetGlobalDefaultPermissionsAsync([PermissionType.Edit]);
        }
        else
        {
            await fixture.Service.SetTenantDefaultPermissionsAsync(fixture.TenantId, [PermissionType.Read]);
            await fixture.Service.SetTenantDefaultPermissionsAsync(fixture.TenantId, [PermissionType.Edit]);
        }

        fixture.Entries.Should().HaveCount(2);
        fixture.Entries[0].OldValue.Should().BeNull();
        var replaced = fixture.Entries[1];
        ReadPermissions(Parse(replaced.OldValue)).Should().Equal("Read");
        ReadPermissions(Parse(replaced.NewValue)).Should().Equal("Write");
        Parse(replaced.NewValue).GetProperty("userId").ValueKind.Should().Be(JsonValueKind.Null);
        replaced.TenantId.Should().Be(global ? null : fixture.TenantId);
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(global ? Guid.Empty.ToString() : fixture.TenantId.ToString(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShareResource_RecordsFinalExpiryInOnePersistedMutation(bool existing)
    {
        await using var fixture = new Fixture();
        if (existing)
        {
            fixture.Seed("Resource", [PermissionType.Read]);
            await fixture.Db.SaveChangesAsync();
        }
        var expiry = new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var savesBefore = fixture.Db.SuccessfulSaveCount;

        await fixture.Service.ShareResourceAsync<GenericResourcePermission, EntityBase>(fixture.ResourceId, fixture.UserId, fixture.TenantId, [PermissionType.Edit], expiry);
        fixture.Db.SuccessfulSaveCount.Should().Be(savesBefore + 1);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        if (existing)
        {
            ReadPermissions(Parse(entry.OldValue)).Should().Equal(Encode("Resource", PermissionType.Read));
        }
        else
        {
            entry.OldValue.Should().BeNull();
        }
        var after = Parse(entry.NewValue);
        after.GetProperty("expiresAt").GetDateTime().Should().Be(expiry);
        after.GetProperty("resourceId").GetGuid().Should().Be(fixture.ResourceId);
        after.GetProperty("resourceType").GetString().Should().Be(nameof(EntityBase));
        fixture.Db.Set<GenericResourcePermission>().Single().ExpiresAt.Should().Be(expiry);
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(fixture.TenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShareResource_ClearingExpiryCapturesPreviousDate()
    {
        await using var fixture = new Fixture();
        var grant = fixture.Seed("Resource", [PermissionType.Read]);
        var expiry = new DateTime(2030, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        grant.ExpiresAt = expiry;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.ShareResourceAsync<GenericResourcePermission, EntityBase>(fixture.ResourceId, fixture.UserId, fixture.TenantId, [PermissionType.Read]);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        Parse(entry.OldValue).GetProperty("expiresAt").GetDateTime().Should().Be(expiry);
        Parse(entry.NewValue).GetProperty("expiresAt").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Cleanup_AuditsEveryExpiredGrantAndInvalidatesTenantAndGlobalOnce()
    {
        await using var fixture = new Fixture();
        var expiry = SystemClock.UtcNow.AddDays(-1);
        foreach (var layer in new[] { "Tenant", "ContentType", "Resource" })
        {
            fixture.Seed(layer, [PermissionType.Read]).ExpiresAt = expiry;
        }
        fixture.Db.Set<TenantPermission>().Add(new TenantPermission { Version = 1, UserId = null, TenantId = null, Permissions = ["Read"], ExpiresAt = expiry });
        fixture.Db.Set<TenantPermission>().Add(new TenantPermission { Version = 1, UserId = Guid.NewGuid(), TenantId = fixture.TenantId, Permissions = ["Read"], ExpiresAt = SystemClock.UtcNow.AddDays(1) });
        fixture.Db.Set<TenantPermission>().Add(new TenantPermission { Version = 1, UserId = Guid.NewGuid(), TenantId = fixture.TenantId, Permissions = ["Read"], ExpiresAt = expiry, IsActive = false });
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.CleanupExpiredPermissionsAsync();
        fixture.Entries.Should().HaveCount(4);
        fixture.Entries.Select(entry => Parse(entry.NewValue).GetProperty("grantId").GetGuid()).Distinct().Should().HaveCount(4);
        foreach (var entry in fixture.Entries)
        {
            entry.Operation.Should().Be(PermissionOperationType.Update);
            entry.PerformedBy.Should().Be(fixture.ActorId);
            Parse(entry.OldValue).GetProperty("isActive").GetBoolean().Should().BeTrue();
            Parse(entry.OldValue).GetProperty("expiresAt").GetDateTime().Should().Be(expiry);
            Parse(entry.NewValue).GetProperty("isActive").GetBoolean().Should().BeFalse();
            entry.Reason.Should().Be("Expired permissions cleanup");
        }
        fixture.Entries.Single(entry => entry.ResourceType == nameof(EntityBase)).ResourceId.Should().Be(fixture.ResourceId);
        fixture.Entries.Single(entry => entry.ResourceType == "Document").UserId.Should().Be(fixture.UserId);
        fixture.Entries.Single(entry => entry.TenantId == null).UserId.Should().BeNull();
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(fixture.TenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cleanup_WithoutExpiredGrants_DoesNotProduceFalseAuditEntries()
    {
        await using var fixture = new Fixture();
        fixture.Seed("Tenant", [PermissionType.Read]);
        await fixture.Db.SaveChangesAsync();
        await fixture.Service.CleanupExpiredPermissionsAsync();
        fixture.Entries.Should().BeEmpty();
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Tenant")]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    [InlineData("Share")]
    [InlineData("Cleanup")]
    public async Task PersistenceFailure_DoesNotPublishSuccessfulAuditOrInvalidate(string operation)
    {
        await using var fixture = new Fixture();
        if (operation == "Cleanup")
        {
            fixture.Seed("Tenant", [PermissionType.Read]).ExpiresAt = SystemClock.UtcNow.AddDays(-1);
            await fixture.Db.SaveChangesAsync();
        }
        fixture.Db.FailNextSave = true;
        Func<Task> act = () => operation switch
        {
            "Share" => fixture.Service.ShareResourceAsync<GenericResourcePermission, EntityBase>(fixture.ResourceId, fixture.UserId, fixture.TenantId, [PermissionType.Read], SystemClock.UtcNow.AddDays(1)),
            "Cleanup" => fixture.Service.CleanupExpiredPermissionsAsync(),
            _ => fixture.GrantAsync(operation, PermissionType.Read)
        };
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("save failed");
        fixture.Entries.Should().BeEmpty();
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VersionFailure_PropagatesBeforeSuccessfulAudit()
    {
        await using var fixture = new Fixture();
        fixture.VersionStore.Setup(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("version failed"));
        Func<Task> act = () => fixture.GrantAsync("Tenant", PermissionType.Read);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("version failed");
        fixture.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditFailure_PropagatesAfterPersistenceAndInvalidation()
    {
        await using var fixture = new Fixture();
        fixture.SetupAuditFailure();
        Func<Task> act = () => fixture.GrantAsync("Tenant", PermissionType.Read);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("audit failed");
        fixture.Db.Set<TenantPermission>().Should().ContainSingle();
        fixture.VersionStore.Verify(store => store.IncrementVersionAsync(fixture.TenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BulkAndMembershipPaths_UseTheSameSnapshotCapture()
    {
        await using var fixture = new Fixture();
        var secondUser = Guid.NewGuid();
        await fixture.Service.BulkGrantTenantPermissionAsync([fixture.UserId, secondUser, fixture.UserId, Guid.Empty], fixture.TenantId, [PermissionType.Read]);
        fixture.Entries.Should().HaveCount(2);
        fixture.Entries.Should().OnlyContain(entry => entry.OldValue == null && entry.NewValue != null);
        fixture.Db.Set<TenantPermission>().Single(grant => grant.UserId == fixture.UserId).Version = 1; // The production version interceptor is absent from this test context.
        await fixture.Service.LeaveTenantAsync(fixture.UserId, fixture.TenantId);
        fixture.Entries.Should().HaveCount(3);
        Parse(fixture.Entries[2].NewValue).GetProperty("isDeleted").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData("ContentType")]
    [InlineData("Resource")]
    public async Task NonTenantSnapshot_ExcludesFreeFormNotesAndDisplayFields(string layer)
    {
        await using var fixture = new Fixture();
        var grant = (WithPermissions)fixture.Seed(layer, [PermissionType.Read]);
        grant.Notes = "private free-form notes";
        grant.GrantedBy = fixture.ActorId;
        if (grant is ContentTypePermission content)
        {
            content.Description = "private description";
        }
        if (grant is GenericResourcePermission resource)
        {
            resource.ResourceTitle = "private resource title";
        }
        await fixture.Db.SaveChangesAsync();
        await fixture.GrantAsync(layer, PermissionType.Edit);
        var entry = fixture.Entries.Should().ContainSingle().Which;
        foreach (var value in new[] { entry.OldValue, entry.NewValue })
        {
            var state = Parse(value);
            state.GetProperty("grantedBy").GetGuid().Should().Be(fixture.ActorId);
            state.TryGetProperty("notes", out _).Should().BeFalse();
            state.TryGetProperty("description", out _).Should().BeFalse();
            state.TryGetProperty("resourceTitle", out _).Should().BeFalse();
            value.Should().NotContain("private free-form notes").And.NotContain("private description").And.NotContain("private resource title");
        }
    }

    private static void AssertIdentity(JsonElement state, Fixture fixture, string layer)
    {
        state.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        state.GetProperty("grantId").GetGuid().Should().NotBeEmpty();
        state.GetProperty("layer").GetString().Should().Be(layer);
        state.GetProperty("userId").GetGuid().Should().Be(fixture.UserId);
        state.GetProperty("tenantId").GetGuid().Should().Be(fixture.TenantId);
        if (layer == "ContentType")
        {
            state.GetProperty("contentTypeName").GetString().Should().Be("Document");
        }
        if (layer == "Resource")
        {
            state.GetProperty("resourceId").GetGuid().Should().Be(fixture.ResourceId);
        }
    }

    private static string Encode(string layer, PermissionType permission) => layer == "Tenant" ? permission.ToString() : ((int)permission).ToString(CultureInfo.InvariantCulture);
    private static string?[] ReadPermissions(JsonElement state) => state.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).ToArray();
    private static JsonElement Parse(string? value)
    {
        value.Should().NotBeNull();
        using var document = JsonDocument.Parse(value!);
        return document.RootElement.Clone();
    }

    private sealed record AuditEntry(PermissionOperationType Operation, Guid? UserId, Guid PerformedBy, Guid? TenantId, Guid? ResourceId, string? ResourceType, string? OldValue, string? NewValue, string? Reason);

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid ActorId { get; } = Guid.NewGuid();
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid ResourceId { get; } = Guid.NewGuid();
        public SnapshotDb Db { get; } = new();
        public Mock<IPermissionAuditService> Audit { get; } = new();
        public Mock<ITenantSecurityVersionStore> VersionStore { get; } = new();
        public PermissionService Service { get; }
        public List<AuditEntry> Entries => Audit.Invocations
            .Where(invocation => invocation.Method.Name == nameof(IPermissionAuditService.LogPermissionChangeAsync))
            .Select(invocation => new AuditEntry((PermissionOperationType)invocation.Arguments[0], (Guid?)invocation.Arguments[1], (Guid)invocation.Arguments[2], (Guid?)invocation.Arguments[3], (Guid?)invocation.Arguments[5], (string?)invocation.Arguments[6], (string?)invocation.Arguments[7], (string?)invocation.Arguments[8], (string?)invocation.Arguments[9]))
            .ToList();

        public Fixture()
        {
            var actor = new Mock<IActorContextAccessor>();
            actor.SetupGet(accessor => accessor.ActorContext).Returns(ActorContextBuilder.ForUser(ActorId).WithTenantId(TenantId).Build());
            VersionStore.Setup(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
            Audit.Setup(audit => audit.LogPermissionChangeAsync(It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PermissionAuditLog());
            Service = new PermissionService(Db, VersionStore.Object, Audit.Object, actor.Object);
        }

        public void SetupAuditFailure() => Audit.Setup(audit => audit.LogPermissionChangeAsync(It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("audit failed"));

        public PermissionBase Seed(string layer, PermissionType[] permissions)
        {
            if (layer == "Tenant")
            {
                var grant = new TenantPermission { Version = 1, UserId = UserId, TenantId = TenantId, Permissions = permissions.Select(permission => permission.ToString()).ToArray() };
                Db.Set<TenantPermission>().Add(grant);
                return grant;
            }
            if (layer == "ContentType")
            {
                var grant = new ContentTypePermission(UserId, TenantId, "Document") { Version = 1 };
                grant.SetPermissions(permissions);
                Db.Set<ContentTypePermission>().Add(grant);
                return grant;
            }
            var resource = new GenericResourcePermission(UserId, TenantId, ResourceId, nameof(EntityBase)) { Version = 1 };
            resource.SetPermissions(permissions);
            Db.Set<GenericResourcePermission>().Add(resource);
            return resource;
        }

        public async Task GrantAsync(string layer, PermissionType permission)
        {
            switch (layer)
            {
                case "Tenant": await Service.GrantTenantPermissionAsync(UserId, TenantId, [permission]); break;
                case "ContentType": await Service.GrantContentTypePermissionAsync(UserId, TenantId, "Document", [permission]); break;
                case "Resource": await Service.GrantResourcePermissionAsync<GenericResourcePermission, EntityBase>(UserId, TenantId, ResourceId, [permission]); break;
                default: throw new ArgumentException("Unknown permission layer.", nameof(layer));
            }
        }

        public Task RevokeAsync(string layer, PermissionType permission) => layer switch
        {
            "Tenant" => Service.RevokeTenantPermissionAsync(UserId, TenantId, [permission]),
            "ContentType" => Service.RevokeContentTypePermissionAsync(UserId, TenantId, "Document", [permission]),
            "Resource" => Service.RevokeResourceAccessAsync<GenericResourcePermission, EntityBase>(UserId, TenantId, ResourceId),
            _ => throw new ArgumentException("Unknown permission layer.", nameof(layer))
        };

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class SnapshotDb : DbContext, IApplicationDbContext
    {
        public bool FailNextSave { get; set; }
        public int SuccessfulSaveCount { get; private set; }
        protected override void OnConfiguring(DbContextOptionsBuilder builder) => builder.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<TenantPermission>().Ignore(grant => grant.Metadata);
            builder.Entity<ContentTypePermission>();
            builder.Entity<GenericResourcePermission>();
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new InvalidOperationException("save failed");
            }
            var result = await base.SaveChangesAsync(cancellationToken);
            SuccessfulSaveCount++;
            return result;
        }
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
    }
}
