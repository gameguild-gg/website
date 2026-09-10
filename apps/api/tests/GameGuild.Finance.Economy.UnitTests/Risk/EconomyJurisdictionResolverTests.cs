using FluentAssertions;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyJurisdictionResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("95000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("95000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task ResolveAsync_UsesCurrentSignedApprovedKycJurisdiction()
    {
        var evidence = Evidence() with { JurisdictionCode = "BRA" };
        var resolver = new EconomyJurisdictionResolver(new StubEvidenceReader(evidence));

        var result = await resolver.ResolveAsync(
            TenantId, ActorId, "bra", "BRA", Now, CancellationToken.None);

        result.Should().Be(new EconomyJurisdictionResolution("BRA", 4, 7, "evidence-hash"));
    }

    [Theory]
    [InlineData(ComplianceEvidenceResult.Rejected, true, "BRA", 1)]
    [InlineData(ComplianceEvidenceResult.Approved, false, "BRA", 1)]
    [InlineData(ComplianceEvidenceResult.Approved, true, null, 1)]
    [InlineData(ComplianceEvidenceResult.Approved, true, "BRA", -1)]
    public async Task ResolveAsync_FailsClosedForUnusableKycEvidence(
        ComplianceEvidenceResult result,
        bool signatureVerified,
        string? jurisdiction,
        int expiryDays)
    {
        var evidence = Evidence(result, signatureVerified, expiryDays) with
        {
            JurisdictionCode = jurisdiction
        };
        var resolver = new EconomyJurisdictionResolver(new StubEvidenceReader(evidence));

        var act = () => resolver.ResolveAsync(
            TenantId, ActorId, null, null, Now, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<EconomyJurisdictionUnavailableException>();
    }

    [Theory]
    [InlineData("DEU", null)]
    [InlineData(null, "USA")]
    [InlineData("BR", null)]
    public async Task ResolveAsync_RejectsConflictingOrInvalidServerMetadata(
        string? providerJurisdiction,
        string? destinationJurisdiction)
    {
        var resolver = new EconomyJurisdictionResolver(
            new StubEvidenceReader(Evidence() with { JurisdictionCode = "BRA" }));

        var act = () => resolver.ResolveAsync(
            TenantId,
            ActorId,
            providerJurisdiction,
            destinationJurisdiction,
            Now,
            CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<EconomyJurisdictionConflictException>();
    }

    private static DurableComplianceEvidence Evidence(
        ComplianceEvidenceResult result = ComplianceEvidenceResult.Approved,
        bool signatureVerified = true,
        int expiryDays = 1) => new(
        "sumsub",
        "production",
        "event-4",
        TenantId,
        EconomySubjectReference.ForUser(TenantId, ActorId),
        ComplianceEvidenceKinds.KycAml,
        4,
        result,
        7,
        "evidence-hash",
        signatureVerified,
        Now.AddHours(-1),
        Now.AddDays(expiryDays));

    private sealed class StubEvidenceReader(DurableComplianceEvidence? evidence) : IComplianceEvidenceReader
    {
        public ValueTask<DurableComplianceEvidence?> ReadLatestAsync(
            Guid tenantId,
            string subjectHash,
            string evidenceKind,
            CancellationToken cancellationToken) => ValueTask.FromResult(evidence);
    }
}
