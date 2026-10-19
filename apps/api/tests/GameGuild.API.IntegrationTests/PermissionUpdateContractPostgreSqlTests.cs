using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Facade = GameGuild.Identity.Authentication.PermissionService;
using Legacy = GameGuild.Identity.Authentication.IPermissionService;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PermissionUpdateContractPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task HostResolvesOneScopedServiceForLegacyAndEveryAddedContract()
    {
        await using var first = fixture.Factory.Services.CreateAsyncScope();
        await using var second = fixture.Factory.Services.CreateAsyncScope();
        var concrete = first.ServiceProvider.GetRequiredService<Facade>();
        Assert.Same(concrete, first.ServiceProvider.GetRequiredService<Legacy>());
        Assert.Same(concrete, first.ServiceProvider.GetRequiredService<IPermissionServiceV2>());
        Assert.Same(concrete, first.ServiceProvider.GetRequiredService<IPermissionReadService>());
        Assert.Same(concrete, first.ServiceProvider.GetRequiredService<IPermissionUpdateService>());
        Assert.Same(concrete, first.ServiceProvider.GetRequiredService<IPermissionLifecycleService>());
        Assert.NotSame(concrete, second.ServiceProvider.GetRequiredService<Facade>());
    }

    [Fact]
    public async Task ThreeNativeFamiliesUpdateAccessStatePreservingIdentityDeniesAndGrantor()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var grantor = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var tenantGrant = new TenantPermission
        {
            TenantId = tenant, UserId = subject, GrantedBy = grantor, GrantedAt = DateTime.UtcNow.Date,
            Permissions = ["documents:read"], DenyPermissions = ["documents:delete"]
        };
        var content = new ContentTypePermission(subject, tenant, "Document") { GrantedBy = grantor };
        // Binding length exceeds the audit family's column bound; the exact binding belongs in the snapshot.
        var binding = new string('R', 256);
        var resource = new GenericResourcePermission(subject, tenant, resourceId, binding) { GrantedBy = grantor };
        content.SetPermissions([PermissionType.Read]);
        resource.SetPermissions([PermissionType.Read]);
        db.AddRange(tenantGrant, content, resource);
        await db.SaveChangesAsync();
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var originalSecurityVersion = await versions.IncrementVersionAsync(tenant.ToString("D"));
        var originalVersions = new[] { tenantGrant.Version, content.Version, resource.Version };
        var originalGrantedAt = tenantGrant.GrantedAt;
        var actors = services.GetRequiredService<IActorContextAccessor>();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(actorId)
            .WithTenantId(tenant).WithRole("TenantAdmin").Build());
        var service = services.GetRequiredService<IPermissionUpdateService>();
        var expiry = DateTime.UtcNow.Date.AddDays(2);

        var tenantResult = await service.UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = tenantGrant.Id, ExpectedVersion = tenantGrant.Version,
            Permissions = ["documents:write", "DOCUMENTS:WRITE", "custom:permission"], ExpiresAt = expiry, Reason = "replace allows"
        });
        var contentResult = await service.UpdateContentTypePermissionAsync(Update(content, [PermissionType.Edit]));
        var resourceResult = await service.UpdateResourcePermissionAsync<GenericResourcePermission, EntityBase>(
            Update(resource, [PermissionType.Edit]) with { IsActive = false });

        Assert.True(tenantResult.IsSuccess);
        Assert.True(contentResult.IsSuccess);
        Assert.True(resourceResult.IsSuccess);
        db.ChangeTracker.Clear();
        var saved = await db.Set<TenantPermission>().SingleAsync(row => row.Id == tenantGrant.Id);
        Assert.Equal(new[] { "documents:write", "custom:permission" }, saved.Permissions);
        Assert.Equal(new[] { "documents:delete" }, saved.DenyPermissions);
        Assert.Equal(subject, saved.UserId);
        Assert.Equal(grantor, saved.GrantedBy);
        Assert.Equal(originalGrantedAt, saved.GrantedAt);
        Assert.Equal(expiry, saved.ExpiresAt);
        Assert.Equal(originalVersions[0] + 1, saved.Version);
        var savedContent = await db.Set<ContentTypePermission>().SingleAsync(row => row.Id == content.Id);
        var savedResource = await db.Set<GenericResourcePermission>().SingleAsync(row => row.Id == resource.Id);
        Assert.Equal("Document", savedContent.ContentTypeName);
        Assert.Equal(grantor, savedContent.GrantedBy);
        Assert.Equal(new[] { PermissionType.Edit }, savedContent.GetPermissionsAsEnum());
        Assert.Equal(originalVersions[1] + 1, savedContent.Version);
        Assert.Equal(resourceId, savedResource.ResourceId);
        Assert.Equal(binding, savedResource.ResourceType);
        Assert.Equal(grantor, savedResource.GrantedBy);
        Assert.False(savedResource.IsActive);
        Assert.Equal(originalVersions[2] + 1, savedResource.Version);
        Assert.Equal(originalSecurityVersion + 3, await versions.GetVersionAsync(tenant.ToString("D")));

        var requiredAudit = await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync();
        Assert.Equal(3, requiredAudit.Count);
        Assert.All(requiredAudit, row =>
        {
            Assert.Equal(PermissionOperationType.Update, row.OperationType);
            Assert.Equal("GrantState", row.PermissionType);
            Assert.Equal(subject, row.UserId);
            Assert.Equal(new TenantId(tenant), row.TenantId);
            Assert.NotNull(row.OldValue);
            Assert.NotNull(row.NewValue);
        });
        var snapshot = JsonDocument.Parse(requiredAudit.Single(row => row.ResourceId == resourceId).NewValue!).RootElement;
        Assert.Equal(binding, snapshot.GetProperty("resourceType").GetString());
        Assert.Equal(resourceId, snapshot.GetProperty("resourceId").GetGuid());
        var centralized = await db.Set<AuditLog>().Where(row => row.UserId == actorId && row.Category == AuditCategory.Permission).ToListAsync();
        Assert.Equal(3, centralized.Count);
        Assert.All(centralized, row => Assert.Equal(tenant, row.TenantId));
        var grantKeys = new[] { tenantGrant.Id.ToString(), content.Id.ToString(), resource.Id.ToString() };
        var entityEvidence = await db.Set<EntityChangeAuditRecord>()
            .Where(row => row.ActorId == actorId && grantKeys.Contains(row.KeyId)).ToListAsync();
        Assert.Equal(3, entityEvidence.Count);
        Assert.All(entityEvidence, row => Assert.Equal(tenant, row.TenantId));
    }

    [Fact]
    public async Task ExplicitGlobalUpdateUsesGlobalVersionAndNullAuditTenantInsideAnotherTenantContext()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var grant = new TenantPermission { UserId = null, TenantId = null, Permissions = ["Read"], GrantedBy = Guid.NewGuid() };
        db.Add(grant);
        await db.SaveChangesAsync();
        var actors = services.GetRequiredService<IActorContextAccessor>();
        var actorId = Guid.NewGuid();
        var actorTenant = Guid.NewGuid();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(actorId).WithTenantId(actorTenant)
            .WithPermission(SystemPermission.Keys.ManageGlobalDefaults).Build());
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var beforeGlobal = await versions.GetVersionAsync(Guid.Empty.ToString("D"));
        var beforeTenant = await versions.GetVersionAsync(actorTenant.ToString("D"));
        var result = await services.GetRequiredService<IPermissionUpdateService>().UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = grant.Id, ExpectedVersion = grant.Version, GlobalScope = true, Permissions = []
        });
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Permissions);
        Assert.True(await versions.GetVersionAsync(Guid.Empty.ToString("D")) > beforeGlobal);
        Assert.Equal(beforeTenant, await versions.GetVersionAsync(actorTenant.ToString("D")));
        var evidence = await db.Set<PermissionAuditLog>().SingleAsync(row => row.PerformedBy == actorId);
        Assert.Null(evidence.TenantId);
        var centralized = await db.Set<AuditLog>().SingleAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission);
        Assert.Null(centralized.TenantId);
        var entityEvidence = await db.Set<EntityChangeAuditRecord>()
            .SingleAsync(row => row.ActorId == actorId && row.KeyId == grant.Id.ToString());
        Assert.Null(entityEvidence.TenantId);
    }

    [Fact]
    public async Task StaleVersionAndAnotherTenantsRecordCannotChangePersistedState()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        var foreign = new TenantPermission { TenantId = Guid.NewGuid(), UserId = grant.UserId, Permissions = ["Read"] };
        db.AddRange(grant, foreign);
        await db.SaveChangesAsync();
        var actorId = Guid.NewGuid();
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        var service = services.GetRequiredService<IPermissionUpdateService>();
        var accepted = new PermissionGrantUpdate<string> { GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"] };
        Assert.True((await service.UpdateTenantPermissionAsync(accepted)).IsSuccess);
        Assert.Equal(ErrorType.Conflict, (await service.UpdateTenantPermissionAsync(accepted)).Error.Type);
        Assert.Equal(ErrorType.NotFound, (await service.UpdateTenantPermissionAsync(accepted with { GrantId = foreign.Id })).Error.Type);
        Assert.Equal(ErrorType.Forbidden, (await service.UpdateTenantPermissionAsync(accepted with { GrantId = foreign.Id, TenantId = foreign.TenantId })).Error.Type);
        db.ChangeTracker.Clear();
        Assert.Equal(new[] { "Read" }, (await db.Set<TenantPermission>().SingleAsync(row => row.Id == foreign.Id)).Permissions);
        Assert.Single(await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync());
    }

    [Fact]
    public async Task FailureAfterRequiredAuditWasWrittenRollsBackGrantVersionAndEveryAuditRow()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(grant);
        await db.SaveChangesAsync();
        var originalVersion = grant.Version;
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var securityVersion = await versions.GetVersionAsync(tenant.ToString("D"));
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        IPermissionUpdateService service = new Facade(db, versions, FailingAudit(services), services.GetRequiredService<IActorContextAccessor>());
        await Assert.ThrowsAsync<AuditFailureException>(() => service.UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"]
        }));
        // The entity was tracked before the savepoint; in-memory and persisted state both recover.
        Assert.Equal(new[] { "Read" }, grant.Permissions);
        Assert.Equal(originalVersion, grant.Version);
        db.ChangeTracker.Clear();
        var saved = await db.Set<TenantPermission>().SingleAsync(row => row.Id == grant.Id);
        Assert.Equal(new[] { "Read" }, saved.Permissions);
        Assert.Equal(originalVersion, saved.Version);
        Assert.Equal(securityVersion, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
        await db.FlushPendingPermissionAuditChangesAsync();
        Assert.False(await db.Set<AuditLog>().AnyAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
        Assert.False(await db.Set<EntityChangeAuditRecord>().AnyAsync(row => row.ActorId == actorId));
    }

    [Fact]
    public async Task AmbientSavepointRollbackPreservesEarlierCallerWorkAndLaterOperationEvidence()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(grant);
        await db.SaveChangesAsync();
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        await using var transaction = await db.Database.BeginTransactionAsync();
        var earlier = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"], GrantedBy = actorId };
        db.Add(earlier);
        await db.SaveChangesAsync();
        var operation = new UseCaseOperationContext("test.permission.update", "PermissionUpdateTest", tenant, actorId, Guid.NewGuid(), null);
        using var operationScope = services.GetRequiredService<IUseCaseOperationContextAccessor>().Begin(operation);
        IPermissionUpdateService service = new Facade(db, services.GetRequiredService<ITenantSecurityVersionStore>(),
            FailingAudit(services), services.GetRequiredService<IActorContextAccessor>());
        await Assert.ThrowsAsync<AuditFailureException>(() => service.UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"]
        }));
        Assert.False(operation.OperationEventCaptured);
        Assert.False(operation.BusinessMutationObserved);
        Assert.Empty(operation.ProducedEventTypes);
        Assert.Equal(new[] { "Read" }, grant.Permissions);
        var later = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"], GrantedBy = actorId };
        db.Add(later);
        await db.SaveChangesAsync();
        Assert.True(operation.OperationEventCaptured);
        var eventId = operation.GetOrCreateOperationEvent("ignored", "ignored").EventId;
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
        await db.FlushPendingPermissionAuditChangesAsync();
        db.ChangeTracker.Clear();
        Assert.True(await db.Set<TenantPermission>().AnyAsync(row => row.Id == earlier.Id));
        Assert.True(await db.Set<TenantPermission>().AnyAsync(row => row.Id == later.Id));
        Assert.True(await db.Set<OutboxMessage>().AnyAsync(row => row.EventId == eventId && row.AggregateId == later.Id.ToString()));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
        var logs = await db.Set<AuditLog>().Where(row => row.UserId == actorId && row.Category == AuditCategory.Permission).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, row => row.ResourceId == earlier.Id.ToString());
        Assert.Contains(logs, row => row.ResourceId == later.Id.ToString());
        Assert.DoesNotContain(logs, row => row.ResourceId == grant.Id.ToString());
    }

    [Fact]
    public async Task PendingCallerMutationIsRejectedWithoutPersistingEitherChange()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        var unrelated = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(unrelated);
        var result = await services.GetRequiredService<IPermissionUpdateService>().UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = Guid.NewGuid(), ExpectedVersion = 1, Permissions = ["Write"]
        });
        Assert.Equal("PermissionWrite.PendingWork", result.Error.Code);
        Assert.Equal(EntityState.Added, db.Entry(unrelated).State);
        Assert.False(await db.Set<TenantPermission>().AnyAsync(row => row.Id == unrelated.Id));
    }

    [Fact]
    public async Task CancellationAfterSecurityVersionWriteStillRollsBackUsingAnUncancelledCleanupToken()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(grant);
        await db.SaveChangesAsync();
        var originalVersion = grant.Version;
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var securityVersion = await versions.GetVersionAsync(tenant.ToString("D"));
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        using var cancellation = new CancellationTokenSource();
        var failingVersion = new Mock<ITenantSecurityVersionStore>(MockBehavior.Strict);
        failingVersion.Setup(value => value.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string target, CancellationToken token) =>
            {
                await versions.IncrementVersionAsync(target, token);
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return -1L;
            });
        IPermissionUpdateService service = new Facade(db, failingVersion.Object,
            services.GetRequiredService<IPermissionAuditService>(), services.GetRequiredService<IActorContextAccessor>());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"]
        }, cancellation.Token));
        Assert.Equal(originalVersion, grant.Version);
        Assert.Equal(new[] { "Read" }, grant.Permissions);
        db.ChangeTracker.Clear();
        Assert.Equal(securityVersion, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.Equal(originalVersion, (await db.Set<TenantPermission>().SingleAsync(row => row.Id == grant.Id)).Version);
        await db.FlushPendingPermissionAuditChangesAsync();
        Assert.False(await db.Set<AuditLog>().AnyAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
    }

    [Fact]
    public async Task SoftDeletedGrantCannotBeReactivatedThroughUpdate()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(grant);
        await db.SaveChangesAsync();
        grant.SoftDelete();
        await db.SaveChangesAsync();
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        var result = await services.GetRequiredService<IPermissionUpdateService>().UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
        {
            GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"]
        });
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.True(grant.IsDeleted);
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
    }

    [Fact]
    public async Task ExplicitChangeDetectionPreservesWritesAndPendingWorkGuardWhenCallerDisablesAutomaticDetection()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        db.Add(grant);
        await db.SaveChangesAsync();
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var beforeSecurityVersion = await versions.GetVersionAsync(tenant.ToString("D"));
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(Guid.NewGuid()).WithTenantId(tenant).WithRole("TenantAdmin").Build());
        var service = services.GetRequiredService<IPermissionUpdateService>();
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        var request = new PermissionGrantUpdate<string> { GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = ["Write"] };
        Assert.True((await service.UpdateTenantPermissionAsync(request)).IsSuccess);
        Assert.False(db.ChangeTracker.AutoDetectChangesEnabled);
        var afterFirst = grant.Version;
        Assert.True((await service.UpdateTenantPermissionAsync(request with { ExpectedVersion = afterFirst })).IsSuccess);
        Assert.Equal(afterFirst + 1, grant.Version);
        db.ChangeTracker.Clear();
        var saved = await db.Set<TenantPermission>().SingleAsync(row => row.Id == grant.Id);
        Assert.Equal(new[] { "Write" }, saved.Permissions);
        Assert.True(await versions.GetVersionAsync(tenant.ToString("D")) >= beforeSecurityVersion + 2);
        saved.Reason = "unpersisted caller edit";
        var rejected = await service.UpdateTenantPermissionAsync(request with { ExpectedVersion = saved.Version });
        Assert.Equal("PermissionWrite.PendingWork", rejected.Error.Code);
        Assert.False(db.ChangeTracker.AutoDetectChangesEnabled);
        Assert.True(db.Entry(saved).Property(row => row.Reason).IsModified);
    }

    [Fact]
    public async Task ConcurrentPublicUpdatesAcceptOneExpectedVersionAndWriteOneRequiredAudit()
    {
        var tenant = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var grant = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"] };
        await using (var seed = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(grant);
            await db.SaveChangesAsync();
        }
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<Result<TenantPermission>> UpdateAsync(string permission)
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
                ActorContextBuilder.ForUser(actorId).WithTenantId(tenant).WithRole("TenantAdmin").Build());
            if (Interlocked.Increment(ref arrived) == 2)
            {
                ready.SetResult(true);
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return await services.GetRequiredService<IPermissionUpdateService>().UpdateTenantPermissionAsync(new PermissionGrantUpdate<string>
            {
                GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = [permission]
            });
        }
        var results = await Task.WhenAll(UpdateAsync("Write"), UpdateAsync("Delete"));
        var success = Assert.Single(results.Where(result => result.IsSuccess));
        Assert.Equal(ErrorType.Conflict, Assert.Single(results.Where(result => result.IsFailure)).Error.Type);
        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await context.Set<TenantPermission>().SingleAsync(row => row.Id == grant.Id);
        Assert.Equal(success.Value.Permissions, saved.Permissions);
        Assert.Equal(grant.Version + 1, saved.Version);
        Assert.Single(await context.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync());
    }

    private static PermissionGrantUpdate<PermissionType> Update(PermissionBase grant, PermissionType[] permissions) => new()
    {
        GrantId = grant.Id, ExpectedVersion = grant.Version, Permissions = permissions
    };

    private static IPermissionAuditService FailingAudit(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IPermissionAuditLogRepository>();
        var failure = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        failure.Setup(value => value.CreateAsync(It.IsAny<PermissionAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(async (PermissionAuditLog row, CancellationToken token) =>
            {
                await repository.CreateAsync(row, token);
                throw new AuditFailureException();
            });
        return new PermissionAuditService(failure.Object, NullLogger<PermissionAuditService>.Instance);
    }

    private sealed class AuditFailureException : Exception { }
}
