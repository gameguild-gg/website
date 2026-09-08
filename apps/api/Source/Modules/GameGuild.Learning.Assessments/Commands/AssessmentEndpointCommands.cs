using GameGuild.CQRS;

namespace GameGuild.Learning.Assessments;

public sealed record CreateAssessmentEndpointCommand(CreateAssessmentRequest Request) : ICommand<Result<Assessment>>;
public sealed record CreateAssessmentGroupEndpointCommand(CreateAssessmentGroupRequest Request) : ICommand<Result<AssessmentGroup>>;
public sealed record UpdateAssessmentGroupEndpointCommand(Guid GroupId, UpdateAssessmentGroupRequest Request) : ICommand<Result<AssessmentGroup>>;
public sealed record DeleteAssessmentGroupEndpointCommand(Guid GroupId) : ICommand<Result>;
public sealed record UpdateAssessmentEndpointCommand(Guid AssessmentId, UpdateAssessmentRequest Request) : ICommand<Result<Assessment>>;
public sealed record AssignAssessmentToGroupEndpointCommand(Guid AssessmentId, AssignAssessmentGroupRequest Request) : ICommand<Result<Assessment>>;
public sealed record LinkInteractiveVideoCueEndpointCommand(Guid AssessmentId, LinkInteractiveVideoCueRequest Request) : ICommand<Result<InteractiveVideoAssessmentCue>>;
public sealed record UnlinkInteractiveVideoCueEndpointCommand(Guid AssessmentId, Guid CueId) : ICommand<Result>;
public sealed record DeleteAssessmentEndpointCommand(Guid AssessmentId) : ICommand<Result>;
public sealed record RestoreAssessmentEndpointCommand(Guid AssessmentId) : ICommand<Result>;
public sealed record StartAssessmentSubmissionEndpointCommand(Guid AssessmentId, Guid EnrollmentId, Guid UserId) : ICommand<Result<AssessmentSubmission>>;
public sealed record SubmitAssessmentEndpointCommand(Guid SubmissionId, SubmitAssessmentRequest? Request) : ICommand<Result<AssessmentSubmission>>;
public sealed record GradeAssessmentSubmissionEndpointCommand(Guid SubmissionId, GradeSubmissionRequest Request) : ICommand<Result<AssessmentSubmission>>;

public sealed class AssessmentEndpointCommandHandler(IAssessmentService service) :
    ICommandHandler<CreateAssessmentEndpointCommand, Result<Assessment>>,
    ICommandHandler<CreateAssessmentGroupEndpointCommand, Result<AssessmentGroup>>,
    ICommandHandler<UpdateAssessmentGroupEndpointCommand, Result<AssessmentGroup>>,
    ICommandHandler<DeleteAssessmentGroupEndpointCommand, Result>,
    ICommandHandler<UpdateAssessmentEndpointCommand, Result<Assessment>>,
    ICommandHandler<AssignAssessmentToGroupEndpointCommand, Result<Assessment>>,
    ICommandHandler<LinkInteractiveVideoCueEndpointCommand, Result<InteractiveVideoAssessmentCue>>,
    ICommandHandler<UnlinkInteractiveVideoCueEndpointCommand, Result>,
    ICommandHandler<DeleteAssessmentEndpointCommand, Result>,
    ICommandHandler<RestoreAssessmentEndpointCommand, Result>,
    ICommandHandler<StartAssessmentSubmissionEndpointCommand, Result<AssessmentSubmission>>,
    ICommandHandler<SubmitAssessmentEndpointCommand, Result<AssessmentSubmission>>,
    ICommandHandler<GradeAssessmentSubmissionEndpointCommand, Result<AssessmentSubmission>>
{
    public Task<Result<Assessment>> Handle(CreateAssessmentEndpointCommand request, CancellationToken cancellationToken) => service.CreateAssessmentAsync(request.Request);
    public Task<Result<AssessmentGroup>> Handle(CreateAssessmentGroupEndpointCommand request, CancellationToken cancellationToken) => service.CreateAssessmentGroupAsync(request.Request);
    public Task<Result<AssessmentGroup>> Handle(UpdateAssessmentGroupEndpointCommand request, CancellationToken cancellationToken) => service.UpdateAssessmentGroupAsync(request.GroupId, request.Request);
    public Task<Result> Handle(DeleteAssessmentGroupEndpointCommand request, CancellationToken cancellationToken) => service.DeleteAssessmentGroupAsync(request.GroupId);
    public Task<Result<Assessment>> Handle(UpdateAssessmentEndpointCommand request, CancellationToken cancellationToken) => service.UpdateAssessmentAsync(request.AssessmentId, request.Request);
    public Task<Result<Assessment>> Handle(AssignAssessmentToGroupEndpointCommand request, CancellationToken cancellationToken) => service.AssignAssessmentToGroupAsync(request.AssessmentId, request.Request);
    public Task<Result<InteractiveVideoAssessmentCue>> Handle(LinkInteractiveVideoCueEndpointCommand request, CancellationToken cancellationToken) => service.LinkInteractiveVideoCueAsync(request.AssessmentId, request.Request);
    public Task<Result> Handle(UnlinkInteractiveVideoCueEndpointCommand request, CancellationToken cancellationToken) => service.UnlinkInteractiveVideoCueAsync(request.AssessmentId, request.CueId);
    public Task<Result> Handle(DeleteAssessmentEndpointCommand request, CancellationToken cancellationToken) => service.DeleteAssessmentAsync(request.AssessmentId);
    public Task<Result> Handle(RestoreAssessmentEndpointCommand request, CancellationToken cancellationToken) => service.RestoreAssessmentAsync(request.AssessmentId, cancellationToken);
    public Task<Result<AssessmentSubmission>> Handle(StartAssessmentSubmissionEndpointCommand request, CancellationToken cancellationToken) => service.StartSubmissionAsync(request.AssessmentId, request.EnrollmentId, request.UserId);
    public Task<Result<AssessmentSubmission>> Handle(SubmitAssessmentEndpointCommand request, CancellationToken cancellationToken) => service.SubmitAsync(request.SubmissionId, request.Request);
    public Task<Result<AssessmentSubmission>> Handle(GradeAssessmentSubmissionEndpointCommand request, CancellationToken cancellationToken) => service.GradeSubmissionAsync(request.SubmissionId, request.Request);
}

public sealed record CreateCourseGroupSetEndpointCommand(Guid CourseId, string Name) : ICommand<Result<CourseGroupSet>>;
public sealed record CreateCourseGroupEndpointCommand(Guid CourseId, Guid GroupSetId, string Name, int Capacity) : ICommand<Result<CourseGroup>>;
public sealed record JoinCourseGroupEndpointCommand(Guid CourseId, Guid GroupId, Guid UserId) : ICommand<Result<CourseGroupMember>>;
public sealed record LeaveCourseGroupEndpointCommand(Guid CourseId, Guid GroupId, Guid UserId) : ICommand<Result>;
public sealed record AddCourseGroupMemberEndpointCommand(Guid CourseId, Guid GroupId, Guid UserId) : ICommand<Result<CourseGroupMember>>;
public sealed record RemoveCourseGroupMemberEndpointCommand(Guid CourseId, Guid GroupId, Guid UserId) : ICommand<Result>;

public sealed class CourseGroupEndpointCommandHandler(IGroupSetService service) :
    ICommandHandler<CreateCourseGroupSetEndpointCommand, Result<CourseGroupSet>>,
    ICommandHandler<CreateCourseGroupEndpointCommand, Result<CourseGroup>>,
    ICommandHandler<JoinCourseGroupEndpointCommand, Result<CourseGroupMember>>,
    ICommandHandler<LeaveCourseGroupEndpointCommand, Result>,
    ICommandHandler<AddCourseGroupMemberEndpointCommand, Result<CourseGroupMember>>,
    ICommandHandler<RemoveCourseGroupMemberEndpointCommand, Result>
{
    public Task<Result<CourseGroupSet>> Handle(CreateCourseGroupSetEndpointCommand request, CancellationToken cancellationToken) => service.CreateGroupSetAsync(request.CourseId, request.Name);
    public Task<Result<CourseGroup>> Handle(CreateCourseGroupEndpointCommand request, CancellationToken cancellationToken) => service.CreateGroupAsync(request.CourseId, request.GroupSetId, request.Name, request.Capacity);
    public Task<Result<CourseGroupMember>> Handle(JoinCourseGroupEndpointCommand request, CancellationToken cancellationToken) => service.JoinAsync(request.CourseId, request.GroupId, request.UserId);
    public Task<Result> Handle(LeaveCourseGroupEndpointCommand request, CancellationToken cancellationToken) => service.LeaveAsync(request.CourseId, request.GroupId, request.UserId);
    public Task<Result<CourseGroupMember>> Handle(AddCourseGroupMemberEndpointCommand request, CancellationToken cancellationToken) => service.AddMemberAsync(request.CourseId, request.GroupId, request.UserId);
    public Task<Result> Handle(RemoveCourseGroupMemberEndpointCommand request, CancellationToken cancellationToken) => service.RemoveMemberAsync(request.CourseId, request.GroupId, request.UserId);
}

public sealed record ClaimPeerReviewEndpointCommand(Guid AssessmentId, Guid UserId) : ICommand<Result<PeerReviewClaimResult>>;
public sealed record SubmitPeerReviewEndpointCommand(AssessmentPeerReview Review, int Score, string Feedback, string? RubricScores) : ICommand<Result<AssessmentPeerReview>>;

public sealed class PeerReviewEndpointCommandHandler(IPeerReviewAssignmentService service) :
    ICommandHandler<ClaimPeerReviewEndpointCommand, Result<PeerReviewClaimResult>>,
    ICommandHandler<SubmitPeerReviewEndpointCommand, Result<AssessmentPeerReview>>
{
    public Task<Result<PeerReviewClaimResult>> Handle(ClaimPeerReviewEndpointCommand request, CancellationToken cancellationToken) => service.ClaimAsync(request.AssessmentId, request.UserId);
    public Task<Result<AssessmentPeerReview>> Handle(SubmitPeerReviewEndpointCommand request, CancellationToken cancellationToken) => service.SubmitReviewAsync(request.Review, request.Score, request.Feedback, request.RubricScores);
}

public sealed record PutAssessmentRubricEndpointCommand(Guid AssessmentId, SaveRubricRequest Request) : ICommand<Result<RubricDto>>;
public sealed record DeleteAssessmentRubricEndpointCommand(Guid AssessmentId) : ICommand<Result>;

public sealed class AssessmentRubricEndpointCommandHandler(IRubricService service) :
    ICommandHandler<PutAssessmentRubricEndpointCommand, Result<RubricDto>>,
    ICommandHandler<DeleteAssessmentRubricEndpointCommand, Result>
{
    public Task<Result<RubricDto>> Handle(PutAssessmentRubricEndpointCommand request, CancellationToken cancellationToken) => service.SaveAsync(request.AssessmentId, request.Request);
    public Task<Result> Handle(DeleteAssessmentRubricEndpointCommand request, CancellationToken cancellationToken) => service.DeleteAsync(request.AssessmentId);
}
