using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Marketplace.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Marketplace;

public sealed record DurableMarketplacePolicySnapshot(
    Guid TenantId,
    ProductCurrencyPolicyVersion Policy,
    Guid PlatformFeeWalletId,
    TimeSpan RefundHold,
    string PayloadHash,
    string KeyId,
    string Signature);

public interface IDurableMarketplacePolicyReader
{
    ValueTask<DurableMarketplacePolicySnapshot> GetEffectiveAsync(
        Guid tenantId,
        Guid productId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);

    ValueTask<DurableMarketplacePolicySnapshot> GetVersionAsync(
        Guid tenantId,
        Guid productId,
        long version,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlDurableMarketplacePolicyReader : IDurableMarketplacePolicyReader
{
    private readonly DbContext _db;
    private readonly ICapabilityPolicySignatureVerifier _signatureVerifier;

    public PostgreSqlDurableMarketplacePolicyReader(
        IApplicationDbContext context,
        ICapabilityPolicySignatureVerifier signatureVerifier)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Durable Marketplace policies require the application's relational DbContext.");
        _signatureVerifier = signatureVerifier;
    }

    public async ValueTask<DurableMarketplacePolicySnapshot> GetEffectiveAsync(
        Guid tenantId,
        Guid productId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty)
            throw new ArgumentException("Tenant and product IDs are required.");
        var row = await _db.Set<MarketplaceCurrencyPolicyVersionRow>()
            .AsNoTracking()
            .Where(policy => policy.TenantId == tenantId && policy.ProductId == productId &&
                             policy.EffectiveAt <= at && at < policy.ExpiresAt)
            .OrderByDescending(policy => policy.Version)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new MarketplaceCurrencyPolicyException(
                "No effective signed Marketplace policy is available.");
        return await ValidateAndMapAsync(row, cancellationToken);
    }

    public async ValueTask<DurableMarketplacePolicySnapshot> GetVersionAsync(
        Guid tenantId,
        Guid productId,
        long version,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty || version <= 0)
            throw new ArgumentException("Tenant, product and positive version are required.");
        var row = await _db.Set<MarketplaceCurrencyPolicyVersionRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(policy => policy.TenantId == tenantId &&
                                            policy.ProductId == productId &&
                                            policy.Version == version,
                cancellationToken)
            ?? throw new MarketplaceCurrencyPolicyException(
                "The signed Marketplace policy version was not found.");
        return await ValidateAndMapAsync(row, cancellationToken);
    }

    private async ValueTask<DurableMarketplacePolicySnapshot> ValidateAndMapAsync(
        MarketplaceCurrencyPolicyVersionRow row,
        CancellationToken cancellationToken)
    {
        var payloadHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(row.CanonicalPayload)));
        var validSignature = payloadHash == row.PayloadHash &&
                             await _signatureVerifier.VerifyAsync(
                                 row.CanonicalPayload, row.KeyId, row.Signature, cancellationToken);
        if (!validSignature || row.ProposedBy == row.ApprovedBy || row.PlatformFeeWalletId == Guid.Empty)
            throw new MarketplaceCurrencyPolicyException(
                "The Marketplace policy is unsigned, unapproved, or incomplete.");
        var policy = ProductCurrencyPolicyVersion.Create(
            row.ProductId,
            row.SellerId,
            row.Version,
            row.Mode,
            row.HardPriceUnits,
            row.SoftPriceUnits,
            row.PlatformFeePpm,
            row.EffectiveAt);
        return new DurableMarketplacePolicySnapshot(
            row.TenantId, policy, row.PlatformFeeWalletId, TimeSpan.FromTicks(row.RefundHoldTicks),
            row.PayloadHash, row.KeyId, row.Signature);
    }
}
