using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;
using Nethereum.Siwe.Core;
using Nethereum.Util;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Generates and verifies EIP-4361 Sign-In with Ethereum challenges.
/// </summary>
public class Web3Service : IWeb3Service
{
    private const string ChallengeKeyPrefix = "web3:challenge:";
    private const string SiweConfigurationPrefix = "Authentication:Web3:Siwe";
    private const string DefaultStatement = "Sign in with your Ethereum wallet.";
    private static readonly TimeSpan s_challengeLifetime = TimeSpan.FromMinutes(5);

    private readonly ILogger<Web3Service> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly TimeProvider _timeProvider;
    private readonly Uri _origin;
    private readonly string _statement;
    private readonly HashSet<string> _allowedChainIds;

    public Web3Service(ILogger<Web3Service> logger, IMemoryCache memoryCache, IConfiguration configuration)
        : this(logger, memoryCache, configuration, TimeProvider.System)
    {
    }

    public Web3Service(ILogger<Web3Service> logger, IMemoryCache memoryCache, IConfiguration configuration, TimeProvider timeProvider)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        var origin = configuration[$"{SiweConfigurationPrefix}:Origin"];
        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new InvalidOperationException($"{SiweConfigurationPrefix}:Origin must be configured for SIWE authentication.");
        }

        _origin = ParseOrigin(origin);
        _statement = string.IsNullOrWhiteSpace(configuration[$"{SiweConfigurationPrefix}:Statement"])
            ? DefaultStatement
            : configuration[$"{SiweConfigurationPrefix}:Statement"]!;
        _allowedChainIds = LoadAllowedChainIds(configuration);
    }

    public Task<Web3Challenge> GenerateChallengeAsync(string walletAddress) =>
        GenerateChallengeAsync(walletAddress, tenantId: null, chainId: "1");

    public Task<Web3Challenge> GenerateChallengeAsync(string walletAddress, Guid? tenantId) =>
        GenerateChallengeAsync(walletAddress, tenantId, chainId: "1");

    public Task<Web3Challenge> GenerateChallengeAsync(string walletAddress, string chainId) =>
        GenerateChallengeAsync(walletAddress, tenantId: null, chainId);

    public Task<Web3Challenge> GenerateChallengeAsync(string walletAddress, Guid? tenantId, string chainId)
    {
        if (!IsValidWalletAddress(walletAddress))
        {
            throw new ArgumentException("Invalid Ethereum address", nameof(walletAddress));
        }

        var normalizedChainId = GetAllowedChainId(chainId);
        var checksummedAddress = AddressUtil.Current.ConvertToChecksumAddress(walletAddress);
        var nonce = GenerateNonce();
        var issuedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = issuedAt.Add(s_challengeLifetime);
        var siweMessage = new SiweMessage
        {
            // SIWE's Domain field is the authority. HTTPS is implicit per EIP-4361; local HTTP
            // development origins include their scheme in the first line.
            Domain = _origin.Authority,
            Address = checksummedAddress,
            Statement = _statement,
            Uri = _origin.AbsoluteUri,
            Version = "1",
            ChainId = normalizedChainId,
            Nonce = nonce,
            IssuedAt = ToSiweTimestamp(issuedAt),
            ExpirationTime = ToSiweTimestamp(expiresAt)
        };

        var builtMessage = SiweMessageStringBuilder.BuildMessage(siweMessage);
        var challenge = new Web3Challenge
        {
            Message = _origin.Scheme == Uri.UriSchemeHttps ? builtMessage : $"{_origin.Scheme}://{builtMessage}",
            WalletAddress = checksummedAddress,
            Nonce = nonce,
            ChainId = normalizedChainId,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            TenantId = tenantId
        };

        // The host cache is size-bounded. Account for the message and binding entries
        // explicitly; an unknown or evicted challenge always fails verification.
        _memoryCache.Set(ChallengeKeyPrefix + nonce, challenge, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt, Size = Encoding.UTF8.GetByteCount(challenge.Message) + 512L
        });
        _memoryCache.Set(GetWalletKey(walletAddress), nonce, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt, Size = nonce.Length * 2L + walletAddress.Length * 2L + 128L
        });

        // MemoryCache may silently decline an entry at capacity. Never return a
        // challenge whose nonce and wallet binding were not actually retained.
        if (!_memoryCache.TryGetValue(ChallengeKeyPrefix + nonce, out Web3Challenge? storedChallenge)
            || !ReferenceEquals(storedChallenge, challenge)
            || !_memoryCache.TryGetValue(GetWalletKey(walletAddress), out string? storedNonce)
            || !string.Equals(storedNonce, nonce, StringComparison.Ordinal))
        {
            _memoryCache.Remove(ChallengeKeyPrefix + nonce);
            throw new InvalidOperationException("SIWE challenge storage is unavailable.");
        }

        _logger.LogInformation("Generated SIWE challenge for wallet {WalletAddress} on chain {ChainId}", checksummedAddress, normalizedChainId);
        return Task.FromResult(challenge);
    }

    public Task<bool> VerifySignatureAsync(string walletAddress, string signature, string originalMessage) =>
        VerifySignatureAsync(walletAddress, signature, originalMessage, expectedChainId: null, expectedNonce: null);

    public Task<bool> VerifySignatureAsync(
        string walletAddress,
        string signature,
        string originalMessage,
        string? expectedChainId) =>
        VerifySignatureAsync(walletAddress, signature, originalMessage, expectedChainId, expectedNonce: null);

    public Task<bool> VerifySignatureAsync(
        string walletAddress,
        string signature,
        string originalMessage,
        string? expectedChainId,
        string? expectedNonce)
    {
        if (!IsValidWalletAddress(walletAddress))
        {
            _logger.LogWarning("Invalid wallet address format: {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        var walletKey = GetWalletKey(walletAddress);
        if (!_memoryCache.TryGetValue(walletKey, out string? nonce) || string.IsNullOrEmpty(nonce))
        {
            _logger.LogWarning("SIWE challenge not found for wallet {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        var cacheKey = ChallengeKeyPrefix + nonce;
        if (!_memoryCache.TryGetValue(cacheKey, out Web3Challenge? challenge) || challenge is null)
        {
            _logger.LogWarning("SIWE challenge not found for wallet {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        if (!string.Equals(challenge.Message, originalMessage, StringComparison.Ordinal)
            || !challenge.IsValidAt(_timeProvider.GetUtcNow().UtcDateTime)
            || !TryParseSiweMessage(originalMessage, out var siweMessage)
            || !HasExpectedSiweFields(siweMessage, challenge, walletAddress, expectedChainId, expectedNonce))
        {
            if (!challenge.IsValidAt(_timeProvider.GetUtcNow().UtcDateTime))
            {
                _memoryCache.Remove(cacheKey);
                _memoryCache.Remove(walletKey);
            }

            _logger.LogWarning("SIWE message validation failed for wallet {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        if (!VerifyEthereumSignature(originalMessage, signature, challenge.WalletAddress))
        {
            _logger.LogWarning("Invalid EIP-191 signature for wallet {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        // The cached object is shared by all requests in this process. Claim it atomically so
        // two concurrent requests cannot both establish a session with the same SIWE nonce.
        if (!challenge.TryConsume())
        {
            _logger.LogWarning("SIWE challenge was already consumed for wallet {WalletAddress}", walletAddress);
            return Task.FromResult(false);
        }

        _memoryCache.Remove(cacheKey);
        _memoryCache.Remove(walletKey);
        _logger.LogInformation("Successfully verified SIWE signature for wallet {WalletAddress}", walletAddress);
        return Task.FromResult(true);
    }

    public bool IsValidWalletAddress(string walletAddress)
    {
        if (string.IsNullOrEmpty(walletAddress)
            || !walletAddress.StartsWith("0x", StringComparison.Ordinal)
            || walletAddress.Length != 42
            || !walletAddress.AsSpan(2).ToString().All(IsAsciiHexCharacter))
        {
            return false;
        }

        // Uniform-case hexadecimal inputs remain compatible; mixed-case inputs must
        // carry a valid EIP-55 checksum before canonical SIWE formatting.
        var value = walletAddress[2..];
        var mixedCase = value.Any(character => character is >= 'a' and <= 'f')
                        && value.Any(character => character is >= 'A' and <= 'F');
        return !mixedCase || AddressUtil.Current.IsChecksumAddress(walletAddress);
    }

    private bool HasExpectedSiweFields(
        SiweMessage message,
        Web3Challenge challenge,
        string walletAddress,
        string? expectedChainId,
        string? expectedNonce)
    {
        if (!string.Equals(message.Domain, _origin.Authority, StringComparison.OrdinalIgnoreCase)
            || !Uri.TryCreate(message.Uri, UriKind.Absolute, out var messageUri)
            || messageUri != _origin
            || !string.Equals(message.Version, "1", StringComparison.Ordinal)
            || !string.Equals(message.Address, challenge.WalletAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.Address, walletAddress, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(message.Nonce, challenge.Nonce, StringComparison.Ordinal)
            || (expectedNonce is not null && !string.Equals(message.Nonce, expectedNonce, StringComparison.Ordinal))
            || !TryNormalizeChainId(message.ChainId, out var messageChainId)
            || !_allowedChainIds.Contains(messageChainId)
            || !string.Equals(messageChainId, challenge.ChainId, StringComparison.Ordinal))
        {
            return false;
        }

        if (expectedChainId is not null
            && (!TryNormalizeChainId(expectedChainId, out var requestedChainId)
                || !_allowedChainIds.Contains(requestedChainId)
                || !string.Equals(requestedChainId, challenge.ChainId, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(message.IssuedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var issuedAt)
            || !DateTimeOffset.TryParse(message.ExpirationTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiresAt))
        {
            return false;
        }

        var expectedIssuedAt = new DateTimeOffset(challenge.IssuedAt.ToUniversalTime());
        var expectedExpiresAt = new DateTimeOffset(challenge.ExpiresAt.ToUniversalTime());
        var now = _timeProvider.GetUtcNow();
        if (issuedAt != expectedIssuedAt
            || expiresAt != expectedExpiresAt
            || issuedAt > now.AddMinutes(1)
            || expiresAt <= issuedAt
            || expiresAt <= now
            || expiresAt - issuedAt > s_challengeLifetime)
        {
            return false;
        }

        return string.IsNullOrEmpty(message.NotBefore)
               || DateTimeOffset.TryParse(message.NotBefore, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var notBefore)
               && notBefore <= now;
    }

    private bool TryParseSiweMessage(string value, out SiweMessage message)
    {
        try
        {
            var authorityHeader = $"{_origin.Authority} wants you to sign in with your Ethereum account:";
            var configuredHeader = _origin.Scheme == Uri.UriSchemeHttps
                ? authorityHeader
                : $"{_origin.Scheme}://{authorityHeader}";
            if (!value.StartsWith(configuredHeader + "\n", StringComparison.Ordinal))
            {
                message = null!;
                return false;
            }

            // Nethereum.Siwe.Core 6.1.0's ABNF parser rejects the optional scheme production.
            // Validate the configured scheme above, then parse the RFC's authority-only form.
            var parserInput = _origin.Scheme == Uri.UriSchemeHttps
                ? value
                : authorityHeader + value[configuredHeader.Length..];
            message = SiweMessageParser.ParseUsingAbnf(parserInput);
            if (!message.HasRequiredFields())
            {
                return false;
            }

            var canonicalBody = SiweMessageStringBuilder.BuildMessage(message);
            return string.Equals(canonicalBody, parserInput, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            // The parser works on untrusted wallet input and reports malformed messages by throwing.
            message = null!;
            return false;
        }
    }

    private bool VerifyEthereumSignature(string message, string signature, string walletAddress)
    {
        if (string.IsNullOrEmpty(signature) || signature.Length != 132 || !signature.StartsWith("0x", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = Convert.FromHexString(signature.AsSpan(2));
            var recoveredAddress = new EthereumMessageSigner().EncodeUTF8AndEcRecover(message, signature);
            return string.Equals(recoveredAddress, walletAddress, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            // Malformed signatures are authentication failures, not server errors.
            return false;
        }
    }

    private string GetAllowedChainId(string chainId)
    {
        if (!TryNormalizeChainId(chainId, out var normalized) || !_allowedChainIds.Contains(normalized))
        {
            throw new ArgumentException("The requested Ethereum chain is not enabled for SIWE authentication.", nameof(chainId));
        }

        return normalized;
    }

    private static HashSet<string> LoadAllowedChainIds(IConfiguration configuration)
    {
        var configuredValues = configuration.GetSection($"{SiweConfigurationPrefix}:AllowedChainIds")
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (configuredValues.Length == 0)
        {
            return ["1"];
        }

        var chainIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var configuredValue in configuredValues)
        {
            if (!TryNormalizeChainId(configuredValue, out var normalized))
            {
                throw new InvalidOperationException("Authentication:Web3:Siwe:AllowedChainIds contains an invalid chain ID.");
            }

            chainIds.Add(normalized);
        }

        return chainIds;
    }

    private static Uri ParseOrigin(string originValue)
    {
        if (!Uri.TryCreate(originValue, UriKind.Absolute, out var origin)
            || (origin.Scheme != Uri.UriSchemeHttps && origin.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment)
            || (origin.Scheme == Uri.UriSchemeHttp && !origin.IsLoopback))
        {
            throw new InvalidOperationException(
                "Authentication:Web3:Siwe:Origin must be an HTTPS origin, or an HTTP loopback origin for local development.");
        }

        return origin;
    }

    private static bool TryNormalizeChainId(string? value, out string normalized)
    {
        if (BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var chainId) && chainId > BigInteger.Zero)
        {
            normalized = chainId.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static string GetWalletKey(string walletAddress) => ChallengeKeyPrefix + "wallet:" + walletAddress.ToLowerInvariant();

    private static bool IsAsciiHexCharacter(char character) => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static string GenerateNonce()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ToSiweTimestamp(DateTime dateTime) => new DateTimeOffset(dateTime.ToUniversalTime()).ToString("O", CultureInfo.InvariantCulture);
}
