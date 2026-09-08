using GameGuild.CQRS;
using GameGuild.Learning.Experience.Social.Services;

namespace GameGuild.Learning.Experience.Social;

public sealed record CreateDiscussionCommand(Guid CourseId, Guid AuthorId, string Title, string Content, Guid? ContentId) : ICommand<Result<CourseDiscussion>>;
public sealed record PinDiscussionCommand(Guid DiscussionId) : ICommand<Result<CourseDiscussion>>;
public sealed record UnpinDiscussionCommand(Guid DiscussionId) : ICommand<Result<CourseDiscussion>>;
public sealed record MarkDiscussionResolvedCommand(Guid DiscussionId) : ICommand<Result<CourseDiscussion>>;
public sealed record DeleteDiscussionCommand(Guid DiscussionId, Guid UserId) : ICommand<Result<bool>>;

public sealed class DiscussionCommandHandler(IDiscussionService service) :
    ICommandHandler<CreateDiscussionCommand, Result<CourseDiscussion>>,
    ICommandHandler<PinDiscussionCommand, Result<CourseDiscussion>>,
    ICommandHandler<UnpinDiscussionCommand, Result<CourseDiscussion>>,
    ICommandHandler<MarkDiscussionResolvedCommand, Result<CourseDiscussion>>,
    ICommandHandler<DeleteDiscussionCommand, Result<bool>>
{
    public Task<Result<CourseDiscussion>> Handle(CreateDiscussionCommand request, CancellationToken cancellationToken) =>
        service.CreateDiscussionAsync(request.CourseId, request.AuthorId, request.Title, request.Content, request.ContentId, cancellationToken);

    public Task<Result<CourseDiscussion>> Handle(PinDiscussionCommand request, CancellationToken cancellationToken) =>
        service.PinDiscussionAsync(request.DiscussionId, cancellationToken);

    public Task<Result<CourseDiscussion>> Handle(UnpinDiscussionCommand request, CancellationToken cancellationToken) =>
        service.UnpinDiscussionAsync(request.DiscussionId, cancellationToken);

    public Task<Result<CourseDiscussion>> Handle(MarkDiscussionResolvedCommand request, CancellationToken cancellationToken) =>
        service.MarkDiscussionResolvedAsync(request.DiscussionId, cancellationToken);

    public Task<Result<bool>> Handle(DeleteDiscussionCommand request, CancellationToken cancellationToken) =>
        service.DeleteDiscussionAsync(request.DiscussionId, request.UserId, cancellationToken);
}

public sealed record GenerateFeedItemsCommand(Guid UserId, Guid? TenantId) : ICommand<Result<int>>;
public sealed record MarkFeedItemViewedCommand(Guid FeedItemId) : ICommand<Result<PersonalizedFeedItem>>;
public sealed record DismissFeedItemCommand(Guid FeedItemId, Guid UserId) : ICommand<Result<PersonalizedFeedItem>>;

public sealed class FeedCommandHandler(IFeedService service) :
    ICommandHandler<GenerateFeedItemsCommand, Result<int>>,
    ICommandHandler<MarkFeedItemViewedCommand, Result<PersonalizedFeedItem>>,
    ICommandHandler<DismissFeedItemCommand, Result<PersonalizedFeedItem>>
{
    public Task<Result<int>> Handle(GenerateFeedItemsCommand request, CancellationToken cancellationToken) =>
        service.GenerateFeedItemsAsync(request.UserId, request.TenantId, cancellationToken);

    public Task<Result<PersonalizedFeedItem>> Handle(MarkFeedItemViewedCommand request, CancellationToken cancellationToken) =>
        service.MarkFeedItemViewedAsync(request.FeedItemId, cancellationToken);

    public Task<Result<PersonalizedFeedItem>> Handle(DismissFeedItemCommand request, CancellationToken cancellationToken) =>
        service.DismissFeedItemAsync(request.FeedItemId, request.UserId, cancellationToken);
}

public sealed record LikeCourseCommand(Guid CourseId, Guid UserId, Guid? TenantId) : ICommand<Result<CourseLike>>;
public sealed record UnlikeCourseCommand(Guid CourseId, Guid UserId) : ICommand<Result<bool>>;

public sealed class LikeCommandHandler(ILikeService service) :
    ICommandHandler<LikeCourseCommand, Result<CourseLike>>,
    ICommandHandler<UnlikeCourseCommand, Result<bool>>
{
    public Task<Result<CourseLike>> Handle(LikeCourseCommand request, CancellationToken cancellationToken) =>
        service.LikeCourseAsync(request.CourseId, request.UserId, request.TenantId, cancellationToken);

    public Task<Result<bool>> Handle(UnlikeCourseCommand request, CancellationToken cancellationToken) =>
        service.UnlikeCourseAsync(request.CourseId, request.UserId, cancellationToken);
}

public sealed record CreateReplyCommand(Guid DiscussionId, Guid AuthorId, string Content, Guid? ParentReplyId) : ICommand<Result<DiscussionReply>>;
public sealed record AcceptReplyAsAnswerCommand(Guid ReplyId, Guid DiscussionAuthorId) : ICommand<Result<DiscussionReply>>;
public sealed record UpvoteReplyCommand(Guid ReplyId) : ICommand<Result<DiscussionReply>>;
public sealed record DeleteReplyCommand(Guid ReplyId, Guid UserId) : ICommand<Result<bool>>;

public sealed class ReplyCommandHandler(IReplyService service) :
    ICommandHandler<CreateReplyCommand, Result<DiscussionReply>>,
    ICommandHandler<AcceptReplyAsAnswerCommand, Result<DiscussionReply>>,
    ICommandHandler<UpvoteReplyCommand, Result<DiscussionReply>>,
    ICommandHandler<DeleteReplyCommand, Result<bool>>
{
    public Task<Result<DiscussionReply>> Handle(CreateReplyCommand request, CancellationToken cancellationToken) =>
        service.CreateReplyAsync(request.DiscussionId, request.AuthorId, request.Content, request.ParentReplyId, cancellationToken);

    public Task<Result<DiscussionReply>> Handle(AcceptReplyAsAnswerCommand request, CancellationToken cancellationToken) =>
        service.AcceptReplyAsAnswerAsync(request.ReplyId, request.DiscussionAuthorId, cancellationToken);

    public Task<Result<DiscussionReply>> Handle(UpvoteReplyCommand request, CancellationToken cancellationToken) =>
        service.UpvoteReplyAsync(request.ReplyId, cancellationToken);

    public Task<Result<bool>> Handle(DeleteReplyCommand request, CancellationToken cancellationToken) =>
        service.DeleteReplyAsync(request.ReplyId, request.UserId, cancellationToken);
}

public sealed record CreateReviewCommand(Guid CourseId, Guid UserId, int Rating, string? Title, string? Content, Guid? EnrollmentId) : ICommand<Result<CourseReview>>;
public sealed record MarkReviewHelpfulCommand(Guid ReviewId) : ICommand<Result<CourseReview>>;
public sealed record DeleteReviewCommand(Guid ReviewId, Guid UserId) : ICommand<Result<bool>>;
public sealed record ApproveReviewCommand(Guid ReviewId) : ICommand<Result<CourseReview>>;
public sealed record FeatureReviewCommand(Guid ReviewId) : ICommand<Result<CourseReview>>;
public sealed record UpdateReviewModerationCommand(Guid ReviewId, bool IsApproved, bool IsFeatured) : ICommand<Result<CourseReview>>;

public sealed class ReviewCommandHandler(IReviewService service) :
    ICommandHandler<CreateReviewCommand, Result<CourseReview>>,
    ICommandHandler<MarkReviewHelpfulCommand, Result<CourseReview>>,
    ICommandHandler<DeleteReviewCommand, Result<bool>>,
    ICommandHandler<ApproveReviewCommand, Result<CourseReview>>,
    ICommandHandler<FeatureReviewCommand, Result<CourseReview>>,
    ICommandHandler<UpdateReviewModerationCommand, Result<CourseReview>>
{
    public Task<Result<CourseReview>> Handle(CreateReviewCommand request, CancellationToken cancellationToken) =>
        service.CreateReviewAsync(request.CourseId, request.UserId, request.Rating, request.Title, request.Content, request.EnrollmentId, cancellationToken);

    public Task<Result<CourseReview>> Handle(MarkReviewHelpfulCommand request, CancellationToken cancellationToken) =>
        service.MarkReviewHelpfulAsync(request.ReviewId, cancellationToken);

    public Task<Result<bool>> Handle(DeleteReviewCommand request, CancellationToken cancellationToken) =>
        service.DeleteReviewAsync(request.ReviewId, request.UserId, cancellationToken);

    public Task<Result<CourseReview>> Handle(ApproveReviewCommand request, CancellationToken cancellationToken) =>
        service.ApproveReviewAsync(request.ReviewId, cancellationToken);

    public Task<Result<CourseReview>> Handle(FeatureReviewCommand request, CancellationToken cancellationToken) =>
        service.FeatureReviewAsync(request.ReviewId, cancellationToken);

    public Task<Result<CourseReview>> Handle(UpdateReviewModerationCommand request, CancellationToken cancellationToken) =>
        service.UpdateReviewModerationAsync(request.ReviewId, request.IsApproved, request.IsFeatured, cancellationToken);
}

public sealed record AddToWishlistCommand(Guid CourseId, Guid UserId, bool NotifyOnSale, bool NotifyOnUpdate) : ICommand<Result<CourseWishlist>>;
public sealed record RemoveFromWishlistCommand(Guid CourseId, Guid UserId) : ICommand<Result<bool>>;
public sealed record UpdateWishlistPreferencesCommand(Guid CourseId, Guid UserId, bool NotifyOnSale, bool NotifyOnUpdate) : ICommand<Result<CourseWishlist>>;

public sealed class WishlistCommandHandler(IWishlistService service) :
    ICommandHandler<AddToWishlistCommand, Result<CourseWishlist>>,
    ICommandHandler<RemoveFromWishlistCommand, Result<bool>>,
    ICommandHandler<UpdateWishlistPreferencesCommand, Result<CourseWishlist>>
{
    public Task<Result<CourseWishlist>> Handle(AddToWishlistCommand request, CancellationToken cancellationToken) =>
        service.AddToWishlistAsync(request.CourseId, request.UserId, request.NotifyOnSale, request.NotifyOnUpdate, cancellationToken);

    public Task<Result<bool>> Handle(RemoveFromWishlistCommand request, CancellationToken cancellationToken) =>
        service.RemoveFromWishlistAsync(request.CourseId, request.UserId, cancellationToken);

    public Task<Result<CourseWishlist>> Handle(UpdateWishlistPreferencesCommand request, CancellationToken cancellationToken) =>
        service.UpdateWishlistPreferencesAsync(request.CourseId, request.UserId, request.NotifyOnSale, request.NotifyOnUpdate, cancellationToken);
}
