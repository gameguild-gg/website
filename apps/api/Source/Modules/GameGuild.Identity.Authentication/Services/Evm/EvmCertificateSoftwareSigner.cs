using System.Numerics;
using System.Security.Cryptography;
using Nethereum.Model;
using Nethereum.Signer;

namespace GameGuild.Identity.Authentication;

/// <summary>Opt-in EIP-1559 software signing with explicit public key references and captured-intent binding.</summary>
/// <remarks>Clears the transferred input buffer; third-party cryptography may retain temporary managed copies.</remarks>
public sealed class EvmCertificateSoftwareSigner(IEvmCertificatePrivateKeySource source) : IEvmCertificateTransactionSigner
{
    private static readonly BigInteger CurveOrder = BigInteger.Parse(
        "115792089237316195423570985008687907852837564279074904382605163141518161494337",
        System.Globalization.CultureInfo.InvariantCulture);
    private readonly IEvmCertificatePrivateKeySource keys = source ?? throw new ArgumentNullException(nameof(source));

    public Task<string> GetAddressAsync(string reference, CancellationToken cancellationToken) =>
        WithKeyAsync(reference, key => key.GetPublicAddress().ToLowerInvariant(), cancellationToken);

    public Task<string> SignAsync(string reference, EvmCertificatePreparedTransaction intent, BigInteger maximumFeePerGasWei,
        BigInteger priorityFeePerGasWei, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(intent.Network);
        ArgumentNullException.ThrowIfNull(intent.Budget);
        EvmCertificatePublicationValues.Address(intent.SignerAddress, nameof(intent.SignerAddress));
        var digest = EvmCertificateProtocol.NormalizeHashArgument(intent.CertificateHash, nameof(intent.CertificateHash));
        if (!Enum.IsDefined(intent.Kind) || !string.Equals(digest, intent.CertificateHash, StringComparison.Ordinal) ||
            maximumFeePerGasWei <= 0 || maximumFeePerGasWei > intent.Budget.MaximumFeePerGasWei ||
            priorityFeePerGasWei < 0 || priorityFeePerGasWei > maximumFeePerGasWei)
        {
            throw new EvmCertificateJournalException(EvmCertificateJournalFailure.InvalidTransaction);
        }
        return WithKeyAsync(reference, key =>
        {
            if (!string.Equals(key.GetPublicAddress(), intent.SignerAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw Unavailable();
            }
            var transaction = new Transaction1559(intent.Network.ChainId, intent.Nonce, priorityFeePerGasWei,
                maximumFeePerGasWei, intent.Budget.GasLimit, intent.Network.ContractAddress, 0, intent.Input, []);
            var raw = "0x" + new Transaction1559Signer().SignTransaction(key, transaction);
            return EvmCertificateSignedEnvelope.Validate(raw, intent).RawTransaction;
        }, cancellationToken);
    }

    private async Task<T> WithKeyAsync<T>(string reference, Func<EthECKey, T> action, CancellationToken cancellationToken)
    {
        EvmCertificatePublicationValues.Key(reference, nameof(reference));
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = null;
        try
        {
            bytes = await keys.GetPrivateKeyAsync(reference, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (bytes is null || bytes.Length != 32 || bytes.All(b => b == 0) ||
                new BigInteger(bytes, isUnsigned: true, isBigEndian: true) >= CurveOrder)
            {
                throw Unavailable();
            }
            var result = action(new EthECKey(bytes, true));
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Preserve cancellation, but never retain a provider exception that may include key material.
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            throw Unavailable();
        }
        finally
        {
            if (bytes is not null)
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    private static EvmCertificateJournalException Unavailable() => new(EvmCertificateJournalFailure.SignerUnavailable);
}
