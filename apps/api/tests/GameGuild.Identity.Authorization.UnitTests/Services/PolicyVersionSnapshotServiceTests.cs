using System.Text.Json;
using FluentAssertions;
using GameGuild.CQRS.Models;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
/// Append-only policy version snapshots (issue #188): every mutation appends the next
/// per-policy version inside the mutating unit of work.
/// </summary>
public class PolicyVersionSnapshotServiceTests
{
    private static (PolicyVersionSnapshotService Service, Mock<IPolicyVersionHistoryRepository> History) Sut(params int[] existingVersions)
    {
        var history = new Mock<IPolicyVersionHistoryRepository>(MockBehavior.Strict);
        var setup = history.SetupSequence(h => h.GetLatestVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()));
        foreach (var existing in existingVersions)
        {
            setup = setup.ReturnsAsync(existing);
        }

        history.Setup(h => h.Append(It.IsAny<PolicyVersionHistory>()));
        return (new PolicyVersionSnapshotService(history.Object), history);
    }

    [Fact]
    public async Task SnapshotConditionalPolicy_FirstVersion_IsOneAndCarriesFullState()
    {
        var tenantId = Guid.NewGuid();
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = new TenantId(tenantId),
            Name = "business-hours-only",
            PermissionType = "billing:charge",
            Priority = 5,
            IsEnabled = true
        };
        var (service, history) = Sut();

        var snapshot = await service.SnapshotConditionalPolicyAsync(policy, PolicyVersionChangeType.Created);

        snapshot.Version.Should().Be(1);
        snapshot.Kind.Should().Be(PolicyVersionKind.ConditionalPolicy);
        snapshot.TenantId.Should().Be(tenantId);
        snapshot.PolicyName.Should().Be("business-hours-only");
        snapshot.ChangeType.Should().Be(PolicyVersionChangeType.Created);
        snapshot.SnapshotJson.Should().Contain("business-hours-only").And.Contain("billing:charge");
        history.Verify(h => h.Append(It.Is<PolicyVersionHistory>(row => row.Version == 1 && row.PolicyId == policy.Id)), Times.Once);
    }

    [Fact]
    public async Task SnapshotPolicyDefinition_IncrementsFromLatest()
    {
        var policy = new PolicyDefinitionEntity
        {
            Id = Guid.NewGuid(),
            PolicyName = "RequireEditor",
            RequiredPermissionsJson = "[\"content:edit\"]"
        };
        var (service, history) = Sut(3);

        var snapshot = await service.SnapshotPolicyDefinitionAsync(
            policy,
            PolicyVersionChangeType.Updated,
            changedBy: Guid.NewGuid(),
            reason: "tighten");

        snapshot.Version.Should().Be(4);
        snapshot.Kind.Should().Be(PolicyVersionKind.PolicyDefinition);
        snapshot.ChangedBy.Should().NotBeNull();
        snapshot.Reason.Should().Be("tighten");
        using var document = JsonDocument.Parse(snapshot.SnapshotJson);
        document.RootElement.GetProperty("RequiredPermissionsJson").GetString().Should().Contain("content:edit");
        history.Verify(h => h.Append(It.Is<PolicyVersionHistory>(row => row.Version == 4)), Times.Once);
    }

    [Fact]
    public async Task SnapshotPolicyDefinition_DeleteCapturesLastKnownState()
    {
        var policy = new PolicyDefinitionEntity { Id = Guid.NewGuid(), PolicyName = "Legacy" };
        var (service, _) = Sut(7);

        var snapshot = await service.SnapshotPolicyDefinitionAsync(policy, PolicyVersionChangeType.Deleted);

        snapshot.Version.Should().Be(8);
        snapshot.ChangeType.Should().Be(PolicyVersionChangeType.Deleted);
        snapshot.SnapshotJson.Should().Contain("Legacy");
    }
}
