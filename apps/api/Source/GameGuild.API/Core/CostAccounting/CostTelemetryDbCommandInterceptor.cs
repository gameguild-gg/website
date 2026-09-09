using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.API.Core.CostAccounting;

internal sealed class CostTelemetryContext : ICostTelemetryRecorder
{
    private long _databaseTicks;
    private long _cacheOperations;

    public decimal DatabaseMilliseconds =>
        (decimal)TimeSpan.FromTicks(Interlocked.Read(ref _databaseTicks)).TotalMilliseconds;

    public long CacheOperations => Interlocked.Read(ref _cacheOperations);

    public void Reset()
    {
        Interlocked.Exchange(ref _databaseTicks, 0);
        Interlocked.Exchange(ref _cacheOperations, 0);
    }

    public void RecordDatabase(TimeSpan duration) => Interlocked.Add(ref _databaseTicks, duration.Ticks);
    public void RecordCacheOperation() => Interlocked.Increment(ref _cacheOperations);
}

internal sealed class CostTelemetryDbCommandInterceptor(CostTelemetryContext telemetry) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(result);

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        telemetry.RecordDatabase(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        telemetry.RecordDatabase(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        telemetry.RecordDatabase(eventData.Duration);
        return ValueTask.FromResult(result);
    }

    public override Task CommandFailedAsync(
        DbCommand command,
        CommandErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        telemetry.RecordDatabase(eventData.Duration);
        return Task.CompletedTask;
    }
}
