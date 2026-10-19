using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.API.Core.Integration;

/// <summary>Explicit host selection. Endpoints and credentials are never part of a durable publication identity.</summary>
internal sealed class CertificateEvmHostOptions
{
    public static string SectionName => "BlockchainCertificates:Evm";
    public bool Enabled { get; set; }
    public string Profile { get; set; } = string.Empty;
    public string CanonicalIssuer { get; set; } = string.Empty;
    public EvmCertificateNetworkOptions Network { get; set; } = new();
    public string SignerReference { get; set; } = string.Empty;
    public string PrivateKeyEnvironmentVariable { get; set; } = string.Empty;
    public ulong GasLimit { get; set; }
    public string MaximumFeePerGasWei { get; set; } = string.Empty;
    public string MaximumTransactionFeeWei { get; set; } = string.Empty;
    public string InitialFeePerGasWei { get; set; } = string.Empty;
    public string InitialPriorityFeePerGasWei { get; set; } = string.Empty;
    public TimeSpan CheckDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan ReplacementDelay { get; set; } = TimeSpan.FromMinutes(2);
    public int MaximumAttempts { get; set; } = 3;
}
