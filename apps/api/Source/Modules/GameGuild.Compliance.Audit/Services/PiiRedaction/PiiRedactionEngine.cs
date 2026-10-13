using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Compiled, ready-to-apply form of a <see cref="PiiRedactionRule"/>. Matchers are compiled once per
/// rule load; per-value application is a bounded-timeout regex replace.
/// </summary>
public sealed record CompiledPiiRule
{
    public required Guid RuleId { get; init; }

    public required string RuleName { get; init; }

    public required PiiRuleScope Scope { get; init; }

    public required PiiRuleAction Action { get; init; }

    public required int Priority { get; init; }

    public required string Replacement { get; init; }

    /// <summary>Preset identity when the matcher is a built-in preset; null for custom regex rules.</summary>
    public PiiPresetPattern? Preset { get; init; }

    /// <summary>Field names matched case-insensitively; empty for global rules.</summary>
    public required IReadOnlyCollection<string> TargetFields { get; init; }

    public required Regex Matcher { get; init; }
}

/// <summary>
/// Deterministic PII matcher and transformation engine. Pure and synchronous: given a rule
/// set and a value, produce the transformed value plus a summary of what fired. Rules are applied
/// most-specific-first (field-name rules before global rules, then ascending priority) — the engine
/// applies that precedence ordering itself so it holds even when a caller passes an unordered set,
/// and the first matching rule wins, so a value is never double-transformed.
/// Fail-closed for per-value matcher errors: a throwing or timing-out rule transforms the whole value
/// instead of passing it through.
/// </summary>
public static partial class PiiRedactionEngine
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly ConcurrentDictionary<PiiPresetPattern, Regex> PresetCache = new();

    /// <summary>High-precision preset matchers for common PII types (issue #192 presets).</summary>
    public static Regex GetPresetPattern(PiiPresetPattern preset) => PresetCache.GetOrAdd(preset, BuildPreset);

    private static Regex BuildPreset(PiiPresetPattern preset) => preset switch
    {
        PiiPresetPattern.Email => new Regex(
            @"[A-Za-z0-9!#$%&'*+/=?^_`{|}~.-]+@[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)+",
            RegexOptions.Compiled,
            MatchTimeout),
        // 8..19 characters of digits/separators; digit-count validation happens in IsValidPresetMatch.
        PiiPresetPattern.Phone => new Regex(
            @"\+?[0-9][0-9()\s.-]{7,17}[0-9]",
            RegexOptions.Compiled,
            MatchTimeout),
        // US social-security national identifiers (the v1 national-id shape).
        PiiPresetPattern.NationalId => new Regex(
            @"\b\d{3}-\d{2}-\d{4}\b",
            RegexOptions.Compiled,
            MatchTimeout),
        // 13-19 digit card numbers with optional separators; Luhn-validated in IsValidPresetMatch.
        PiiPresetPattern.CreditCard => new Regex(
            @"\b(?:\d[ -]?){12,18}\d\b",
            RegexOptions.Compiled,
            MatchTimeout),
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown PII preset pattern.")
    };

    /// <summary>Compiles a persisted rule into its executable form; invalid regexes throw <see cref="ArgumentException"/>.</summary>
    public static CompiledPiiRule Compile(PiiRedactionRule rule)
    {
        var matcher = rule.PatternType == PiiRulePatternType.Preset
            ? GetPresetPattern(rule.Preset!.Value)
            : new Regex(rule.RegexPattern!, RegexOptions.Compiled, MatchTimeout);

        return new CompiledPiiRule
        {
            RuleId = rule.Id,
            RuleName = rule.RuleName,
            Scope = rule.Scope,
            Action = rule.Action,
            Priority = rule.Priority,
            Preset = rule.PatternType == PiiRulePatternType.Preset ? rule.Preset : null,
            Replacement = rule.Replacement,
            TargetFields = rule.TargetFields,
            Matcher = matcher
        };
    }

    /// <summary>
    /// Applies the rule set to a single value under a field name. The first rule (in the
    /// most-specific-first order documented on this class) whose matcher fires wins and its action
    /// applies to every match inside the value.
    /// </summary>
    public static PiiValueOutcome ApplyToValue(string? value, string fieldName, IReadOnlyList<CompiledPiiRule> rules, IPiiTokenSource tokens)
    {
        if (string.IsNullOrEmpty(value))
        {
            return PiiValueOutcome.Unchanged;
        }

        foreach (var rule in OrderByPrecedence(rules))
        {
            if (rule.Scope == PiiRuleScope.FieldName && !rule.TargetFields.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!HasValidMatch(rule, value))
            {
                continue;
            }

            var replaced = ReplaceMatches(rule, value, tokens, out var matchCount);
            return new PiiValueOutcome(replaced, rule.RuleId, rule.Action, matchCount);
        }

        return PiiValueOutcome.Unchanged;
    }

    /// <summary>
    /// Most-specific-first precedence: field-name rules before global rules, then ascending priority
    /// (lower applies first). Stable, so equally ranked rules keep their input order — defense in
    /// depth over the ordering the rule provider already guarantees.
    /// </summary>
    private static IEnumerable<CompiledPiiRule> OrderByPrecedence(IReadOnlyList<CompiledPiiRule> rules) =>
        rules
            .OrderBy(rule => rule.Scope == PiiRuleScope.Global ? 1 : 0)
            .ThenBy(rule => rule.Priority);

    private static bool HasValidMatch(CompiledPiiRule rule, string value)
    {
        try
        {
            foreach (Match match in rule.Matcher.Matches(value))
            {
                if (match.Success && IsValidPresetMatch(rule, match.Value))
                {
                    return true;
                }
            }
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            // Catastrophic-backtracking defence: treat as matched so the value is transformed.
            return true;
        }
    }

    private static string ReplaceMatches(CompiledPiiRule rule, string value, IPiiTokenSource tokens, out int matchCount)
    {
        try
        {
            var count = 0;
            var result = rule.Matcher.Replace(value, match =>
            {
                if (!IsValidPresetMatch(rule, match.Value))
                {
                    return match.Value;
                }
                count++;
                return rule.Action == PiiRuleAction.Tokenize ? tokens.Tokenize(match.Value) : rule.Replacement;
            });
            matchCount = count;
            return result;
        }
        catch (RegexMatchTimeoutException)
        {
            matchCount = 1;
            return rule.Action == PiiRuleAction.Tokenize ? tokens.Tokenize(value) : rule.Replacement;
        }
    }

    /// <summary>Preset-specific semantic validation that trims false positives the regexes alone would keep.</summary>
    internal static bool IsValidPresetMatch(CompiledPiiRule rule, string candidate) => rule.Preset switch
    {
        PiiPresetPattern.Phone => IsValidPhone(candidate),
        PiiPresetPattern.CreditCard => IsValidCreditCard(candidate),
        _ => true
    };

    internal static bool IsValidPhone(string candidate)
    {
        var digits = candidate.Count(char.IsDigit);
        return digits is >= 10 and <= 15;
    }

    internal static bool IsValidCreditCard(string candidate)
    {
        var digits = new List<int>(24);
        foreach (var character in candidate)
        {
            if (!char.IsDigit(character))
            {
                continue;
            }
            digits.Add(character - '0');
        }

        return digits.Count is >= 13 and <= 19 && PassesLuhn(digits);
    }

    private static bool PassesLuhn(List<int> digits)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var index = digits.Count - 1; index >= 0; index--)
        {
            var digit = digits[index];
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9)
                {
                    digit -= 9;
                }
            }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }
}

/// <summary>Supplies deterministic tokens for tokenize-action rules.</summary>
public interface IPiiTokenSource
{
    string Tokenize(string value);
}

/// <summary>Outcome of applying the rule set to one value.</summary>
/// <param name="Value">Transformed value; null when no rule fired.</param>
/// <param name="AppliedRuleId">Rule that fired, when any.</param>
/// <param name="Action">Action of the fired rule.</param>
/// <param name="MatchCount">Number of matches replaced within the value.</param>
public sealed record PiiValueOutcome(
    string? Value = null,
    Guid? AppliedRuleId = null,
    PiiRuleAction? Action = null,
    int MatchCount = 0)
{
    public static readonly PiiValueOutcome Unchanged = new();

    public bool Changed => Value is not null && MatchCount > 0;
}

/// <summary>
/// Deterministic HMAC tokenizer for PII values (issue #192). Tokens are stable across processes and
/// restarts, so redacted values stay joinable for investigations. The HMAC key is derived from the
/// platform encryption key configuration (<c>Encryption:EncryptionKey</c> / <c>Encryption:Key</c>) and
/// the tokenizer fails closed exactly like the platform encryption service: no configured key means
/// no tokens.
/// </summary>
public sealed class PiiHmacTokenizer(IConfiguration configuration) : IPiiTokenSource
{
    public const string TokenPrefix = "pii_tok_v1_";

    private const string KeyPurpose = "gameguild:pii-tokenization:v1:";

    private static readonly TimeSpan KeyCacheLifetime = TimeSpan.FromMinutes(10);

    private readonly object _keyGate = new();

    private byte[]? _cachedKey;

    private string? _cachedKeyFingerprint;

    private DateTime _keyLoadedAt;

    public string Tokenize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return TokenPrefix;
        }

        var key = ResolveKey();
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
        // 128-bit prefix: collision-resistant for realistic token populations and short enough for log fields.
        return TokenPrefix + Base64UrlEncode(digest, length: 16);
    }

    /// <summary>Stable fingerprint of the active key for diagnostics; never exposes key material.</summary>
    public string? KeyFingerprint()
    {
        ResolveKey();
        return _cachedKeyFingerprint;
    }

    private byte[] ResolveKey()
    {
        lock (_keyGate)
        {
            if (_cachedKey is not null && SystemClock.UtcNow - _keyLoadedAt < KeyCacheLifetime)
            {
                return _cachedKey;
            }

            var configuredKey = configuration["Encryption:EncryptionKey"];
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                configuredKey = configuration["Encryption:Key"];
            }

            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                // Fail closed, mirroring the platform encryption service: no configured key, no tokens.
                throw new InvalidOperationException(
                    "Encryption:EncryptionKey (or Encryption:Key) is not configured. PII tokenization fails closed; set a key of at least 32 bytes before enabling tokenize-action redaction rules.");
            }

            var derived = SHA256.HashData(Encoding.UTF8.GetBytes(KeyPurpose + configuredKey));
            _cachedKey = derived;
            _cachedKeyFingerprint = Convert.ToHexString(SHA256.HashData(derived))[..16].ToLowerInvariant();
            _keyLoadedAt = SystemClock.UtcNow;
            System.Diagnostics.Trace.TraceInformation("PII tokenization key material refreshed (fingerprint " + _cachedKeyFingerprint + ")");
            return derived;
        }
    }

    private static string Base64UrlEncode(byte[] digest, int length)
    {
        var span = new ReadOnlySpan<byte>(digest, 0, length);
        return Convert.ToBase64String(span).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
