using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Configuration;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using Facade = GameGuild.Identity.Authentication.PermissionService;
using LegacyContract = GameGuild.Identity.Authentication.IPermissionService;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PermissionReadContractTests
{
    private static readonly DateTime GrantTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 201)]
    [InlineData(int.MaxValue, 200)]
    public async Task InvalidPageReturnsValidationWithoutReadingStorage(int page, int pageSize)
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var service = new Facade(database.Object, actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid()));
        var result = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { Page = page, PageSize = pageSize });
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingActorFailsClosedWithoutReadingStorage()
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var result = await new Facade(database.Object).QueryTenantPermissionsAsync(new TenantPermissionListQuery());
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CrossTenantOtherSubjectAndImplicitDefaultReadsAreDeniedBeforeStorage()
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var service = new Facade(database.Object, actorContextAccessor: Actor(user, tenant));
        var requests = new TenantPermissionListQuery[]
        {
            new() { TenantId = Guid.NewGuid() }, new() { UserId = Guid.NewGuid() },
            new() { AllUsers = true }, new() { IncludeDefaultSubjects = true }, new() { GlobalScope = true }
        };
        foreach (var request in requests)
        {
            var result = await service.QueryTenantPermissionsAsync(request);
            Assert.True(result.IsFailure);
            Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        }
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TenantAdministratorCannotReadAnotherSubjectsHistoricalSnapshots()
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var service = new Facade(database.Object,
            actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid(), "TenantAdmin"));
        var result = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery { UserId = Guid.NewGuid() });
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error.Type);
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidFiltersAndAmbiguousScopeReturnTypedErrorsBeforeStorage()
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var service = new Facade(database.Object,
            actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid(), "SystemAdmin"));
        var badContent = await service.QueryContentTypePermissionsAsync(new ContentTypePermissionListQuery { ContentTypeName = " " });
        var badResource = await service.QueryResourcePermissionsAsync<GenericResourcePermission, EntityBase>(
            new ResourcePermissionListQuery { ResourceId = Guid.Empty });
        var badWindow = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery
            { FromUtc = GrantTime.AddHours(1), ToUtc = GrantTime });
        var badKind = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery
            { Operation = (PermissionOperationType)int.MaxValue });
        var badClock = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery
            { FromUtc = DateTime.SpecifyKind(GrantTime, DateTimeKind.Unspecified) });
        var badGlobal = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery
            { GlobalScope = true, TenantId = Guid.NewGuid() });
        var badSubjects = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery
            { AllUsers = true, UserId = Guid.NewGuid() });
        foreach (var error in new[] { badContent.Error, badResource.Error, badWindow.Error, badKind.Error,
                     badClock.Error, badGlobal.Error, badSubjects.Error })
        {
            Assert.Equal(ErrorType.Validation, error.Type);
        }
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationPropagatesBeforeStorage()
    {
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var service = new Facade(database.Object, actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.QueryTenantPermissionsAsync(
            new TenantPermissionListQuery(), cancellation.Token));
        database.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TenantPagesUseStableTiesFilteredTotalsAndUntrackedRows()
    {
        await using var db = CreateDatabase();
        var tenant = Guid.NewGuid();
        var grants = Enumerable.Range(1, 5).Select(index => new TenantPermission
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), UserId = Guid.NewGuid(), TenantId = tenant,
            Permissions = [nameof(PermissionType.Read)], GrantedAt = GrantTime
        }).ToArray();
        db.AddRange(grants);
        db.Add(new TenantPermission { TenantId = Guid.NewGuid(), UserId = Guid.NewGuid() });
        db.Add(new TenantPermission { TenantId = tenant, UserId = null });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new Facade(db, actorContextAccessor: Actor(Guid.NewGuid(), tenant, "SystemAdmin"));
        var result = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { AllUsers = true, Page = 2, PageSize = 2 });
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(new[] { grants[2].Id, grants[3].Id }, result.Value.Items.Select(row => row.Id));
        Assert.Empty(db.ChangeTracker.Entries());
        var beyond = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { AllUsers = true, Page = 20, PageSize = 2 });
        Assert.True(beyond.IsSuccess);
        Assert.Equal(5, beyond.Value.TotalCount);
        Assert.Empty(beyond.Value.Items);
    }

    [Fact]
    public async Task ContentAndTypedResourceFiltersKeepUserTenantAndTypeIsolation()
    {
        await using var db = CreateDatabase();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var resource = Guid.NewGuid();
        var selectedContent = new ContentTypePermission(user, tenant, "Document");
        var selectedResource = new GenericResourcePermission(user, tenant, resource, "Document");
        db.AddRange(selectedContent, new ContentTypePermission(user, tenant, "Other"),
            new ContentTypePermission(Guid.NewGuid(), tenant, "Document"), new ContentTypePermission(user, Guid.NewGuid(), "Document"));
        db.AddRange(selectedResource, new GenericResourcePermission(user, tenant, Guid.NewGuid(), "Document"),
            new GenericResourcePermission(Guid.NewGuid(), tenant, resource, "Document"),
            new GenericResourcePermission(user, tenant, resource, "Other"),
            new GenericResourcePermission(user, Guid.NewGuid(), resource, "Document"));
        await db.SaveChangesAsync();
        var service = new Facade(db, actorContextAccessor: Actor(user, tenant));
        var content = await service.QueryContentTypePermissionsAsync(new ContentTypePermissionListQuery { ContentTypeName = "Document" });
        var resources = await service.QueryResourcePermissionsAsync<GenericResourcePermission, EntityBase>(
            new ResourcePermissionListQuery { ResourceId = resource, ResourceType = "Document" });
        Assert.True(content.IsSuccess);
        Assert.True(resources.IsSuccess);
        Assert.Equal(selectedContent.Id, Assert.Single(content.Value.Items).Id);
        Assert.Equal(selectedResource.Id, Assert.Single(resources.Value.Items).Id);
    }

    [Fact]
    public async Task InactiveAndExpiredGrantRecordsRequireExplicitMetadataOptIn()
    {
        await using var db = CreateDatabase();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        db.Add(new TenantPermission
        {
            UserId = user, TenantId = tenant, Permissions = [nameof(PermissionType.Read)],
            GrantedAt = GrantTime, ExpiresAt = SystemClock.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
        var service = new Facade(db, actorContextAccessor: Actor(user, tenant));
        var active = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery());
        var metadata = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { IncludeInactive = true });
        Assert.True(active.IsSuccess);
        Assert.True(metadata.IsSuccess);
        Assert.Empty(active.Value.Items);
        Assert.Single(metadata.Value.Items);
        Assert.False(await service.HasTenantPermissionAsync(user, tenant, PermissionType.Read));
    }

    [Fact]
    public async Task HistoryKeepsExactStoredEvidenceAndAppliesSingleSidedBounds()
    {
        await using var db = CreateDatabase();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var first = Audit(user, tenant, GrantTime);
        first.NewValue = "{\"permissions\":[\"Read\",\"Write\"]}";
        var second = Audit(user, tenant, GrantTime.AddHours(1));
        db.AddRange(first, second, Audit(Guid.NewGuid(), tenant, GrantTime), Audit(user, Guid.NewGuid(), GrantTime));
        await db.SaveChangesAsync();
        var service = new Facade(db, actorContextAccessor: Actor(user, tenant));
        var lower = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery { FromUtc = second.Timestamp });
        var upper = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery { ToUtc = first.Timestamp });
        Assert.True(lower.IsSuccess);
        Assert.True(upper.IsSuccess);
        Assert.Equal(second.Id, Assert.Single(lower.Value.Items).Id);
        var stored = Assert.Single(upper.Value.Items);
        Assert.Equal(first.Id, stored.Id);
        Assert.Equal(first.NewValue, stored.NewValue);
    }

    [Fact]
    public async Task GlobalScopeIsExplicitAndSystemAdminCrossTenantSelectionIsBounded()
    {
        await using var db = CreateDatabase();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        db.AddRange(new TenantPermission { TenantId = null, UserId = null },
            new TenantPermission { TenantId = tenant, UserId = null },
            new TenantPermission { TenantId = otherTenant, UserId = user });
        await db.SaveChangesAsync();
        var service = new Facade(db, actorContextAccessor: Actor(user, tenant, "SystemAdmin"));
        var global = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery
            { GlobalScope = true, AllUsers = true, IncludeDefaultSubjects = true });
        var cross = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { TenantId = otherTenant });
        Assert.True(global.IsSuccess);
        Assert.Null(Assert.Single(global.Value.Items).TenantId);
        Assert.True(cross.IsSuccess);
        Assert.Equal(otherTenant, Assert.Single(cross.Value.Items).TenantId);
    }

    [Fact]
    public void ProductionRegistrationsAliasTheSameScopedVersionedAndLegacyService()
    {
        IServiceCollection registrations = new ServiceCollection();
        registrations.AddAuthenticationPresentation(new ConfigurationBuilder().Build());
        IServiceCollection isolated = new ServiceCollection();
        isolated.AddSingleton(Mock.Of<IApplicationDbContext>());
        isolated.AddSingleton(Actor(Guid.NewGuid(), Guid.NewGuid()));
        foreach (var descriptor in registrations.Where(descriptor =>
                     descriptor.ServiceType == typeof(Facade) || descriptor.ServiceType == typeof(LegacyContract) ||
                     descriptor.ServiceType == typeof(IPermissionServiceV2) || descriptor.ServiceType == typeof(IPermissionReadService) ||
                     descriptor.ServiceType == typeof(IPermissionUpdateService) || descriptor.ServiceType == typeof(IPermissionLifecycleService)))
        {
            isolated.Add(descriptor);
        }
        using var provider = isolated.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var legacy = scope.ServiceProvider.GetRequiredService<LegacyContract>();
        Assert.Same(legacy, scope.ServiceProvider.GetRequiredService<IPermissionServiceV2>());
        Assert.Same(legacy, scope.ServiceProvider.GetRequiredService<IPermissionReadService>());
        Assert.Same(legacy, scope.ServiceProvider.GetRequiredService<IPermissionUpdateService>());
        Assert.Same(legacy, scope.ServiceProvider.GetRequiredService<IPermissionLifecycleService>());
        Assert.Same(legacy, scope.ServiceProvider.GetRequiredService<Facade>());
        using var other = provider.CreateScope();
        Assert.NotSame(legacy, other.ServiceProvider.GetRequiredService<IPermissionServiceV2>());
    }

    [Fact]
    public async Task StorageFailurePropagatesInsteadOfReturningAnEmptySuccessfulPage()
    {
        var failure = new InvalidOperationException("Synthetic persistence failure");
        var database = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        database.Setup(value => value.Set<TenantPermission>()).Throws(failure);
        var service = new Facade(database.Object, actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid()));
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueryTenantPermissionsAsync(new TenantPermissionListQuery()));
        Assert.Same(failure, observed);
    }

    [Fact]
    public async Task ExactGetsReturnEachOwnNativeFamilyIncludingInactiveOrExpiredMetadataUntracked()
    {
        await using var db = CreateDatabase();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var own = new TenantPermission { UserId = user, TenantId = tenant, IsActive = false, Permissions = ["custom:permission"] };
        var content = new ContentTypePermission(user, tenant, "Document") { ExpiresAt = StartForLookup() };
        var resource = new GenericResourcePermission(user, tenant, Guid.NewGuid(), "Document") { IsActive = false };
        db.AddRange(own, content, resource);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        IPermissionReadService service = new Facade(db, actorContextAccessor: Actor(user, tenant));
        var first = await service.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = own.Id });
        var second = await service.GetContentTypePermissionAsync(new PermissionGrantLookup { GrantId = content.Id });
        var third = await service.GetResourcePermissionAsync<GenericResourcePermission, EntityBase>(new PermissionGrantLookup { GrantId = resource.Id });
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.True(third.IsSuccess);
        Assert.Equal(own.Id, first.Value.Id);
        Assert.False(first.Value.IsActive);
        Assert.Equal(new[] { "custom:permission" }, first.Value.Permissions);
        Assert.Equal(content.Id, second.Value.Id);
        Assert.Equal(StartForLookup(), second.Value.ExpiresAt);
        Assert.Equal(resource.Id, third.Value.Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData("anonymous", ErrorType.Unauthorized)]
    [InlineData("empty-id", ErrorType.Validation)]
    [InlineData("cross-tenant", ErrorType.Forbidden)]
    [InlineData("global", ErrorType.Forbidden)]
    [InlineData("all-users", ErrorType.Forbidden)]
    [InlineData("other-subject", ErrorType.Forbidden)]
    public async Task ExactGetsRejectInvalidScopeBeforeAnyStorageCall(string scenario, ErrorType expected)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var roles = scenario is "all-users" or "other-subject" ? Array.Empty<string>() : new[] { "TenantAdmin" };
        IPermissionReadService service = new Facade(context.Object,
            actorContextAccessor: scenario == "anonymous" ? null : Actor(user, tenant, roles));
        var query = new PermissionGrantLookup
        {
            GrantId = scenario == "empty-id" ? Guid.Empty : Guid.NewGuid(),
            TenantId = scenario == "cross-tenant" ? Guid.NewGuid() : null,
            GlobalScope = scenario == "global", AllUsers = scenario == "all-users",
            UserId = scenario == "other-subject" ? Guid.NewGuid() : null
        };
        Assert.Equal(expected, (await service.GetTenantPermissionAsync(query)).Error.Type);
        Assert.Equal(expected, (await service.GetContentTypePermissionAsync(query)).Error.Type);
        Assert.Equal(expected, (await service.GetResourcePermissionAsync<GenericResourcePermission, EntityBase>(query)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExactGetHidesOtherSubjectsTenantsAndDeletedRecordsUntilScopeIsExplicitlyAuthorized()
    {
        await using var db = CreateDatabase();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var other = new TenantPermission { UserId = Guid.NewGuid(), TenantId = tenant };
        var foreign = new TenantPermission { UserId = user, TenantId = Guid.NewGuid() };
        var deleted = new TenantPermission { UserId = user, TenantId = tenant, Version = 1 };
        deleted.SoftDelete();
        db.AddRange(other, foreign, deleted);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        IPermissionReadService own = new Facade(db, actorContextAccessor: Actor(user, tenant));
        foreach (var id in new[] { other.Id, foreign.Id, deleted.Id, Guid.NewGuid() })
        {
            Assert.Equal(ErrorType.NotFound, (await own.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = id })).Error.Type);
        }
        IPermissionReadService admin = new Facade(db, actorContextAccessor: Actor(user, tenant, "TenantAdmin"));
        Assert.Equal(ErrorType.NotFound, (await admin.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = other.Id })).Error.Type);
        var allowed = await admin.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = other.Id, UserId = other.UserId });
        Assert.True(allowed.IsSuccess);
        Assert.Equal(other.Id, allowed.Value.Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExactGetGlobalDefaultsAndCrossTenantSelectionRequireExplicitSystemAdminScope()
    {
        await using var db = CreateDatabase();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var global = new TenantPermission { TenantId = null, UserId = null };
        var foreign = new TenantPermission { TenantId = Guid.NewGuid(), UserId = user };
        db.AddRange(global, foreign);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        IPermissionReadService service = new Facade(db, actorContextAccessor: Actor(user, tenant, "SystemAdmin"));
        var defaults = await service.GetTenantPermissionAsync(new PermissionGrantLookup
            { GrantId = global.Id, GlobalScope = true, AllUsers = true, IncludeDefaultSubjects = true });
        var cross = await service.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = foreign.Id, TenantId = foreign.TenantId });
        Assert.True(defaults.IsSuccess);
        Assert.Null(defaults.Value.TenantId);
        Assert.True(cross.IsSuccess);
        Assert.Equal(foreign.TenantId, cross.Value.TenantId);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExactGetNullAndCancelledRequestsDoNotAccessStorage()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionReadService service = new Facade(context.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var query = new PermissionGrantLookup { GrantId = Guid.NewGuid() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetTenantPermissionAsync(query, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetTenantPermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetContentTypePermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetResourcePermissionAsync<GenericResourcePermission, EntityBase>(null!));
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExactGetDoesNotConvertAStorageFailureIntoNotFound()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        context.Setup(value => value.Set<TenantPermission>()).Throws(new InvalidOperationException("storage unavailable"));
        IPermissionReadService service = new Facade(context.Object, actorContextAccessor: Actor(Guid.NewGuid(), Guid.NewGuid()));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = Guid.NewGuid() }));
        Assert.Equal("storage unavailable", failure.Message);
    }

    private static DateTime StartForLookup() => new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IActorContextAccessor Actor(Guid user, Guid tenant, params string[] roles)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(
            ActorContextBuilder.ForUser(user).WithTenantId(tenant).WithRoles(roles).Build());
        return accessor.Object;
    }

    private static PermissionAuditLog Audit(Guid user, Guid tenant, DateTime timestamp) => new()
    {
        UserId = user, TenantId = new TenantId(tenant), Timestamp = timestamp,
        PerformedBy = Guid.NewGuid(), OperationType = PermissionOperationType.Grant, ResourceType = "Document"
    };

    private static ReadContext CreateDatabase() => new(new DbContextOptionsBuilder<ReadContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private sealed class ReadContext(DbContextOptions<ReadContext> options) : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantPermission>().Ignore(row => row.Metadata);
            modelBuilder.Entity<ContentTypePermission>();
            modelBuilder.Entity<GenericResourcePermission>();
            modelBuilder.ApplyConfiguration(new PermissionAuditLogConfiguration());
        }
    }
}
