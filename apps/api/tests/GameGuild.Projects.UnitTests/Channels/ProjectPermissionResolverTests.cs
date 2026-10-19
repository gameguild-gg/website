using GameGuild.Identity.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Projects.UnitTests.Channels;

public sealed class ProjectPermissionResolverTests
{
    private static readonly PermissionType[] ProjectPermissions =
    [
        PermissionType.Read, PermissionType.Edit, PermissionType.Delete,
        PermissionType.Create, PermissionType.Share, PermissionType.Comment,
        PermissionType.Reply, PermissionType.Review, PermissionType.Approve,
        PermissionType.Publish, PermissionType.Archive, PermissionType.Restore,
    ];

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(12, true)]
    public async Task EffectivePermissions_ContainOnlyCentralAuthorizerGrants(int grantedCount, bool expectedOwner)
    {
        var projectId = Guid.NewGuid();
        var granted = ProjectPermissions.Take(grantedCount).ToArray();
        var authorization = new Mock<IProjectAuthorizationService>(MockBehavior.Strict);
        authorization.Setup(service => service.HasPermissionAsync(
                projectId, It.IsAny<PermissionType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, PermissionType permission, CancellationToken _) => granted.Contains(permission));
        IPermissionResolver resolver = new ProjectPermissionResolver(authorization.Object);

        var result = (await resolver.GetEffectivePermissionsAsync(
            Guid.NewGuid(), Guid.NewGuid(), projectId, nameof(Project))).ToArray();

        if (grantedCount == 0)
        {
            Assert.Empty(result);
        }
        else
        {
            var entry = Assert.Single(result);
            Assert.Equal(projectId, entry.ResourceId);
            Assert.Equal(nameof(Project), entry.ResourceType);
            Assert.Equal(granted, entry.Permissions);
            Assert.Equal(expectedOwner, entry.IsOwner);
        }

        foreach (var permission in ProjectPermissions)
        {
            authorization.Verify(service => service.HasPermissionAsync(
                projectId, permission, It.IsAny<CancellationToken>()), Times.Once);
        }
        authorization.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizerFailure_IsPropagatedWithoutReturningPartialPermissions()
    {
        var projectId = Guid.NewGuid();
        var failure = new InvalidOperationException("Synthetic authorization failure");
        var authorization = new Mock<IProjectAuthorizationService>(MockBehavior.Strict);
        authorization.Setup(service => service.HasPermissionAsync(
                projectId, PermissionType.Read, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        authorization.Setup(service => service.HasPermissionAsync(
                projectId, PermissionType.Edit, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        IPermissionResolver resolver = new ProjectPermissionResolver(authorization.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.GetEffectivePermissionsAsync(Guid.NewGuid(), Guid.NewGuid(), projectId, nameof(Project)));

        Assert.Same(failure, exception);
    }
}
