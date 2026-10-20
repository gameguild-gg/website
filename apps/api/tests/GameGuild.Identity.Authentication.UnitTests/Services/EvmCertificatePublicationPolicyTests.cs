using System.Numerics;
using GameGuild.Configuration.ApplicationLayer;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificatePublicationPolicyTests
{
    [Theory]
    [InlineData("zero-fee")]
    [InlineData("over-cap")]
    [InlineData("negative-priority")]
    [InlineData("priority-over-fee")]
    [InlineData("short-check")]
    [InlineData("long-check")]
    [InlineData("short-lease")]
    [InlineData("long-lease")]
    [InlineData("short-replacement")]
    [InlineData("long-replacement")]
    [InlineData("no-attempts")]
    [InlineData("too-many-attempts")]
    [InlineData("unsafe-reference")]
    public void RejectsUnboundedOrInconsistentPolicy(string field)
    {
        var network = Network(); var budget = EvmCertificateFeeBudget.Create(100000, 100, 10000000);
        var fee = field == "zero-fee" ? BigInteger.Zero : field == "over-cap" ? 101 : 20;
        var priority = field == "negative-priority" ? -1 : field == "priority-over-fee" ? 21 : 2;
        var check = field == "short-check" ? TimeSpan.Zero : field == "long-check" ? TimeSpan.FromDays(2) : TimeSpan.FromSeconds(5);
        var lease = field == "short-lease" ? TimeSpan.FromSeconds(9) : field == "long-lease" ? TimeSpan.FromDays(2) : TimeSpan.FromMinutes(2);
        var replacement = field == "short-replacement" ? TimeSpan.FromSeconds(1) : field == "long-replacement" ? TimeSpan.FromDays(2) : TimeSpan.FromSeconds(30);
        var attempts = field == "no-attempts" ? 0 : field == "too-many-attempts" ? 11 : 3;
        var reference = field == "unsafe-reference" ? "../../secrets" : "issuer-v1";
        Action create = () => EvmCertificatePublicationPolicy.Create(network, reference, budget, fee, priority, check, lease, replacement, attempts);
        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetainsExactPublicIdentityAndIntegerWeiBounds()
    {
        var network = Network(); var budget = EvmCertificateFeeBudget.Create(100000, 100, 10000000);
        var policy = EvmCertificatePublicationPolicy.Create(network, "issuer-v1", budget, 20, 2,
            TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), 3);
        policy.Network.Should().BeSameAs(network);
        policy.Budget.Should().BeSameAs(budget);
        policy.InitialFeePerGasWei.Should().Be(20);
        policy.InitialPriorityFeePerGasWei.Should().Be(2);
        policy.MaximumAttempts.Should().Be(3);
    }

    [Fact]
    public void WorkerCompositionIsExplicitAndDoesNotSelectPrivateKeyOrPersistenceAdapters()
    {
        var services = new ServiceCollection();
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IHostedService));
        var policy = Policy();
        services.AddEvmCertificatePublicationWorker(policy);
        Assert.Contains(services, d => d.ServiceType == typeof(EvmCertificatePublicationWorker));
        Assert.Contains(services, d => d.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IEvmCertificatePrivateKeySource));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IEvmCertificateTransactionSigner));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(EvmCertificateJournal));
    }

    [Fact]
    public void DuplicateProfileRegistrationIsRejectedWithoutPartialMutation()
    {
        var services = new ServiceCollection();
        services.AddEvmCertificatePublicationWorker(Policy());
        var count = services.Count;
        Assert.Throws<ArgumentException>(() => services.AddEvmCertificatePublicationWorker(Policy()));
        Assert.Equal(count, services.Count);
    }

    private static EvmCertificatePublicationPolicy Policy() => EvmCertificatePublicationPolicy.Create(Network(), "issuer-v1",
        EvmCertificateFeeBudget.Create(100000, 100, 10000000), 20, 2,
        TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(30), 3);

    private static EvmCertificatePublicationNetwork Network() => EvmCertificatePublicationNetwork.Capture("worker-profile", new EvmCertificateNetworkOptions
    {
        ChainId = 12345, RpcEndpoint = "https://rpc.invalid/", ContractAddress = "0x" + new string('1', 40),
        IssuerId = "0x" + new string('2', 64), ExpectedRuntimeCodeSha256 = new string('3', 64),
        FinalityMode = EvmCertificateFinalityMode.Confirmations, MinimumConfirmations = 1
    });
}
