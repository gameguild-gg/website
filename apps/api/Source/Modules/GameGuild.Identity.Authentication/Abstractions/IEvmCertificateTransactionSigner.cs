using System.Numerics;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicit signing boundary. Hardware/remote signers can implement it without exporting private keys.</summary>
public interface IEvmCertificateTransactionSigner
{
    Task<string> GetAddressAsync(string reference, CancellationToken cancellationToken);
    Task<string> SignAsync(string reference, EvmCertificatePreparedTransaction intent, BigInteger maximumFeePerGasWei,
        BigInteger priorityFeePerGasWei, CancellationToken cancellationToken);
}

/// <summary>Software signer key source. Each call transfers a fresh caller-owned buffer that the signer clears.</summary>
/// <remarks>Do not return shared/cached key arrays. No key value belongs in a journal, response, log or exception.</remarks>
public interface IEvmCertificatePrivateKeySource
{
    ValueTask<byte[]> GetPrivateKeyAsync(string reference, CancellationToken cancellationToken);
}
