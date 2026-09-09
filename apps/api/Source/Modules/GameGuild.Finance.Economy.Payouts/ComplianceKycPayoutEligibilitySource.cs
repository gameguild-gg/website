using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Compliance.KYC;

namespace GameGuild.Finance.Economy.Payouts;

public sealed class ComplianceKycPayoutEligibilitySource : IPayoutKycEligibilitySource
{
    private readonly IKycService _kycService;

    public ComplianceKycPayoutEligibilitySource(IKycService kycService) =>
        _kycService = kycService ?? throw new ArgumentNullException(nameof(kycService));

    public async ValueTask<PayoutKycSnapshot> ReadAsync(
        Guid payeeId,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        if (payeeId == Guid.Empty) throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        var result = await _kycService.GetLatestVerificationAsync(payeeId, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return Unavailable(payeeId, observedAt, $"failure:{result.Error.Code}");

        var verification = result.Value;
        if (verification is null)
            return Unavailable(payeeId, observedAt, "missing");

        var issuedAt = new DateTimeOffset(DateTime.SpecifyKind(verification.UpdatedAt, DateTimeKind.Utc));
        var expiresAt = verification.ExpiresAt is { } expiry
            ? new DateTimeOffset(DateTime.SpecifyKind(expiry, DateTimeKind.Utc))
            : observedAt;
        var approved = verification.Status == KycVerificationStatus.Approved &&
                       verification.DeletedAt is null && expiresAt > observedAt;
        return new PayoutKycSnapshot(
            payeeId,
            Math.Max(1, verification.Version),
            approved,
            issuedAt,
            expiresAt,
            Hash(
                verification.Id.ToString("N"),
                verification.Version.ToString(CultureInfo.InvariantCulture),
                ((int)verification.Status).ToString(CultureInfo.InvariantCulture),
                verification.ExternalVerificationId ?? string.Empty,
                issuedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                expiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)));
    }

    private static PayoutKycSnapshot Unavailable(Guid payeeId, DateTimeOffset observedAt, string reason) =>
        new(payeeId, 1, false, observedAt, observedAt, Hash(payeeId.ToString("N"), reason));

    private static string Hash(params string[] values)
    {
        var canonical = string.Join('|', values.Select(value => $"{value.Length}:{value}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
