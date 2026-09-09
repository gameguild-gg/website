using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Compliance.FinancialCrime;

public sealed class FinancialCrimeModule : ModuleBase
{
    public override string Name => "Compliance.FinancialCrime";
    public override bool EnabledByDefault => true;
    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddFinancialCrimeComposition();
}

public static class FinancialCrimeCompositionExtensions
{
    public static IServiceCollection AddFinancialCrimeComposition(this IServiceCollection services)
    {
        services.TryAddSingleton<IComplianceEvidenceReader, UnavailableComplianceEvidenceReader>();
        services.TryAddScoped<IFinancialCrimeRiskInputSource, PostgreSqlFinancialCrimeRiskInputSource>();
        services.TryAddScoped<IFinancialCrimeControlPlane, PostgreSqlFinancialCrimeControlPlane>();
        return services;
    }
}

public sealed class PostgreSqlFinancialCrimeRiskInputSource : IFinancialCrimeRiskInputSource
{
    private readonly IComplianceEvidenceReader _evidence;

    public PostgreSqlFinancialCrimeRiskInputSource(IComplianceEvidenceReader evidence) =>
        _evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));

    public ValueTask<FinancialCrimeRiskInput> ReadAsync(
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueSubjectReference);
        return ValueTask.FromResult(Unavailable(observedAt, "financial-crime-tenant-context-required"));
    }

    public async ValueTask<FinancialCrimeRiskInput> ReadAsync(
        Guid tenantId,
        string opaqueSubjectReference,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(opaqueSubjectReference);
        var current = await _evidence.ReadLatestAsync(
            tenantId, opaqueSubjectReference, ComplianceEvidenceKinds.FinancialCrime, cancellationToken);
        if (current is null)
            return Unavailable(observedAt, "financial-crime-evidence-unavailable");
        return new FinancialCrimeRiskInput(
            current.Version,
            current.IssuedAt,
            current.ExpiresAt,
            Map(current.Result),
            current.EvidenceHash,
            current.SignatureVerified && !string.IsNullOrWhiteSpace(current.EvidenceHash));
    }

    private static FinancialCrimeRiskInput Unavailable(DateTimeOffset observedAt, string evidenceHash) => new(
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
