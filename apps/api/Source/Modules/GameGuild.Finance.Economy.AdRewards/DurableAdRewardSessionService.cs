using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.AdRewards.Persistence;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.AdRewards;

public interface IDurableAdRewardSessionService
{
    ValueTask<DurableAdRewardSessionResult> StartAsync(
        StartDurableAdRewardSessionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DurableAdRewardSessionService : IDurableAdRewardSessionService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(5);
    private readonly DbContext _db;
    private readonly IDurableAdRewardPolicyReader _policies;
    private readonly IAdRewardSessionTokenProtector _tokens;
    private readonly IAdRewardSessionEntropy _entropy;
    private readonly IAdRewardProviderAdapterResolver _providerAdapters;

    public DurableAdRewardSessionService(
        IApplicationDbContext context,
        IDurableAdRewardPolicyReader policies,
        IAdRewardSessionTokenProtector tokens,
        IAdRewardSessionEntropy entropy,
        IAdRewardProviderAdapterResolver providerAdapters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policies);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentNullException.ThrowIfNull(providerAdapters);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable ad reward sessions require the application's relational DbContext.");
        _policies = policies;
        _tokens = tokens;
        _entropy = entropy;
        _providerAdapters = providerAdapters;
    }

    public async ValueTask<DurableAdRewardSessionResult> StartAsync(
        StartDurableAdRewardSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var idempotencyKeyHash = Hash(request.IdempotencyKey.Value);
        var requestHash = Hash(string.Join('|',
            request.TenantId.ToString("N"),
            request.UserId.ToString("N"),
            request.WalletId.Value.ToString("N"),
            request.Network.Trim(),
            request.CreativeId.Trim(),
            request.DeviceRiskHash.Trim(),
            request.IpRiskHash.Trim(),
            request.AsnRiskHash.Trim(),
            request.RequiredDuration.Ticks.ToString(CultureInfo.InvariantCulture)));

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
            _db, IsolationLevel.Serializable, async _ =>
        {
        var duplicate = await _db.Set<AdRewardSessionRow>()
            .SingleOrDefaultAsync(
                session => session.TenantId == request.TenantId &&
                           session.StartIdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);
        if (duplicate is not null)
        {
            if (!string.Equals(duplicate.StartRequestHash, requestHash, StringComparison.Ordinal))
                throw new AdRewardIdempotencyConflictException(
                    "The session idempotency key is already bound to different inputs.");
            var replacementNonce = _entropy.CreateNonce();
            var duplicateClaims = MapClaims(duplicate, replacementNonce);
            var duplicateToken = await _tokens.ProtectAsync(duplicateClaims, cancellationToken);
            duplicate.NonceHash = Hash(replacementNonce);
            duplicate.TokenHash = KmsAdRewardSessionTokenProtector.HashToken(duplicateToken.Value);
            duplicate.TokenKeyId = Hash(duplicateToken.Value.Split('.')[1]);
            duplicate.UpdatedAt = request.RequestedAt;
            duplicate.Version++;
            await _db.SaveChangesAsync(cancellationToken);
            return new DurableAdRewardSessionResult(duplicateClaims, duplicateToken, true);
        }

        var policy = await _policies.GetEffectiveAsync(
            request.TenantId, request.Network, request.RequestedAt, cancellationToken);
        if (!policy.ProviderCertified || policy.Policy.IssuanceMode == AdRewardIssuanceMode.Disabled)
            throw new AdRewardIssuanceDisabledException(
                "The ad network is not certified for reward issuance.");
        if (!policy.Policy.IsReportCurrent(request.RequestedAt))
            throw new AdNetworkReportStaleException(
                "The provider report is stale; new reward sessions are disabled.");
        _providerAdapters.Resolve(policy.Policy.Network);

        var expiresAt = new[] { request.RequestedAt.Add(SessionLifetime), policy.Policy.ExpiresAt }.Min();
        var claims = new DurableAdRewardSessionClaims(
            _entropy.CreateSessionId(),
            request.TenantId,
            request.UserId,
            request.WalletId,
            policy.Policy.Network,
            request.CreativeId.Trim(),
            request.DeviceRiskHash.Trim(),
            request.IpRiskHash.Trim(),
            request.AsnRiskHash.Trim(),
            _entropy.CreateNonce(),
            request.RequiredDuration,
            policy.Policy.Version,
            request.RequestedAt,
            expiresAt);
        var token = await _tokens.ProtectAsync(claims, cancellationToken);
        _db.Set<AdRewardSessionRow>().Add(new AdRewardSessionRow
        {
            Id = claims.SessionId,
            TenantId = claims.TenantId,
            UserId = claims.UserId,
            WalletId = claims.WalletId.Value,
            Network = claims.Network,
            PolicyVersion = claims.PolicyVersion.Value,
            CreativeId = claims.CreativeId,
            DeviceRiskHash = claims.DeviceRiskHash,
            IpRiskHash = claims.IpRiskHash,
            AsnRiskHash = claims.AsnRiskHash,
            NonceHash = Hash(claims.Nonce),
            TokenHash = KmsAdRewardSessionTokenProtector.HashToken(token.Value),
            TokenKeyId = Hash(token.Value.Split('.')[1]),
            RequiredDurationTicks = claims.RequiredDuration.Ticks,
            State = DurableAdRewardSessionState.Issued,
            StartIdempotencyKeyHash = idempotencyKeyHash,
            StartRequestHash = requestHash,
            IssuedAt = claims.IssuedAt,
            ExpiresAt = claims.ExpiresAt,
            UpdatedAt = claims.IssuedAt,
            Version = 1
        });
        _db.Set<AdRewardSessionEventRow>().Add(new AdRewardSessionEventRow
        {
            Id = Guid.NewGuid(),
            SessionId = claims.SessionId,
            Sequence = 1,
            State = DurableAdRewardSessionState.Issued,
            EvidenceHash = requestHash,
            OccurredAt = claims.IssuedAt
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new DurableAdRewardSessionResult(claims, token, false);
        }, cancellationToken);
    }

    internal static DurableAdRewardSessionClaims MapClaims(AdRewardSessionRow row, string nonce) => new(
        row.Id,
        row.TenantId,
        row.UserId,
        new WalletId(row.WalletId),
        row.Network,
        row.CreativeId,
        row.DeviceRiskHash,
        row.IpRiskHash,
        row.AsnRiskHash,
        nonce,
        TimeSpan.FromTicks(row.RequiredDurationTicks),
        new PolicyVersion(row.PolicyVersion),
        row.IssuedAt,
        row.ExpiresAt);

    private static void Validate(StartDurableAdRewardSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty || request.UserId == Guid.Empty || request.WalletId.Value == Guid.Empty)
            throw new ArgumentException("Tenant, user and wallet IDs are required.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Network);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CreativeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceRiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IpRiskHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AsnRiskHash);
        if (request.RequiredDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request));
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
