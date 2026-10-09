using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MockQueryable.Moq;
using Moq;
using Xunit;

using GameGuild;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Identity.Authorization.UnitTests;

// ============================================================================
// Gap 3: permission-change webhooks (HMAC-signed, retried, config-gated)
// ============================================================================

public class WebhookPermissionChangeNotifierTests
{
    private sealed class ScriptedHandler(Func<int, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        public List<byte[]> Bodies { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is ByteArrayContent bytes)
            {
                Bodies.Add(bytes.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult());
            }

            return Task.FromResult(respond(Requests.Count));
        }
    }

    private const string Secret = "test-webhook-secret";

    private static WebhookPermissionChangeNotifier CreateNotifier(
        HttpMessageHandler handler,
        PermissionWebhookOptions options)
    {
        var engineOptions = new PermissionEngineOptions { Webhooks = options };
        return new WebhookPermissionChangeNotifier(
            new HttpClient(handler),
            Options.Create(engineOptions),
            NullLogger<WebhookPermissionChangeNotifier>.Instance);
    }

    private static PermissionWebhookOptions EnabledOptions(int maxAttempts = 1) => new()
    {
        Enabled = true,
        Endpoint = "https://webhooks.example/permissions",
        Secret = Secret,
        MaxAttempts = maxAttempts,
        RetryBaseDelayMilliseconds = 1
    };

    private static PermissionChangeEvent CreateChange() => new(
        PermissionChangeEventType.Granted,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Tenant",
        new[] { "tenant:read" },
        Guid.NewGuid(),
        new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task NotifyAsync_SkipsWhenDisabledWithoutCallingTheEndpoint()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var notifier = CreateNotifier(handler, new PermissionWebhookOptions { Enabled = false });

        var result = await notifier.NotifyAsync(CreateChange());

        result.Should().BeEquivalentTo(new PermissionChangeNotificationResult(false, 0, true));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_SkipsWhenEndpointOrSecretMissing()
    {
        var noEndpoint = new PermissionWebhookOptions { Enabled = true, Secret = Secret };
        var noSecret = new PermissionWebhookOptions { Enabled = true, Endpoint = "https://webhooks.example" };
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        (await CreateNotifier(handler, noEndpoint).NotifyAsync(CreateChange())).Skipped.Should().BeTrue();
        (await CreateNotifier(handler, noSecret).NotifyAsync(CreateChange())).Skipped.Should().BeTrue();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_SignsPayloadWithHmacSha256Header()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var notifier = CreateNotifier(handler, EnabledOptions());

        var result = await notifier.NotifyAsync(CreateChange());

        result.Delivered.Should().BeTrue();
        var request = handler.Requests.Should().ContainSingle().Subject;
        request.RequestUri!.ToString().Should().Be("https://webhooks.example/permissions");
        request.Method.Should().Be(HttpMethod.Post);
        request.Content!.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = handler.Bodies.Single();
        var header = request.Headers.GetValues(WebhookPermissionChangeNotifier.SignatureHeaderName).Single();
        header.Should().StartWith("sha256=");
        header.Should().Be(WebhookPermissionChangeNotifier.ComputeSignature(body, Secret));

        // Independent HMAC recomputation over the exact body bytes.
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        header.Should().Be($"sha256={Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant()}");

        // The payload itself is stable JSON with the event type name.
        var payload = JsonSerializer.Deserialize<JsonDocument>(body)!.RootElement;
        payload.GetProperty("eventType").GetString().Should().Be("permission.granted");
        payload.GetProperty("occurredAtUtc").GetDateTime().Should().Be(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task NotifyAsync_RetriesOnFailureUntilDelivered()
    {
        var handler = new ScriptedHandler(attempt => attempt >= 2
            ? new HttpResponseMessage(HttpStatusCode.OK)
            : new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var notifier = CreateNotifier(handler, EnabledOptions(maxAttempts: 3));

        var result = await notifier.NotifyAsync(CreateChange());

        result.Delivered.Should().BeTrue();
        result.Attempts.Should().Be(2);
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task NotifyAsync_GivesUpAfterMaxAttempts()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var notifier = CreateNotifier(handler, EnabledOptions(maxAttempts: 3));

        var result = await notifier.NotifyAsync(CreateChange());

        result.Delivered.Should().BeFalse();
        result.Attempts.Should().Be(3);
        result.Skipped.Should().BeFalse();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task NotifyAsync_SwallowsHttpExceptionsAndKeepsRetrying()
    {
        var handler = new ScriptedHandler(attempt => attempt >= 2
            ? new HttpResponseMessage(HttpStatusCode.OK)
            : throw new HttpRequestException("connection reset"));
        var notifier = CreateNotifier(handler, EnabledOptions(maxAttempts: 3));

        var result = await notifier.NotifyAsync(CreateChange());

        result.Delivered.Should().BeTrue();
        result.Attempts.Should().Be(2);
    }

    [Fact]
    public void ComputeSignature_IsDeterministicForKnownVector()
    {
        var body = Encoding.UTF8.GetBytes("""{"eventType":"permission.revoked"}""");
        var signature = WebhookPermissionChangeNotifier.ComputeSignature(body, "secret");

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("secret"));
        var expected = $"sha256={Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant()}";
        signature.Should().Be(expected);
        WebhookPermissionChangeNotifier.ComputeSignature(body, "secret").Should().Be(signature);
    }

    [Fact]
    public void SerializePayload_RoundsTripsEventFields()
    {
        var change = CreateChange();
        var payload = JsonSerializer.Deserialize<JsonDocument>(WebhookPermissionChangeNotifier.SerializePayload(change))!.RootElement;

        payload.GetProperty("eventType").GetString().Should().Be("permission.granted");
        payload.GetProperty("tenantId").GetGuid().Should().Be(change.TenantId!.Value);
        payload.GetProperty("userId").GetGuid().Should().Be(change.UserId!.Value);
        payload.GetProperty("permissionType").GetString().Should().Be("Tenant");
        payload.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()).Should().Equal("tenant:read");
        payload.GetProperty("performedBy").GetGuid().Should().Be(change.PerformedBy);
    }

    [Fact]
    public async Task GrantTenantPermissionAsync_FansOutWebhookNotification()
    {
        var repository = new Mock<ITenantPermissionRepository>();
        repository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        repository
            .Setup(repo => repo.CreateAsync(It.IsAny<TenantPermission>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission permission, CancellationToken _) => permission);
        var audit = new Mock<IPermissionAuditService>();
        audit
            .Setup(service => service.LogPermissionChangeAsync(
                It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuditLog());
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore
            .Setup(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2L);
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string> { "SystemAdmin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var notifier = new Mock<IPermissionChangeNotifier>();
        notifier
            .Setup(n => n.NotifyAsync(It.IsAny<PermissionChangeEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionChangeNotificationResult(true, 1, false));

        var grantService = new PermissionGrantService(
            repository.Object,
            audit.Object,
            versionStore.Object,
            accessor.Object,
            NullLogger<PermissionGrantService>.Instance,
            new[] { notifier.Object });

        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await grantService.GrantTenantPermissionAsync(userId, tenantId, new[] { "tenant:read" });

        notifier.Verify(
            n => n.NotifyAsync(
                It.Is<PermissionChangeEvent>(change =>
                    change.EventType == PermissionChangeEventType.Granted
                    && change.UserId == userId
                    && change.TenantId == tenantId
                    && change.Permissions.SequenceEqual(new[] { "tenant:read" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

// ============================================================================
// Gap 4: external system permission synchronization
// ============================================================================

public class PermissionSyncServiceTests
{
    private readonly Mock<IDynamicRoleRepository> _roleRepository = new();
    private readonly Mock<ITenantPermissionRepository> _permissionRepository = new();
    private readonly Mock<IPermissionGrantService> _grantService = new();
    private readonly Mock<IRoleInheritanceEngine> _inheritanceEngine = new();
    private readonly Mock<IPermissionAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPermissionChangeNotifier> _notifier = new();

    private readonly Guid _tenantId = Guid.NewGuid();

    private PermissionSyncService CreateSut(PermissionSyncOptions? syncOptions = null)
    {
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = _tenantId,
            Roles = new HashSet<string> { "TenantAdmin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(actor);
        _notifier
            .Setup(notifier => notifier.NotifyAsync(It.IsAny<PermissionChangeEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionChangeNotificationResult(true, 1, false));
        _inheritanceEngine
            .Setup(engine => engine.WouldCreateCycleAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _roleRepository
            .Setup(repo => repo.GetByTenantAsync(It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? tenantId, bool includeGlobal, CancellationToken _) => tenantId.HasValue
                ? ExistingRoles.Where(role => role.TenantId == tenantId).ToList()
                : ExistingRoles.Where(role => role.TenantId == null).ToList());
        _permissionRepository
            .Setup(repo => repo.GetByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>());
        _permissionRepository
            .Setup(repo => repo.GetByUserAndTenantAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TenantPermission?)null);
        _grantService
            .Setup(service => service.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? userId, Guid? tenantId, string[] permissions, Guid? _, DateTime? _, string? _, CancellationToken _) =>
                new TenantPermission { UserId = userId, TenantId = tenantId, Permissions = permissions });

        return new PermissionSyncService(
            _roleRepository.Object,
            _permissionRepository.Object,
            _grantService.Object,
            _inheritanceEngine.Object,
            _auditService.Object,
            _actorAccessor.Object,
            new[] { _notifier.Object },
            Options.Create(new PermissionEngineOptions
            {
                ExternalSync = syncOptions ?? new PermissionSyncOptions()
            }),
            NullLogger<PermissionSyncService>.Instance);
    }

    private List<DynamicRole> ExistingRoles { get; } = new();

    private ExternalPermissionSyncDocument Document(
        List<ExternalRoleDefinition>? roles = null,
        List<ExternalPermissionEntry>? permissions = null,
        Guid? tenantId = null)
        => new(
            ExternalPermissionSyncDocument.SupportedSchemaVersion,
            SystemClock.UtcNow,
            tenantId ?? _tenantId,
            roles ?? new List<ExternalRoleDefinition>(),
            permissions ?? new List<ExternalPermissionEntry>());

    [Fact]
    public async Task ExportAsync_IncludesRolesWithPortableParentNames()
    {
        var editor = new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "Editor",
            DisplayName = "Editor",
            TenantId = _tenantId,
            Permissions = new[] { "content:update" }
        };
        var senior = new DynamicRole
        {
            Id = Guid.NewGuid(),
            Name = "SeniorEditor",
            DisplayName = "SeniorEditor",
            TenantId = _tenantId,
            Permissions = new[] { "content:review" },
            ParentRoleId = editor.Id,
            AdditionalParentRoleIds = new[] { editor.Id },
            BlockedInheritedPermissions = new[] { "content:delete" }
        };
        ExistingRoles.AddRange(new[] { editor, senior });
        var sut = CreateSut();
        _permissionRepository
            .Setup(repo => repo.GetByTenantAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TenantPermission>
            {
                new() { TenantId = _tenantId, UserId = Guid.NewGuid(), Permissions = new[] { "tenant:read" } }
            });

        var document = await sut.ExportAsync(_tenantId);

        document.SchemaVersion.Should().Be("1.0");
        document.TenantId.Should().Be(_tenantId);
        document.Roles.Should().HaveCount(2);
        var seniorDefinition = document.Roles.Single(role => role.Name == "SeniorEditor");
        seniorDefinition.ParentRoleName.Should().Be("Editor");
        seniorDefinition.BlockedInheritedPermissions.Should().Equal("content:delete");
        document.Permissions.Should().ContainSingle().Which.Permissions.Should().Equal("tenant:read");
    }

    [Fact]
    public async Task ImportAsync_RejectsUnsupportedSchema_WithoutApplyingAnything()
    {
        var document = Document(permissions: new List<ExternalPermissionEntry>
        {
            new(Guid.NewGuid(), new[] { "tenant:read" }, Array.Empty<string>(), true)
        });
        document = document with { SchemaVersion = "0.9" };

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: false);

        result.IsValid.Should().BeFalse();
        result.Applied.Should().BeFalse();
        result.ValidationErrors.Should().Contain(error => error.Contains("schema"));
        _grantService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_RejectsAdminWildcard_FailClosed()
    {
        var document = Document(roles: new List<ExternalRoleDefinition>
        {
            new("Super", "Super", null, new[] { "admin:*" }, Array.Empty<string>(), null, null, null, 0, true)
        });

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: false);

        result.IsValid.Should().BeFalse();
        result.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task ImportAsync_RejectsCrossTenantDocuments()
    {
        var document = Document(tenantId: Guid.NewGuid());

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: false);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(error => error.Contains("does not match"));
    }

    [Fact]
    public async Task ImportAsync_RejectsDocumentInternalCycles()
    {
        var document = Document(roles: new List<ExternalRoleDefinition>
        {
            new("A", "A", null, Array.Empty<string>(), Array.Empty<string>(), "B", null, null, 0, true),
            new("B", "B", null, Array.Empty<string>(), Array.Empty<string>(), "A", null, null, 0, true)
        });

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: false);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(error => error.Contains("cycle"));
    }

    [Fact]
    public async Task ImportAsync_RejectsUnknownParentReferences()
    {
        var document = Document(roles: new List<ExternalRoleDefinition>
        {
            new("A", "A", null, Array.Empty<string>(), Array.Empty<string>(), "DoesNotExist", null, null, 0, true)
        });

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: true);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(error => error.Contains("unknown parent"));
    }

    [Fact]
    public async Task PreviewImport_DryRun_PlansChangesWithoutApplying()
    {
        var document = Document(permissions: new List<ExternalPermissionEntry>
        {
            new(Guid.NewGuid(), new[] { "tenant:read", "content:read" }, Array.Empty<string>(), true)
        });

        var result = await CreateSut().PreviewImportAsync(document, _tenantId);

        result.IsValid.Should().BeTrue();
        result.DryRun.Should().BeTrue();
        result.Applied.Should().BeFalse();
        result.Changes.Should().Contain(change =>
            change.Kind == "permissions.grant"
            && change.Payload["permissions"] == "tenant:read,content:read");
        _grantService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ImportAsync_AppliesPlannedChangesThroughGuardedGrantService()
    {
        var userId = Guid.NewGuid();
        var document = Document(permissions: new List<ExternalPermissionEntry>
        {
            new(userId, new[] { "tenant:read" }, new[] { "content:delete" }, true)
        });

        var result = await CreateSut().ImportAsync(document, _tenantId, dryRun: false);

        result.Applied.Should().BeTrue();
        _grantService.Verify(
            service => service.GrantTenantPermissionAsync(
                userId, _tenantId,
                It.Is<string[]>(permissions => permissions.SequenceEqual(new[] { "tenant:read" })),
                It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _grantService.Verify(
            service => service.DenyTenantPermissionAsync(
                userId, _tenantId,
                It.Is<string[]>(permissions => permissions.SequenceEqual(new[] { "content:delete" })),
                It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _notifier.Verify(
            notifier => notifier.NotifyAsync(
                It.Is<PermissionChangeEvent>(change => change.EventType == PermissionChangeEventType.Synced),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ImportAsync_RespectsDocumentSizeCaps()
    {
        var roles = Enumerable.Range(0, 3)
            .Select(index => new ExternalRoleDefinition($"Role{index}", $"Role{index}", null, Array.Empty<string>(), Array.Empty<string>(), null, null, null, 0, true))
            .ToList();
        var document = Document(roles: roles);

        var result = await CreateSut(new PermissionSyncOptions { MaxRolesPerImport = 2 }).ImportAsync(document, _tenantId, dryRun: true);

        result.IsValid.Should().BeFalse();
        result.ValidationErrors.Should().Contain(error => error.Contains("cap"));
    }
}

public class PermissionEngineTenantGuardTests
{
    private static PermissionEngineTenantGuard CreateGuard(ActorContext actor)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return new PermissionEngineTenantGuard(accessor.Object);
    }

    private static ActorContext Actor(bool authenticated, Guid? tenantId, params string[] roles) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = Guid.NewGuid().ToString(),
        TenantId = tenantId,
        Roles = new HashSet<string>(roles),
        Permissions = new HashSet<string>(),
        IsAuthenticated = authenticated
    };

    [Fact]
    public void UnauthenticatedActor_IsRejected()
    {
        var guard = CreateGuard(Actor(false, null));
        var act = () => guard.ResolveAuthorizedTenant(Guid.NewGuid(), "test");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void TenantAdminOwnTenant_IsAuthorized()
    {
        var tenantId = Guid.NewGuid();
        var guard = CreateGuard(Actor(true, tenantId, "TenantAdmin"));
        guard.ResolveAuthorizedTenant(tenantId, "test").Should().Be(tenantId);
    }

    [Fact]
    public void TenantAdminOtherTenant_IsRejected()
    {
        var guard = CreateGuard(Actor(true, Guid.NewGuid(), "TenantAdmin"));
        var act = () => guard.ResolveAuthorizedTenant(Guid.NewGuid(), "test");
        act.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void GlobalScope_RequiresSystemAdmin()
    {
        var guard = CreateGuard(Actor(true, Guid.NewGuid(), "TenantAdmin"));
        var act = () => guard.ResolveAuthorizedTenant(null, "test");
        act.Should().Throw<UnauthorizedAccessException>();

        var systemAdmin = CreateGuard(Actor(true, null, "SystemAdmin"));
        systemAdmin.ResolveAuthorizedTenant(null, "test").Should().BeNull();
        systemAdmin.ResolveAuthorizedTenant(Guid.NewGuid(), "test").Should().NotBeNull();
    }
}

// ============================================================================
// Gap 7: permission restoration
// ============================================================================

public class PermissionRestorationServiceTests
{
    private readonly Mock<IPermissionAuditLogRepository> _auditLogRepository = new();
    private readonly Mock<IPermissionGrantService> _grantService = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<IPermissionChangeNotifier> _notifier = new();
    private readonly Mock<IApplicationDbContext> _context = new();

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();

    public PermissionRestorationServiceTests()
    {
        _versionStore
            .Setup(store => store.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7L);
        _actorAccessor.SetupGet(a => a.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = _actorId.ToString(),
            TenantId = _tenantId,
            Roles = new HashSet<string> { "TenantAdmin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });
        _notifier
            .Setup(notifier => notifier.NotifyAsync(It.IsAny<PermissionChangeEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionChangeNotificationResult(true, 1, false));
        _auditLogRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<PermissionAuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PermissionAuditLog log, CancellationToken _) => log);
        _grantService
            .Setup(service => service.RevokeTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _grantService
            .Setup(service => service.GrantTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<Guid?>(),
                It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? userId, Guid? tenantId, string[] permissions, Guid? _, DateTime? _, string? _, CancellationToken _) =>
                new TenantPermission { UserId = userId, TenantId = tenantId, Permissions = permissions });
        _grantService
            .Setup(service => service.RemoveDenyPermissionsAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private PermissionRestorationService CreateSut(List<TenantPermission> rows, PermissionRestorationOptions? options = null)
    {
        var set = rows.AsQueryable().BuildMockDbSet();
        _context.Setup(database => database.Set<TenantPermission>()).Returns(set.Object);
        _context
            .Setup(database => database.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return new PermissionRestorationService(
            _context.Object,
            _auditLogRepository.Object,
            _grantService.Object,
            _versionStore.Object,
            _actorAccessor.Object,
            new[] { _notifier.Object },
            Options.Create(new PermissionEngineOptions { Restoration = options ?? new PermissionRestorationOptions() }),
            NullLogger<PermissionRestorationService>.Instance);
    }

    [Fact]
    public async Task RestoreDeletedPermission_RestoresRowAndBumpsVersionAndAudits()
    {
        var permissionId = Guid.NewGuid();
        var deleted = new TenantPermission
        {
            Id = permissionId,
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            Permissions = new[] { "tenant:read" },
            Version = 1
        };
        deleted.SoftDelete();

        var sut = CreateSut(new List<TenantPermission> { deleted });

        var result = await sut.RestoreDeletedPermissionAsync(permissionId);

        result.Succeeded.Should().BeTrue();
        result.TenantId.Should().Be(_tenantId);
        deleted.IsDeleted.Should().BeFalse("the row is restored");
        _versionStore.Verify(store => store.IncrementVersionAsync(_tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        _auditLogRepository.Verify(
            repo => repo.CreateAsync(
                It.Is<PermissionAuditLog>(log =>
                    log.OperationType == PermissionOperationType.Restore
                    && log.TenantId!.Value == new TenantId(_tenantId)
                    && log.PerformedBy == _actorId),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notifier.Verify(
            notifier => notifier.NotifyAsync(
                It.Is<PermissionChangeEvent>(change => change.EventType == PermissionChangeEventType.Restored),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreDeletedPermission_OutsideRetentionWindow_IsRejected()
    {
        var permissionId = Guid.NewGuid();
        var deleted = new TenantPermission
        {
            Id = permissionId,
            TenantId = _tenantId,
            UserId = Guid.NewGuid(),
            Permissions = new[] { "tenant:read" },
            Version = 1
        };
        deleted.SoftDelete();

        var sut = CreateSut(
            new List<TenantPermission> { deleted },
            new PermissionRestorationOptions { RetentionDays = 1 });

        // Simulate an old deletion via the audit-visible timestamp field.
        deleted.DeletedAt = SystemClock.UtcNow.AddDays(-2);
        var result = await sut.RestoreDeletedPermissionAsync(permissionId);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("window");
        deleted.IsDeleted.Should().BeTrue("nothing was restored");
        _versionStore.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RestoreDeletedPermission_UnknownOrLiveRow_Fails()
    {
        var sut = CreateSut(new List<TenantPermission>());

        var result = await sut.RestoreDeletedPermissionAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UndoGrant_RevokesOnlyTheGrantedDelta()
    {
        var userId = Guid.NewGuid();
        var auditLog = new PermissionAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            UserId = userId,
            OperationType = PermissionOperationType.Grant,
            PermissionType = "Tenant",
            OldValue = "tenant:read",
            NewValue = "tenant:read,content:delete",
            PerformedBy = Guid.NewGuid(),
            Success = true,
            Timestamp = SystemClock.UtcNow.AddHours(-1)
        };
        _auditLogRepository
            .Setup(repo => repo.GetByIdAsync(auditLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var sut = CreateSut(new List<TenantPermission>());

        var result = await sut.UndoAuditEntryAsync(auditLog.Id);

        result.Succeeded.Should().BeTrue();
        _grantService.Verify(
            service => service.RevokeTenantPermissionAsync(
                userId, _tenantId,
                It.Is<string[]>(permissions => permissions.SequenceEqual(new[] { "content:delete" })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UndoRevoke_RegrantsTheRevokedDelta()
    {
        var userId = Guid.NewGuid();
        var auditLog = new PermissionAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            UserId = userId,
            OperationType = PermissionOperationType.Revoke,
            OldValue = "tenant:read,billing:read",
            NewValue = "tenant:read",
            PerformedBy = Guid.NewGuid(),
            Success = true,
            Timestamp = SystemClock.UtcNow.AddHours(-1)
        };
        _auditLogRepository
            .Setup(repo => repo.GetByIdAsync(auditLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var sut = CreateSut(new List<TenantPermission>());

        var result = await sut.UndoAuditEntryAsync(auditLog.Id);

        result.Succeeded.Should().BeTrue();
        _grantService.Verify(
            service => service.GrantTenantPermissionAsync(
                userId, _tenantId,
                It.Is<string[]>(permissions => permissions.SequenceEqual(new[] { "billing:read" })),
                _actorId, It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UndoEntry_OutsideRetentionWindow_IsRejected()
    {
        var auditLog = new PermissionAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            OperationType = PermissionOperationType.Grant,
            OldValue = null,
            NewValue = "tenant:read",
            PerformedBy = Guid.NewGuid(),
            Success = true,
            Timestamp = SystemClock.UtcNow.AddDays(-90)
        };
        _auditLogRepository
            .Setup(repo => repo.GetByIdAsync(auditLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var sut = CreateSut(
            new List<TenantPermission>(),
            new PermissionRestorationOptions { RetentionDays = 30 });

        var result = await sut.UndoAuditEntryAsync(auditLog.Id);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("window");
        _grantService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UndoEntry_NonUndoableOperationType_IsRejected()
    {
        var auditLog = new PermissionAuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(_tenantId),
            OperationType = PermissionOperationType.Review,
            PerformedBy = Guid.NewGuid(),
            Success = true,
            Timestamp = SystemClock.UtcNow
        };
        _auditLogRepository
            .Setup(repo => repo.GetByIdAsync(auditLog.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auditLog);

        var sut = CreateSut(new List<TenantPermission>());

        var result = await sut.UndoAuditEntryAsync(auditLog.Id);

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("cannot be undone");
    }

    [Fact]
    public async Task Restoration_ByForeignTenantAdmin_IsRejected()
    {
        var permissionId = Guid.NewGuid();
        var deleted = new TenantPermission
        {
            Id = permissionId,
            TenantId = Guid.NewGuid(), // a different tenant than the actor's
            UserId = Guid.NewGuid(),
            Permissions = new[] { "tenant:read" },
            Version = 1
        };
        deleted.SoftDelete();

        var sut = CreateSut(new List<TenantPermission> { deleted });

        var act = () => sut.RestoreDeletedPermissionAsync(permissionId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _versionStore.VerifyNoOtherCalls();
    }
}

// ============================================================================
// Gap 8: compliance reporting from the durable evaluation log
// ============================================================================

public class PermissionComplianceReportTests
{
    private readonly Mock<IPermissionEvaluationLogEntryRepository> _repository = new();

    private PermissionComplianceReportService CreateSut()
        => new(_repository.Object, NullLogger<PermissionComplianceReportService>.Instance);

    private static PermissionEvaluationLogEntry Entry(
        PermissionEvaluationOutcome outcome,
        DateTime at,
        string permission,
        string source = "graphql",
        string? operation = "guarded")
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ResourceType = "Project",
            RequiredPermissions = new[] { permission },
            Outcome = outcome,
            Source = source,
            Operation = operation,
            EvaluatedAtUtc = at
        };

    [Fact]
    public async Task BuildReport_AggregatesOutcomeRatesAndBreakdowns()
    {
        var from = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);
        _repository
            .Setup(repo => repo.GetRangeAsync(It.IsAny<Guid?>(), from, to, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Entry(PermissionEvaluationOutcome.Allow, from.AddMinutes(1), "projects:read"),
                Entry(PermissionEvaluationOutcome.Allow, from.AddMinutes(2), "projects:read"),
                Entry(PermissionEvaluationOutcome.Deny, from.AddMinutes(3), "projects:delete"),
                Entry(PermissionEvaluationOutcome.Error, from.AddMinutes(4), "projects:delete")
            });

        var report = await CreateSut().BuildReportAsync(null, from, to);

        report.TotalEvaluations.Should().Be(4);
        report.AllowCount.Should().Be(2);
        report.DenyCount.Should().Be(1);
        report.ErrorCount.Should().Be(1);
        report.AllowRate.Should().BeApproximately(0.5, 0.0001);
        report.DenyRate.Should().BeApproximately(0.25, 0.0001);

        var readRow = report.ByPermission.Should().Contain(row => row.Key == "projects:read").Subject;
        readRow.Total.Should().Be(2);
        readRow.Allow.Should().Be(2);
        var deleteRow = report.ByPermission.Should().Contain(row => row.Key == "projects:delete").Subject;
        deleteRow.Total.Should().Be(2);
        deleteRow.DenyRate.Should().BeApproximately(0.5, 0.0001);

        report.BySource.Should().ContainSingle().Which.Key.Should().Be("graphql");
        report.ByOperation.Should().ContainSingle().Which.Key.Should().Be("guarded");
    }

    [Fact]
    public async Task BuildReport_EmptyRange_YieldsZeroRates()
    {
        var from = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        _repository
            .Setup(repo => repo.GetRangeAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PermissionEvaluationLogEntry>());

        var report = await CreateSut().BuildReportAsync(Guid.NewGuid(), from, from.AddHours(1));

        report.TotalEvaluations.Should().Be(0);
        report.AllowRate.Should().Be(0);
        report.DenyRate.Should().Be(0);
        report.ByPermission.Should().BeEmpty();
    }

    [Fact]
    public async Task BuildReport_RejectsInvertedRanges()
    {
        var from = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var act = () => CreateSut().BuildReportAsync(null, from, from.AddHours(-1));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void EvaluationLogEntry_RoundTripsThroughTheRecordForm()
    {
        var record = new PermissionEvaluationRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Project",
            "res-1",
            new[] { "projects:read" },
            PermissionEvaluationOutcome.Deny,
            "graphql",
            "guarded",
            "permission_denied",
            new DateTime(2026, 10, 8, 8, 0, 0, DateTimeKind.Utc));

        var entry = PermissionEvaluationLogEntry.FromRecord(record);

        entry.TenantId.Should().Be(record.TenantId);
        entry.UserId.Should().Be(record.UserId);
        entry.Outcome.Should().Be(PermissionEvaluationOutcome.Deny);
        entry.RequiredPermissions.Should().Equal("projects:read");

        var roundTripped = entry.ToRecord();
        roundTripped.Should().BeEquivalentTo(record);
    }

    [Fact]
    public async Task DatabaseSink_FailsSoftWhenPersistenceThrows()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Loose);
        var set = Array.Empty<PermissionEvaluationLogEntry>().AsQueryable().BuildMockDbSet();
        set.Setup(s => s.Add(It.IsAny<PermissionEvaluationLogEntry>())).Throws(new InvalidOperationException("database offline"));
        context.Setup(database => database.Set<PermissionEvaluationLogEntry>()).Returns(set.Object);
        var sink = new PermissionEvaluationLogEntryRepository(
            context.Object,
            NullLogger<PermissionEvaluationLogEntryRepository>.Instance);

        var persisted = await sink.TryRecordAsync(new PermissionEvaluationRecord(
            null, null, "Project", null, new[] { "projects:read" }, PermissionEvaluationOutcome.Allow, "test"));

        persisted.Should().BeFalse("sink failures must never change the authorization decision");
    }
}

// ============================================================================
// Controller surface (issue #358 endpoints delegate to the sender)
// ============================================================================

public class PermissionEngineControllerTests
{
    [Fact]
    public async Task SyncController_Export_SendsCommandWithTenantScope()
    {
        var tenantId = Guid.NewGuid();
        var document = new ExternalPermissionSyncDocument("1.0", SystemClock.UtcNow, tenantId, [], []);
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.Is<ExportPermissionSyncCommand>(command => command.TenantId == tenantId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var controller = new PermissionSyncController(sender.Object, NullLogger<PermissionSyncController>.Instance);
        var result = await controller.Export(tenantId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(document);
    }

    [Fact]
    public async Task SyncController_Import_SendsCommandWithDocumentAndDryRunFlag()
    {
        var document = new ExternalPermissionSyncDocument("1.0", SystemClock.UtcNow, null, [], []);
        var report = new PermissionSyncImportResult(true, [], [], Applied: true, DryRun: false);
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<ImportPermissionSyncCommand>(command =>
                    command.Document == document
                    && command.DryRun
                    && command.TenantId == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var controller = new PermissionSyncController(sender.Object, NullLogger<PermissionSyncController>.Instance);
        var result = await controller.Import(new ImportPermissionSyncRequest(document, null, true), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(report);
    }

    [Fact]
    public async Task RestorationController_RestoreDeleted_SendsCommand()
    {
        var permissionId = Guid.NewGuid();
        var outcome = new PermissionRestorationResult(true, permissionId, Guid.NewGuid(), "restored");
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.Is<RestoreDeletedPermissionCommand>(command => command.PermissionId == permissionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

        var controller = new PermissionRestorationController(sender.Object, NullLogger<PermissionRestorationController>.Instance);
        var result = await controller.RestoreDeleted(permissionId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(outcome);
    }

    [Fact]
    public async Task RestorationController_UndoChange_SendsCommand()
    {
        var auditLogId = Guid.NewGuid();
        var outcome = new PermissionRestorationResult(true, null, Guid.NewGuid(), "undone");
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.Is<UndoPermissionChangeCommand>(command => command.AuditLogId == auditLogId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);

        var controller = new PermissionRestorationController(sender.Object, NullLogger<PermissionRestorationController>.Instance);
        var result = await controller.UndoChange(auditLogId, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(outcome);
    }

    [Fact]
    public async Task ComplianceController_GetReport_SendsQuery()
    {
        var tenantId = Guid.NewGuid();
        var from = new DateTime(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(1);
        var report = new PermissionComplianceReport(
            tenantId, from, to, 10, 6, 3, 1, [], [], []);
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetPermissionComplianceReportQuery>(query =>
                    query.TenantId == tenantId
                    && query.FromUtc == from
                    && query.ToUtc == to),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        var controller = new PermissionComplianceController(sender.Object, NullLogger<PermissionComplianceController>.Instance);
        var result = await controller.GetReport(tenantId, from, to, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(report);
    }

    [Fact]
    public async Task ComplianceReportHandler_DefaultsToTrailing24HoursAndGuardsScope()
    {
        var tenantId = Guid.NewGuid();
        var to = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
        SystemClock.SetProvider(new FixedNowProvider(to));
        try
        {
            var reportService = new Mock<IPermissionComplianceReportService>();
            reportService
                .Setup(service => service.BuildReportAsync(tenantId, to.AddHours(-24), to, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PermissionComplianceReport(tenantId, to.AddHours(-24), to, 0, 0, 0, 0, [], [], []));
            var accessor = new Mock<IActorContextAccessor>();
            accessor.SetupGet(a => a.ActorContext).Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Roles = new HashSet<string> { "TenantAdmin" },
                Permissions = new HashSet<string>(),
                IsAuthenticated = true
            });
            var guard = new PermissionEngineTenantGuard(accessor.Object);
            var handler = new GetPermissionComplianceReportHandler(
                reportService.Object,
                guard,
                NullLogger<GetPermissionComplianceReportHandler>.Instance);

            var report = await handler.Handle(
                new GetPermissionComplianceReportQuery { TenantId = tenantId },
                CancellationToken.None);

            report.TotalEvaluations.Should().Be(0);
            reportService.VerifyAll();
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    private sealed class FixedNowProvider(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero);
    }
}
