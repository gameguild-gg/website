namespace GameGuild.Identity.Authentication;

/// <summary>Explicit allowlist of public signer references to process environment variable names.</summary>
/// <remarks>Optional software/testnet adapter. It is not registered or activated by default.</remarks>
public sealed class EvmCertificateEnvironmentKeySource : IEvmCertificatePrivateKeySource
{
    private readonly IReadOnlyDictionary<string, string> references;

    public EvmCertificateEnvironmentKeySource(IReadOnlyDictionary<string, string> allowedReferences)
    {
        ArgumentNullException.ThrowIfNull(allowedReferences);
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in allowedReferences)
        {
            EvmCertificatePublicationValues.Key(entry.Key, nameof(allowedReferences));
            var name = entry.Value;
            if (string.IsNullOrEmpty(name) || name.Length > 256 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            {
                throw new ArgumentException("A bounded environment variable name is required.", nameof(allowedReferences));
            }
            copy.Add(entry.Key, name);
        }
        references = copy;
    }

    public ValueTask<byte[]> GetPrivateKeyAsync(string reference, CancellationToken cancellationToken)
    {
        EvmCertificatePublicationValues.Key(reference, nameof(reference));
        cancellationToken.ThrowIfCancellationRequested();
        if (!references.TryGetValue(reference, out var variable))
        {
            throw Unavailable();
        }
        var value = Environment.GetEnvironmentVariable(variable);
        if (value is null)
        {
            throw Unavailable();
        }
        var hex = value.AsSpan();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            hex = hex[2..];
        }
        if (hex.Length != 64)
        {
            throw Unavailable();
        }
        try
        {
            return ValueTask.FromResult(Convert.FromHexString(hex));
        }
        catch (FormatException)
        {
            throw Unavailable();
        }
    }

    private static EvmCertificateJournalException Unavailable() => new(EvmCertificateJournalFailure.SignerUnavailable);
}
