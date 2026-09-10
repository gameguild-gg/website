using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.TrustSafety;

public sealed class TrustSafetyModule : ModuleBase
{
    public override string Name => "TrustSafety";
    public override bool EnabledByDefault => true;
    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddTrustSafetyComposition();
}

public static class TrustSafetyCompositionExtensions
{
    public static IServiceCollection AddTrustSafetyComposition(this IServiceCollection services)
    {
        services.TryAddSingleton<IComplianceEvidenceReader, UnavailableComplianceEvidenceReader>();
        services.TryAddScoped<ITrustSafetyRiskInputSource, PostgreSqlTrustSafetyRiskInputSource>();
        services.TryAddScoped<ITrustSafetyEventSignatureVerifier, EconomyTrustSafetyEventSignatureVerifier>();
        services.TryAddScoped<ITrustSafetyControlPlane, PostgreSqlTrustSafetyControlPlane>();
        return services;
    }
}

public sealed class PostgreSqlTrustSafetyRiskInputSource : ITrustSafetyRiskInputSource
{
    private readonly IComplianceEvidenceReader _evidence;

    public PostgreSqlTrustSafetyRiskInputSource(IComplianceEvidenceReader evidence) =>
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));

    public ValueTask<TrustSafetyRiskInput> ReadAsync(
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueSubjectReference);
        return ValueTask.FromResult(Unavailable(observedAt, "trust-safety-tenant-context-required"));
    }

    public async ValueTask<TrustSafetyRiskInput> ReadAsync(
        Guid tenantId,
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueSubjectReference);
        var current = await _evidence.ReadLatestAsync(
            tenantId, opaqueSubjectReference, ComplianceEvidenceKinds.TrustSafety, cancellationToken);
        if (current is null)
            return Unavailable(observedAt, "trust-safety-evidence-unavailable");
        return new TrustSafetyRiskInput(
            current.Version,
            current.IssuedAt,
            current.ExpiresAt,
            Map(current.Result),
            current.EvidenceHash,
            current.SignatureVerified && !string.IsNullOrWhiteSpace(current.EvidenceHash));
    }

    private static TrustSafetyRiskInput Unavailable(DateTimeOffset observedAt, string evidenceHash) => new(
            1,
            observedAt,
            observedAt.AddMinutes(1),
            ExternalRiskOutcome.Unavailable,
            evidenceHash,
            true);

    private static ExternalRiskOutcome Map(ComplianceEvidenceResult result) => result switch
    {
        ComplianceEvidenceResult.Approved => ExternalRiskOutcome.Allow,
        ComplianceEvidenceResult.Rejected => ExternalRiskOutcome.Deny,
        ComplianceEvidenceResult.NeedsReview => ExternalRiskOutcome.Review,
        _ => ExternalRiskOutcome.Unavailable
    };
}
