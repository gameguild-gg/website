using System.Data.Common;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Configuration;
using GameGuild.Identity.Context.Actors;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using Facade = GameGuild.Identity.Authentication.PermissionService;

namespace GameGuild.Identity.Authentication.IntegrationTests;

public sealed class PermissionReadContractPostgreSqlTests
{
    private static readonly DateTime Start = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ActualDatabasePagesAllThreePermissionFamiliesWithScopedPredicates()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("permission_read_contracts");
        var observer = new SelectObserver();
        var options = new DbContextOptionsBuilder<ReadContext>().UseNpgsql(database.ConnectionString)
            .AddInterceptors(observer).Options;
        await using var context = new ReadContext(options);
        await context.Database.EnsureCreatedAsync();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var resource = Guid.NewGuid();
        var grants = Enumerable.Range(1, 5).Select(index => new TenantPermission
        {
            Id = Guid.Parse($"00000000-0000-0000-0000-{index:D12}"), TenantId = tenant,
            UserId = index == 1 ? user : Guid.NewGuid(), Permissions = [nameof(PermissionType.Read)], GrantedAt = Start
        }).ToArray();
        context.AddRange(grants);
        context.Add(new TenantPermission { UserId = Guid.NewGuid(), TenantId = otherTenant });
        context.Add(new TenantPermission { UserId = null, TenantId = tenant });
        var content = new ContentTypePermission(user, tenant, "Document");
        var target = new GenericResourcePermission(user, tenant, resource, "Document");
        context.AddRange(content, new ContentTypePermission(user, otherTenant, "Document"),
            new ContentTypePermission(Guid.NewGuid(), tenant, "Document"));
        context.AddRange(target, new GenericResourcePermission(user, otherTenant, resource, "Document"),
            new GenericResourcePermission(Guid.NewGuid(), tenant, resource, "Document"),
            new GenericResourcePermission(user, tenant, resource, "Other"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        observer.Statements.Clear();

        var actors = new ActorContextAccessor();
        using var administrator = actors.BeginActorContextScope(
            ActorContextBuilder.ForUser(user).WithTenantId(tenant).WithRole("SystemAdmin").Build());
        IPermissionServiceV2 service = new Facade(context, actorContextAccessor: actors);
        var page = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery
            { AllUsers = true, Page = 2, PageSize = 2 });
        Assert.True(page.IsSuccess);
        Assert.Equal(5, page.Value.TotalCount);
        Assert.Equal(new[] { grants[2].Id, grants[3].Id }, page.Value.Items.Select(value => value.Id));

        using (actors.BeginActorContextScope(ActorContextBuilder.ForUser(user).WithTenantId(tenant).Build()))
        {
            var ownTenant = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery());
            var types = await service.QueryContentTypePermissionsAsync(new ContentTypePermissionListQuery { ContentTypeName = "Document" });
            var resources = await service.QueryResourcePermissionsAsync<GenericResourcePermission, EntityBase>(
                new ResourcePermissionListQuery { ResourceId = resource, ResourceType = "Document" });
            Assert.True(ownTenant.IsSuccess);
            Assert.True(types.IsSuccess);
            Assert.True(resources.IsSuccess);
            Assert.Equal(grants[0].Id, Assert.Single(ownTenant.Value.Items).Id);
            Assert.Equal(content.Id, Assert.Single(types.Value.Items).Id);
            Assert.Equal(target.Id, Assert.Single(resources.Value.Items).Id);
            var beforeDenial = observer.Statements.Count;
            var denied = await service.QueryTenantPermissionsAsync(new TenantPermissionListQuery { TenantId = otherTenant });
            Assert.Equal(ErrorType.Forbidden, denied.Error.Type);
            Assert.Equal(beforeDenial, observer.Statements.Count);
        }

        Assert.Empty(context.ChangeTracker.Entries());
        var pageReads = observer.Statements.Where(sql => sql.Contains("LIMIT", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Equal(4, pageReads.Length);
        Assert.All(pageReads, sql => Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase));
        Assert.All(pageReads, sql => Assert.Contains("TenantId", sql, StringComparison.Ordinal));
        Assert.Contains(pageReads, sql => sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase));

        observer.Statements.Clear();
        using (actors.BeginActorContextScope(ActorContextBuilder.ForUser(user).WithTenantId(tenant).Build()))
        {
            var exactTenant = await service.GetTenantPermissionAsync(new PermissionGrantLookup { GrantId = grants[0].Id });
            var exactContent = await service.GetContentTypePermissionAsync(new PermissionGrantLookup { GrantId = content.Id });
            var exactResource = await service.GetResourcePermissionAsync<GenericResourcePermission, EntityBase>(new PermissionGrantLookup { GrantId = target.Id });
            Assert.True(exactTenant.IsSuccess);
            Assert.True(exactContent.IsSuccess);
            Assert.True(exactResource.IsSuccess);
            Assert.Equal(grants[0].Id, exactTenant.Value.Id);
            Assert.Equal(content.Id, exactContent.Value.Id);
            Assert.Equal(target.Id, exactResource.Value.Id);
        }
        Assert.Equal(3, observer.Statements.Count);
        Assert.All(observer.Statements, sql =>
        {
            Assert.Contains("WHERE", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TenantId", sql, StringComparison.Ordinal);
            Assert.Contains("UserId", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("count(*)", sql, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ActualDatabaseHistoryKeepsOpaqueSnapshotsAndFiltersBeforePaging()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("permission_history_contracts");
        var options = new DbContextOptionsBuilder<ReadContext>().UseNpgsql(database.ConnectionString).Options;
        await using var context = new ReadContext(options);
        await context.Database.EnsureCreatedAsync();
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var first = History(user, tenant, Start);
        var second = History(user, tenant, Start.AddHours(1));
        first.NewValue = "{\"permissions\":[\"Read\",\"Write\"]}";
        context.AddRange(first, second, History(Guid.NewGuid(), tenant, Start), History(user, Guid.NewGuid(), Start));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var actors = new ActorContextAccessor();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(user).WithTenantId(tenant).Build());
        IPermissionReadService service = new Facade(context, actorContextAccessor: actors);
        var upper = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery
            { ToUtc = Start, Operation = PermissionOperationType.Grant, Success = true, PageSize = 1 });
        var lower = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery { FromUtc = second.Timestamp });
        Assert.True(upper.IsSuccess);
        Assert.True(lower.IsSuccess);
        Assert.Equal(1, upper.Value.TotalCount);
        Assert.Equal(first.NewValue, Assert.Single(upper.Value.Items).NewValue);
        Assert.Equal(second.Id, Assert.Single(lower.Value.Items).Id);
        var denied = await service.QueryPermissionHistoryAsync(new PermissionAuditHistoryQuery { AllUsers = true });
        Assert.Equal(ErrorType.Forbidden, denied.Error.Type);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static PermissionAuditLog History(Guid user, Guid tenant, DateTime timestamp) => new()
    {
        UserId = user, TenantId = new TenantId(tenant), Timestamp = timestamp,
        OperationType = PermissionOperationType.Grant, PerformedBy = Guid.NewGuid(), ResourceType = "Document"
    };

    private sealed class ReadContext(DbContextOptions<ReadContext> options) : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantPermission>().Ignore(value => value.Metadata);
            modelBuilder.Entity<ContentTypePermission>();
            modelBuilder.Entity<GenericResourcePermission>();
            modelBuilder.ApplyConfiguration(new PermissionAuditLogConfiguration());
        }
    }

    private sealed class SelectObserver : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            cancellationToken.ThrowIfCancellationRequested();
            if (command.CommandText.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            {
                Statements.Add(command.CommandText);
            }
            return ValueTask.FromResult(result);
        }
    }
}
