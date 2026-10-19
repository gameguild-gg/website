using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Facade = GameGuild.Identity.Authentication.PermissionService;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PermissionLifecycleContractPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task ActualHostCreatesAndDeletesAllThreeFamiliesWithPersistedSnapshotsAndAuthenticatedGrantor()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid(); var subject = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var service = services.GetRequiredService<IPermissionLifecycleService>();
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var initialVersion = await versions.IncrementVersionAsync(tenant.ToString("D"));
        var expiry = DateTime.UtcNow.AddDays(2).AddTicks(1);
        var tenantResult = await service.CreateTenantPermissionAsync(new TenantPermissionCreation
            { UserId = subject, Permissions = ["custom:read", "CUSTOM:READ"], ExpiresAt = expiry });
        var contentResult = await service.CreateContentTypePermissionAsync(new ContentTypePermissionCreation
            { UserId = subject, ContentTypeName = "Document", Permissions = [PermissionType.Read] });
        var resourceId = Guid.NewGuid(); var binding = new string('R', 256);
        var resourceResult = await service.CreateResourcePermissionAsync<GenericResourcePermission, EntityBase>(new ResourcePermissionCreation
            { UserId = subject, ResourceId = resourceId, ResourceType = binding, Permissions = [PermissionType.Edit] });
        Assert.True(tenantResult.IsSuccess); Assert.True(contentResult.IsSuccess); Assert.True(resourceResult.IsSuccess);
        Assert.Equal(new[] { "custom:read" }, tenantResult.Value.Permissions);
        Assert.Equal(actorId, tenantResult.Value.GrantedBy);
        Assert.Equal(actorId, contentResult.Value.GrantedBy);
        Assert.Equal(actorId, resourceResult.Value.GrantedBy);
        Assert.Equal(binding, resourceResult.Value.ResourceType);
        Assert.Equal(resourceId, resourceResult.Value.ResourceId);
        var persisted = await db.Set<TenantPermission>().AsNoTracking().SingleAsync(row => row.Id == tenantResult.Value.Id);
        Assert.Equal(persisted.ExpiresAt, tenantResult.Value.ExpiresAt);
        Assert.Equal(persisted.GrantedAt, tenantResult.Value.GrantedAt);
        Assert.Equal(initialVersion + 3, await versions.GetVersionAsync(tenant.ToString("D")));
        var grantLogs = await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId && row.OperationType == PermissionOperationType.Grant).ToListAsync();
        Assert.Equal(3, grantLogs.Count);
        Assert.All(grantLogs, row => { Assert.Null(row.OldValue); Assert.Equal("GrantState", row.PermissionType); });
        var grantSnapshot = JsonDocument.Parse(grantLogs.Single(row => row.ResourceType == nameof(TenantPermission)).NewValue!).RootElement;
        Assert.Equal(persisted.ExpiresAt, grantSnapshot.GetProperty("expiresAt").GetDateTime());
        Assert.Equal(persisted.GrantedAt, grantSnapshot.GetProperty("grantedAt").GetDateTime());

        var deletedTenant = await service.DeleteTenantPermissionAsync(Delete(tenantResult.Value));
        var deletedContent = await service.DeleteContentTypePermissionAsync(Delete(contentResult.Value));
        var deletedResource = await service.DeleteResourcePermissionAsync<GenericResourcePermission, EntityBase>(Delete(resourceResult.Value));
        Assert.True(deletedTenant.IsSuccess); Assert.True(deletedContent.IsSuccess); Assert.True(deletedResource.IsSuccess);
        Assert.All(new PermissionBase[] { deletedTenant.Value, deletedContent.Value, deletedResource.Value }, grant =>
        {
            Assert.True(grant.IsDeleted); Assert.False(grant.IsActive); Assert.Equal(2, grant.Version);
        });
        Assert.Equal(initialVersion + 6, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.Equal(ErrorType.NotFound, (await service.DeleteTenantPermissionAsync(Delete(deletedTenant.Value))).Error.Type);
        var history = await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync();
        Assert.Equal(6, history.Count);
        Assert.All(history.Where(row => row.OperationType == PermissionOperationType.Revoke), row =>
        {
            var after = JsonDocument.Parse(row.NewValue!).RootElement;
            Assert.True(after.GetProperty("isDeleted").GetBoolean());
            Assert.False(after.GetProperty("isActive").GetBoolean());
        });
        Assert.Equal(6, await db.Set<AuditLog>().CountAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("content")]
    [InlineData("resource")]
    public async Task EachFamilyBulkGrantAndRevokeKeepsOrderAndUsesOneVersionBumpPerBatch(string family)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var baseline = await versions.IncrementVersionAsync(tenant.ToString("D"));
        var service = services.GetRequiredService<IPermissionLifecycleService>();
        var created = await CreateBatchAsync(service, family, 5);
        Assert.True(created.Applied);
        Assert.Equal(Enumerable.Range(0, 5), created.Items.Select(item => item.Index));
        Assert.All(created.Items, item => Assert.True(item.Result.IsSuccess));
        Assert.Equal(baseline + 1, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.Equal(5, await db.Set<PermissionAuditLog>().CountAsync(row => row.PerformedBy == actorId));
        var createdAudit = await db.Set<AuditLog>().SingleAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission);
        Assert.Equal(5, JsonDocument.Parse(createdAudit.Metadata!).RootElement.GetProperty("Changes").GetArrayLength());
        var deletions = created.Items.Select(item => Delete(item.Result.Value)).Reverse().ToArray();
        var revoked = family switch
        {
            "tenant" => await AsBase(service.BulkRevokeTenantPermissionsAsync(deletions)),
            "content" => await AsBase(service.BulkRevokeContentTypePermissionsAsync(deletions)),
            _ => await AsBase(service.BulkRevokeResourcePermissionsAsync<GenericResourcePermission, EntityBase>(deletions))
        };
        Assert.True(revoked.Applied);
        Assert.Equal(deletions.Select(item => item.GrantId), revoked.Items.Select(item => item.Result.Value.Id));
        Assert.All(revoked.Items, item => Assert.True(item.Result.Value.IsDeleted));
        Assert.Equal(baseline + 2, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.Equal(10, await db.Set<PermissionAuditLog>().CountAsync(row => row.PerformedBy == actorId));
        Assert.Equal(2, await db.Set<AuditLog>().CountAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
    }

    [Fact]
    public async Task ExistingBindingRejectsWholeCreateBatchAndRollsBackItsProvisionalVersionWrite()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var service = services.GetRequiredService<IPermissionLifecycleService>();
        var original = Creation();
        Assert.True((await service.CreateTenantPermissionAsync(original)).IsSuccess);
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var before = await versions.GetVersionAsync(tenant.ToString("D"));
        var proposed = Creation();
        var result = await service.BulkGrantTenantPermissionsAsync([proposed, original]);
        Assert.True(result.IsSuccess); Assert.False(result.Value.Applied);
        Assert.Equal("PermissionWrite.BatchNotApplied", result.Value.Items[0].Result.Error.Code);
        Assert.Equal("PermissionWrite.AlreadyExists", result.Value.Items[1].Result.Error.Code);
        db.ChangeTracker.Clear();
        Assert.Equal(before, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.False(await db.Set<TenantPermission>().AnyAsync(row => row.TenantId == tenant && row.UserId == proposed.UserId));
        Assert.Single(await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync());
        await db.FlushPendingPermissionAuditChangesAsync();
        Assert.Equal(1, await db.Set<AuditLog>().CountAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
    }

    [Fact]
    public async Task RevokeBatchRetainsMissingAndStaleErrorsAndDoesNotDeleteItsValidSibling()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var service = services.GetRequiredService<IPermissionLifecycleService>();
        var created = await service.BulkGrantTenantPermissionsAsync([Creation(), Creation()]);
        Assert.True(created.IsSuccess); Assert.True(created.Value.Applied);
        var first = created.Value.Items[0].Result.Value; var second = created.Value.Items[1].Result.Value;
        var versions = services.GetRequiredService<ITenantSecurityVersionStore>();
        var before = await versions.GetVersionAsync(tenant.ToString("D"));
        var result = await service.BulkRevokeTenantPermissionsAsync([Delete(first), Delete(second) with { ExpectedVersion = second.Version + 1 },
            new PermissionGrantDeletion { GrantId = Guid.NewGuid(), ExpectedVersion = 1 }]);
        Assert.True(result.IsSuccess); Assert.False(result.Value.Applied);
        Assert.Equal("PermissionWrite.BatchNotApplied", result.Value.Items[0].Result.Error.Code);
        Assert.Equal(ErrorType.Conflict, result.Value.Items[1].Result.Error.Type);
        Assert.Equal(ErrorType.NotFound, result.Value.Items[2].Result.Error.Type);
        db.ChangeTracker.Clear();
        Assert.False((await db.Set<TenantPermission>().SingleAsync(row => row.Id == first.Id)).IsDeleted);
        Assert.False((await db.Set<TenantPermission>().SingleAsync(row => row.Id == second.Id)).IsDeleted);
        Assert.Equal(before, await versions.GetVersionAsync(tenant.ToString("D")));
        Assert.Equal(2, await db.Set<PermissionAuditLog>().CountAsync(row => row.PerformedBy == actorId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureAfterSecondRequiredAuditWriteRollsBackEveryGrantVersionAndAuditStore(bool legacy)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var real = services.GetRequiredService<IPermissionAuditLogRepository>();
        var attempts = 0;
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(value => value.CreateAsync(It.IsAny<PermissionAuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(async (PermissionAuditLog row, CancellationToken token) =>
            {
                var saved = await real.CreateAsync(row, token);
                if (++attempts == 2) { throw new AuditFailureException(); }
                return saved;
            });
        var service = new Facade(db, services.GetRequiredService<ITenantSecurityVersionStore>(),
            new PermissionAuditService(repository.Object, NullLogger<PermissionAuditService>.Instance), services.GetRequiredService<IActorContextAccessor>());
        Func<Task> mutation = legacy
            ? () => service.BulkGrantTenantPermissionAsync([Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], tenant, [PermissionType.Read])
            : () => service.BulkGrantTenantPermissionsAsync([Creation(), Creation(), Creation()]);
        await Assert.ThrowsAsync<AuditFailureException>(mutation);
        Assert.Equal(2, attempts);
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<TenantPermission>().AnyAsync(row => row.TenantId == tenant));
        Assert.Equal(0L, await services.GetRequiredService<ITenantSecurityVersionStore>().GetVersionAsync(tenant.ToString("D")));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
        Assert.False(await db.Set<EntityChangeAuditRecord>().AnyAsync(row => row.ActorId == actorId));
        await db.FlushPendingPermissionAuditChangesAsync();
        Assert.False(await db.Set<AuditLog>().AnyAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentSameBindingCreationAcceptsOneGrantIncludingTheInitialVersionRowRace(bool initializeVersion)
    {
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid(); var request = Creation();
        if (initializeVersion)
        {
            await using var setup = fixture.Factory.Services.CreateAsyncScope();
            await setup.ServiceProvider.GetRequiredService<ITenantSecurityVersionStore>().IncrementVersionAsync(tenant.ToString("D"));
        }
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<Result<TenantPermission>> CreateAsync()
        {
            await using var scope = fixture.Factory.Services.CreateAsyncScope();
            using var actor = BeginActor(scope.ServiceProvider, actorId, tenant);
            if (Interlocked.Increment(ref arrived) == 2) { ready.SetResult(true); }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return await scope.ServiceProvider.GetRequiredService<IPermissionLifecycleService>().CreateTenantPermissionAsync(request);
        }
        var outcomes = await Task.WhenAll(CreateAsync(), CreateAsync());
        Assert.Single(outcomes, outcome => outcome.IsSuccess);
        Assert.Equal(ErrorType.Conflict, Assert.Single(outcomes, outcome => outcome.IsFailure).Error.Type);
        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Set<TenantPermission>().Where(row => row.TenantId == tenant && row.UserId == request.UserId && row.DeletedAt == null).ToListAsync());
        Assert.Single(await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync());
    }

    [Fact]
    public async Task RejectedAmbientBatchRestoresOperationCheckpointAndPreservesEarlierCallerEvidence()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        var earlier = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"], GrantedBy = actorId };
        var later = new TenantPermission { TenantId = tenant, UserId = Guid.NewGuid(), Permissions = ["Read"], GrantedBy = actorId };
        var operation = new UseCaseOperationContext("test.permission.batch", "PermissionBatchTest", tenant, actorId, Guid.NewGuid(), null);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Add(earlier); await db.SaveChangesAsync();
            using var operationScope = services.GetRequiredService<IUseCaseOperationContextAccessor>().Begin(operation);
            var result = await services.GetRequiredService<IPermissionLifecycleService>().BulkGrantTenantPermissionsAsync(
                [new TenantPermissionCreation { UserId = earlier.UserId, Permissions = ["Write"] }, Creation()]);
            Assert.True(result.IsSuccess); Assert.False(result.Value.Applied);
            Assert.False(operation.OperationEventCaptured); Assert.False(operation.BusinessMutationObserved);
            Assert.Empty(operation.ProducedEventTypes);
            db.Add(later); await db.SaveChangesAsync();
            Assert.True(operation.OperationEventCaptured);
            await transaction.CommitAsync();
        }
        await db.FlushPendingPermissionAuditChangesAsync();
        var eventId = operation.GetOrCreateOperationEvent("ignored", "ignored").EventId;
        Assert.True(await db.Set<OutboxMessage>().AnyAsync(row => row.EventId == eventId && row.AggregateId == later.Id.ToString()));
        Assert.Equal(2, await db.Set<TenantPermission>().CountAsync(row => row.TenantId == tenant));
        Assert.Equal(2, await db.Set<AuditLog>().CountAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
    }

    [Fact]
    public async Task GlobalDefaultCreationKeepsExplicitNullScopeAndAuthenticatedActorInEveryAuditStore()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var actorId = Guid.NewGuid(); var actorTenant = Guid.NewGuid(); var type = "Document-" + Guid.NewGuid().ToString("N");
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(actorTenant).WithPermission(SystemPermission.Keys.ManageGlobalDefaults).Build());
        var result = await services.GetRequiredService<IPermissionLifecycleService>().CreateContentTypePermissionAsync(new ContentTypePermissionCreation
            { GlobalScope = true, DefaultSubject = true, ContentTypeName = type, Permissions = [PermissionType.Read] });
        Assert.True(result.IsSuccess); Assert.Null(result.Value.TenantId); Assert.Null(result.Value.UserId);
        Assert.Equal(actorId, result.Value.GrantedBy);
        Assert.Null((await db.Set<PermissionAuditLog>().SingleAsync(row => row.PerformedBy == actorId)).TenantId);
        Assert.Null((await db.Set<AuditLog>().SingleAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission)).TenantId);
        Assert.Null((await db.Set<EntityChangeAuditRecord>().SingleAsync(row => row.ActorId == actorId && row.KeyId == result.Value.Id.ToString())).TenantId);
        Assert.Equal(0L, await services.GetRequiredService<ITenantSecurityVersionStore>().GetVersionAsync(actorTenant.ToString("D")));
    }

    [Fact]
    public async Task AppliedAmbientBatchStillBelongsToTheCallersTransactionAndCanBeRolledBackByItsOwner()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var tenant = Guid.NewGuid(); var actorId = Guid.NewGuid();
        using var actor = BeginActor(services, actorId, tenant);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var result = await services.GetRequiredService<IPermissionLifecycleService>().BulkGrantTenantPermissionsAsync([Creation(), Creation()]);
            Assert.True(result.IsSuccess); Assert.True(result.Value.Applied);
            await using var outsider = fixture.Factory.Services.CreateAsyncScope();
            Assert.False(await outsider.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<TenantPermission>().AnyAsync(row => row.TenantId == tenant));
            await transaction.RollbackAsync();
        }
        db.DiscardPendingPermissionAuditChanges();
        db.ChangeTracker.Clear();
        Assert.False(await db.Set<TenantPermission>().AnyAsync(row => row.TenantId == tenant));
        Assert.False(await db.Set<PermissionAuditLog>().AnyAsync(row => row.PerformedBy == actorId));
        Assert.False(await db.Set<AuditLog>().AnyAsync(row => row.UserId == actorId && row.Category == AuditCategory.Permission));
    }

    [Fact]
    public async Task CompatibleCrossTenantCleanupIsAtomicAndUsesEachRecordsTenantWithTheActualAdministrator()
    {
        // This operation intentionally scans all tenants. Give it an isolated migrated database
        // so it cannot expire records prepared by another test in the shared collection fixture.
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("permission_cleanup_contract");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(database.ConnectionString,
                provider => provider.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
        await using (var bootstrap = new ApplicationDbContext(options)) { await bootstrap.Database.MigrateAsync(); }
        using var isolatedFactory = fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
                ["ConnectionStrings:MigrationConnection"] = database.ConnectionString
            }));
            builder.ConfigureTestServices(registrations =>
            {
                registrations.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                registrations.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                registrations.AddDbContext<ApplicationDbContext>(dbOptions => dbOptions.UseNpgsql(database.ConnectionString,
                    provider => provider.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
            });
        });
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var actorId = Guid.NewGuid(); var grantor = Guid.NewGuid(); var firstTenant = Guid.NewGuid(); var secondTenant = Guid.NewGuid();
        var expired = DateTime.UtcNow.Date.AddDays(-1);
        var global = new TenantPermission { UserId = null, TenantId = null, Permissions = ["Read"], GrantedBy = grantor, ExpiresAt = expired };
        var content = new ContentTypePermission(Guid.NewGuid(), firstTenant, "Document") { GrantedBy = grantor, ExpiresAt = expired };
        var resource = new GenericResourcePermission(Guid.NewGuid(), secondTenant, Guid.NewGuid(), "Document") { GrantedBy = grantor, ExpiresAt = expired };
        content.SetPermissions([PermissionType.Read]); resource.SetPermissions([PermissionType.Read]);
        db.AddRange(global, content, resource); await db.SaveChangesAsync();
        using var actor = services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(
            ActorContextBuilder.ForUser(actorId).WithTenantId(Guid.NewGuid()).WithRole("SystemAdmin").Build());
        await services.GetRequiredService<Facade>().CleanupExpiredPermissionsAsync();
        db.ChangeTracker.Clear();
        Assert.False((await db.Set<TenantPermission>().SingleAsync(row => row.Id == global.Id)).IsActive);
        Assert.False((await db.Set<ContentTypePermission>().SingleAsync(row => row.Id == content.Id)).IsActive);
        Assert.False((await db.Set<GenericResourcePermission>().SingleAsync(row => row.Id == resource.Id)).IsActive);
        var required = await db.Set<PermissionAuditLog>().Where(row => row.PerformedBy == actorId).ToListAsync();
        Assert.Equal(3, required.Count);
        Assert.Contains(required, row => row.TenantId == null);
        Assert.Contains(required, row => row.TenantId == new TenantId(firstTenant));
        Assert.Contains(required, row => row.TenantId == new TenantId(secondTenant));
        var centralized = await db.Set<AuditLog>().Where(row => row.UserId == actorId && row.Category == AuditCategory.Permission).ToListAsync();
        Assert.Equal(3, centralized.Count);
        Assert.Contains(centralized, row => row.TenantId == null);
        Assert.Contains(centralized, row => row.TenantId == firstTenant);
        Assert.Contains(centralized, row => row.TenantId == secondTenant);
        var keys = new[] { global.Id.ToString(), content.Id.ToString(), resource.Id.ToString() };
        var changes = await db.Set<EntityChangeAuditRecord>().Where(row => row.ActorId == actorId && keys.Contains(row.KeyId)).ToListAsync();
        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, row => row.TenantId == null);
        Assert.Contains(changes, row => row.TenantId == firstTenant);
        Assert.Contains(changes, row => row.TenantId == secondTenant);
    }

    private static IDisposable BeginActor(IServiceProvider services, Guid actor, Guid tenant) =>
        services.GetRequiredService<IActorContextAccessor>().BeginActorContextScope(ActorContextBuilder.ForUser(actor).WithTenantId(tenant).WithRole("TenantAdmin").Build());
    private static TenantPermissionCreation Creation() => new() { UserId = Guid.NewGuid(), Permissions = ["Read"] };
    private static PermissionGrantDeletion Delete(PermissionBase grant) => new() { GrantId = grant.Id, ExpectedVersion = grant.Version };
    private static async Task<PermissionMutationBatchResult<PermissionBase>> CreateBatchAsync(IPermissionLifecycleService service, string family, int count) => family switch
    {
        "tenant" => await AsBase(service.BulkGrantTenantPermissionsAsync(Enumerable.Range(0, count).Select(_ => Creation()).ToArray())),
        "content" => await AsBase(service.BulkGrantContentTypePermissionsAsync(Enumerable.Range(0, count).Select(_ => new ContentTypePermissionCreation
            { UserId = Guid.NewGuid(), ContentTypeName = "Document", Permissions = [PermissionType.Read] }).ToArray())),
        _ => await AsBase(service.BulkGrantResourcePermissionsAsync<GenericResourcePermission, EntityBase>(Enumerable.Range(0, count).Select(_ => new ResourcePermissionCreation
            { UserId = Guid.NewGuid(), ResourceType = "Document", ResourceId = Guid.NewGuid(), Permissions = [PermissionType.Read] }).ToArray()))
    };
    private static async Task<PermissionMutationBatchResult<PermissionBase>> AsBase<T>(Task<Result<PermissionMutationBatchResult<T>>> operation) where T : PermissionBase
    {
        var result = await operation; Assert.True(result.IsSuccess);
        return new PermissionMutationBatchResult<PermissionBase>(result.Value.Applied, result.Value.Items.Select(item =>
            new PermissionMutationItemResult<PermissionBase>(item.Index, item.Result.IsSuccess
                ? Result.Success<PermissionBase>(item.Result.Value) : Result.Failure<PermissionBase>(item.Result.Error))).ToArray());
    }
    private sealed class AuditFailureException : Exception { }
}
