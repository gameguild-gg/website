using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Assessments.Grading.Persistence;

public interface IAssessmentExecutableVersionPreflight
{
    Task ValidateAsync(CancellationToken cancellationToken = default);
}

public sealed class AssessmentExecutableVersionPreflight(
    IApplicationDbContext context,
    IAssessmentTypeAdapterResolver adapters,
    IAssessmentExecutionPolicyResolver executionPolicies,
    IReviewStageHandlerResolver stageHandlers) : IAssessmentExecutableVersionPreflight
{
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var requirements = new Dictionary<Guid, HashSet<ReviewExecutionContext>>();
        var retainedRevisionIds = await context.Set<AssessmentDefinitionRevision>()
            .AsNoTracking()
            .Select(revision => revision.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var revisionId in retainedRevisionIds)
        {
            AddRequirement(requirements, revisionId, ReviewExecutionContext.AuthorTest);
        }

        var publishedRevisionIds = await context.Set<Assessment>()
            .AsNoTracking()
            .Where(assessment => assessment.DeletedAt == null && assessment.PublishedDefinitionRevisionId.HasValue)
            .Select(assessment => assessment.PublishedDefinitionRevisionId!.Value)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var revisionId in publishedRevisionIds)
        {
            AddRequirement(requirements, revisionId, ReviewExecutionContext.OfficialSubmission);
        }

        var executions = await context.Set<GradingExecution>()
            .AsNoTracking()
            .Where(execution => execution.DeletedAt == null &&
                (execution.ExecutionContext == ReviewExecutionContext.OfficialSubmission ||
                 execution.Status != PersistedGradingExecutionStatus.Completed &&
                 execution.Status != PersistedGradingExecutionStatus.Failed))
            .Select(execution => new { execution.DefinitionRevisionId, execution.ExecutionContext })
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var execution in executions)
        {
            AddRequirement(requirements, execution.DefinitionRevisionId, execution.ExecutionContext);
        }

        if (requirements.Count == 0) return;
        var revisionIds = requirements.Keys.ToArray();
        var revisions = await context.Set<AssessmentDefinitionRevision>()
            .AsNoTracking()
            .Where(revision => revisionIds.Contains(revision.Id))
            .ToDictionaryAsync(revision => revision.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var requirement in requirements)
        {
            if (!revisions.TryGetValue(requirement.Key, out var revision))
                throw new InvalidOperationException($"Executable assessment revision {requirement.Key} is missing.");
            var snapshot = AssessmentDefinitionRevisionReader.ReadValidated(revision);
            foreach (var contextKind in requirement.Value)
            {
                ResolveSnapshot(snapshot, contextKind);
            }
        }
    }

    private void ResolveSnapshot(AssessmentExecutionSnapshotV1 snapshot, ReviewExecutionContext contextKind)
    {
        foreach (var item in snapshot.Manifest.Items)
        {
            adapters.Resolve(snapshot.AuthoringSource.ContentType, item.AdapterKey, item.AdapterVersion, contextKind);
        }

        foreach (var stage in snapshot.Manifest.Stages)
        {
            stageHandlers.Resolve(stage.Method, stage.HandlerKey, stage.HandlerVersion, contextKind);
        }

        foreach (var policy in snapshot.Manifest.Policies)
        {
            executionPolicies.Resolve(policy.PolicyKey, policy.PolicyVersion, contextKind);
        }
    }

    private static void AddRequirement(
        IDictionary<Guid, HashSet<ReviewExecutionContext>> requirements,
        Guid revisionId,
        ReviewExecutionContext contextKind)
    {
        if (!requirements.TryGetValue(revisionId, out var contexts))
        {
            contexts = [];
            requirements.Add(revisionId, contexts);
        }
        contexts.Add(contextKind);
    }
}

public sealed class AssessmentExecutableVersionPreflightHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<AssessmentExecutableVersionPreflightHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var preflight = scope.ServiceProvider.GetRequiredService<IAssessmentExecutableVersionPreflight>();
        await preflight.ValidateAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Assessment executable-version preflight completed successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
