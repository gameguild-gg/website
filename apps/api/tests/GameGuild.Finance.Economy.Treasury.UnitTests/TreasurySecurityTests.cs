using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Reserves;

namespace GameGuild.Finance.Economy.Treasury.UnitTests;

public sealed class TreasurySecurityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ActivationRejectsAProposalWhoseRawEvidenceManifestWasChangedAfterSigning()
    {
        var signer = new TreasuryProposalSigner(Enumerable.Repeat((byte)11, 32).ToArray());
        var envelope = TreasuryReservePlanner.Build(TreasuryTestFixture.Request(Now), signer, Now);
        var tampered = envelope with { EvidenceManifest = envelope.EvidenceManifest + "|tampered" };

        var act = () => new TreasuryCoreActivationGateway(new CoreReserveAuthority(), signer).Activate(tampered, Now);

        act.Should().Throw<TreasurySignatureException>();
    }

    [Fact]
    public void OperationGateRejectsCustodyEvidenceSignedByAnotherKey()
    {
        var proposalSigner = new TreasuryProposalSigner(Enumerable.Repeat((byte)12, 32).ToArray());
        var authority = new CoreReserveAuthority();
        var envelope = TreasuryReservePlanner.Build(TreasuryTestFixture.Request(Now), proposalSigner, Now);
        var head = new TreasuryCoreActivationGateway(authority, proposalSigner).Activate(envelope, Now);
        var trustedSigner = new TreasuryCustodySigner(Enumerable.Repeat((byte)13, 32).ToArray());
        var untrustedSigner = new TreasuryCustodySigner(Enumerable.Repeat((byte)14, 32).ToArray());
        var report = new TreasuryCustodyReconciler(untrustedSigner).Reconcile(
            head,
            head.AssetAllocations.Select(asset => new TreasuryCustodyObservation(
                asset.AssetKey, asset.EligibleUsdNanos, 0, Now.AddMinutes(-1), Now.AddMinutes(1), "custody")).ToArray(),
            Now);

        var act = () => new TreasuryOperationGate(authority, trustedSigner).Authorize(
            TreasuryProtectedOperation.PayoutDispatch,
            head.Version,
            head.AuthorizationEpoch,
            report,
            null,
            Now);

        act.Should().Throw<TreasurySignatureException>();
    }

    [Fact]
    public void CustodySigningRejectsInvalidDependenciesSecretsAndSignatures()
    {
        ((Action)(() => new TreasuryCustodySigner(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => new TreasuryCustodySigner(new byte[31]))).Should().Throw<ArgumentException>();
        ((Action)(() => new TreasuryCustodyReconciler(null!))).Should().Throw<ArgumentNullException>();

        var signer = new TreasuryCustodySigner(Enumerable.Repeat((byte)15, 32).ToArray());
        ((Action)(() => signer.Sign(null!))).Should().Throw<ArgumentNullException>();
        ((Action)(() => signer.Verify(null!))).Should().Throw<ArgumentNullException>();

        var unsigned = new TreasuryCustodyReport(
            new ReserveVersion(1), 1, Now.AddMinutes(-1), Now.AddMinutes(1),
            0, 0, 0, 0, [], "evidence", string.Empty);
        signer.Verify(unsigned).Should().BeFalse();
        signer.Verify(unsigned with { Signature = "not-base64" }).Should().BeFalse();
        signer.Verify(unsigned with { Signature = Convert.ToBase64String(new byte[32]) }).Should().BeFalse();

        var signed = unsigned with { Signature = signer.Sign(unsigned) };
        signer.Verify(signed).Should().BeTrue();
    }
}

internal static class TreasuryTestFixture
{
    internal static TreasuryProposalRequest Request(DateTimeOffset now)
    {
        var ledger = new GameGuild.Finance.Economy.Ledger.InMemoryLedgerKernelStore();
        var rule = new TreasuryBufferRule(0, 0);
        var policy = new TreasuryBufferPolicy(
            new PolicyVersion(1), rule, rule, rule, rule, rule, rule, rule,
            now.AddMinutes(-1), now.AddMinutes(1), "finance");
        return new TreasuryProposalRequest(
            new ReserveVersion(1), null, new PolicyVersion(1), 1,
            now.AddMinutes(-1), now.AddMinutes(1), ledger, new HashSet<WalletId>(),
            policy, new TreasuryBufferExposure(0, 0, 0, 0, 0, 0, 0), [], [], []);
    }
}
