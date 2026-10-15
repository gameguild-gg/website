using GameGuild.CQRS;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;

namespace GameGuild.Learning.Assessments;

public sealed record SaveAssessmentDraftEndpointCommand(
    Guid CourseId,
    Guid ContentId,
    Guid ActorId,
    SaveAssessmentDraftRequest Request) : ICommand<Result<AssessmentDraftResult>>;

public sealed record PrepareAssessmentRevisionEndpointCommand(
    Guid AssessmentId,
    Guid ActorId,
    PrepareAssessmentRevisionRequest Request) : ICommand<Result<PreparedAssessmentRevisionResult>>;

public sealed record PublishAssessmentRevisionEndpointCommand(
    Guid AssessmentId,
    Guid ActorId,
    PublishAssessmentRevisionRequest Request) : ICommand<Result<PreparedAssessmentRevisionResult>>;

public sealed record UnpublishAssessmentRevisionEndpointCommand(
    Guid AssessmentId,
    Guid ActorId,
    UnpublishAssessmentRevisionRequest Request) : ICommand<Result>;

public sealed class AssessmentAuthoringEndpointCommandHandler(IAssessmentAuthoringService service) :
    ICommandHandler<SaveAssessmentDraftEndpointCommand, Result<AssessmentDraftResult>>,
    ICommandHandler<PrepareAssessmentRevisionEndpointCommand, Result<PreparedAssessmentRevisionResult>>,
    ICommandHandler<PublishAssessmentRevisionEndpointCommand, Result<PreparedAssessmentRevisionResult>>,
    ICommandHandler<UnpublishAssessmentRevisionEndpointCommand, Result>
{
    public Task<Result<AssessmentDraftResult>> Handle(
        SaveAssessmentDraftEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SaveDraftAsync(
            request.CourseId,
            request.ContentId,
            request.ActorId,
            request.Request,
            cancellationToken);

    public Task<Result<PreparedAssessmentRevisionResult>> Handle(
        PrepareAssessmentRevisionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.PrepareAsync(request.AssessmentId, request.ActorId, request.Request, cancellationToken);

    public Task<Result<PreparedAssessmentRevisionResult>> Handle(
        PublishAssessmentRevisionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.PublishAsync(request.AssessmentId, request.ActorId, request.Request, cancellationToken);

    public Task<Result> Handle(
        UnpublishAssessmentRevisionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.UnpublishAsync(request.AssessmentId, request.ActorId, request.Request, cancellationToken);
}

public sealed record StartAssessmentTestRunEndpointCommand(
    Guid AssessmentId,
    Guid ActorId,
    StartAssessmentTestRunCommand Command) : ICommand<AssessmentTestRunViewV1>;

public sealed record SubmitAssessmentTestRunEndpointCommand(
    Guid TestRunId,
    Guid ActorId,
    SubmitAssessmentResponseCommand Command) : ICommand<AssessmentTestRunViewV1>;

public sealed record ResolveAssessmentTestRunEndpointCommand(
    Guid TestRunId,
    Guid ActorId,
    InstructorReviewResolutionV1 Resolution,
    string IdempotencyKey,
    Guid? SubjectId = null) : ICommand<AssessmentTestRunViewV1>;

public sealed record SubmitAssessmentTestSelfReviewEndpointCommand(
    Guid TestRunId,
    Guid ActorId,
    SubmitSelfReviewCommand Command) : ICommand<AssessmentTestRunViewV1>;

public sealed record RestartAssessmentTestRunEndpointCommand(
    Guid TestRunId,
    Guid ActorId,
    string IdempotencyKey) : ICommand<AssessmentTestRunViewV1>;

public sealed record StartIndividualRuntimeSubmissionEndpointCommand(
    Guid AssessmentId,
    StartIndividualSubmissionCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record StartCollectiveRuntimeSubmissionEndpointCommand(
    Guid AssessmentId,
    StartCollectiveSubmissionCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record SaveCollectiveRuntimeDraftEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    SaveCollectiveAssessmentDraftCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record SaveCollectiveRuntimeSelfReviewDraftEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    SaveCollectiveSelfReviewDraftCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record SubmitRuntimeSelfReviewEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    SubmitSelfReviewCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record SubmitRuntimeSubmissionEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    SubmitAssessmentResponseCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed record ResolveRuntimeInstructorReviewEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    InstructorReviewResolutionV1 Resolution,
    string IdempotencyKey) : ICommand<AssessmentSubmissionViewV1>;

public sealed record RegradeRuntimeSubmissionEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    RegradeExecutionCommand Command) : ICommand<AssessmentSubmissionViewV1>;

public sealed class AssessmentGradingRuntimeEndpointCommandHandler(IAssessmentGradingRuntimeService service) :
    ICommandHandler<StartAssessmentTestRunEndpointCommand, AssessmentTestRunViewV1>,
    ICommandHandler<SubmitAssessmentTestRunEndpointCommand, AssessmentTestRunViewV1>,
    ICommandHandler<ResolveAssessmentTestRunEndpointCommand, AssessmentTestRunViewV1>,
    ICommandHandler<SubmitAssessmentTestSelfReviewEndpointCommand, AssessmentTestRunViewV1>,
    ICommandHandler<RestartAssessmentTestRunEndpointCommand, AssessmentTestRunViewV1>,
    ICommandHandler<StartIndividualRuntimeSubmissionEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<StartCollectiveRuntimeSubmissionEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<SaveCollectiveRuntimeDraftEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<SaveCollectiveRuntimeSelfReviewDraftEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<SubmitRuntimeSelfReviewEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<SubmitRuntimeSubmissionEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<ResolveRuntimeInstructorReviewEndpointCommand, AssessmentSubmissionViewV1>,
    ICommandHandler<RegradeRuntimeSubmissionEndpointCommand, AssessmentSubmissionViewV1>
{
    public Task<AssessmentTestRunViewV1> Handle(
        StartAssessmentTestRunEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.StartTestRunAsync(request.AssessmentId, request.ActorId, request.Command, cancellationToken);

    public Task<AssessmentTestRunViewV1> Handle(
        SubmitAssessmentTestRunEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SubmitTestRunAsync(request.TestRunId, request.ActorId, request.Command, cancellationToken);

    public Task<AssessmentTestRunViewV1> Handle(
        ResolveAssessmentTestRunEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.ResolveTestInstructorReviewAsync(
            request.TestRunId,
            request.ActorId,
            request.Resolution,
            request.IdempotencyKey,
            request.SubjectId,
            cancellationToken);

    public Task<AssessmentTestRunViewV1> Handle(
        SubmitAssessmentTestSelfReviewEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SubmitTestSelfReviewAsync(
            request.TestRunId,
            request.ActorId,
            request.Command,
            cancellationToken);

    public Task<AssessmentTestRunViewV1> Handle(
        RestartAssessmentTestRunEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.RestartTestRunAsync(
            request.TestRunId,
            request.ActorId,
            request.IdempotencyKey,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        StartIndividualRuntimeSubmissionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.StartIndividualSubmissionAsync(request.AssessmentId, request.Command, cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        StartCollectiveRuntimeSubmissionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.StartCollectiveSubmissionAsync(request.AssessmentId, request.Command, cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        SaveCollectiveRuntimeDraftEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SaveCollectiveDraftAsync(
            request.SubmissionId,
            request.ActorId,
            request.Command,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        SaveCollectiveRuntimeSelfReviewDraftEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SaveCollectiveSelfReviewDraftAsync(
            request.SubmissionId,
            request.ActorId,
            request.Command,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        SubmitRuntimeSelfReviewEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SubmitOfficialSelfReviewAsync(
            request.SubmissionId,
            request.ActorId,
            request.Command,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        SubmitRuntimeSubmissionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.SubmitOfficialAsync(
            request.SubmissionId,
            request.ActorId,
            request.Command,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        ResolveRuntimeInstructorReviewEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.ResolveOfficialInstructorReviewAsync(
            request.SubmissionId,
            request.ActorId,
            request.Resolution,
            request.IdempotencyKey,
            cancellationToken);

    public Task<AssessmentSubmissionViewV1> Handle(
        RegradeRuntimeSubmissionEndpointCommand request,
        CancellationToken cancellationToken) =>
        service.RegradeOfficialAsync(
            request.SubmissionId,
            request.ActorId,
            request.Command,
            cancellationToken);
}

public sealed record ReleaseRuntimeSubmissionEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    ReleaseGradeResultCommand Command) : ICommand<GradeResultReleaseResponse>;

public sealed record ScheduleRuntimeSubmissionReleaseEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    ScheduleGradeResultReleaseCommand Command) : ICommand<GradeResultReleaseScheduleViewV1>;

public sealed record CancelRuntimeSubmissionReleaseScheduleEndpointCommand(
    Guid SubmissionId,
    Guid ActorId,
    CancelScheduledGradeResultReleaseCommand Command) : ICommand<GradeResultReleaseScheduleViewV1>;

public sealed class GradeReleaseEndpointCommandHandler(
    IGradeReleaseService service,
    IGradeResultReleaseScheduleService scheduleService) :
    ICommandHandler<ReleaseRuntimeSubmissionEndpointCommand, GradeResultReleaseResponse>,
    ICommandHandler<ScheduleRuntimeSubmissionReleaseEndpointCommand, GradeResultReleaseScheduleViewV1>,
    ICommandHandler<CancelRuntimeSubmissionReleaseScheduleEndpointCommand, GradeResultReleaseScheduleViewV1>
{
    public async Task<GradeResultReleaseResponse> Handle(
        ReleaseRuntimeSubmissionEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var release = await service.ReleaseByActorAsync(
                request.SubmissionId,
                request.Command.ExpectedRoundId,
                checked((int)request.Command.ExpectedSubmissionVersion),
                request.ActorId,
                request.Command.IdempotencyKey,
                request.Command.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return new GradeResultReleaseResponse(release.Id, release.GradeRoundId, release.ReleasedAt);
    }

    public Task<GradeResultReleaseScheduleViewV1> Handle(
        ScheduleRuntimeSubmissionReleaseEndpointCommand request,
        CancellationToken cancellationToken) =>
        scheduleService.ScheduleByActorAsync(
            request.SubmissionId,
            request.Command.ExpectedRoundId,
            checked((int)request.Command.ExpectedSubmissionVersion),
            request.Command.ExpectedScheduleVersion,
            request.Command.ScheduledFor,
            request.ActorId,
            request.Command.IdempotencyKey,
            request.Command.Reason,
            cancellationToken);

    public Task<GradeResultReleaseScheduleViewV1> Handle(
        CancelRuntimeSubmissionReleaseScheduleEndpointCommand request,
        CancellationToken cancellationToken) =>
        scheduleService.CancelByActorAsync(
            request.SubmissionId,
            request.Command.ExpectedRoundId,
            checked((int)request.Command.ExpectedSubmissionVersion),
            request.Command.ExpectedScheduleVersion,
            request.ActorId,
            request.Command.IdempotencyKey,
            request.Command.Reason,
            cancellationToken);
}
