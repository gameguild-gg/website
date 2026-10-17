using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
///     Tests for the content-type paths of the centralized DAC permission resolver
/// (issues #360/#365/#320): layer-qualified content-type checks, content-type context
/// routing, and tenant → content-type → resource inheritance under
/// <see cref="PermissionLayer.Auto"/>.
/// </summary>
public class DacPermissionResolverContentTypeTests
{
    private readonly Mock<IEffectivePermissionResolver> _engine = new();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private DacPermissionResolver CreateSut()
        => new(_engine.Object, NullLogger<DacPermissionResolver>.Instance);

    private EffectivePermissionContext? Captured { get; set; }

    private void SetupEngineReturning(params string[] permissions)
        => _engine
            .Setup(e => e.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EffectivePermissions
            {
                Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
                Sources = permissions.ToDictionary(
                    p => p, _ => PermissionSource.DirectGrant, StringComparer.OrdinalIgnoreCase)
            });

    [Fact]
    public async Task ResolveAsync_ContentTypeQuery_RoutesThroughContentTypeContext()
    {
        SetupEngineReturning("Course.Read");
        Captured = null;
        _engine
            .Setup(e => e.ResolveAsync(It.IsAny<EffectivePermissionContext>(), It.IsAny<CancellationToken>()))
            .Callback<EffectivePermissionContext, CancellationToken>((context, _) => Captured = context)
            .ReturnsAsync(new EffectivePermissions
            {
                Permissions = new HashSet<string>(["Course.Read"], StringComparer.OrdinalIgnoreCase),
                Sources = new Dictionary<string, PermissionSource>(StringComparer.OrdinalIgnoreCase)
            });

        var resolution = await CreateSut().ResolveAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType,
            ContentType = "Course"
        });

        resolution.ContextValid.Should().BeTrue();
        Captured.Should().NotBeNull();
        Captured!.ContentType.Should().Be("Course");
        Captured.HasResource.Should().BeFalse();
        resolution.Grants("Read").Should().BeTrue("the content-type qualified name was minted by the engine");
        resolution.Grants("Write").Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_ContentTypeLayer_RequiresContentTypeScope()
    {
        SetupEngineReturning("Course.Read");

        // A content-type layer query without a content type fails closed without
        // consulting the engine.
        var granted = await CreateSut().HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.ContentType
        }, "Read");

        granted.Should().BeFalse();
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_TenantGrantSatisfiesContentTypeAndResourceChecks()
    {
        // Tenant → content-type → resource inheritance: a plain tenant-layer name
        // grants at every layer under Auto.
        SetupEngineReturning("Read");

        var sut = CreateSut();
        (await sut.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Auto,
            ContentType = "Course"
        }, "Read")).Should().BeTrue("the tenant-layer name satisfies the content-type Auto check");

        (await sut.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Auto,
            ResourceType = "Course",
            ResourceId = "42"
        }, "Read")).Should().BeTrue("the tenant-layer name satisfies the resource Auto check");
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_ContentTypeGrantSatisfiesResourceChecksOfSameType()
    {
        // ct → resource inheritance: the resource-type qualified two-segment name is
        // consulted for resource Auto queries even without an explicit ContentType.
        SetupEngineReturning("Course.Read");

        var granted = await CreateSut().HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Auto,
            ResourceType = "Course",
            ResourceId = "42"
        }, "Read");

        granted.Should().BeTrue("a content-type grant applies to every resource of the type under Auto");
    }

    [Fact]
    public async Task HasPermissionAsync_Auto_ContentTypeGrantDoesNotSatisfyAnotherTypesResourceChecks()
    {
        SetupEngineReturning("Course.Read");

        var granted = await CreateSut().HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Auto,
            ResourceType = "Project",
            ResourceId = "42"
        }, "Read");

        granted.Should().BeFalse("a Course content-type grant never applies to Project resources");
    }

    [Fact]
    public async Task HasPermissionAsync_ExplicitResourceLayer_IsStrictToTheExactResource()
    {
        // Explicit layers stay strict: the resource layer checks only the fully
        // qualified {Type}.{Id}.{Permission} name (owner-override parity).
        SetupEngineReturning("Course.Read");

        var sut = CreateSut();
        (await sut.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "Course",
            ResourceId = "42"
        }, "Read")).Should().BeFalse("the two-segment content-type name does not satisfy the strict resource layer");

        SetupEngineReturning("Course.42.Read");
        (await sut.HasPermissionAsync(new DacPermissionQuery
        {
            UserId = _userId,
            TenantId = _tenantId,
            Layer = PermissionLayer.Resource,
            ResourceType = "Course",
            ResourceId = "42"
        }, "Read")).Should().BeTrue("the fully qualified resource name satisfies the strict resource layer");
    }
}
