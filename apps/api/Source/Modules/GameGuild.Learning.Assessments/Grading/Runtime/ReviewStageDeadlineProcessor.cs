using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments.Grading.Runtime;

public interface IReviewStageDeadlineProcessor
{
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resumes generic review stages whose evidence deadline elapsed. The stage handler remains
/// responsible for deciding whether available evidence is sufficient.
/// </summary>
public sealed class ReviewStageDeadlineProcessor(
    IApplicationDbContext context,
    IGradingExecutionOrchestrator orchestrator,
    ILogger<ReviewStageDeadlineProcessor> logger) : IReviewStageDeadlineProcessor
{
    private const int BatchSize = 25;

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = SystemClock.UtcNow;
        var executionIds = await (
                from stage in context.Set<ReviewStage>().AsNoTracking()
                join round in context.Set<GradeRound>().AsNoTracking() on stage.GradeRoundId equals round.Id
                where stage.Status == PersistedReviewStageStatus.AwaitingEvidence &&
                      stage.EvidenceDeadlineAt.HasValue &&
                      stage.EvidenceDeadlineAt <= now &&
                      round.Status == PersistedGradeRoundStatus.AwaitingEvidence
                orderby stage.EvidenceDeadlineAt, stage.Id
                select round.GradingExecutionId)
            .Distinct()
            .Take(BatchSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var processed = 0;
        foreach (var executionId in executionIds)
        {
            try
            {
                await orchestrator.ResumeAsync(executionId, cancellationToken).ConfigureAwait(false);
                processed++;
                GradingOperationalMetrics.RecordDeadlineResume(succeeded: true);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Failed to resume grading execution {ExecutionId} after a review deadline",
                    executionId);
                GradingOperationalMetrics.RecordDeadlineResume(succeeded: false);
            }
        }

        return processed;
    }
}

public sealed class ReviewStageDeadlineBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewStageDeadlineBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IReviewStageDeadlineProcessor>();
                var processed = await processor.ProcessDueAsync(stoppingToken).ConfigureAwait(false);
                if (processed == 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Review-stage deadline processing cycle failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
