using System.Text.Json;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using Moq;

namespace GameGuild.API.UnitTests.Eventing;

public sealed class UseCaseOperationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenCommandRuns_EstablishesAmbientOperationContext()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out var actorId);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);
        UseCaseOperationContext? observed = null;

        // When
        var response = await behavior.Handle(
            new TestMutationCommand(),
            async () =>
            {
                observed = operationAccessor.Current;
                context.Add(new Tenant
                {
                    Name = "Operation tenant",
                    Slug = $"operation-{Guid.NewGuid():N}",
                    AdminEmail = "admin@example.com"
                });
                await context.SaveChangesAsync();
                return true;
            },
            CancellationToken.None);

        // Then
        response.Should().BeTrue();
        observed.Should().NotBeNull();
        observed!.OperationCode.Should().Be("test-mutation");
        observed.CommandType.Should().Be(nameof(TestMutationCommand));
        observed.TenantId.Should().Be(tenantId);
        observed.ActorId.Should().Be(actorId);
        operationAccessor.Current.Should().BeNull();
        var operationEvent = await context.Set<OutboxMessage>().SingleAsync();
        operationEvent.EventName.Should().Be("platform.use-case-operation.occurred.v1");
        operationEvent.TenantId.Should().Be(tenantId);
        operationEvent.ActorId.Should().Be(actorId);
    }

    [Fact]
    public async Task Handle_WhenPermissionChanges_LogsBeforeAndAfterStateAfterSuccessfulCommand()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out var actorId);
        var targetUserId = Guid.NewGuid();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        var auditHook = new RecordingPermissionAuditHook();
        await using var context = new ApplicationDbContext(
            options,
            null,
            operationAccessor,
            auditService.Object,
            permissionAuditHooks: [auditHook]);

        context.Add(new TenantPermission
        {
            UserId = targetUserId,
            TenantId = tenantId,
            Permissions = ["project:read"],
            GrantedBy = actorId
        });
        await context.SaveChangesAsync();
        ((CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!)
            .ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        auditService.Invocations.Clear();
        auditHook.LastContext = null;

        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);
        var auditWasWrittenBeforeHandlerReturned = false;

        var response = await behavior.Handle(
            new TestMutationCommand(),
            async () =>
            {
                var permission = await context.Set<TenantPermission>().SingleAsync();
                permission.Permissions = ["project:read", "project:write"];
                await context.SaveChangesAsync();
                auditWasWrittenBeforeHandlerReturned = auditService.Invocations.Count > 0;
                return true;
            },
            CancellationToken.None);

        response.Should().BeTrue();
        auditWasWrittenBeforeHandlerReturned.Should().BeFalse();
        var request = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        request.ActionType.Should().Be(AuditActionTypes.PermissionChanged);
        request.Category.Should().Be(AuditCategory.Permission);
        request.UserId.Should().Be(actorId);
        request.TenantId.Should().Be(tenantId);
        request.CorrelationId.Should().NotBeNullOrWhiteSpace();

        var metadata = JsonDocument.Parse(JsonSerializer.Serialize(request.Metadata)).RootElement;
        var change = metadata.GetProperty("Changes")[0];
        change.GetProperty("TargetUserId").GetGuid().Should().Be(targetUserId);
        change.GetProperty("BeforeState").GetString().Should().Contain("project:read").And.NotContain("project:write");
        change.GetProperty("AfterState").GetString().Should().Contain("project:read").And.Contain("project:write");
        auditHook.LastContext.Should().NotBeNull();
        auditHook.LastContext!.Changes.Should().ContainSingle();
        auditHook.LastContext.Changes[0].TargetUserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task Handle_WhenPermissionIsRevoked_LogsRevocationAction()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out var actorId);
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        await using var context = new ApplicationDbContext(options, null, operationAccessor, auditService.Object);
        context.Add(new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = tenantId,
            Permissions = ["project:read", "project:write"],
            GrantedBy = actorId
        });
        await context.SaveChangesAsync();
        auditService.Invocations.Clear();
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);

        var response = await behavior.Handle(
            new TestMutationCommand(),
            async () =>
            {
                var permission = await context.Set<TenantPermission>().SingleAsync();
                permission.Permissions = ["project:read"];
                await context.SaveChangesAsync();
                return true;
            },
            CancellationToken.None);

        response.Should().BeTrue();
        var request = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        request.ActionType.Should().Be(AuditActionTypes.PermissionRevoked);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleAssignmentChanges_LogsPermissionGrantAndRevocation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        await using var context = new ApplicationDbContext(options, null, operationAccessor, auditService.Object);
        var tenantId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var role = new Role("auditable", "Auditable role", tenantId) { Id = Guid.NewGuid() };
        context.Add(role);
        await context.SaveChangesAsync();
        auditService.Invocations.Clear();

        role.Permissions = "[\"project:read\",\"project:write\"]";
        await context.SaveChangesAsync();
        auditService.Invocations.Clear();
        role.Permissions = "[\"project:read\"]";
        await context.SaveChangesAsync();
        ((CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!)
            .ActionType.Should().Be(AuditActionTypes.PermissionRevoked);
        auditService.Invocations.Clear();

        var assignment = new UserRole(targetUserId, role.Id, actorId)
        {
            Id = Guid.NewGuid(),
            Role = role
        };
        context.Add(assignment);
        await context.SaveChangesAsync();

        var grantRequest = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        grantRequest.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        grantRequest.UserId.Should().Be(actorId);
        var grantMetadata = JsonDocument.Parse(JsonSerializer.Serialize(grantRequest.Metadata)).RootElement;
        grantMetadata.GetProperty("Changes")[0].GetProperty("TargetUserId").GetGuid().Should().Be(targetUserId);

        auditService.Invocations.Clear();
        context.Remove(assignment);
        await context.SaveChangesAsync();

        var revokeRequest = (CreateAuditLogRequest)auditService.Invocations.Single().Arguments[0]!;
        revokeRequest.ActionType.Should().Be(AuditActionTypes.PermissionRevoked);
        revokeRequest.UserId.Should().Be(actorId);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPermissionEntityIsExcluded_SkipsItsAuditRecord()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns(Task.CompletedTask);
        await using var context = new ApplicationDbContext(
            options,
            null,
            operationAccessor,
            auditService.Object,
            permissionAuditOptions: Options.Create(new PermissionAuditOptions
            {
                ExcludedEntityTypes = new(StringComparer.OrdinalIgnoreCase) { nameof(TenantPermission) }
            }));

        context.Add(new TenantPermission
        {
            UserId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Permissions = ["project:read"]
        });

        await context.SaveChangesAsync();

        (await context.Set<TenantPermission>().CountAsync()).Should().Be(1);
        auditService.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCentralAuditServiceFails_KeepsSuccessfulPermissionMutation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out var actorId);
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("Audit storage is unavailable"));
        var throwingHook = new ThrowingPermissionAuditHook();
        await using var context = new ApplicationDbContext(
            options,
            null,
            operationAccessor,
            auditService.Object,
            permissionAuditHooks: [throwingHook]);
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);

        var response = await behavior.Handle(
            new TestMutationCommand(),
            async () =>
            {
                context.Add(new TenantPermission
                {
                    UserId = Guid.NewGuid(),
                    TenantId = tenantId,
                    Permissions = ["project:read"],
                    GrantedBy = actorId
                });
                await context.SaveChangesAsync();
                return true;
            },
            CancellationToken.None);

        response.Should().BeTrue();
        (await context.Set<TenantPermission>().CountAsync()).Should().Be(1);
        auditService.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Once);
        throwingHook.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSuccessfulCommandDoesNotMutate_DoesNotInventAnOperationEvent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out _, out _);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);

        var response = await behavior.Handle(
            new TestMutationCommand(),
            () => Task.FromResult(true),
            CancellationToken.None);

        response.Should().BeTrue();
        (await context.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenRelationalCommandRuns_UsesProviderExecutionStrategyAroundTransaction()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .ReplaceService<IExecutionStrategyFactory, CountingExecutionStrategyFactory>()
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out _, out _);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        await context.Database.OpenConnectionAsync();
        CountingExecutionStrategyFactory.Reset();
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);

        var response = await behavior.Handle(
            new TestMutationCommand(),
            () => Task.FromResult(true),
            CancellationToken.None);

        response.Should().BeTrue();
        CountingExecutionStrategyFactory.CreateCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Handle_WhenRequestIsCanceled_RollsBackWithoutRequestTokenAndPreservesOriginalFailure(bool rollbackFails)
    {
        var rollback = new CancellationRollbackInterceptor(rollbackFails);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .AddInterceptors(rollback)
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out _);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        await context.Database.OpenConnectionAsync();
        using var requestCancellation = new CancellationTokenSource();
        var original = new OperationCanceledException("The command was canceled during credential issuance.", requestCancellation.Token);
        UseCaseOperationContext? observed = null;
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(context, actorAccessor, operationAccessor);

        Func<Task> act = async () => await behavior.Handle(new TestMutationCommand(), () =>
        {
            observed = operationAccessor.Current;
            observed!.MarkBusinessMutationObserved();
            context.Add(new TenantPermission { UserId = Guid.NewGuid(), TenantId = tenantId, Permissions = ["project:read"] });
            requestCancellation.Cancel();
            throw original;
        }, requestCancellation.Token);

        var failure = await act.Should().ThrowAsync<OperationCanceledException>();
        failure.Which.Should().BeSameAs(original);
        rollback.CallCount.Should().Be(1);
        rollback.RequestTokenWasPassed.Should().BeFalse();
        context.ChangeTracker.Entries().Should().BeEmpty();
        context.Database.CurrentTransaction.Should().BeNull();
        observed!.BusinessMutationObserved.Should().BeFalse();
        operationAccessor.Current.Should().BeNull();
    }

    private sealed class CancellationRollbackInterceptor(bool rollbackFails) : DbTransactionInterceptor
    {
        public int CallCount { get; private set; }
        public bool RequestTokenWasPassed { get; private set; }

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            ArgumentNullException.ThrowIfNull(eventData);
            CallCount++;
            RequestTokenWasPassed = cancellationToken.CanBeCanceled;
            cancellationToken.ThrowIfCancellationRequested();
            if (rollbackFails) { throw new InvalidOperationException("The rollback transport failed."); }
            return ValueTask.FromResult(result);
        }
    }

    private static ActorContextAccessor NewActorAccessor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.NewGuid();
        actorId = Guid.NewGuid();
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(actorId).WithTenantId(tenantId).Build());
        return accessor;
    }

    private sealed record TestMutationCommand : ICommand<bool>;

    private sealed class RecordingPermissionAuditHook : IPermissionAuditHook
    {
        public PermissionAuditHookContext? LastContext { get; set; }

        public Task OnPermissionChangesAuditedAsync(
            PermissionAuditHookContext context,
            CancellationToken cancellationToken = default)
        {
            LastContext = context;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPermissionAuditHook : IPermissionAuditHook
    {
        public int CallCount { get; private set; }

        public Task OnPermissionChangesAuditedAsync(
            PermissionAuditHookContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The custom audit hook failed");
        }
    }

    private sealed class CountingExecutionStrategyFactory(ExecutionStrategyDependencies dependencies)
        : IExecutionStrategyFactory
    {
        private static int _createCount;

        public static int CreateCount => _createCount;

        public static void Reset() => _createCount = 0;

        public IExecutionStrategy Create()
        {
            Interlocked.Increment(ref _createCount);
            return new NonRetryingExecutionStrategy(dependencies);
        }
    }
}
