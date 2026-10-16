using GameGuild.CQRS;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Executes endpoint commands directly against the service doubles used by controller tests.
/// This keeps those tests at the controller boundary while matching the production CQRS wiring.
/// </summary>
internal sealed class AssessmentEndpointTestSender(
    IAssessmentService? assessmentService = null,
    IGroupSetService? groupSetService = null,
    IPeerReviewAssignmentService? peerReviewService = null,
    IRubricService? rubricService = null) : ISender
{
    public List<object> SentRequests { get; } = [];

    public async Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        var response = await DispatchAsync(request, cancellationToken).ConfigureAwait(false);
        return response is TResponse typed
            ? typed
            : throw new InvalidOperationException(
                $"Command {request.GetType().Name} returned an unexpected response type.");
    }

    public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest =>
        _ = await DispatchAsync(request, cancellationToken).ConfigureAwait(false);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
        DispatchAsync(request, cancellationToken);

    private async Task<object?> DispatchAsync(object request, CancellationToken cancellationToken)
    {
        SentRequests.Add(request);
        return request switch
        {
            CreateAssessmentEndpointCommand command when assessmentService is not null =>
                await assessmentService.CreateAssessmentAsync(command.Request).ConfigureAwait(false),
            CreateAssessmentGroupEndpointCommand command when assessmentService is not null =>
                await assessmentService.CreateAssessmentGroupAsync(command.Request).ConfigureAwait(false),
            UpdateAssessmentGroupEndpointCommand command when assessmentService is not null =>
                await assessmentService.UpdateAssessmentGroupAsync(command.GroupId, command.Request).ConfigureAwait(false),
            DeleteAssessmentGroupEndpointCommand command when assessmentService is not null =>
                await assessmentService.DeleteAssessmentGroupAsync(command.GroupId).ConfigureAwait(false),
            UpdateAssessmentEndpointCommand command when assessmentService is not null =>
                await assessmentService.UpdateAssessmentAsync(command.AssessmentId, command.Request).ConfigureAwait(false),
            AssignAssessmentToGroupEndpointCommand command when assessmentService is not null =>
                await assessmentService.AssignAssessmentToGroupAsync(command.AssessmentId, command.Request).ConfigureAwait(false),
            LinkInteractiveVideoCueEndpointCommand command when assessmentService is not null =>
                await assessmentService.LinkInteractiveVideoCueAsync(command.AssessmentId, command.Request).ConfigureAwait(false),
            UnlinkInteractiveVideoCueEndpointCommand command when assessmentService is not null =>
                await assessmentService.UnlinkInteractiveVideoCueAsync(command.AssessmentId, command.CueId).ConfigureAwait(false),
            DeleteAssessmentEndpointCommand command when assessmentService is not null =>
                await assessmentService.DeleteAssessmentAsync(command.AssessmentId).ConfigureAwait(false),
            RestoreAssessmentEndpointCommand command when assessmentService is not null =>
                await assessmentService.RestoreAssessmentAsync(command.AssessmentId, cancellationToken).ConfigureAwait(false),
            StartAssessmentSubmissionEndpointCommand command when assessmentService is not null =>
                await assessmentService.StartSubmissionAsync(command.AssessmentId, command.EnrollmentId, command.UserId).ConfigureAwait(false),
            SubmitAssessmentEndpointCommand command when assessmentService is not null =>
                await assessmentService.SubmitAsync(command.SubmissionId, command.Request).ConfigureAwait(false),

            CreateCourseGroupSetEndpointCommand command when groupSetService is not null =>
                await groupSetService.CreateGroupSetAsync(command.CourseId, command.Name).ConfigureAwait(false),
            CreateCourseGroupEndpointCommand command when groupSetService is not null =>
                await groupSetService.CreateGroupAsync(command.CourseId, command.GroupSetId, command.Name, command.Capacity).ConfigureAwait(false),
            JoinCourseGroupEndpointCommand command when groupSetService is not null =>
                await groupSetService.JoinAsync(command.CourseId, command.GroupId, command.UserId).ConfigureAwait(false),
            LeaveCourseGroupEndpointCommand command when groupSetService is not null =>
                await groupSetService.LeaveAsync(command.CourseId, command.GroupId, command.UserId).ConfigureAwait(false),
            AddCourseGroupMemberEndpointCommand command when groupSetService is not null =>
                await groupSetService.AddMemberAsync(command.CourseId, command.GroupId, command.UserId).ConfigureAwait(false),
            RemoveCourseGroupMemberEndpointCommand command when groupSetService is not null =>
                await groupSetService.RemoveMemberAsync(command.CourseId, command.GroupId, command.UserId).ConfigureAwait(false),

            ClaimPeerReviewEndpointCommand command when peerReviewService is not null =>
                await peerReviewService.ClaimAsync(command.AssessmentId, command.UserId).ConfigureAwait(false),
            ClaimAuthorTestPeerReviewEndpointCommand command when peerReviewService is not null =>
                await peerReviewService.ClaimAuthorTestAsync(
                    command.TestRunId,
                    command.ReviewerTestRunSubjectId,
                    command.ActorUserId).ConfigureAwait(false),
            SubmitPeerReviewEndpointCommand command when peerReviewService is not null =>
                await peerReviewService.SubmitReviewAsync(
                    command.ReviewId,
                    command.ActorUserId,
                    command.Score,
                    command.Feedback,
                    command.RubricScores).ConfigureAwait(false),

            PutAssessmentRubricEndpointCommand command when rubricService is not null =>
                await rubricService.SaveAsync(command.AssessmentId, command.Request).ConfigureAwait(false),
            DeleteAssessmentRubricEndpointCommand command when rubricService is not null =>
                await rubricService.DeleteAsync(command.AssessmentId).ConfigureAwait(false),

            _ => throw new NotSupportedException(
                $"No endpoint test service is configured for {request.GetType().Name}.")
        };
    }
}
