using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace GameGuild.Assets.Deduplication;

/// <summary>
/// Service for content-based deduplication using hashing.
/// </summary>
public interface IDeduplicationService
{
    /// <summary>
    /// Computes the SHA-256 hash of content for deduplication.
    /// </summary>
    Task<string> ComputeContentHashAsync(Stream content, CancellationToken ct = default);

    /// <summary>
    /// Computes a perceptual hash for images (for near-duplicate detection).
    /// </summary>
    Task<string?> ComputePerceptualHashAsync(Stream content, string mimeType, CancellationToken ct = default);

    /// <summary>
    /// Checks if content with this hash already exists.
    /// </summary>
    Task<Guid?> FindExistingContentAsync(string contentHash, CancellationToken ct = default);
}

/// <summary>
/// Configuration for deduplication.
/// </summary>
public class DeduplicationOptions
{
    public const string SectionName = "Assets:Deduplication";

    /// <summary>
    /// Whether content deduplication is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether perceptual hashing is enabled for images.
    /// </summary>
    public bool EnablePerceptualHashing { get; set; } = true;

    /// <summary>
    /// Hamming distance threshold for perceptual hash matching.
    /// </summary>
    public int PerceptualHashThreshold { get; set; } = 5;
}

/// <summary>
/// Implementation of content deduplication.
/// </summary>
public class DeduplicationService : IDeduplicationService
{
    private readonly IAssetContentRepository _contentRepository;
    private readonly DeduplicationOptions _options;
    private readonly ILogger<DeduplicationService> _logger;

    /// <summary>
    /// Size for perceptual hash computation (8x8 = 64 bits)
    /// </summary>
    private const int HashSize = 8;

    public DeduplicationService(
        IAssetContentRepository contentRepository,
        Microsoft.Extensions.Options.IOptions<DeduplicationOptions> options,
        ILogger<DeduplicationService> logger)
    {
        _contentRepository = contentRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> ComputeContentHashAsync(Stream content, CancellationToken ct = default)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, ct).ConfigureAwait(false);
        content.Position = 0; // Reset stream position for subsequent reads
        
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<string?> ComputePerceptualHashAsync(Stream content, string mimeType, CancellationToken ct = default)
    {
        if (!_options.EnablePerceptualHashing)
        {
            return null;
        }

        // Only compute perceptual hash for images
        if (!mimeType.StartsWith("image/"))
        {
            return null;
        }

        try
        {
            // Reset stream position if needed
            if (content.CanSeek && content.Position != 0)
            {
                content.Position = 0;
            }

            // Decode raster data, resize with the Catmull-Rom bicubic filter and
            // retain the 8x8 Rec.709 average-hash contract.
            var grayscale = await RasterPerceptualImageReader.ReadAsync(content, ct).ConfigureAwait(false);

            // Calculate average pixel value
            double totalBrightness = 0;
            for (int y = 0; y < HashSize; y++)
            {
                for (int x = 0; x < HashSize; x++)
                {
                    totalBrightness += grayscale[(y * HashSize) + x];
                }
            }
            var avgBrightness = totalBrightness / (HashSize * HashSize);

            // Build hash: 1 if pixel >= average, 0 otherwise
            ulong hash = 0;
            for (int y = 0; y < HashSize; y++)
            {
                for (int x = 0; x < HashSize; x++)
                {
                    if (grayscale[(y * HashSize) + x] >= avgBrightness)
                    {
                        var bitPosition = (y * HashSize) + x;
                        hash |= 1UL << bitPosition;
                    }
                }
            }

            // Reset stream position for subsequent reads
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            // Return as 16-char hex string (64 bits)
            return hash.ToString("x16");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to compute perceptual hash for image");
            
            // Reset stream position on error
            if (content.CanSeek)
            {
                content.Position = 0;
            }
            
            return null;
        }
    }

    /// <summary>
    /// Computes Hamming distance between two perceptual hashes.
    /// Lower distance = more similar images.
    /// </summary>
    public static int ComputeHammingDistance(string hash1, string hash2)
    {
        if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
            return int.MaxValue;

        if (!ulong.TryParse(hash1, System.Globalization.NumberStyles.HexNumber, null, out var h1) ||
            !ulong.TryParse(hash2, System.Globalization.NumberStyles.HexNumber, null, out var h2))
            return int.MaxValue;

        var xor = h1 ^ h2;
        return System.Numerics.BitOperations.PopCount(xor);
    }

    /// <summary>
    /// Checks if two perceptual hashes are similar based on configured threshold.
    /// </summary>
    public bool AreSimilar(string? hash1, string? hash2)
    {
        if (string.IsNullOrEmpty(hash1) || string.IsNullOrEmpty(hash2))
            return false;

        var distance = ComputeHammingDistance(hash1, hash2);
        return distance <= _options.PerceptualHashThreshold;
    }

    public async Task<Guid?> FindExistingContentAsync(string contentHash, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return null;
        }

        var existing = await _contentRepository.GetByContentHashAsync(contentHash, ct).ConfigureAwait(false);
        return existing?.Id;
    }
}
