using System.IO.Compression;
using GameGuild.Configuration.PresentationLayer.Authorization;

namespace GameGuild.Identity.Authorization.Caching;

/// <summary>
///     Encodes and decodes compressed L2 (distributed) cache payloads using a versioned envelope.
/// </summary>
/// <remarks>
///     <para>
///         Enveloped values start with a 4-byte magic marker (<c>GGC1</c>: GameGuild Compression,
///         envelope version 1) followed by one algorithm byte and the compressed payload. Values
///         that do not start with the marker are raw JSON written by deployments predating
///         compression (or by <see cref="L2CompressionAlgorithm.None"/>), and are passed through
///         unchanged so mixed-version fleets stay readable during rollout.
///     </para>
///     <para>
///         <see cref="TryUnwrap"/> never throws: an enveloped payload that cannot be decoded
///         (unknown algorithm byte or corrupted stream) returns <c>false</c> and the cache treats
///         the entry as a miss.
///     </para>
/// </remarks>
public static class PermissionCacheL2Payload
{
    /// <summary>Envelope magic marker: ASCII "GGC1" (GameGuild Compression envelope v1).</summary>
    public static ReadOnlySpan<byte> Magic => "GGC1"u8;

    private const byte GZipAlgorithmId = 1;

    private const byte BrotliAlgorithmId = 2;

    /// <summary>
    ///     Compresses <paramref name="payload"/> and wraps it in the versioned envelope.
    /// </summary>
    /// <param name="payload">Serialized (raw JSON) payload bytes.</param>
    /// <param name="algorithm">Compression algorithm to apply.</param>
    /// <returns>The enveloped payload to store in L2.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="algorithm"/> is <see cref="L2CompressionAlgorithm.None"/> or undefined.
    /// </exception>
    public static byte[] Wrap(ReadOnlySpan<byte> payload, L2CompressionAlgorithm algorithm)
    {
        switch (algorithm)
        {
            case L2CompressionAlgorithm.GZip:
                return Envelope(payload, GZipAlgorithmId, static (input, output) =>
                {
                    using var compressor = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true);
                    input.CopyTo(compressor);
                });
            case L2CompressionAlgorithm.Brotli:
                return Envelope(payload, BrotliAlgorithmId, static (input, output) =>
                {
                    using var compressor = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true);
                    input.CopyTo(compressor);
                });
            case L2CompressionAlgorithm.None:
                throw new ArgumentOutOfRangeException(nameof(algorithm), "Compression cannot wrap a payload with algorithm None.");
            default:
                throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown compression algorithm.");
        }
    }

    /// <summary>
    ///     Determines whether <paramref name="stored"/> carries the compression envelope.
    /// </summary>
    public static bool IsEnveloped(ReadOnlySpan<byte> stored) =>
        stored.Length > Magic.Length && stored[..Magic.Length].SequenceEqual(Magic);

    /// <summary>
    ///     Decodes an L2 payload, transparently handling both enveloped and legacy raw values.
    /// </summary>
    /// <param name="stored">Bytes as read from the distributed cache.</param>
    /// <param name="plain">
    ///     The plain payload bytes: decompressed when the value was enveloped, otherwise the input
    ///     bytes passed through unchanged.
    /// </param>
    /// <returns>
    ///     <c>true</c> when the payload is usable; <c>false</c> when the value is enveloped but
    ///     cannot be decoded (unknown algorithm or corrupted stream), in which case the caller
    ///     should treat the entry as a miss.
    /// </returns>
    public static bool TryUnwrap(ReadOnlySpan<byte> stored, out byte[] plain)
    {
        if (!IsEnveloped(stored))
        {
            plain = stored.ToArray();
            return true;
        }

        plain = Array.Empty<byte>();
        if (stored.Length <= Magic.Length + 1)
        {
            return false;
        }

        var algorithmId = stored[Magic.Length];
        var compressed = stored[(Magic.Length + 1)..];
        try
        {
            plain = algorithmId switch
            {
                GZipAlgorithmId => Decompress(compressed.ToArray(), static output => new GZipStream(output, CompressionMode.Decompress)),
                BrotliAlgorithmId => Decompress(compressed.ToArray(), static output => new BrotliStream(output, CompressionMode.Decompress)),
                _ => Array.Empty<byte>()
            };
        }
        catch (Exception)
        {
            // Corrupted or truncated streams are cache misses, not request failures.
            return false;
        }

        return plain.Length > 0;
    }

    private static byte[] Envelope(ReadOnlySpan<byte> payload, byte algorithmId, Action<Stream, Stream> compress)
    {
        using var compressed = new MemoryStream();
        compress(new MemoryStream(payload.ToArray()), compressed);
        var result = new byte[Magic.Length + 1 + checked((int)compressed.Length)];
        Magic.CopyTo(result);
        result[Magic.Length] = algorithmId;
        compressed.ToArray().CopyTo(result, Magic.Length + 1);
        return result;
    }

    private static byte[] Decompress(byte[] compressed, Func<Stream, Stream> createDecompressor)
    {
        using var input = new MemoryStream(compressed);
        using var decompressor = createDecompressor(input);
        using var output = new MemoryStream();
        decompressor.CopyTo(output);
        return output.ToArray();
    }
}
