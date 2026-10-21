using System.Globalization;
using System.Numerics;
using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>Immutable public identity. RPC endpoints and key material never enter the journal.</summary>
public sealed class EvmCertificatePublicationNetwork
{
    internal EvmCertificatePublicationNetwork(string profile, long chainId, string contractAddress, string issuerId,
        string runtimeCodeSha256, EvmCertificateFinalityMode finalityMode, int confirmations)
    {
        Profile = profile;
        ChainId = chainId;
        ContractAddress = contractAddress;
        IssuerId = issuerId;
        RuntimeCodeSha256 = runtimeCodeSha256;
        FinalityMode = finalityMode;
        MinimumConfirmations = confirmations;
    }

    public string Profile { get; }
    public long ChainId { get; }
    public string ContractAddress { get; }
    public string IssuerId { get; }
    public string RuntimeCodeSha256 { get; }
    public EvmCertificateFinalityMode FinalityMode { get; }
    public int MinimumConfirmations { get; }

    public static EvmCertificatePublicationNetwork Capture(string profile, EvmCertificateNetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        EvmCertificatePublicationValues.Key(profile, nameof(profile));
        var errors = options.Validate();
        if (errors.Count != 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        }
        return new(profile, options.ChainId, options.ContractAddress.ToLowerInvariant(), options.IssuerId.ToLowerInvariant(),
            options.ExpectedRuntimeCodeSha256.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant(),
            options.FinalityMode, options.MinimumConfirmations);
    }
}

/// <summary>Explicit upper bound in integer wei, captured before a nonce is allocated.</summary>
public sealed class EvmCertificateFeeBudget
{
    private EvmCertificateFeeBudget(ulong gasLimit, BigInteger maximumFeePerGasWei, BigInteger maximumTransactionFeeWei)
    {
        GasLimit = gasLimit;
        MaximumFeePerGasWei = maximumFeePerGasWei;
        MaximumTransactionFeeWei = maximumTransactionFeeWei;
    }

    public ulong GasLimit { get; }
    public BigInteger MaximumFeePerGasWei { get; }
    public BigInteger MaximumTransactionFeeWei { get; }

    public static EvmCertificateFeeBudget Create(ulong gasLimit, BigInteger maximumFeePerGasWei, BigInteger maximumTransactionFeeWei)
    {
        if (gasLimit < 21000 || maximumFeePerGasWei <= 0 || maximumTransactionFeeWei <= 0 ||
            maximumFeePerGasWei >= (BigInteger.One << 256) || maximumTransactionFeeWei >= (BigInteger.One << 256) ||
            gasLimit * maximumFeePerGasWei > maximumTransactionFeeWei)
        {
            throw new ArgumentException("The gas and integer-wei limits do not form a funded transaction budget.");
        }
        return new(gasLimit, maximumFeePerGasWei, maximumTransactionFeeWei);
    }
}

/// <summary>Only the trusted host producer creates intents; this type is not a public HTTP command.</summary>
public sealed record EvmCertificateJournalRequest(EvmCertificatePublicationNetwork Network, Guid DocumentId,
    Guid OwnerUserId, Guid? TenantId, string Producer, string CertificateHash, EvmCertificateOperationKind Kind);

public sealed record EvmCertificatePreparedTransaction(EvmCertificatePublicationNetwork Network,
    EvmCertificateOperationKind Kind, string CertificateHash, string SignerAddress, ulong Nonce, EvmCertificateFeeBudget Budget)
{
    public string Input => Kind switch
    {
        EvmCertificateOperationKind.Anchor => "0xeecdf927" + CertificateHash[2..],
        EvmCertificateOperationKind.Revoke => "0xb75c7dc6" + CertificateHash[2..],
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };
}

public enum EvmCertificatePublicationStatus
{
    Queued, NonceReserved, Prepared, Submitted, Confirmed, Revoked, Reorged, Rejected, NeedsAttention
}

public enum EvmCertificateJournalFailure
{
    Conflict, LeaseLost, InvalidTransaction, InvalidState, InvalidEvidence, RpcUnavailable, SignerUnavailable, NonceExhausted
}

/// <summary>Safe journal failure without raw transactions, provider responses or credential-bearing values.</summary>
public sealed class EvmCertificateJournalException(EvmCertificateJournalFailure failure)
    : Exception($"EVM certificate journal rejected ({failure}).")
{
    public EvmCertificateJournalFailure Failure { get; } = failure;
}

/// <summary>A worker-only fencing capability. Never include its token in a user-facing response.</summary>
public sealed record EvmCertificateJournalLease(Guid OperationId, Guid Token);

public sealed record EvmCertificateJournalSnapshot(Guid Id, EvmCertificatePublicationNetwork Network, Guid DocumentId,
    Guid OwnerUserId, Guid? TenantId, string Producer, string CertificateHash, EvmCertificateOperationKind Kind,
    EvmCertificatePublicationStatus Status, ulong? Nonce, string? SignerReference, string? SignerAddress,
    EvmCertificateFeeBudget? Budget, string? TransactionHash, DateTimeOffset CreatedAt, DateTimeOffset NextAttemptAt,
    long Revision, EvmCertificateJournalFailure? LastFailure)
{
    public EvmCertificatePreparedTransaction PreparedTransaction => Nonce.HasValue && SignerAddress is not null && Budget is not null
        ? new(Network, Kind, CertificateHash, SignerAddress, Nonce.Value, Budget)
        : throw new EvmCertificateJournalException(EvmCertificateJournalFailure.InvalidState);
}

public sealed record EvmCertificateJournalAttempt(Guid Id, Guid OperationId, int Sequence, string TransactionHash,
    string RawTransaction, BigInteger MaximumFeePerGasWei, BigInteger PriorityFeePerGasWei, DateTimeOffset PreparedAt,
    DateTimeOffset? BroadcastAt);

public sealed record EvmCertificateJournalEvent(long Revision, string Code, EvmCertificatePublicationStatus Status,
    string? TransactionHash, EvmCertificateProofStatus? ProofStatus, string? BlockHash, ulong? BlockNumber, DateTimeOffset At);

internal static class EvmCertificatePublicationValues
{
    internal static void Key(string value, string argument)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 100 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException("A bounded public configuration key is required.", argument);
        }
    }

    internal static string Address(string value, string argument)
    {
        if (value is null || value.Length != 42 || !value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            !value.Skip(2).All(Uri.IsHexDigit) || !value.Skip(2).Any(c => c != '0'))
        {
            throw new ArgumentException("A nonzero 20-byte address is required.", argument);
        }
        return value.ToLowerInvariant();
    }

    internal static string Wei(BigInteger value) => value.ToString(CultureInfo.InvariantCulture);
    internal static BigInteger Wei(string value) => BigInteger.Parse(value, CultureInfo.InvariantCulture);
}
