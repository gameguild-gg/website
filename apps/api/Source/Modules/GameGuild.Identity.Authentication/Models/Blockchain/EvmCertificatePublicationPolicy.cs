using System.Numerics;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicit bounded worker policy. Network and fee budgets remain captured on each operation.</summary>
public sealed class EvmCertificatePublicationPolicy
{
    private EvmCertificatePublicationPolicy(EvmCertificatePublicationNetwork network, string signerReference,
        EvmCertificateFeeBudget budget, BigInteger initialFee, BigInteger priorityFee, TimeSpan checkDelay,
        TimeSpan leaseDuration, TimeSpan replacementDelay, int maximumAttempts)
    {
        Network = network; SignerReference = signerReference; Budget = budget;
        InitialFeePerGasWei = initialFee; InitialPriorityFeePerGasWei = priorityFee;
        CheckDelay = checkDelay; LeaseDuration = leaseDuration; ReplacementDelay = replacementDelay; MaximumAttempts = maximumAttempts;
    }

    public EvmCertificatePublicationNetwork Network { get; }
    public string SignerReference { get; }
    public EvmCertificateFeeBudget Budget { get; }
    public BigInteger InitialFeePerGasWei { get; }
    public BigInteger InitialPriorityFeePerGasWei { get; }
    public TimeSpan CheckDelay { get; }
    public TimeSpan LeaseDuration { get; }
    public TimeSpan ReplacementDelay { get; }
    public int MaximumAttempts { get; }

    public static EvmCertificatePublicationPolicy Create(EvmCertificatePublicationNetwork network, string signerReference,
        EvmCertificateFeeBudget budget, BigInteger initialFeePerGasWei, BigInteger initialPriorityFeePerGasWei,
        TimeSpan checkDelay, TimeSpan leaseDuration, TimeSpan replacementDelay, int maximumAttempts)
    {
        ArgumentNullException.ThrowIfNull(network); ArgumentNullException.ThrowIfNull(budget);
        EvmCertificatePublicationValues.Key(signerReference, nameof(signerReference));
        if (initialFeePerGasWei <= 0 || initialFeePerGasWei > budget.MaximumFeePerGasWei || initialPriorityFeePerGasWei < 0 ||
            initialPriorityFeePerGasWei > initialFeePerGasWei || checkDelay < TimeSpan.FromSeconds(1) || checkDelay > TimeSpan.FromDays(1) ||
            leaseDuration < TimeSpan.FromSeconds(10) || leaseDuration > TimeSpan.FromDays(1) || replacementDelay < checkDelay ||
            replacementDelay > TimeSpan.FromDays(1) || maximumAttempts is < 1 or > 10)
        {
            throw new ArgumentException("A bounded lease, retry, fee and attempt policy is required.");
        }
        return new(network, signerReference, budget, initialFeePerGasWei, initialPriorityFeePerGasWei,
            checkDelay, leaseDuration, replacementDelay, maximumAttempts);
    }

    internal bool Matches(EvmCertificatePublicationNetwork other) => Network.Profile == other.Profile &&
        Network.ChainId == other.ChainId && Network.ContractAddress == other.ContractAddress && Network.IssuerId == other.IssuerId &&
        Network.RuntimeCodeSha256 == other.RuntimeCodeSha256 && Network.FinalityMode == other.FinalityMode &&
        Network.MinimumConfirmations == other.MinimumConfirmations;
}
