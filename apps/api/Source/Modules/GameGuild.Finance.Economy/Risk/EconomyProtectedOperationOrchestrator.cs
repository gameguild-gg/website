using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Risk;

public sealed class EconomyProtectedOperationOrchestrator(
    IActorContextAccessor actorContextAccessor,
    IEconomyTrustedProtectedOperationAuthorizer trustedAuthorizer) : IEconomyProtectedOperationOrchestrator
{
    public async Task<TResult> ExecuteAsync<TResult>(
        EconomyProtectedOperationIntent intent,
        Func<EconomyProtectedOperationAuthorization, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "A protected Economy operation requires an authenticated tenant actor.");
        return await trustedAuthorizer.ExecuteAsync(
            tenantId,
            actorId,
            intent,
            operation,
            cancellationToken).ConfigureAwait(false);
    }

    internal static string Fingerprint(
        Guid tenantId,
        Guid actorId,
        EconomyProtectedOperationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var fields = new List<string>
        {
            tenantId.ToString("N"),
            actorId.ToString("N"),
            ((int)intent.Capability).ToString(CultureInfo.InvariantCulture),
            ((int)intent.TemplateKind).ToString(CultureInfo.InvariantCulture),
            intent.SourceWalletId.Value.ToString("N"),
            intent.DestinationWalletId.Value.ToString("N"),
            ((int)intent.Amount.Currency).ToString(CultureInfo.InvariantCulture),
            intent.Amount.Units.ToString(CultureInfo.InvariantCulture),
            string.Join(',', intent.CurrencyLegs.Select(leg =>
                $"{(int)leg.Currency}:{leg.Units.ToString(CultureInfo.InvariantCulture)}")),
            string.Join(',', intent.SourceRoots.Select(root => root.Value.ToString("N"))),
            intent.ProviderReferenceHash,
            intent.DestinationHash,
            intent.IdempotencyKey.Value,
            intent.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };
        if (intent.ProtectedSubjectId is { } protectedSubjectId)
        {
            fields.Insert(2, "protected-subject-v1");
            fields.Insert(3, protectedSubjectId.ToString("N"));
        }
        var canonical = string.Concat(fields.Select(value =>
            $"{Encoding.UTF8.GetByteCount(value)}:{value}"));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    internal static string HashRoot(SourceStampId root) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(root.Value.ToString("N"))));

    internal static void Validate(EconomyProtectedOperationIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!Enum.IsDefined(intent.Capability)) throw new ArgumentOutOfRangeException(nameof(intent));
        if (!Enum.IsDefined(intent.TemplateKind)) throw new ArgumentOutOfRangeException(nameof(intent));
        if (intent.SourceWalletId.Value == Guid.Empty || intent.DestinationWalletId.Value == Guid.Empty)
            throw new ArgumentException("Protected operation wallets are required.", nameof(intent));
        if (intent.ProtectedSubjectId == Guid.Empty)
            throw new ArgumentException("Protected operation subject IDs cannot be empty.", nameof(intent));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(intent.Amount.Units);
        ArgumentNullException.ThrowIfNull(intent.CurrencyLegs);
        if (intent.CurrencyLegs.Count == 0)
            throw new ArgumentException("Protected operation currency legs are required.", nameof(intent));
        ArgumentNullException.ThrowIfNull(intent.SourceRoots);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.ProviderReferenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(intent.DestinationHash);
    }

}
