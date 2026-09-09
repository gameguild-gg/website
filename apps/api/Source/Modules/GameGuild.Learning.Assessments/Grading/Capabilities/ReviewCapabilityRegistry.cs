using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Capabilities;

public enum ExecutableComponentKind
{
    AssessmentTypeAdapter,
    ExecutionPolicy,
}

public sealed record ExecutableComponentDescriptor(
    ExecutableComponentKind Kind,
    string Key,
    string Version,
    IReadOnlySet<ReviewExecutionContext> Contexts);

public sealed record ReviewCapabilityDescriptor(
    ReviewMethod Method,
    string HandlerKey,
    string HandlerVersion,
    IReadOnlySet<ReviewExecutionContext> Contexts,
    string? ProviderKey = null);

public interface IReviewCapabilityRegistry
{
    void Register(ExecutableComponentDescriptor descriptor);
    void Register(ReviewCapabilityDescriptor descriptor);
    ExecutableComponentDescriptor? Resolve(
        ExecutableComponentKind kind,
        string key,
        string version,
        ReviewExecutionContext context);
    ReviewCapabilityDescriptor? ResolveReview(
        ReviewMethod method,
        string key,
        string version,
        ReviewExecutionContext context);
}

public interface IReviewCapabilityRegistration
{
    void Register(IReviewCapabilityRegistry registry);
}

public static class AssessmentExecutionPolicyCapability
{
    public const string Key = "grading.execution-policy";
    public const string Version = "1";
}

public sealed class CoreGradingCapabilityRegistration : IReviewCapabilityRegistration
{
    private static readonly IReadOnlySet<ReviewExecutionContext> Contexts =
        new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };

    public void Register(IReviewCapabilityRegistry registry) =>
        registry.Register(new ExecutableComponentDescriptor(
            ExecutableComponentKind.ExecutionPolicy,
            AssessmentExecutionPolicyCapability.Key,
            AssessmentExecutionPolicyCapability.Version,
            Contexts));
}

public sealed class ReviewCapabilityRegistry : IReviewCapabilityRegistry
{
    private readonly Dictionary<string, ExecutableComponentDescriptor> _components = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReviewCapabilityDescriptor> _reviews = new(StringComparer.Ordinal);

    public void Register(ExecutableComponentDescriptor descriptor)
    {
        var registryKey = ComponentKey(descriptor.Kind, descriptor.Key, descriptor.Version);
        RejectDivergent(_components, registryKey, descriptor);
        _components[registryKey] = descriptor;
    }

    public void Register(ReviewCapabilityDescriptor descriptor)
    {
        descriptor.Method.EnsureValid();

        var registryKey = ReviewKey(descriptor.Method, descriptor.HandlerKey, descriptor.HandlerVersion);
        RejectDivergent(_reviews, registryKey, descriptor);
        _reviews[registryKey] = descriptor;
    }

    public ExecutableComponentDescriptor? Resolve(
        ExecutableComponentKind kind,
        string key,
        string version,
        ReviewExecutionContext context) =>
        _components.TryGetValue(ComponentKey(kind, key, version), out var descriptor) && descriptor.Contexts.Contains(context)
            ? descriptor
            : null;

    public ReviewCapabilityDescriptor? ResolveReview(
        ReviewMethod method,
        string key,
        string version,
        ReviewExecutionContext context) =>
        _reviews.TryGetValue(ReviewKey(method, key, version), out var descriptor) && descriptor.Contexts.Contains(context)
            ? descriptor
            : null;

    private static string ComponentKey(ExecutableComponentKind kind, string key, string version) => $"{kind}:{key}@{version}";
    private static string ReviewKey(ReviewMethod method, string key, string version) => $"{method}:{key}@{version}";

    private static void RejectDivergent<T>(Dictionary<string, T> registry, string key, T descriptor)
    {
        if (registry.TryGetValue(key, out var current) && !EqualityComparer<T>.Default.Equals(current, descriptor))
        {
            throw new InvalidOperationException($"Capability {key} is already registered with a different descriptor.");
        }
    }
}
