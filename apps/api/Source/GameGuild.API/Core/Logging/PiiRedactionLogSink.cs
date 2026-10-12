using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace GameGuild.API.Core.Logging;

/// <summary>
/// Structured-logging integration for the PII redaction pipeline (issue #192). Wraps the NDJSON file
/// sink and rewrites string-valued log-event properties using the platform-global redaction rules
/// (tenant-scoped rules do not apply here: generic log events carry no tenant context). When no rule
/// snapshot has been loaded yet — cold start, rule-store outage with no cache — the sink fails closed:
/// every non-allowlisted string property is replaced with the fail-closed marker rather than written raw.
/// </summary>
public sealed class PiiRedactionLogSink(
    ILogEventSink innerSink,
    IPiiRedactionRuleProvider ruleProvider,
    IPiiTokenSource tokenizer,
    ILogger<PiiRedactionLogSink> logger) : ILogEventSink, IDisposable
{
    /// <summary>
    /// Structural properties that carry no user-supplied payload; redacting them would break log
    /// consumption without adding privacy (values originate from code, not callers).
    /// </summary>
    private static readonly HashSet<string> AllowlistedProperties = new(StringComparer.Ordinal)
    {
        "Application",
        "Environment",
        "MachineName",
        "ProcessId",
        "ThreadId",
        "SourceContext",
        "RequestMethod",
        "RequestPath",
        "StatusCode",
        "RequestHost",
        "RequestScheme",
        "Elapsed",
        "Timestamp",
        "Level",
        "MessageTemplate",
        "Exception",
        "PiiRedaction"
    };

    private int failClosedLogged;

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        LogEvent? forwarded = null;
        try
        {
            var snapshot = ruleProvider.TryGetGlobalSnapshot();
            if (snapshot is null || snapshot.Rules.Count == 0)
            {
                if (snapshot is null)
                {
                    forwarded = FailClosedClone(logEvent);
                }
                else
                {
                    forwarded = null; // rules exist nowhere: pass through unchanged
                }
            }
            else
            {
                forwarded = RedactingClone(logEvent, snapshot.Rules);
            }
        }
        catch (Exception exception)
        {
            // Never let the redaction sink kill logging; fail closed and forward.
            logger.LogError(exception, "PII redaction sink failed while processing a log event; failing closed");
            forwarded = FailClosedClone(logEvent);
        }

        innerSink.Emit(forwarded ?? logEvent);
    }

    private LogEvent? RedactingClone(LogEvent logEvent, IReadOnlyList<CompiledPiiRule> rules)
    {
        var changed = false;
        var updatedProperties = new Dictionary<string, LogEventPropertyValue>(logEvent.Properties.Count, StringComparer.Ordinal);
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is ScalarValue { Value: string text })
            {
                if (IsAllowlisted(name, text))
                {
                    updatedProperties[name] = value;
                    continue;
                }

                var outcome = PiiRedactionEngine.ApplyToValue(text, name, rules, tokenizer);
                if (outcome.Changed && outcome.Value is not null)
                {
                    updatedProperties[name] = new ScalarValue(outcome.Value);
                    changed = true;
                    continue;
                }
            }

            updatedProperties[name] = value;
        }

        if (!changed)
        {
            return null;
        }

        updatedProperties["PiiRedaction"] = new ScalarValue("applied");
        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            ToProperties(updatedProperties));
    }

    private LogEvent FailClosedClone(LogEvent logEvent)
    {
        if (Interlocked.Exchange(ref failClosedLogged, 1) == 0)
        {
            logger.LogError(
                "PII redaction global rule snapshot is unavailable; structured-log sink is failing closed (non-structural string properties are redacted wholesale)");
        }

        var updatedProperties = new Dictionary<string, LogEventPropertyValue>(logEvent.Properties.Count, StringComparer.Ordinal);
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is ScalarValue { Value: string text } && !IsAllowlisted(name, text))
            {
                updatedProperties[name] = new ScalarValue(PiiRedactionFailClosed.Marker);
            }
            else
            {
                updatedProperties[name] = value;
            }
        }

        updatedProperties["PiiRedaction"] = new ScalarValue("fail-closed");
        return new LogEvent(
            logEvent.Timestamp,
            logEvent.Level,
            logEvent.Exception,
            logEvent.MessageTemplate,
            ToProperties(updatedProperties));
    }

    private static IEnumerable<LogEventProperty> ToProperties(Dictionary<string, LogEventPropertyValue> values) =>
        values.Select(pair => new LogEventProperty(pair.Key, pair.Value));

    private static bool IsAllowlisted(string name, string text) =>
        AllowlistedProperties.Contains(name) || string.IsNullOrEmpty(text);

    public void Dispose()
    {
        if (innerSink is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
