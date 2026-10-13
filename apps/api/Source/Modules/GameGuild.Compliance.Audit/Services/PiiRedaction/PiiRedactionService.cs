using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using GameGuild.Identity.Authentication;

namespace GameGuild.Compliance.Audit;

/// <summary>Summary of one pipeline application.</summary>
/// <param name="Changed">True when at least one value was transformed or fail-closed redaction was applied.</param>
/// <param name="FailClosed">True when rules could not be loaded/applied and protectable fields were redacted wholesale.</param>
/// <param name="FailureReason">Reason captured when <paramref name="FailClosed"/> is true.</param>
/// <param name="RedactedValueCount">Values replaced with redaction replacements.</param>
/// <param name="TokenizedValueCount">Values replaced with deterministic tokens.</param>
/// <param name="AppliedRuleIds">Rules whose matchers fired, in application order.</param>
public sealed record PiiRedactionSummary(
    bool Changed = false,
    bool FailClosed = false,
    string? FailureReason = null,
    int RedactedValueCount = 0,
    int TokenizedValueCount = 0,
    IReadOnlyList<Guid> AppliedRuleIds = null!);

/// <summary>Text-redaction result: the transformed content plus the application summary.</summary>
public sealed record PiiRedactionTextResult(string RedactedContent, PiiRedactionSummary Summary);

/// <summary>
/// The PII redaction pipeline (issue #192): applies enabled <see cref="PiiRedactionRule"/>s to audit
/// records and structured-log payloads before persistence. The pipeline is fail-closed: when rules
/// cannot be loaded or an application error occurs, protectable fields are redacted wholesale and the
/// failure is logged — raw values are never silently passed through.
/// </summary>
public interface IPiiRedactionService
{
    /// <summary>
    /// Applies tenant + global rules to a persisted audit record in place. Vault entries and a
    /// <see cref="PiiRedactionOperation"/> audit-trail row are staged on <paramref name="context"/> so a
    /// single SaveChanges persists record, vault, and trail atomically.
    /// </summary>
    Task<PiiRedactionSummary> ApplyToAuditLogAsync(AuditLog entry, IApplicationDbContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a sanitized copy of a request used for durable structured-log fallbacks and spool files:
    /// protectable text fields are redacted with the fail-closed marker when rules are unavailable.
    /// Never returns the original references unmodified.
    /// </summary>
    Task<CreateAuditLogRequest> RedactRequestForDurableFallbackAsync(CreateAuditLogRequest request, CancellationToken cancellationToken);

    /// <summary>Applies tenant + global rules to free text (manual redaction and the test endpoint).</summary>
    Task<PiiRedactionTextResult> RedactTextAsync(string content, Guid? tenantId, PiiRedactionSource source, IApplicationDbContext? context, CancellationToken cancellationToken);
}

/// <summary>Replacement used when the pipeline must fail closed.</summary>
public static class PiiRedactionFailClosed
{
    public const string Marker = "[PII-REDACTED-FAILEDCLOSED]";
}

/// <summary>Default pipeline implementation. Scoped: vault encryption uses the platform scoped encryption service.</summary>
public sealed class PiiRedactionService(
    IPiiRedactionRuleProvider ruleProvider,
    IPiiTokenSource tokenizer,
    IEncryptionService encryptionService,
    ILogger<PiiRedactionService> logger) : IPiiRedactionService
{
    private static readonly JsonSerializerOptions MetadataJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PiiRedactionSummary> ApplyToAuditLogAsync(AuditLog entry, IApplicationDbContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        PiiRuleSet rules;
        try
        {
            rules = await ruleProvider.GetTenantRuleSetAsync(entry.TenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (PiiRedactionUnavailableException exception)
        {
            logger.LogError(
                exception,
                "PII redaction rule set unavailable for tenant {TenantId}; failing closed and redacting audit record {AuditLogId} protectable fields",
                entry.TenantId,
                entry.Id);
            FailClosedRedact(entry);
            StageOperation(context, entry.TenantId, PiiRedactionSource.AuditSink, [], 0, 0, failClosed: true, exception.InnerException!.Message);
            return new PiiRedactionSummary(Changed: true, FailClosed: true, FailureReason: exception.InnerException!.Message, RedactedValueCount: 1);
        }

        return ApplyWithRules(entry, context, rules, PiiRedactionSource.AuditSink);
    }

    public async Task<CreateAuditLogRequest> RedactRequestForDurableFallbackAsync(CreateAuditLogRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        PiiRuleSet rules;
        try
        {
            rules = await ruleProvider.GetTenantRuleSetAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (PiiRedactionUnavailableException exception)
        {
            logger.LogError(
                exception,
                "PII redaction rule set unavailable while sanitizing a durable fallback payload for action {ActionType}; using the fail-closed marker",
                request.ActionType);
            return new CreateAuditLogRequest
            {
                ActionType = request.ActionType,
                ResourceType = request.ResourceType,
                ResourceId = PiiRedactionFailClosed.Marker,
                UserId = request.UserId,
                TenantId = request.TenantId,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent is null ? null : PiiRedactionFailClosed.Marker,
                SessionId = request.SessionId,
                Description = request.Description is null ? null : PiiRedactionFailClosed.Marker,
                Metadata = new { PiiRedaction = PiiRedactionFailClosed.Marker },
                Success = request.Success,
                ErrorMessage = request.ErrorMessage is null ? null : PiiRedactionFailClosed.Marker,
                RiskLevel = request.RiskLevel,
                Category = request.Category,
                CorrelationId = request.CorrelationId
            };
        }

        var sanitized = new CreateAuditLogRequest
        {
            ActionType = request.ActionType,
            ResourceType = request.ResourceType,
            ResourceId = Apply(request.ResourceId, nameof(AuditLog.ResourceId), rules),
            UserId = request.UserId,
            TenantId = request.TenantId,
            IpAddress = request.IpAddress,
            UserAgent = Apply(request.UserAgent, nameof(AuditLog.UserAgent), rules),
            SessionId = request.SessionId,
            Description = Apply(request.Description, nameof(AuditLog.Description), rules),
            Metadata = RedactMetadata(request.Metadata, rules),
            Success = request.Success,
            ErrorMessage = Apply(request.ErrorMessage, nameof(AuditLog.ErrorMessage), rules),
            RiskLevel = request.RiskLevel,
            Category = request.Category,
            CorrelationId = request.CorrelationId
        };
        return sanitized;

        string? Apply(string? value, string field, PiiRuleSet ruleSet) =>
            PiiRedactionEngine.ApplyToValue(value, field, ruleSet.Rules, tokenizer).Value ?? value;
    }

    public async Task<PiiRedactionTextResult> RedactTextAsync(string content, Guid? tenantId, PiiRedactionSource source, IApplicationDbContext? context, CancellationToken cancellationToken)
    {
        PiiRuleSet rules;
        try
        {
            rules = await ruleProvider.GetTenantRuleSetAsync(tenantId, cancellationToken).ConfigureAwait(false);
        }
        catch (PiiRedactionUnavailableException exception)
        {
            logger.LogError(exception, "PII redaction rule set unavailable for manual redaction in scope {Scope}", tenantId);
            var failClosedSummary = new PiiRedactionSummary(Changed: true, FailClosed: true, FailureReason: exception.InnerException!.Message, RedactedValueCount: 1);
            return new PiiRedactionTextResult(PiiRedactionFailClosed.Marker, failClosedSummary);
        }

        if (context is not null)
        {
            var counter = new ApplicationCounter();
            var transformed = ApplyValue(content, "Text", rules, counter, context, tenantId) ?? content;
            if (counter.Changed)
            {
                StageOperation(context, tenantId, source, counter.AppliedRuleIds.ToList(), counter.Redacted, counter.Tokenized, failClosed: false, null);
            }
            var summary = new PiiRedactionSummary(counter.Changed, false, null, counter.Redacted, counter.Tokenized, counter.AppliedRuleIds.ToList());
            return new PiiRedactionTextResult(transformed, summary);
        }

        var outcome = PiiRedactionEngine.ApplyToValue(content, "Text", rules.Rules, tokenizer);
        var standalone = new PiiRedactionSummary(
            outcome.Changed,
            false,
            null,
            outcome.Changed && outcome.Action == PiiRuleAction.Redact ? outcome.MatchCount : 0,
            outcome.Changed && outcome.Action == PiiRuleAction.Tokenize ? outcome.MatchCount : 0,
            outcome.AppliedRuleId is null ? [] : [outcome.AppliedRuleId.Value]);
        return new PiiRedactionTextResult(outcome.Value ?? content, standalone);
    }

    private PiiRedactionSummary ApplyWithRules(AuditLog entry, IApplicationDbContext context, PiiRuleSet rules, PiiRedactionSource source)
    {
        var counter = new ApplicationCounter();

        entry.Description = ApplyValue(entry.Description, nameof(AuditLog.Description), rules, counter, context, entry.TenantId);
        entry.ErrorMessage = ApplyValue(entry.ErrorMessage, nameof(AuditLog.ErrorMessage), rules, counter, context, entry.TenantId);
        entry.UserAgent = ApplyValue(entry.UserAgent, nameof(AuditLog.UserAgent), rules, counter, context, entry.TenantId);
        entry.ResourceId = ApplyValue(entry.ResourceId, nameof(AuditLog.ResourceId), rules, counter, context, entry.TenantId);
        entry.Metadata = ApplyMetadata(entry.Metadata, rules, counter, context, entry.TenantId);

        if (counter.Changed)
        {
            StageOperation(context, entry.TenantId, source, counter.AppliedRuleIds.ToList(), counter.Redacted, counter.Tokenized, failClosed: false, null);
        }

        return new PiiRedactionSummary(counter.Changed, false, null, counter.Redacted, counter.Tokenized, counter.AppliedRuleIds.ToList());
    }

    private string? ApplyValue(string? value, string field, PiiRuleSet rules, ApplicationCounter counter, IApplicationDbContext context, Guid? tenantId)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var outcome = PiiRedactionEngine.ApplyToValue(value, field, rules.Rules, tokenizer);
        if (!outcome.Changed)
        {
            return value;
        }

        counter.Record(outcome);
        if (outcome.Action == PiiRuleAction.Tokenize)
        {
            StageTokenizedMatches(context, tenantId, outcome);
        }

        return outcome.Value;
    }

    private string? ApplyMetadata(string? metadata, PiiRuleSet rules, ApplicationCounter counter, IApplicationDbContext context, Guid? tenantId)
    {
        if (string.IsNullOrEmpty(metadata))
        {
            return metadata;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(metadata);
        }
        catch (JsonException)
        {
            // Not JSON: treat as opaque text under the Metadata field name.
            return ApplyValue(metadata, nameof(AuditLog.Metadata), rules, counter, context, tenantId);
        }

        using (document)
        {
            var rewritten = RewriteJsonNode(JsonSerializer.SerializeToNode(document.RootElement), nameof(AuditLog.Metadata), rules, counter, context, tenantId);
            return rewritten is null ? metadata : JsonSerializer.Serialize(rewritten, MetadataJsonOptions);
        }
    }

    private JsonNode? RewriteJsonNode(JsonNode? node, string fieldName, PiiRuleSet rules, ApplicationCounter counter, IApplicationDbContext context, Guid? tenantId)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue value when value.TryGetValue<string>(out var text):
            {
                var outcome = PiiRedactionEngine.ApplyToValue(text, fieldName, rules.Rules, tokenizer);
                if (!outcome.Changed)
                {
                    return value;
                }
                counter.Record(outcome);
                if (outcome.Action == PiiRuleAction.Tokenize)
                {
                    StageTokenizedMatches(context, tenantId, outcome);
                }
                return JsonValue.Create(outcome.Value);
            }
            case JsonArray array:
            {
                for (var index = 0; index < array.Count; index++)
                {
                    array[index] = RewriteJsonNode(array[index], fieldName, rules, counter, context, tenantId);
                }
                return array;
            }
            case JsonObject map:
            {
                foreach (var key in map.Select(pair => pair.Key).ToList())
                {
                    map[key] = RewriteJsonNode(map[key], key, rules, counter, context, tenantId);
                }
                return map;
            }
            default:
                return node;
        }
    }

    private object? RedactMetadata(object? metadata, PiiRuleSet rules)
    {
        if (metadata is null)
        {
            return null;
        }

        try
        {
            var node = JsonSerializer.SerializeToNode(metadata, MetadataJsonOptions);
            var rewritten = RewriteJsonNodePlain(node, rules);
            return rewritten;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new { PiiRedaction = PiiRedactionFailClosed.Marker };
        }
    }

    private JsonNode? RewriteJsonNodePlain(JsonNode? node, PiiRuleSet rules)
    {
        switch (node)
        {
            case null:
                return null;
            case JsonValue value when value.TryGetValue<string>(out var text):
            {
                var outcome = PiiRedactionEngine.ApplyToValue(text, "Metadata", rules.Rules, tokenizer);
                return outcome.Changed && outcome.Value is not null ? JsonValue.Create(outcome.Value) : value;
            }
            case JsonArray array:
            {
                for (var index = 0; index < array.Count; index++)
                {
                    array[index] = RewriteJsonNodePlain(array[index], rules);
                }
                return array;
            }
            case JsonObject map:
            {
                foreach (var key in map.Select(pair => pair.Key).ToList())
                {
                    map[key] = RewriteJsonNodePlain(map[key], rules);
                }
                return map;
            }
            default:
                return node;
        }
    }

    private static void FailClosedRedact(AuditLog entry)
    {
        entry.Description = entry.Description is null ? null : PiiRedactionFailClosed.Marker;
        entry.ErrorMessage = entry.ErrorMessage is null ? null : PiiRedactionFailClosed.Marker;
        entry.UserAgent = entry.UserAgent is null ? null : PiiRedactionFailClosed.Marker;
        entry.ResourceId = entry.ResourceId is null ? null : PiiRedactionFailClosed.Marker;
        entry.Metadata = entry.Metadata is null ? null : JsonSerializer.Serialize(new { PiiRedaction = PiiRedactionFailClosed.Marker });
    }

    /// <summary>
    /// Stages one vault row per tokenized match: the deterministic token maps back to exactly the
    /// original PII value it replaced (not the whole surrounding field), so detokenization during an
    /// investigation recovers the precise PII value.
    /// </summary>
    private void StageTokenizedMatches(IApplicationDbContext context, Guid? tenantId, PiiValueOutcome outcome)
    {
        foreach (var match in outcome.Tokens)
        {
            StageVaultEntry(context, tenantId, match.Original, match.Token, outcome.AppliedRuleId);
        }
    }

    /// <summary>Stages the token vault row for a tokenized original, encrypted with the platform key.</summary>
    private void StageVaultEntry(IApplicationDbContext context, Guid? tenantId, string originalValue, string token, Guid? ruleId)
    {
        try
        {
            var encrypted = encryptionService.Encrypt(originalValue);

            var scopeKey = PiiRedactionRuleProvider.ScopeKey(tenantId);
            var existing = context.Set<PiiTokenVaultEntry>().Local
                .FirstOrDefault(entry => entry.Token == token && entry.ScopeKey == scopeKey);
            if (existing is not null)
            {
                existing.RecordReuse();
                return;
            }

            context.Set<PiiTokenVaultEntry>().Add(PiiTokenVaultEntry.Create(tenantId, token, encrypted, ruleId));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // Encryption not configured (or key invalid): tokens stay deterministic and joinable, but
            // reversibility for this value is unavailable. The pipeline must not drop the audit record.
            logger.LogWarning(
                exception,
                "PII token vault write skipped because platform encryption is unavailable; tokens remain deterministic but not reversible");
        }
    }

    private static void StageOperation(
        IApplicationDbContext context,
        Guid? tenantId,
        PiiRedactionSource source,
        IReadOnlyList<Guid> appliedRuleIds,
        int redacted,
        int tokenized,
        bool failClosed,
        string? failureReason)
    {
        context.Set<PiiRedactionOperation>().Add(PiiRedactionOperation.Create(tenantId, source, appliedRuleIds, redacted, tokenized, failClosed, failureReason));
    }

    private sealed class ApplicationCounter
    {
        private readonly List<Guid> _appliedRuleIds = [];

        public int Redacted { get; private set; }

        public int Tokenized { get; private set; }

        public bool Changed => _appliedRuleIds.Count > 0;

        public IReadOnlyList<Guid> AppliedRuleIds => _appliedRuleIds;

        public void Record(PiiValueOutcome outcome)
        {
            if (outcome.Action == PiiRuleAction.Tokenize)
            {
                Tokenized += outcome.MatchCount;
            }
            else
            {
                Redacted += outcome.MatchCount;
            }

            if (outcome.AppliedRuleId is { } ruleId && !_appliedRuleIds.Contains(ruleId))
            {
                _appliedRuleIds.Add(ruleId);
            }
        }
    }
}
