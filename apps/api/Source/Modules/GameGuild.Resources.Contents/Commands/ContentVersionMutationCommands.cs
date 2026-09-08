using GameGuild.CQRS;

namespace GameGuild.Resources.Contents;

public sealed record CreateContentDraftCommand(CreateDraftRequest Draft) : ICommand<Result<ContentVersion>>;
public sealed record UpdateContentDraftCommand(Guid VersionId, UpdateDraftRequest Draft) : ICommand<Result<ContentVersion>>;
public sealed record SubmitContentForReviewCommand(Guid VersionId) : ICommand<Result<ContentVersion>>;
public sealed record ApproveContentVersionCommand(Guid VersionId, string? ReviewNotes) : ICommand<Result<ContentVersion>>;
public sealed record RejectContentVersionCommand(Guid VersionId, string? ReviewNotes) : ICommand<Result<ContentVersion>>;
public sealed record PublishContentVersionCommand(Guid VersionId) : ICommand<Result<ContentVersion>>;
public sealed record ScheduleContentPublishCommand(Guid VersionId, DateTime ScheduledAt) : ICommand<Result<ContentVersion>>;
public sealed record CancelContentPublishCommand(Guid VersionId) : ICommand<Result<ContentVersion>>;
public sealed record RollbackContentVersionCommand(Guid EntityId, string EntityType, int TargetVersionNumber, string? Reason) : ICommand<Result<ContentVersion>>;
public sealed record AddContentVersionReviewCommand(Guid VersionId, AddReviewRequest Review) : ICommand<Result<ContentVersionReview>>;

public sealed class CreateContentDraftCommandHandler(IContentVersioningService service)
    : ICommandHandler<CreateContentDraftCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(CreateContentDraftCommand command, CancellationToken cancellationToken)
    {
        var draft = command.Draft;
        return service.CreateDraftAsync(
            draft.EntityId,
            draft.EntityType,
            draft.Title,
            draft.CreatedBy,
            draft.Summary,
            draft.Body,
            draft.Metadata,
            draft.ChangeNotes,
            cancellationToken);
    }
}

public sealed class UpdateContentDraftCommandHandler(IContentVersioningService service)
    : ICommandHandler<UpdateContentDraftCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(UpdateContentDraftCommand command, CancellationToken cancellationToken)
    {
        var draft = command.Draft;
        return service.UpdateDraftAsync(
            command.VersionId,
            draft.Title,
            draft.Summary,
            draft.Body,
            draft.Metadata,
            draft.ChangeNotes,
            cancellationToken);
    }
}

public sealed class SubmitContentForReviewCommandHandler(IContentVersioningService service)
    : ICommandHandler<SubmitContentForReviewCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(SubmitContentForReviewCommand command, CancellationToken cancellationToken) =>
        service.SubmitForReviewAsync(command.VersionId, cancellationToken);
}

public sealed class ApproveContentVersionCommandHandler(IContentVersioningService service)
    : ICommandHandler<ApproveContentVersionCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(ApproveContentVersionCommand command, CancellationToken cancellationToken) =>
        service.ApproveAsync(command.VersionId, command.ReviewNotes, cancellationToken);
}

public sealed class RejectContentVersionCommandHandler(IContentVersioningService service)
    : ICommandHandler<RejectContentVersionCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(RejectContentVersionCommand command, CancellationToken cancellationToken) =>
        service.RejectAsync(command.VersionId, command.ReviewNotes, cancellationToken);
}

public sealed class PublishContentVersionCommandHandler(IContentVersioningService service)
    : ICommandHandler<PublishContentVersionCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(PublishContentVersionCommand command, CancellationToken cancellationToken) =>
        service.PublishAsync(command.VersionId, cancellationToken);
}

public sealed class ScheduleContentPublishCommandHandler(IContentVersioningService service)
    : ICommandHandler<ScheduleContentPublishCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(ScheduleContentPublishCommand command, CancellationToken cancellationToken) =>
        service.SchedulePublishAsync(command.VersionId, command.ScheduledAt, cancellationToken);
}

public sealed class CancelContentPublishCommandHandler(IContentVersioningService service)
    : ICommandHandler<CancelContentPublishCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(CancelContentPublishCommand command, CancellationToken cancellationToken) =>
        service.CancelScheduledPublishAsync(command.VersionId, cancellationToken);
}

public sealed class RollbackContentVersionCommandHandler(IContentVersioningService service)
    : ICommandHandler<RollbackContentVersionCommand, Result<ContentVersion>>
{
    public Task<Result<ContentVersion>> Handle(RollbackContentVersionCommand command, CancellationToken cancellationToken) =>
        service.RollbackAsync(
            command.EntityId,
            command.EntityType,
            command.TargetVersionNumber,
            command.Reason,
            cancellationToken);
}

public sealed class AddContentVersionReviewCommandHandler(IContentVersioningService service)
    : ICommandHandler<AddContentVersionReviewCommand, Result<ContentVersionReview>>
{
    public Task<Result<ContentVersionReview>> Handle(AddContentVersionReviewCommand command, CancellationToken cancellationToken) =>
        service.AddReviewAsync(
            command.VersionId,
            command.Review.Decision,
            command.Review.Feedback,
            command.Review.Suggestions,
            cancellationToken);
}
