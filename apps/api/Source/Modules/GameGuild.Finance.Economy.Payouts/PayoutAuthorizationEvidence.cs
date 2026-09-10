using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public enum PayoutAuthorizationPhase
{
    Reservation = 1,
    Dispatch = 2
}

public sealed record PayoutAuthorizationEvidence(
    Guid OperationId,
    Guid TenantId,
    Guid ActorId,
    PayoutAuthorizationPhase Phase,
    Guid RiskDecisionId,
    string ReauthenticationEvidenceHash,
    string OperationFingerprintHash,
    Guid CapabilityReceiptId,
    string CapabilityReceiptHash,
    DateTimeOffset RecordedAt);

public interface IPayoutAuthorizationEvidenceWriter
{
    Task AppendAsync(
        PayoutAuthorizationEvidence evidence,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlPayoutAuthorizationEvidenceWriter : IPayoutAuthorizationEvidenceWriter
{
    private readonly DbContext _db;

    public PostgreSqlPayoutAuthorizationEvidenceWriter(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL payout authorization evidence requires the application's relational DbContext.");
    }

    public async Task AppendAsync(
        PayoutAuthorizationEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT economy_private.append_payout_authorization_evidence_v1(
                {evidence.OperationId},
                {evidence.TenantId},
                {evidence.ActorId},
                {(int)evidence.Phase},
                {evidence.RiskDecisionId},
                {evidence.ReauthenticationEvidenceHash},
                {evidence.OperationFingerprintHash},
                {evidence.CapabilityReceiptId},
                {evidence.CapabilityReceiptHash},
                {evidence.RecordedAt});
            """, cancellationToken).ConfigureAwait(false);
    }
}
