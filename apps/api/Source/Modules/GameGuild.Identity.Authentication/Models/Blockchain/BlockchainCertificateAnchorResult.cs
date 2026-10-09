namespace GameGuild.Identity.Authentication;

/// <summary>
///     Concrete, instantiable anchor operation result for the certificate anchoring providers.
/// </summary>
public sealed class BlockchainCertificateAnchorResult : BlockchainAnchorResult
{
    /// <summary>
    ///     Creates a successful anchor result.
    /// </summary>
    public static BlockchainCertificateAnchorResult Success(
        string transactionHash,
        string network,
        string dataHash,
        long? blockNumber,
        DateTime timestamp) => new()
    {
        IsSuccess = true,
        TransactionHash = transactionHash,
        Network = network,
        DataHash = dataHash,
        BlockNumber = blockNumber,
        Timestamp = timestamp
    };

    /// <summary>
    ///     Creates a failed anchor result.
    /// </summary>
    public static BlockchainCertificateAnchorResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
