using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class ServiceAccountCertificateAuthenticationTests : IDisposable
{
    private readonly X509Certificate2 _certificateAuthority = ClientCertificateTestFactory.CreateCertificateAuthority("GameGuild Service Test Root");
    private readonly X509Certificate2 _clientCertificate;

    public ServiceAccountCertificateAuthenticationTests()
    {
        _clientCertificate = ClientCertificateTestFactory.CreateClientCertificate("service-client", _certificateAuthority);
    }

    public void Dispose()
    {
        _certificateAuthority.Dispose();
        _clientCertificate.Dispose();
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_WithBoundThumbprint_ReturnsAccountAndRecordsSuccess()
    {
        var account = CreateBoundAccount();
        account.BindCertificate(ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(_clientCertificate));
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-cert", _clientCertificate, "192.0.2.10");

        authenticated.Should().Be(account);
        authenticated!.AuthenticationCount.Should().Be(1);
        authenticated.LastAuthenticatedFromIp.Should().Be("192.0.2.10");
        repository.Verify(repo => repo.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_WithBoundSpkiPin_ReturnsAccount()
    {
        var account = CreateBoundAccount();
        account.BindCertificate(null!, ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(_clientCertificate));
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-cert", _clientCertificate, null);

        authenticated.Should().Be(account);
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_WithoutBoundCertificate_FailsClosed()
    {
        var account = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "svc-cert",
            ClientSecretHash = "hash",
            Name = "Unbound",
            Scopes = "read",
            IsActive = true
        };
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-cert", _clientCertificate, null);

        authenticated.Should().BeNull();
        account.FailedAuthenticationAttempts.Should().Be(1);
        repository.Verify(repo => repo.UpdateAsync(account, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_WithDifferentCertificate_FailsClosedAndCountsFailure()
    {
        var account = CreateBoundAccount();
        account.BindCertificate(ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(_clientCertificate));
        using var otherCertificate = ClientCertificateTestFactory.CreateClientCertificate("other-client", _certificateAuthority);
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-cert", otherCertificate, null);

        authenticated.Should().BeNull();
        account.FailedAuthenticationAttempts.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_UnknownClientId_FailsClosedWithoutLockoutSideEffects()
    {
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-missing", _clientCertificate, null);

        authenticated.Should().BeNull();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<ServiceAccount>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_LockedAccount_FailsClosed()
    {
        var account = CreateBoundAccount();
        account.BindCertificate(ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(_clientCertificate));
        account.Lock("locked by test");
        var repository = new Mock<IServiceAccountRepository>();
        repository.Setup(repo => repo.GetByClientIdAsync("svc-cert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var service = CreateService(repository);

        var authenticated = await service.AuthenticateWithCertificateAsync("svc-cert", _clientCertificate, null);

        authenticated.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateWithCertificateAsync_NullCertificate_Throws()
    {
        var service = CreateService(new Mock<IServiceAccountRepository>());

        var act = () => service.AuthenticateWithCertificateAsync("svc-cert", null!, null);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void BindCertificate_SetsThumbprintAndKeyPin()
    {
        var account = new ServiceAccount { ClientId = "svc-cert", Name = "n", Scopes = "s" };
        var thumbprint = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(_clientCertificate);
        var spki = ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(_clientCertificate);

        account.BindCertificate(thumbprint, spki);

        account.CertificateThumbprint.Should().Be(thumbprint);
        account.CertificateSpkiSha256.Should().Be(spki);
        account.UnbindCertificate();
        account.CertificateThumbprint.Should().BeNull();
        account.CertificateSpkiSha256.Should().BeNull();
    }

    private static ServiceAccount CreateBoundAccount()
    {
        return new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "svc-cert",
            ClientSecretHash = "hash",
            Name = "Cert bound service",
            Scopes = "read:users",
            IsActive = true
        };
    }

    private static ServiceAccountService CreateService(Mock<IServiceAccountRepository> repository) =>
        new(repository.Object, Mock.Of<IRefreshTokenHasher>(), NullLogger<ServiceAccountService>.Instance);
}
