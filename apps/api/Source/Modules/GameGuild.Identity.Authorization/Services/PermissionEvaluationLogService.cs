using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Default <see cref="IPermissionEvaluationLogService"/> implementation.
///     Emits one structured monitoring event per evaluation and fans the record out to
///     every registered <see cref="IPermissionEvaluationLogSink"/>. Sink failures never
///     throw and never change the authorization decision; they are counted in the
///     returned result and logged as errors so delivery problems stay observable.
/// </summary>
public sealed class PermissionEvaluationLogService(
    IEnumerable<IPermissionEvaluationLogSink> sinks,
    ILogger<PermissionEvaluationLogService> logger) : IPermissionEvaluationLogService
{
    public async Task<PermissionEvaluationLogResult> RecordAsync(
        PermissionEvaluationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (record.EvaluatedAtUtc == default)
        {
            record = record with { EvaluatedAtUtc = SystemClock.UtcNow };
        }

        LogMonitoringEvent(record);

        var sinkList = sinks as IReadOnlyCollection<IPermissionEvaluationLogSink> ?? sinks.ToArray();
        var failures = 0;
        var persisted = false;

        foreach (var sink in sinkList)
        {
            try
            {
                if (await sink.TryRecordAsync(record, cancellationToken).ConfigureAwait(false))
                {
                    persisted = true;
                }
                else
                {
                    failures++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                failures++;
                logger.LogError(
                    exception,
                    "Permission evaluation log sink {SinkType} failed to persist outcome {Outcome} for permission(s) {Permissions} on {ResourceType} {ResourceId}; the authorization decision is unaffected.",
                    sink.GetType().Name,
                    record.Outcome,
                    string.Join(",", record.RequiredPermissions),
                    record.ResourceType,
                    record.ResourceId);
            }
        }

        if (sinkList.Count == 0)
        {
            logger.LogWarning(
                "No permission evaluation log sink is configured; outcome {Outcome} for permission(s) {Permissions} on {ResourceType} {ResourceId} was not persisted durably.",
                record.Outcome,
                string.Join(",", record.RequiredPermissions),
                record.ResourceType,
                record.ResourceId);
        }
        else if (failures > 0 && !persisted)
        {
            logger.LogError(
                "Permission evaluation outcome {Outcome} for permission(s) {Permissions} on {ResourceType} {ResourceId} was NOT durably persisted ({FailureCount}/{SinkCount} sinks failed).",
                record.Outcome,
                string.Join(",", record.RequiredPermissions),
                record.ResourceType,
                record.ResourceId,
                failures,
                sinkList.Count);
        }

        return new PermissionEvaluationLogResult(persisted, sinkList.Count, failures);
    }

    private void LogMonitoringEvent(PermissionEvaluationRecord record)
    {
        var level = record.Outcome == PermissionEvaluationOutcome.Allow ? LogLevel.Debug : LogLevel.Warning;
        logger.Log(
            level,
            "Permission evaluation: outcome={Outcome} user={UserId} tenant={TenantId} resource={ResourceType}/{ResourceId} permissions=[{Permissions}] source={Source} operation={Operation} reason={Reason}",
            record.Outcome,
            record.UserId,
            record.TenantId,
            record.ResourceType,
            record.ResourceId,
            string.Join(",", record.RequiredPermissions),
            record.Source,
            record.Operation,
            record.Reason);
    }
}
