using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace GameGuild.Learning.Courses;

public sealed class CourseContentAccessRuleEvaluator(
    ICourseAccessEvaluator courseAccessEvaluator) : IRuleEvaluator
{
    private const string PublicOutlineAccess = "PublicOutline";
    private const string LearnerAccess = "Learner";
    private const string ManageAccess = "Manage";

    public string RuleType => RuleTypes.CourseContentAccess;

    public async Task<RuleEvaluationResult> EvaluateAsync(
        AuthorizationHandlerContext context,
        RuleParameters parameters,
        CancellationToken cancellationToken = default)
    {
        if (context.Resource is not Program program)
        {
            return RuleEvaluationResult.Fail("Course content access requires a Program resource");
        }

        return parameters.GetString("access") switch
        {
            PublicOutlineAccess => EvaluatePublicOutline(program),
            LearnerAccess => await EvaluateCapabilityAsync(
                program,
                CourseCapability.Learn,
                "User is not actively enrolled in the course",
                cancellationToken).ConfigureAwait(false),
            ManageAccess => await EvaluateManagementCapabilityAsync(
                program,
                parameters,
                cancellationToken).ConfigureAwait(false),
            _ => RuleEvaluationResult.Fail("Unknown course content access mode")
        };
    }

    private static RuleEvaluationResult EvaluatePublicOutline(Program program)
    {
        return program.Status == ContentStatus.Published
               && program.Visibility == ContentVisibility.Public
            ? RuleEvaluationResult.Success()
            : RuleEvaluationResult.Fail("Course is not published for public access");
    }

    private async Task<RuleEvaluationResult> EvaluateCapabilityAsync(
        Program program,
        CourseCapability capability,
        string failureReason,
        CancellationToken cancellationToken)
    {
        var capabilities = await courseAccessEvaluator
            .GetCapabilitiesAsync(program, cancellationToken)
            .ConfigureAwait(false);

        return capabilities.Has(capability)
            ? RuleEvaluationResult.Success()
            : RuleEvaluationResult.Fail(failureReason);
    }

    private async Task<RuleEvaluationResult> EvaluateManagementCapabilityAsync(
        Program program,
        RuleParameters parameters,
        CancellationToken cancellationToken)
    {
        var configured = parameters.GetString("capability");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return await EvaluateCapabilityAsync(
                program,
                CourseCapability.Edit,
                "The requested course capability was not granted",
                cancellationToken).ConfigureAwait(false);
        }

        if (!Enum.TryParse<CourseCapability>(configured, ignoreCase: true, out var capability) ||
            capability is not (CourseCapability.Edit or CourseCapability.Publish or CourseCapability.StaffReview))
        {
            return RuleEvaluationResult.Fail("Unknown course management capability");
        }

        return await EvaluateCapabilityAsync(
            program,
            capability,
            "The requested course capability was not granted",
            cancellationToken).ConfigureAwait(false);
    }
}
