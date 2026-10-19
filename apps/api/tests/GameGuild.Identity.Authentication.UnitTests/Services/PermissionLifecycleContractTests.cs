using System.Collections;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PermissionLifecycleContractTests
{
    [Theory]
    [InlineData("anonymous", ErrorType.Unauthorized)]
    [InlineData("member", ErrorType.Forbidden)]
    [InlineData("foreign-admin", ErrorType.Forbidden)]
    [InlineData("global-admin", ErrorType.Forbidden)]
    [InlineData("empty-tenant", ErrorType.Validation)]
    [InlineData("ambiguous-global", ErrorType.Validation)]
    public async Task AllSixSingleLifecycleOperationsRejectInvalidAuthorityBeforeStorage(string scenario, ErrorType expected)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var tenant = Guid.NewGuid();
        var actors = Actors(tenant, scenario == "member" ? "Member" : "TenantAdmin", anonymous: scenario == "anonymous");
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: actors);
        var selectedTenant = scenario == "foreign-admin" ? Guid.NewGuid() : scenario == "empty-tenant" ? Guid.Empty : (Guid?)null;
        var global = scenario is "global-admin" or "ambiguous-global";
        if (scenario == "ambiguous-global") { selectedTenant = tenant; }
        var creation = TenantCreation() with { TenantId = selectedTenant, GlobalScope = global };
        var content = ContentCreation() with { TenantId = selectedTenant, GlobalScope = global };
        var resource = ResourceCreation() with { TenantId = selectedTenant, GlobalScope = global };
        var deletion = Deletion() with { TenantId = selectedTenant, GlobalScope = global };
        Assert.Equal(expected, (await service.CreateTenantPermissionAsync(creation)).Error.Type);
        Assert.Equal(expected, (await service.CreateContentTypePermissionAsync(content)).Error.Type);
        Assert.Equal(expected, (await service.CreateResourcePermissionAsync<GenericResourcePermission, EntityBase>(resource)).Error.Type);
        Assert.Equal(expected, (await service.DeleteTenantPermissionAsync(deletion)).Error.Type);
        Assert.Equal(expected, (await service.DeleteContentTypePermissionAsync(deletion)).Error.Type);
        Assert.Equal(expected, (await service.DeleteResourcePermissionAsync<GenericResourcePermission, EntityBase>(deletion)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("empty-user")]
    [InlineData("missing-user")]
    [InlineData("ambiguous-default")]
    [InlineData("empty-payload")]
    [InlineData("null-payload")]
    [InlineData("blank-key")]
    [InlineData("long-key")]
    [InlineData("expired")]
    [InlineData("non-utc")]
    [InlineData("long-reason")]
    public async Task CreationValidatesNativePayloadSubjectAndLifetimeBeforeStorage(string invalid)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        var request = invalid switch
        {
            "empty-user" => TenantCreation() with { UserId = Guid.Empty },
            "missing-user" => TenantCreation() with { UserId = null },
            "ambiguous-default" => TenantCreation() with { DefaultSubject = true },
            "empty-payload" => TenantCreation() with { Permissions = [] },
            "null-payload" => TenantCreation() with { Permissions = null! },
            "blank-key" => TenantCreation() with { Permissions = [" "] },
            "long-key" => TenantCreation() with { Permissions = [new string('x', 257)] },
            "expired" => TenantCreation() with { ExpiresAt = DateTime.UtcNow.AddDays(-1) },
            "non-utc" => TenantCreation() with { ExpiresAt = new DateTime(2099, 1, 1) },
            _ => TenantCreation() with { Reason = new string('x', 501) }
        };
        Assert.Equal(ErrorType.Validation, (await service.CreateTenantPermissionAsync(request)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TypedCreationValidatesEnumsBindingsAndExplicitResourceSubjects()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        foreach (var request in new[] { ContentCreation() with { ContentTypeName = " " },
                     ContentCreation() with { ContentTypeName = new string('x', 257) },
                     ContentCreation() with { Permissions = [(PermissionType)int.MaxValue] } })
        {
            Assert.Equal(ErrorType.Validation, (await service.CreateContentTypePermissionAsync(request)).Error.Type);
        }
        foreach (var request in new[] { ResourceCreation() with { ResourceId = Guid.Empty },
                     ResourceCreation() with { ResourceType = "" }, ResourceCreation() with { Permissions = [(PermissionType)int.MaxValue] },
                     ResourceCreation() with { UserId = null, DefaultSubject = true } })
        {
            Assert.Equal(ErrorType.Validation, (await service.CreateResourcePermissionAsync<GenericResourcePermission, EntityBase>(request)).Error.Type);
        }
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task AllDeleteFamiliesRequireAnAdvanceableExpectedVersion(int version)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        var request = Deletion() with { ExpectedVersion = version };
        Assert.Equal(ErrorType.Validation, (await service.DeleteTenantPermissionAsync(request)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.DeleteContentTypePermissionAsync(request)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.DeleteResourcePermissionAsync<GenericResourcePermission, EntityBase>(request)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectedBatchesKeepEveryInputIndexAndAllDuplicateOrInvalidFailures()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        var first = TenantCreation();
        var result = await service.BulkGrantTenantPermissionsAsync([first, first, TenantCreation() with { UserId = Guid.Empty }, TenantCreation()]);
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Applied);
        Assert.Equal(new[] { 0, 1, 2, 3 }, result.Value.Items.Select(item => item.Index));
        Assert.All(result.Value.Items.Take(3), item => Assert.Equal(ErrorType.Validation, item.Result.Error.Type));
        Assert.Equal("PermissionWrite.BatchNotApplied", result.Value.Items[3].Result.Error.Code);
        var deletion = Deletion();
        var revoked = await service.BulkRevokeTenantPermissionsAsync([deletion, deletion, Deletion() with { ExpectedVersion = 0 }]);
        Assert.True(revoked.IsSuccess);
        Assert.False(revoked.Value.Applied);
        Assert.Equal(new[] { 0, 1, 2 }, revoked.Value.Items.Select(item => item.Index));
        Assert.All(revoked.Value.Items, item => Assert.Equal(ErrorType.Validation, item.Result.Error.Type));
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(257)]
    public async Task AllSixBatchOperationsRejectInvalidSizesBeforeStorage(int count)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        Assert.Equal(ErrorType.Validation, (await service.BulkGrantTenantPermissionsAsync(Enumerable.Range(0, count).Select(_ => TenantCreation()).ToArray())).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.BulkGrantContentTypePermissionsAsync(Enumerable.Range(0, count).Select(_ => ContentCreation()).ToArray())).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.BulkGrantResourcePermissionsAsync<GenericResourcePermission, EntityBase>(Enumerable.Range(0, count).Select(_ => ResourceCreation()).ToArray())).Error.Type);
        var deletions = Enumerable.Range(0, count).Select(_ => Deletion()).ToArray();
        Assert.Equal(ErrorType.Validation, (await service.BulkRevokeTenantPermissionsAsync(deletions)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.BulkRevokeContentTypePermissionsAsync(deletions)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.BulkRevokeResourcePermissionsAsync<GenericResourcePermission, EntityBase>(deletions)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EvenSystemAdministratorBatchesExplicitlySelectOneScope()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid(), "SystemAdmin"));
        var first = TenantCreation() with { TenantId = Guid.NewGuid() };
        var result = await service.BulkGrantTenantPermissionsAsync([first, TenantCreation() with { TenantId = Guid.NewGuid() }]);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheClonedPayloadIsValidatedInsteadOfEnumeratingAnAlterableCollectionAgain()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var payload = new ChangingPayload();
        IPermissionLifecycleService service = new PermissionService(context.Object, actorContextAccessor: Actors(Guid.NewGuid()));
        var result = await service.CreateTenantPermissionAsync(TenantCreation() with { Permissions = payload });
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(1, payload.Enumerations);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ValidLifecycleRequestsRequireMandatoryCollaboratorsAndRealTransactionalStorage()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = Actors(Guid.NewGuid());
        IPermissionLifecycleService unconfigured = new PermissionService(context.Object, actorContextAccessor: actors);
        Assert.Equal("PermissionWrite.CollaboratorsRequired", (await unconfigured.CreateTenantPermissionAsync(TenantCreation())).Error.Code);
        Assert.Equal("PermissionWrite.CollaboratorsRequired", (await unconfigured.DeleteTenantPermissionAsync(Deletion())).Error.Code);
        var versions = new Mock<ITenantSecurityVersionStore>(MockBehavior.Strict);
        var audit = new Mock<IPermissionAuditService>(MockBehavior.Strict);
        IPermissionLifecycleService configured = new PermissionService(context.Object, versions.Object, audit.Object, actors);
        Assert.Equal("PermissionWrite.RelationalContextRequired", (await configured.CreateTenantPermissionAsync(TenantCreation())).Error.Code);
        Assert.Equal("PermissionWrite.RelationalContextRequired", (await configured.DeleteTenantPermissionAsync(Deletion())).Error.Code);
        context.VerifyNoOtherCalls(); versions.VerifyNoOtherCalls(); audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NullAndCancellationRemainExplicitInsteadOfInventingItemResults()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionLifecycleService service = new PermissionService(context.Object);
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.CreateTenantPermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.DeleteTenantPermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.BulkRevokeTenantPermissionsAsync(null!));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateTenantPermissionAsync(TenantCreation(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteTenantPermissionAsync(Deletion(), cancellation.Token));
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("member")]
    [InlineData("cross-tenant")]
    [InlineData("global")]
    public async Task LegacyMutationFamiliesAlsoRejectUnauthorizedCallsBeforeStorage(string scenario)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var tenant = Guid.NewGuid();
        var selected = scenario == "global" ? (Guid?)null : scenario == "cross-tenant" ? Guid.NewGuid() : tenant;
        var service = new PermissionService(context.Object, actorContextAccessor: Actors(tenant,
            scenario == "member" ? "Member" : "TenantAdmin", anonymous: scenario == "anonymous"));
        var user = Guid.NewGuid(); var resource = Guid.NewGuid();
        var operations = new Func<Task>[]
        {
            () => service.GrantTenantPermissionAsync(user, selected, [PermissionType.Read]),
            () => service.RevokeTenantPermissionAsync(user, selected, [PermissionType.Read]),
            () => service.GrantContentTypePermissionAsync(user, selected, "Document", [PermissionType.Read]),
            () => service.RevokeContentTypePermissionAsync(user, selected, "Document", [PermissionType.Read]),
            () => service.GrantResourcePermissionAsync<GenericResourcePermission, EntityBase>(user, selected, resource, [PermissionType.Read]),
            () => service.RevokeResourceAccessAsync<GenericResourcePermission, EntityBase>(user, selected, resource),
            () => service.BulkGrantResourcePermissionAsync<GenericResourcePermission, EntityBase>(user, selected, [], [PermissionType.Read]),
            () => service.ShareResourceAsync<GenericResourcePermission, EntityBase>(resource, user, selected, [PermissionType.Read])
        };
        foreach (var operation in operations) { await Assert.ThrowsAsync<UnauthorizedAccessException>(operation); }
        if (selected.HasValue)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.JoinTenantAsync(user, selected.Value));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.LeaveTenantAsync(user, selected.Value));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetTenantDefaultPermissionsAsync(selected.Value, [PermissionType.Read]));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.BulkGrantTenantPermissionAsync([], selected.Value, [PermissionType.Read]));
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetGlobalDefaultPermissionsAsync([PermissionType.Read]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CleanupExpiredPermissionsAsync());
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyValidActorCannotWriteWithoutRequiredCollaboratorsOrWithInvalidPayloads()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var tenant = Guid.NewGuid();
        var unconfigured = new PermissionService(context.Object, actorContextAccessor: Actors(tenant));
        await Assert.ThrowsAsync<InvalidOperationException>(() => unconfigured.GrantTenantPermissionAsync(Guid.NewGuid(), tenant, [PermissionType.Read]));
        var configured = PermissionMutationTestServices.Create(context.Object, tenant);
        await Assert.ThrowsAsync<ArgumentException>(() => configured.BulkGrantTenantPermissionAsync([Guid.NewGuid(), Guid.Empty], tenant, [PermissionType.Read]));
        await Assert.ThrowsAsync<ArgumentException>(() => configured.GrantTenantPermissionAsync(Guid.NewGuid(), tenant, [(PermissionType)int.MaxValue]));
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ManagingGlobalDefaultsDoesNotGrantCrossTenantCleanupAuthority()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(value => value.ActorContext).Returns(ActorContextBuilder.ForUser(Guid.NewGuid()).WithTenantId(Guid.NewGuid())
            .WithPermission(SystemPermission.Keys.ManageGlobalDefaults).Build());
        var service = new PermissionService(context.Object, Mock.Of<ITenantSecurityVersionStore>(), Mock.Of<IPermissionAuditService>(), actors.Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CleanupExpiredPermissionsAsync());
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public void FocusedContractsExposeExplicitCancellationAndMatchingConvenienceOverloads()
    {
        var contracts = new[] { typeof(IPermissionReadService), typeof(IPermissionUpdateService), typeof(IPermissionLifecycleService) };
        var count = 0;
        foreach (var contract in contracts)
        {
            var methods = contract.GetMethods();
            foreach (var cancellable in methods.Where(method => method.GetParameters().LastOrDefault()?.ParameterType == typeof(CancellationToken)))
            {
                var parameters = cancellable.GetParameters();
                Assert.False(parameters[^1].IsOptional);
                var convenience = Assert.Single(methods.Where(method => method.Name == cancellable.Name
                    && method.GetGenericArguments().Length == cancellable.GetGenericArguments().Length
                    && method.GetParameters().Select(parameter => parameter.ParameterType)
                        .SequenceEqual(parameters.Take(parameters.Length - 1).Select(parameter => parameter.ParameterType))));
                Assert.Equal(cancellable.ReturnType.ToString(), convenience.ReturnType.ToString());
                count++;
            }
        }
        Assert.Equal(22, count);
    }

    private static TenantPermissionCreation TenantCreation() => new() { UserId = Guid.NewGuid(), Permissions = ["Read"] };
    private static ContentTypePermissionCreation ContentCreation() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionType.Read], ContentTypeName = "Document" };
    private static ResourcePermissionCreation ResourceCreation() => new() { UserId = Guid.NewGuid(), Permissions = [PermissionType.Read], ResourceId = Guid.NewGuid(), ResourceType = "Document" };
    private static PermissionGrantDeletion Deletion() => new() { GrantId = Guid.NewGuid(), ExpectedVersion = 1 };
    private static IActorContextAccessor Actors(Guid tenant, string role = "TenantAdmin", bool anonymous = false)
    {
        var actor = new Mock<IActorContextAccessor>();
        actor.SetupGet(value => value.ActorContext).Returns(anonymous ? ActorContextBuilder.Create().Build()
            : ActorContextBuilder.ForUser(Guid.NewGuid()).WithTenantId(tenant).WithRole(role).Build());
        return actor.Object;
    }
    private sealed class ChangingPayload : IReadOnlyCollection<string>
    {
        public int Count => 1;
        public int Enumerations { get; private set; }
        public IEnumerator<string> GetEnumerator() => (++Enumerations == 1 ? new[] { " " } : new[] { "Read" }).AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
