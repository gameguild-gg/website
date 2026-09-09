using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Capabilities;

public sealed class AssessmentTypeAdapterResolver(
    IReviewCapabilityRegistry capabilities,
    IEnumerable<IAssessmentTypeAdapter> adapters) : IAssessmentTypeAdapterResolver
{
    public IAssessmentTypeAdapter ResolveForAuthoring(ProgramContentType programContentType) =>
        ResolveSingle(
            adapters,
            candidate => candidate.ProgramContentType == programContentType &&
                         candidate.IsCurrentForAuthoring,
            $"current assessment type adapter for {programContentType}");

    public IAssessmentTypeAdapter Resolve(
        string contentType,
        string key,
        string version,
        ReviewExecutionContext context)
    {
        if (capabilities.Resolve(ExecutableComponentKind.AssessmentTypeAdapter, key, version, context) is null)
        {
            throw Unavailable($"assessment type adapter {key}@{version}", context);
        }

        return ResolveSingle(
            adapters,
            candidate => Same(candidate.ContentType, contentType) &&
                         Same(candidate.Key, key) &&
                         Same(candidate.Version, version) &&
                         candidate.Contexts.Contains(context),
            $"assessment type adapter {key}@{version} for {contentType}");
    }

    internal static T ResolveSingle<T>(IEnumerable<T> candidates, Func<T, bool> predicate, string label)
    {
        var matches = candidates.Where(predicate).Take(2).ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException($"The manifest references an unregistered {label}."),
            _ => throw new InvalidOperationException($"More than one implementation is registered for {label}."),
        };
    }

    internal static bool Same(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);

    internal static InvalidOperationException Unavailable(string component, ReviewExecutionContext context) =>
        new($"The manifest references {component}, which is unavailable for {context}.");
}

public sealed class AssessmentExecutionPolicyResolver(IReviewCapabilityRegistry capabilities)
    : IAssessmentExecutionPolicyResolver
{
    public void Resolve(string key, string version, ReviewExecutionContext context)
    {
        if (capabilities.Resolve(ExecutableComponentKind.ExecutionPolicy, key, version, context) is null)
        {
            throw AssessmentTypeAdapterResolver.Unavailable($"execution policy {key}@{version}", context);
        }
    }
}

public sealed class ReviewStageHandlerResolver(
    IReviewCapabilityRegistry capabilities,
    IEnumerable<IReviewStageHandler> handlers) : IReviewStageHandlerResolver
{
    public IReviewStageHandler Resolve(
        ReviewMethod method,
        string key,
        string version,
        ReviewExecutionContext context)
    {
        if (capabilities.ResolveReview(method, key, version, context) is null)
        {
            throw AssessmentTypeAdapterResolver.Unavailable(
                $"review handler {method}:{key}@{version}",
                context);
        }

        return AssessmentTypeAdapterResolver.ResolveSingle(
            handlers,
            candidate => candidate.Method == method &&
                         AssessmentTypeAdapterResolver.Same(candidate.Key, key) &&
                         AssessmentTypeAdapterResolver.Same(candidate.Version, version) &&
                         candidate.Contexts.Contains(context),
            $"review handler {method}:{key}@{version}");
    }
}
