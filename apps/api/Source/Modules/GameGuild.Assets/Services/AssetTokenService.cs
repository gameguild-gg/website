using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets;

/// <summary>
/// Options for asset token configuration.
/// </summary>
public class AssetTokenOptions
{
    public const string SectionName = "Assets:Token";

    /// <summary>
    /// Secret key for HMAC signing (base64 encoded).
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Default token validity in hours.
    /// </summary>
    public int DefaultExpiryHours { get; set; } = 24;

    /// <summary>
    /// Time window size in hours (for rotation).
    /// </summary>
    public int TimeWindowHours { get; set; } = 8;
}

/// <summary>
/// Implementation of asset token generation and validation.
/// Uses HMAC-SHA256 with movable time windows for anti-stampede.
/// </summary>
public class AssetTokenService : IAssetTokenService
{
    private readonly byte[] _secretKey;
    private readonly int _defaultExpiryHours;
    private readonly int _timeWindowHours;
    
    /// <summary>
    /// Cache for validated tokens to avoid O(n) signature verification on repeated requests.
    /// Key: token hash, Value: (payload, expiryTimestamp)
    /// </summary>
    private readonly ConcurrentDictionary<string, (AssetTokenPayload Payload, long ExpiryTimestamp)> _tokenCache = new();
    
    /// <summary>
    /// Maximum cache entries to prevent memory exhaustion.
    /// </summary>
    private const int MaxCacheEntries = 10000;

    private static readonly byte[] DevelopmentFallbackSecretKey = RandomNumberGenerator.GetBytes(32);

    public AssetTokenService(IOptions<AssetTokenOptions> options)
    {
        var opts = options.Value;
        
        // Generate a key if not provided (development only)
        if (string.IsNullOrEmpty(opts.SecretKey))
        {
            _secretKey = DevelopmentFallbackSecretKey;
        }
        else
        {
            _secretKey = Convert.FromBase64String(opts.SecretKey);
        }
        
        _defaultExpiryHours = opts.DefaultExpiryHours > 0 ? opts.DefaultExpiryHours : 24;
        _timeWindowHours = opts.TimeWindowHours > 0 ? opts.TimeWindowHours : 8;
    }

    /// <summary>
    /// Generates a signed access token for an asset.
    /// </summary>
    public string GenerateToken(
        Guid assetReferenceId,
        Guid tenantId,
        AssetAccessPolicy accessPolicy,
        TransformationSpec? transformation = null,
        TimeSpan? customExpiry = null)
    {
        var timeWindow = GetCurrentTimeWindow();
        var expiry = DateTimeOffset.UtcNow.Add(customExpiry ?? TimeSpan.FromHours(_defaultExpiryHours));
        var expiryTimestamp = expiry.ToUnixTimeSeconds();
        var transformSpec = transformation?.ToCanonicalString() ?? string.Empty;

        var payload = BuildPayload(assetReferenceId, timeWindow, expiryTimestamp, accessPolicy, transformSpec, tenantId);
        var signature = ComputeSignature(payload);

        // Encode: timeWindow (4 bytes) + expiry (4 bytes) + signature (16 bytes) = 24 bytes base64
        var tokenBytes = new byte[24];
        BitConverter.GetBytes(timeWindow).CopyTo(tokenBytes, 0);
        BitConverter.GetBytes((int)(expiryTimestamp - GetBaseTimestamp())).CopyTo(tokenBytes, 4);
        signature.AsSpan(0, 16).CopyTo(tokenBytes.AsSpan(8));

        return Base64UrlEncode(tokenBytes);
    }

    /// <summary>
    /// Validates a token and returns the decoded payload if valid.
    /// Uses caching to avoid O(n) signature verification on repeated requests.
    /// </summary>
    public AssetTokenPayload? ValidateToken(string token, Guid assetReferenceId, Guid? tenantId)
    {
        try
        {
            // Create a cache key combining token + context for lookup
            var cacheKey = $"{token}:{assetReferenceId}:{tenantId}";
            
            // Check cache first (O(1) lookup)
            if (_tokenCache.TryGetValue(cacheKey, out var cached))
            {
                // Verify cached entry hasn't expired
                if (cached.ExpiryTimestamp >= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return cached.Payload;
                }
                // Remove expired entry
                _tokenCache.TryRemove(cacheKey, out _);
            }
            
            var tokenBytes = Base64UrlDecode(token);
            if (tokenBytes.Length < 22)
                return null;

            var currentWindow = GetCurrentTimeWindow();
            int timeWindow;
            int expiryOffset;
            ReadOnlySpan<byte> providedSignature;

            if (tokenBytes.Length >= 24)
            {
                timeWindow = BitConverter.ToInt32(tokenBytes, 0);
                expiryOffset = BitConverter.ToInt32(tokenBytes, 4);
                providedSignature = tokenBytes.AsSpan(8, 16);
            }
            else
            {
                var encodedWindow = BitConverter.ToUInt16(tokenBytes, 0);
                timeWindow = (ushort)currentWindow == encodedWindow
                    ? currentWindow
                    : (ushort)(currentWindow - 1) == encodedWindow
                        ? currentWindow - 1
                        : int.MinValue;
                expiryOffset = BitConverter.ToInt32(tokenBytes, 2);
                providedSignature = tokenBytes.AsSpan(6, 16);
            }

            var expiryTimestamp = GetBaseTimestamp() + expiryOffset;

            // Check time window (current or previous)
            if (timeWindow != currentWindow && timeWindow != currentWindow - 1)
                return null;

            // Check expiry
            if (expiryTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return null;

            // Verify signature for all possible access policies (O(n) on cache miss only)
            foreach (var accessPolicy in Enum.GetValues<AssetAccessPolicy>())
            {
                var payload = BuildPayload(assetReferenceId, timeWindow, expiryTimestamp, accessPolicy, string.Empty, tenantId ?? Guid.Empty);
                var expectedSignature = ComputeSignature(payload);

                if (providedSignature.SequenceEqual(expectedSignature.AsSpan(0, 16)))
                {
                    var result = new AssetTokenPayload(
                        assetReferenceId,
                        timeWindow,
                        expiryTimestamp,
                        accessPolicy,
                        string.Empty,
                        tenantId ?? Guid.Empty);
                    
                    // Cache the validated token (with size limit to prevent memory exhaustion)
                    if (_tokenCache.Count < MaxCacheEntries)
                    {
                        _tokenCache.TryAdd(cacheKey, (result, expiryTimestamp));
                    }
                    else
                    {
                        // Evict expired entries when cache is full
                        EvictExpiredEntries();
                        _tokenCache.TryAdd(cacheKey, (result, expiryTimestamp));
                    }
                    
                    return result;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the current time window index.
    /// </summary>
    public int GetCurrentTimeWindow()
    {
        var totalHours = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 3600);
        return totalHours / _timeWindowHours;
    }

    /// <summary>
    /// Generates an ephemeral token (self-contained, with embedded asset reference).
    /// </summary>
    public string GenerateEphemeralToken(
        Guid assetReferenceId,
        TimeSpan expiry,
        Guid? userId = null)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry);
        var expiryTimestamp = expiresAt.ToUnixTimeSeconds();

        // Build payload: assetId:expiry:userId
        var payloadString = $"ephemeral|{assetReferenceId}|{expiryTimestamp}|{userId?.ToString() ?? ""}";
        var signature = ComputeSignature(payloadString);

        // Encode: assetId (16 bytes) + expiry (4 bytes) + hasUser (1 byte) + userId (16 bytes if present) + sig (16 bytes)
        var hasUser = userId.HasValue;
        var tokenBytes = new byte[16 + 4 + 1 + (hasUser ? 16 : 0) + 16];
        
        assetReferenceId.ToByteArray().CopyTo(tokenBytes, 0);
        BitConverter.GetBytes((int)(expiryTimestamp - GetBaseTimestamp())).CopyTo(tokenBytes, 16);
        tokenBytes[20] = (byte)(hasUser ? 1 : 0);
        
        var offset = 21;
        if (hasUser)
        {
            userId!.Value.ToByteArray().CopyTo(tokenBytes, offset);
            offset += 16;
        }
        
        signature.AsSpan(0, 16).CopyTo(tokenBytes.AsSpan(offset));

        return Base64UrlEncode(tokenBytes);
    }

    /// <summary>
    /// Validates an ephemeral token and extracts the asset reference.
    /// </summary>
    public EphemeralTokenPayload? ValidateEphemeralToken(string token)
    {
        try
        {
            var tokenBytes = Base64UrlDecode(token);
            if (tokenBytes.Length < 37) // Minimum: 16 + 4 + 1 + 16 = 37
                return null;

            var assetReferenceId = new Guid(tokenBytes.AsSpan(0, 16));
            var expiryOffset = BitConverter.ToInt32(tokenBytes, 16);
            var expiryTimestamp = GetBaseTimestamp() + expiryOffset;
            var hasUser = tokenBytes[20] == 1;

            var offset = 21;
            Guid? userId = null;
            if (hasUser)
            {
                if (tokenBytes.Length < 53) // With user: 16 + 4 + 1 + 16 + 16 = 53
                    return null;
                userId = new Guid(tokenBytes.AsSpan(offset, 16));
                offset += 16;
            }

            var providedSignature = tokenBytes.AsSpan(offset, 16);

            // Verify signature
            var payloadString = $"ephemeral|{assetReferenceId}|{expiryTimestamp}|{userId?.ToString() ?? ""}";
            var expectedSignature = ComputeSignature(payloadString);

            if (!providedSignature.SequenceEqual(expectedSignature.AsSpan(0, 16)))
                return null;

            return new EphemeralTokenPayload(
                assetReferenceId,
                DateTimeOffset.FromUnixTimeSeconds(expiryTimestamp),
                userId);
        }
        catch
        {
            return null;
        }
    }

    private string BuildPayload(
        Guid assetReferenceId,
        int timeWindow,
        long expiryTimestamp,
        AssetAccessPolicy accessPolicy,
        string transformSpec,
        Guid tenantId)
    {
        return $"{assetReferenceId}|{timeWindow}|{expiryTimestamp}|{(int)accessPolicy}|{transformSpec}|{tenantId}";
    }

    private byte[] ComputeSignature(string payload)
    {
        using var hmac = new HMACSHA256(_secretKey);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static long GetBaseTimestamp()
    {
        // Use Jan 1, 2024 as base to save bytes
        return new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
    
    /// <summary>
    /// Evicts expired entries from the token cache to prevent memory bloat.
    /// Called when cache reaches MaxCacheEntries limit.
    /// </summary>
    private void EvictExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiredKeys = _tokenCache
            .Where(kvp => kvp.Value.ExpiryTimestamp < now)
            .Select(kvp => kvp.Key)
            .ToList();
        
        foreach (var key in expiredKeys)
        {
            _tokenCache.TryRemove(key, out _);
        }
    }
}
