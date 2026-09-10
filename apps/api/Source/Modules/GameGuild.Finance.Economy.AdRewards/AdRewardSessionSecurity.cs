using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.AdRewards;

public sealed record AdRewardSessionClaims(
    Guid SessionId,
    Guid UserId,
    WalletId WalletId,
    string Network,
    string CreativeId,
    string DeviceRiskHash,
    string Nonce,
    TimeSpan RequiredDuration,
    PolicyVersion PolicyVersion,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record SignedAdRewardSession(string Value);

public sealed record AdRewardSessionRequest(
    Guid UserId,
    WalletId WalletId,
    string Network,
    string CreativeId,
    string DeviceRiskHash,
    TimeSpan RequiredDuration);

public sealed record AdRewardSessionStartResult(
    AdRewardSessionClaims Claims,
    SignedAdRewardSession Token);

public interface IAdRewardSessionEntropy
{
    Guid CreateSessionId();
    string CreateNonce();
}

public sealed class CryptographicAdRewardSessionEntropy : IAdRewardSessionEntropy
{
    public Guid CreateSessionId() => Guid.NewGuid();

    public string CreateNonce() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed class AdRewardSessionTokenService
{
    private const char Separator = '\u001f';
    private readonly byte[] _secret;
    private readonly TimeSpan _lifetime;

    public TimeSpan Lifetime => _lifetime;

    public AdRewardSessionTokenService(byte[] secret, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length < 32) throw new ArgumentException("Session signing secret must contain at least 32 bytes.", nameof(secret));
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        _secret = [.. secret];
        _lifetime = lifetime;
    }

    public SignedAdRewardSession Issue(AdRewardSessionClaims claims, DateTimeOffset now)
    {
        ValidateClaims(claims);
        if (claims.IssuedAt != now || claims.ExpiresAt != now + _lifetime)
            throw new InvalidAdRewardSessionTokenException("Session timestamps must be assigned by the token service policy.");
        var payload = Serialize(claims);
        var encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Base64UrlEncode(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(encodedPayload)));
        return new SignedAdRewardSession($"{encodedPayload}.{signature}");
    }

    public AdRewardSessionClaims Validate(string token, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        var parts = token.Split('.', StringSplitOptions.None);
        if (parts.Length != 2) throw new InvalidAdRewardSessionTokenException("Session token format is invalid.");
        byte[] supplied;
        try
        {
            supplied = Base64UrlDecode(parts[1]);
        }
        catch (FormatException)
        {
            throw new InvalidAdRewardSessionTokenException("Session token signature is invalid.");
        }

        var expected = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(parts[0]));
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied))
            throw new InvalidAdRewardSessionTokenException("Session token signature is invalid.");

        AdRewardSessionClaims claims;
        try
        {
            claims = Deserialize(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
            ValidateClaims(claims);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidAdRewardSessionTokenException("Session token payload is invalid.");
        }

        if (now >= claims.ExpiresAt)
            throw new ExpiredAdRewardSessionTokenException("Session token has expired.");
        if (now < claims.IssuedAt)
            throw new InvalidAdRewardSessionTokenException("Session token is not active yet.");
        return claims;
    }

    private static void ValidateClaims(AdRewardSessionClaims claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        if (claims.SessionId == Guid.Empty || claims.UserId == Guid.Empty)
            throw new ArgumentException("Session and user IDs are required.", nameof(claims));
        ValidateText(claims.Network, nameof(claims.Network));
        ValidateText(claims.CreativeId, nameof(claims.CreativeId));
        ValidateText(claims.DeviceRiskHash, nameof(claims.DeviceRiskHash));
        ValidateText(claims.Nonce, nameof(claims.Nonce));
        if (claims.RequiredDuration <= TimeSpan.Zero || claims.ExpiresAt <= claims.IssuedAt)
            throw new ArgumentException("Session timing is invalid.", nameof(claims));
    }

    private static void ValidateText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains(Separator, StringComparison.Ordinal))
            throw new ArgumentException("Session claim contains an invalid separator.", parameterName);
    }

    private static string Serialize(AdRewardSessionClaims claims) => string.Join(Separator,
        claims.SessionId.ToString("N"),
        claims.UserId.ToString("N"),
        claims.WalletId.Value.ToString("N"),
        claims.Network,
        claims.CreativeId,
        claims.DeviceRiskHash,
        claims.Nonce,
        claims.RequiredDuration.Ticks.ToString(CultureInfo.InvariantCulture),
        claims.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture),
        claims.IssuedAt.UtcTicks.ToString(CultureInfo.InvariantCulture),
        claims.ExpiresAt.UtcTicks.ToString(CultureInfo.InvariantCulture));

    private static AdRewardSessionClaims Deserialize(string payload)
    {
        var values = payload.Split(Separator, StringSplitOptions.None);
        if (values.Length != 11) throw new FormatException("Session claim count is invalid.");
        return new AdRewardSessionClaims(
            Guid.ParseExact(values[0], "N"),
            Guid.ParseExact(values[1], "N"),
            new WalletId(Guid.ParseExact(values[2], "N")),
            values[3], values[4], values[5], values[6],
            TimeSpan.FromTicks(long.Parse(values[7], CultureInfo.InvariantCulture)),
            new PolicyVersion(long.Parse(values[8], CultureInfo.InvariantCulture)),
            new DateTimeOffset(long.Parse(values[9], CultureInfo.InvariantCulture), TimeSpan.Zero),
            new DateTimeOffset(long.Parse(values[10], CultureInfo.InvariantCulture), TimeSpan.Zero));
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
        return Convert.FromBase64String(padded);
    }
}

public sealed class AdRewardSessionService
{
    private readonly AdNetworkPolicyStore _policies;
    private readonly AdRewardControlState _controls;
    private readonly AdRewardSessionTokenService _tokens;
    private readonly IAdRewardSessionEntropy _entropy;

    public AdRewardSessionService(
        AdNetworkPolicyStore policies,
        AdRewardControlState controls,
        AdRewardSessionTokenService tokens,
        IAdRewardSessionEntropy entropy)
    {
        _policies = policies ?? throw new ArgumentNullException(nameof(policies));
        _controls = controls ?? throw new ArgumentNullException(nameof(controls));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _entropy = entropy ?? throw new ArgumentNullException(nameof(entropy));
    }

    public AdRewardSessionStartResult Start(AdRewardSessionRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Network);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CreativeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DeviceRiskHash);
        if (request.RequiredDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(request));

        var network = request.Network.Trim();
        _controls.EnsureIssuanceEnabled(network);
        var policy = _policies.Current(network, now);
        if (policy.IssuanceMode == AdRewardIssuanceMode.Disabled)
            throw new AdRewardIssuanceDisabledException("The current network policy disables ad rewards.");
        if (!policy.IsReportCurrent(now))
            throw new AdNetworkReportStaleException("The provider report is stale; new reward sessions are disabled.");

        var claims = new AdRewardSessionClaims(
            _entropy.CreateSessionId(),
            request.UserId,
            request.WalletId,
            policy.Network,
            request.CreativeId.Trim(),
            request.DeviceRiskHash.Trim(),
            _entropy.CreateNonce(),
            request.RequiredDuration,
            policy.Version,
            now,
            now + _tokens.Lifetime);
        return new AdRewardSessionStartResult(claims, _tokens.Issue(claims, now));
    }
}

public sealed class InvalidAdRewardSessionTokenException(string message) : InvalidOperationException(message);
public sealed class ExpiredAdRewardSessionTokenException(string message) : InvalidOperationException(message);
public sealed class AdNetworkReportStaleException(string message) : InvalidOperationException(message);
