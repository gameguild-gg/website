using System.Text.RegularExpressions;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Configurable PII redaction rule applied by the redaction pipeline to audit records and
/// structured-log payloads before persistence. Rules are either tenant-scoped or global
/// (<see cref="TenantId"/> is null) and are compiled to regex matchers by the pipeline.
/// </summary>
public sealed class PiiRedactionRule : EntityBase
{
    public new Guid? TenantId { get; private set; }

    public string RuleName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>
    /// Ordering within the same scope class: lower values apply first. Field-name rules always
    /// take precedence over global rules regardless of priority.
    /// </summary>
    public int Priority { get; private set; }

    /// <summary>How the matcher is derived: a custom regex or a vetted preset pattern.</summary>
    public PiiRulePatternType PatternType { get; private set; }

    /// <summary>Custom regular expression; required when <see cref="PatternType"/> is <see cref="PiiRulePatternType.Regex"/>.</summary>
    public string? RegexPattern { get; private set; }

    /// <summary>Vetted preset matcher; required when <see cref="PatternType"/> is <see cref="PiiRulePatternType.Preset"/>.</summary>
    public PiiPresetPattern? Preset { get; private set; }

    /// <summary>What happens to matched values: replace with <see cref="Replacement"/> or substitute a deterministic token.</summary>
    public PiiRuleAction Action { get; private set; }

    /// <summary>Field-name rules only match the listed field names (case-insensitive); global rules match every scanned value.</summary>
    public PiiRuleScope Scope { get; private set; }

    /// <summary>Target field names; required when <see cref="Scope"/> is <see cref="PiiRuleScope.FieldName"/>.</summary>
    public string[] TargetFields { get; private set; } = Array.Empty<string>();

    /// <summary>Replacement text used by <see cref="PiiRuleAction.Redact"/> rules.</summary>
    public string Replacement { get; private set; } = "[PII-REDACTED]";

    /// <summary>Optimistic-concurrency revision; incremented on every mutation.</summary>
    public int Revision { get; private set; }

    public Guid? CreatedByUserId { get; private set; }

    public Guid? UpdatedByUserId { get; private set; }

    private PiiRedactionRule() { }

    public static PiiRedactionRule Create(
        Guid? tenantId,
        string ruleName,
        PiiRulePatternType patternType,
        string? regexPattern,
        PiiPresetPattern? preset,
        PiiRuleAction action,
        PiiRuleScope scope,
        IReadOnlyList<string> targetFields,
        int priority,
        string? replacement,
        Guid? createdByUserId)
    {
        var rule = new PiiRedactionRule
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleName = ruleName,
            IsEnabled = true,
            PatternType = patternType,
            RegexPattern = regexPattern,
            Preset = preset,
            Action = action,
            Scope = scope,
            TargetFields = NormalizeFields(targetFields),
            Priority = priority,
            CreatedByUserId = createdByUserId
        };
        if (!string.IsNullOrEmpty(replacement))
        {
            rule.Replacement = replacement;
        }
        return rule;
    }

    public void Update(
        string ruleName,
        string? description,
        PiiRulePatternType patternType,
        string? regexPattern,
        PiiPresetPattern? preset,
        PiiRuleAction action,
        PiiRuleScope scope,
        IReadOnlyList<string> targetFields,
        int priority,
        string? replacement,
        bool isEnabled,
        Guid? updatedByUserId)
    {
        RuleName = ruleName;
        Description = description;
        PatternType = patternType;
        RegexPattern = regexPattern;
        Preset = preset;
        Action = action;
        Scope = scope;
        TargetFields = NormalizeFields(targetFields);
        Priority = priority;
        IsEnabled = isEnabled;
        UpdatedByUserId = updatedByUserId;
        if (!string.IsNullOrEmpty(replacement))
        {
            Replacement = replacement;
        }
        Revision++;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Validates the rule shape: regex rules need a compilable pattern (and any supplied regex must
    /// compile regardless of pattern type), preset rules need a preset, and field-name rules need at
    /// least one target field. Returns the errors keyed by field name; an empty result means the rule
    /// can be compiled by the pipeline.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> Validate(
        string ruleName,
        PiiRulePatternType patternType,
        string? regexPattern,
        PiiPresetPattern? preset,
        PiiRuleScope scope,
        IReadOnlyList<string> targetFields,
        string? replacement)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(ruleName))
        {
            errors["ruleName"] = ["A rule name is required."];
        }
        else if (ruleName.Length > 200)
        {
            errors["ruleName"] = ["Rule names are limited to 200 characters."];
        }

        if (string.IsNullOrWhiteSpace(regexPattern))
        {
            if (patternType == PiiRulePatternType.Regex)
            {
                errors["regexPattern"] = ["A regular expression is required for regex rules."];
            }
        }
        else
        {
            // Any supplied pattern must compile regardless of the rule's pattern type: a stray,
            // un-compilable regex on a preset rule is rejected instead of silently ignored.
            try
            {
                _ = new Regex(regexPattern, RegexOptions.Compiled, TimeSpan.FromMilliseconds(500));
            }
            catch (ArgumentException exception)
            {
                errors["regexPattern"] = [$"The regular expression is invalid: {exception.Message}"];
            }
        }

        if (patternType == PiiRulePatternType.Preset && preset is null)
        {
            errors["preset"] = ["A preset pattern is required for preset rules."];
        }

        if (scope == PiiRuleScope.FieldName && NormalizeFields(targetFields).Length == 0)
        {
            errors["targetFields"] = ["Field-name rules must list at least one target field."];
        }

        if (!string.IsNullOrEmpty(replacement) && replacement.Length > 100)
        {
            errors["replacement"] = ["Replacements are limited to 100 characters."];
        }

        return errors;
    }

    private static string[] NormalizeFields(IReadOnlyList<string> fields) =>
        fields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => field.Trim())
            .ToArray();
}

public enum PiiRulePatternType
{
    /// <summary>Custom organization-specific regular expression.</summary>
    Regex = 0,

    /// <summary>Vetted preset matcher for a common PII type.</summary>
    Preset = 1
}

public enum PiiPresetPattern
{
    /// <summary>Email addresses.</summary>
    Email = 0,

    /// <summary>Phone numbers (10 to 15 digits, optional country code and separators).</summary>
    Phone = 1,

    /// <summary>National identifiers, including US social-security numbers.</summary>
    NationalId = 2,

    /// <summary>Payment card numbers (13 to 19 digits validated with the Luhn checksum).</summary>
    CreditCard = 3
}

public enum PiiRuleAction
{
    /// <summary>Replace the matched value with the rule replacement text.</summary>
    Redact = 0,

    /// <summary>Replace the matched value with a deterministic HMAC token joinable for investigations.</summary>
    Tokenize = 1
}

public enum PiiRuleScope
{
    /// <summary>Apply only to values under the listed field names.</summary>
    FieldName = 0,

    /// <summary>Apply to every scanned value (audit text fields and structured-log properties).</summary>
    Global = 1
}

/// <summary>
/// Vault entry mapping a deterministic PII token back to its encrypted original value for
/// authorized detokenization during investigations. Plain values are stored only in
/// <see cref="EncryptedValue"/>, encrypted with the platform encryption service.
/// </summary>
public sealed class PiiTokenVaultEntry : EntityBase
{
    public new Guid? TenantId { get; private set; }

    /// <summary>Scope discriminator: the tenant id in lowercase hex, or "global" for platform rules.</summary>
    public string ScopeKey { get; private set; } = "global";

    /// <summary>The deterministic HMAC token that replaced a PII value.</summary>
    public string Token { get; private set; } = string.Empty;

    /// <summary>AES-GCM encrypted original value (never plaintext at rest).</summary>
    public string EncryptedValue { get; private set; } = string.Empty;

    /// <summary>The rule that produced the token; null when recorded from an unknown rule.</summary>
    public Guid? SourceRuleId { get; private set; }

    public DateTime FirstSeenAt { get; private set; }

    public DateTime LastSeenAt { get; private set; }

    public long UseCount { get; private set; }

    private PiiTokenVaultEntry() { }

    public static PiiTokenVaultEntry Create(Guid? tenantId, string token, string encryptedValue, Guid? sourceRuleId)
    {
        var now = SystemClock.UtcNow;
        return new PiiTokenVaultEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ScopeKey = tenantId?.ToString("N") ?? "global",
            Token = token,
            EncryptedValue = encryptedValue,
            SourceRuleId = sourceRuleId,
            FirstSeenAt = now,
            LastSeenAt = now,
            UseCount = 1
        };
    }

    public void RecordReuse()
    {
        UseCount++;
        LastSeenAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Audit trail entry for redaction-pipeline applications: one row per audit record or structured-log
/// batch where at least one rule matched or the pipeline failed closed. Written only when redaction
/// actually happened, so no-rule deployments do not inflate storage.
/// </summary>
public sealed class PiiRedactionOperation : EntityBase
{
    public new Guid? TenantId { get; private set; }

    /// <summary>Where the pipeline ran: the central audit sink, the structured-log sink, or an explicit call.</summary>
    public PiiRedactionSource Source { get; private set; }

    /// <summary>Ids of the rules whose matchers fired, in application order.</summary>
    public Guid[] AppliedRuleIds { get; private set; } = Array.Empty<Guid>();

    /// <summary>Number of distinct values replaced with redaction replacements.</summary>
    public int RedactedValueCount { get; private set; }

    /// <summary>Number of distinct values replaced with deterministic tokens.</summary>
    public int TokenizedValueCount { get; private set; }

    /// <summary>True when the pipeline could not load or apply rules and redacted protectable fields wholesale.</summary>
    public bool FailClosed { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime AppliedAt { get; private set; }

    private PiiRedactionOperation() { }

    public static PiiRedactionOperation Create(
        Guid? tenantId,
        PiiRedactionSource source,
        IReadOnlyList<Guid> appliedRuleIds,
        int redactedValueCount,
        int tokenizedValueCount,
        bool failClosed,
        string? failureReason)
    {
        return new PiiRedactionOperation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Source = source,
            AppliedRuleIds = appliedRuleIds.ToArray(),
            RedactedValueCount = redactedValueCount,
            TokenizedValueCount = tokenizedValueCount,
            FailClosed = failClosed,
            FailureReason = failureReason,
            AppliedAt = SystemClock.UtcNow
        };
    }
}

public enum PiiRedactionSource
{
    /// <summary>Applied by the EF save interceptor over the central audit sink.</summary>
    AuditSink = 0,

    /// <summary>Applied by the Serilog structured-log file sink (global rules only).</summary>
    StructuredLog = 1,

    /// <summary>Applied by an explicit call (test run or manual redaction).</summary>
    Manual = 2
}
