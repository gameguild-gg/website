namespace GameGuild.Identity.Authentication;

/// <summary>Fixed write-side methods; orchestration must persist the signed attempt before broadcast.</summary>
public interface IEvmCertificatePublicationRpc
{
    Task<ulong> GetPendingNonceAsync(EvmCertificatePublicationNetwork network, string signerAddress, CancellationToken cancellationToken);
    Task<string> SendRawTransactionAsync(EvmCertificatePreparedTransaction intent, string rawTransaction, CancellationToken cancellationToken);
}
