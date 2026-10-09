using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Moq;

namespace GameGuild.Identity.Authentication.UnitTests.Infrastructure;

internal sealed class Web3IdentityTestHarness
{
    public User User { get; } = new() { Id = Guid.NewGuid(), Email = "synthetic-wallet@example.test", IsActive = true, TokenVersion = 12 };
    public Guid TenantId { get; } = Guid.NewGuid();
    public Mock<IUserRepository> Users { get; } = new();
    public Mock<IExternalLoginRepository> Links { get; } = new();
    public Mock<IRefreshTokenHasher> Hashes { get; } = new();
    public Mock<ISessionManagementService> Sessions { get; } = new();
    public Mock<ISender> Sender { get; } = new();

    public Web3IdentityTestHarness()
    {
        PersistedAuthenticationSessions.Configure(Sessions);
        Links.Setup(value => value.GetByProviderKeyAsync("web3", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { Id = Guid.NewGuid(), UserId = User.Id, Provider = "web3" });
        Users.Setup(value => value.GetByIdAsync(User.Id, It.IsAny<CancellationToken>())).ReturnsAsync(User);
        Hashes.Setup(value => value.HashToken(It.IsAny<string>())).Returns((string value) => "synthetic-hash-" + value);
        Sender.Setup(value => value.Send(It.IsAny<GetDefaultTenantQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);
        Sender.Setup(value => value.Send(It.IsAny<GetUserMembershipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserMembershipsResponse
            {
                TotalCount = 1,
                Memberships = [new UserMembershipDto { TenantId = TenantId, TenantName = "Synthetic wallet tenant", TenantSlug = "wallet-unit",
                    IsActive = true, TenantIsActive = true, TenantIsDefault = true, Role = "Member" }]
            });
    }
}
