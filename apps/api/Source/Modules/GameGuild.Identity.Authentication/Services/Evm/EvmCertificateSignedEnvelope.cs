using System.Numerics;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace GameGuild.Identity.Authentication;

/// <summary>Validates a bounded, canonical EIP-1559 signature against the exact reserved intent.</summary>
public sealed class EvmCertificateSignedEnvelope
{
    internal EvmCertificateSignedEnvelope(string raw, string hash, BigInteger fee, BigInteger priority)
    {
        RawTransaction = raw;
        TransactionHash = hash;
        MaximumFeePerGasWei = fee;
        PriorityFeePerGasWei = priority;
    }

    public string RawTransaction { get; }
    public string TransactionHash { get; }
    public BigInteger MaximumFeePerGasWei { get; }
    public BigInteger PriorityFeePerGasWei { get; }

    public static EvmCertificateSignedEnvelope Validate(string raw, EvmCertificatePreparedTransaction intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        try
        {
            if (raw is null || raw.Length is < 4 or > 8194 || !raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                throw Rejected();
            }
            var bytes = Convert.FromHexString(raw[2..]);
            RequireFlatCanonical1559(bytes);
            if (TransactionFactory.CreateTransaction(bytes) is not Transaction1559 tx ||
                tx.ChainId != intent.Network.ChainId || tx.Nonce != intent.Nonce || tx.GasLimit != intent.Budget.GasLimit ||
                tx.Amount != BigInteger.Zero || tx.MaxFeePerGas is null || tx.MaxPriorityFeePerGas is null ||
                tx.MaxFeePerGas <= 0 || tx.MaxFeePerGas > intent.Budget.MaximumFeePerGasWei ||
                tx.MaxPriorityFeePerGas < 0 || tx.MaxPriorityFeePerGas > tx.MaxFeePerGas ||
                !string.Equals(tx.ReceiverAddress, intent.Network.ContractAddress, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Prefix(tx.Data), intent.Input, StringComparison.OrdinalIgnoreCase) ||
                tx.AccessList is { Count: > 0 } || !tx.VerifyTransaction() ||
                !string.Equals(tx.GetSenderAddress(), intent.SignerAddress, StringComparison.OrdinalIgnoreCase))
            {
                throw Rejected();
            }
            var s = new BigInteger(tx.Signature.S, isUnsigned: true, isBigEndian: true);
            var halfOrder = BigInteger.Parse("115792089237316195423570985008687907852837564279074904382605163141518161494337",
                System.Globalization.CultureInfo.InvariantCulture) / 2;
            if (s <= 0 || s > halfOrder)
            {
                throw Rejected();
            }
            // Clear the decoder's cached bytes: comparison must use canonical re-encoding.
            tx.SetSignature(tx.Signature);
            if (!bytes.AsSpan().SequenceEqual(tx.GetRLPEncoded()))
            {
                throw Rejected();
            }
            var hash = Convert.ToHexString(Sha3Keccack.Current.CalculateHash(bytes)).ToLowerInvariant();
            return new("0x" + Convert.ToHexString(bytes).ToLowerInvariant(), "0x" + hash,
                tx.MaxFeePerGas.Value, tx.MaxPriorityFeePerGas.Value);
        }
        catch (Exception ex) when (ex is not EvmCertificateJournalException)
        {
            // Cryptography/RLP exceptions can echo supplied bytes. Retain only the safe category.
            throw Rejected();
        }
    }

    private static string Prefix(string value) => value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value : "0x" + value;
    private static EvmCertificateJournalException Rejected() => new(EvmCertificateJournalFailure.InvalidTransaction);

    private static void RequireFlatCanonical1559(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2 || bytes[0] != 2)
        {
            throw Rejected();
        }
        var cursor = 1;
        var root = Header(bytes, ref cursor);
        if (!root.List || cursor + root.Length != bytes.Length)
        {
            throw Rejected();
        }
        var end = bytes.Length;
        var count = 0;
        while (cursor < end)
        {
            var element = Header(bytes, ref cursor);
            if (cursor + element.Length > end || count >= 12)
            {
                throw Rejected();
            }
            if (count == 8)
            {
                if (!element.List || element.Length != 0)
                {
                    throw Rejected();
                }
            }
            else if (element.List || element.Length > (count == 7 ? 36 : 32) ||
                     (count == 5 && element.Length != 20) || (count == 7 && element.Length != 36) ||
                     (count == 6 && element.Length != 0) ||
                     (count == 9 && (element.Length > 1 || (element.Length == 1 && bytes[cursor] != 1))) ||
                     (count is not 5 and not 7 && element.Length > 0 && bytes[cursor] == 0))
            {
                throw Rejected();
            }
            cursor += element.Length;
            count++;
        }
        if (count != 12)
        {
            throw Rejected();
        }
    }

    private static (bool List, int Length) Header(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        if (cursor >= bytes.Length)
        {
            throw Rejected();
        }
        var tag = bytes[cursor++];
        if (tag < 128)
        {
            cursor--;
            return (false, 1);
        }
        var list = tag >= 192;
        var shortBase = list ? 192 : 128;
        var longBase = list ? 247 : 183;
        var length = tag - shortBase;
        if (tag > longBase)
        {
            var width = tag - longBase;
            if (width > 4 || cursor + width > bytes.Length || bytes[cursor] == 0)
            {
                throw Rejected();
            }
            length = 0;
            for (var i = 0; i < width; i++)
            {
                length = checked(length * 256 + bytes[cursor++]);
            }
            if (length <= 55)
            {
                throw Rejected();
            }
        }
        if (length > bytes.Length - cursor || (!list && length == 1 && bytes[cursor] < 128))
        {
            throw Rejected();
        }
        return (list, length);
    }
}
