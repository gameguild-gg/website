using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the centralized DAC permission resolution contract (issue #339):
///     3-layer name conventions (tenant / content-type / resource), delegation to the
///     canonical effective-permission engine, context selection (tenant vs resource) and
///     fail-closed behavior for invalid queries and unknown layers.
/// </summary>
public class DacPermissionResolverTests
{
    private readonly Mock<IEffectivePermissionResolver> _engine = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private DacPermissionResolver CreateSut()
        => new(_engine.Object, NullLogger<DacPermissionResolver>.Instance);

    private static EffectivePermissions Effective(params string[] permissions)
        => new()
        {
            Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
            Sources = permissions.ToDictionary(
                p => p, _ => PermissionSource.DirectGrant, StringComparer.OrdinalIgnoreCase)
        };

    private void SetupEngineReturning(EffectivePermissions result)
    {
        _engine
            .Setup(e => e.ResolveAsync(
                It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    // ── Fail-closed: invalid queries never reach the engine ───────────────

    [Fact]
    public async Task ResolveAsync_EmptyUser_FailsClosed_WithoutEngineCall()
    {
        var query = new DacPermissionQuery { UserId = Guid.Empty, TenantId = _tenantId };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.ContextValid.Should().BeFalse();
        resolution.Effective.Permissions.Should().BeEmpty();
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_EmptyTenant_FailsClosed_WithoutEngineCall()
    {
        var query = new DacPermissionQuery { UserId = _userId, TenantId = Guid.Empty };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.ContextValid.Should().BeFalse();
        resolution.Effective.Permissions.Should().BeEmpty();
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_HalfResourcePair_FailsClosed_WithoutEngineCall()
    {
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = "Course"
            // ResourceId intentionally missing: half-specified pair is invalid.
        };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.ContextValid.Should().BeFalse();
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_ContentTypeLayerWithoutContentType_FailsClosed()
    {
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType
        };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.ContextValid.Should().BeFalse();
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ResolveAsync_ResourceLayerWithoutResourcePair_FailsClosed()
    {
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ContentType = "Course"
        };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.ContextValid.Should().BeFalse();
        _engine.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HasPermissionAsync_InvalidQuery_ReturnsFalse_WithoutEngineCall()
    {
        var query = new DacPermissionQuery { UserId = Guid.Empty, TenantId = _tenantId };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
        _engine.VerifyNoOtherCalls();
    }

    // ── Tenant layer: raw permission names ────────────────────────────────

    [Fact]
    public async Task HasPermissionAsync_TenantLayer_RawPermission_Granted()
    {
        SetupEngineReturning(Effective("content:read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Tenant
        };

        (await CreateSut().HasPermissionAsync(query, "content:read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_TenantLayer_OnlyContentTypeNamePresent_Denied()
    {
        // Tenant layer checks the raw name only: a content-type qualified grant
        // ("Course.Read") must not answer a raw tenant-level "Read" check.
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Tenant
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_TenantLayer_AbsentPermission_DeniedByDefault()
    {
        SetupEngineReturning(Effective("content:read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Tenant
        };

        (await CreateSut().HasPermissionAsync(query, "content:write")).Should().BeFalse();
    }

    // ── Content-type layer: {ContentType}.{Permission} ────────────────────

    [Fact]
    public async Task HasPermissionAsync_ContentTypeLayer_QualifiedName_Granted()
    {
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "Course"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ContentTypeLayer_RawTenantGrantOnly_Denied()
    {
        SetupEngineReturning(Effective("Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "Course"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ContentTypeQuery_UsesTenantContext()
    {
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "Course"
        };

        await CreateSut().ResolveAsync(query);

        _engine.Verify(e => e.ResolveAsync(
            It.Is<EffectivePermissionContext>(c =>
                c.UserId == _userId
                && c.TenantId == _tenantId
                && !c.HasResource),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Resource layer: {ResourceType}.{ResourceId}.{Permission} ──────────

    [Fact]
    public async Task HasPermissionAsync_ResourceLayer_QualifiedName_Granted()
    {
        var resourceId = Guid.NewGuid();
        SetupEngineReturning(Effective($"Course.{resourceId}.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "Course",
            ResourceId = resourceId.ToString()
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_ResourceLayer_GrantForOtherResource_Denied()
    {
        // Context isolation: a resource grant for a different resource id must not
        // answer a check for this resource.
        var resourceId = Guid.NewGuid();
        var otherResource = Guid.NewGuid();
        SetupEngineReturning(Effective($"Course.{otherResource}.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "Course",
            ResourceId = resourceId.ToString()
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task ResolveAsync_ResourcePair_UsesResourceContext()
    {
        SetupEngineReturning(Effective("Course.42.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = "Course",
            ResourceId = "42"
        };

        await CreateSut().ResolveAsync(query);

        _engine.Verify(e => e.ResolveAsync(
            It.Is<EffectivePermissionContext>(c =>
                c.UserId == _userId
                && c.TenantId == _tenantId
                && c.HasResource
                && c.ResourceType == "Course"
                && c.ResourceId == "42"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Auto layer: any applicable layer may grant ────────────────────────

    [Fact]
    public async Task HasPermissionAsync_Auto_TenantLevelGrant_Grants()
    {
        SetupEngineReturning(Effective("Read"));
        var query = new DacPermissionQuery { UserId = _userId, TenantId = _tenantId };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_ContentTypeGrant_Grants()
    {
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentType = "Course"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_ResourceGrant_Grants()
    {
        SetupEngineReturning(Effective("Course.42.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ResourceType = "Course",
            ResourceId = "42"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_UnrelatedContentTypeGrant_Denied()
    {
        // Auto only consults layers whose scope is present on the query: a grant
        // qualified for another content type must not answer this query.
        SetupEngineReturning(Effective("Lesson.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentType = "Course"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_AbsentEverywhere_DeniedByDefault()
    {
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentType = "Course",
            ResourceType = "Course",
            ResourceId = "42"
        };

        (await CreateSut().HasPermissionAsync(query, "Write")).Should().BeFalse();
    }

    // ── Engine passthrough: DENY-WINS, fail-closed context, throttle ──────

    [Fact]
    public async Task ResolveAsync_ReturnsCanonicalResult_Unmodified()
    {
        var canonical = Effective("Course.Read");
        canonical = canonical with { Throttled = true, ContextValid = true };
        SetupEngineReturning(canonical);
        var query = new DacPermissionQuery { UserId = _userId, TenantId = _tenantId };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.Effective.Should().BeSameAs(canonical);
        resolution.Throttled.Should().BeTrue();
        resolution.ContextValid.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_EngineFailClosedResult_DeniesEverything()
    {
        // DENY-WINS / fail-closed passthrough: an engine result flagged invalid
        // (already an empty set) must deny every layer check.
        SetupEngineReturning(new EffectivePermissions
        {
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase),
            ContextValid = false
        });
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            ContentType = "Course"
        };

        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_UnknownLayerValue_FailsClosed()
    {
        SetupEngineReturning(Effective("Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = (PermissionLayer)999
        };

        // Unknown layer values fail closed even though the query shape is otherwise valid.
        (await CreateSut().HasPermissionAsync(query, "Read")).Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_EmptyPermissionName_ReturnsFalse()
    {
        SetupEngineReturning(Effective("Read"));
        var query = new DacPermissionQuery { UserId = _userId, TenantId = _tenantId };

        (await CreateSut().HasPermissionAsync(query, " ")).Should().BeFalse();
    }

    [Fact]
    public async Task Resolution_Grants_MatchesHasPermissionAsync_LayerSemantics()
    {
        SetupEngineReturning(Effective("Course.Read"));
        var query = new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "Course"
        };

        var resolution = await CreateSut().ResolveAsync(query);

        resolution.Grants("Read").Should().BeTrue();
        resolution.Grants("Write").Should().BeFalse();
    }

    // ── DI registration ───────────────────────────────────────────────────

    [Fact]
    public void AddUnifiedAuthorizationLayer_RegistersDacPermissionResolver_Scoped()
    {
        var services = new ServiceCollection();
        services.AddUnifiedAuthorizationLayer();

        services.Should().Contain(descriptor =>
            descriptor.ServiceType == typeof(IDacPermissionResolver)
            && descriptor.ImplementationType == typeof(DacPermissionResolver)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }
}
