using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed class KmsAdRewardSessionTokenProtector : IAdRewardSessionTokenProtector
{
    private readonly ICapabilityReceiptSigner _signer;
    private readonly ICapabilityPolicySignatureVerifier _verifier;

    public KmsAdRewardSessionTokenProtector(
        ICapabilityReceiptSigner signer,
        ICapabilityPolicySignatureVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(verifier);
        _signer = signer;
        _verifier = verifier;
    }

    public async ValueTask<SignedAdRewardSession> ProtectAsync(
        DurableAdRewardSessionClaims claims,
        CancellationToken cancellationToken = default)
    {
        ValidateClaims(claims);
        var payload = JsonSerializer.Serialize(TokenPayload.FromClaims(claims));
        var signature = await _signer.SignAsync(payload, cancellationToken);
        return new SignedAdRewardSession(string.Join('.',
            Encode(payload),
            Encode(signature.KeyId),
            Encode(signature.Signature)));
    }

    public async ValueTask<DurableAdRewardSessionClaims> UnprotectAsync(
        SignedAdRewardSession token,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(token.Value);
        var parts = token.Value.Split('.', StringSplitOptions.None);
        if (parts.Length != 3)
            throw new InvalidAdRewardSessionTokenException("Session token format is invalid.");
        try
        {
            var payload = Decode(parts[0]);
            var keyId = Decode(parts[1]);
            var signature = Decode(parts[2]);
            var claims = (JsonSerializer.Deserialize<TokenPayload>(payload)
                          ?? throw new InvalidAdRewardSessionTokenException("Session token payload is invalid."))
                .ToClaims();
            ValidateClaims(claims);
            if (!await _verifier.VerifyAsync(payload, keyId, signature, cancellationToken))
                throw new InvalidAdRewardSessionTokenException("Session token signature is invalid.");
            if (now < claims.IssuedAt)
                throw new InvalidAdRewardSessionTokenException("Session token is not active yet.");
            if (now >= claims.ExpiresAt)
                throw new ExpiredAdRewardSessionTokenException("Session token has expired.");
            return claims;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new InvalidAdRewardSessionTokenException("Session token payload is invalid.");
        }
    }

    private static void ValidateClaims(DurableAdRewardSessionClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.SessionId == Guid.Empty || claims.TenantId == Guid.Empty || claims.UserId == Guid.Empty ||
            claims.WalletId.Value == Guid.Empty)
            throw new ArgumentException("Session, tenant, user and wallet IDs are required.", nameof(claims));
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.Network);
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.CreativeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.DeviceRiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.IpRiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.AsnRiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(claims.Nonce);
        if (claims.RequiredDuration <= TimeSpan.Zero || claims.ExpiresAt <= claims.IssuedAt)
            throw new ArgumentException("Session timing is invalid.", nameof(claims));
    }

    internal static string HashToken(string token) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    internal static string HashOpaque(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Decode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized += (normalized.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private sealed record TokenPayload(
        Guid SessionId,
        Guid TenantId,
        Guid UserId,
        Guid WalletId,
        string Network,
        string CreativeId,
        string DeviceRiskHash,
        string IpRiskHash,
        string AsnRiskHash,
        string Nonce,
        long RequiredDurationTicks,
        long PolicyVersion,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt)
    {
        internal static TokenPayload FromClaims(DurableAdRewardSessionClaims claims) => new(
            claims.SessionId,
            claims.TenantId,
            claims.UserId,
            claims.WalletId.Value,
            claims.Network,
            claims.CreativeId,
            claims.DeviceRiskHash,
            claims.IpRiskHash,
            claims.AsnRiskHash,
            claims.Nonce,
            claims.RequiredDuration.Ticks,
            claims.PolicyVersion.Value,
            claims.IssuedAt,
            claims.ExpiresAt);

        internal DurableAdRewardSessionClaims ToClaims()
        {
            try
            {
                return new DurableAdRewardSessionClaims(
                    SessionId,
                    TenantId,
                    UserId,
                    new GameGuild.Finance.Economy.Contracts.WalletId(WalletId),
                    Network,
                    CreativeId,
                    DeviceRiskHash,
                    IpRiskHash,
                    AsnRiskHash,
                    Nonce,
                    TimeSpan.FromTicks(RequiredDurationTicks),
                    new GameGuild.Finance.Economy.Contracts.PolicyVersion(PolicyVersion),
                    IssuedAt,
                    ExpiresAt);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidAdRewardSessionTokenException(
                    $"Session token payload is invalid: {exception.ParamName}.");
            }
        }
    }
}
