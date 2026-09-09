using GameGuild;
using GameGuild.Social.Posts.Commands;

[assembly: UseCaseEventContract(typeof(CreatePostEndpointCommand), "social.posts.create", NoDomainEventReason = "Post creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdatePostEndpointCommand), "social.posts.update", NoDomainEventReason = "Post updates are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DeletePostEndpointCommand), "social.posts.delete", NoDomainEventReason = "Post deletion is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(AddPostCommentEndpointCommand), "social.posts.comments.add", NoDomainEventReason = "Comment creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdatePostCommentEndpointCommand), "social.posts.comments.update", NoDomainEventReason = "Comment updates are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(DeletePostCommentEndpointCommand), "social.posts.comments.delete", NoDomainEventReason = "Comment deletion is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(TogglePostLikeEndpointCommand), "social.posts.likes.toggle", NoDomainEventReason = "Like changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(TogglePostPinEndpointCommand), "social.posts.pin.toggle", NoDomainEventReason = "Pin changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(SharePostEndpointCommand), "social.posts.share", NoDomainEventReason = "Shares are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(RecordPostViewEndpointCommand), "social.posts.view", NoDomainEventReason = "Views are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(FollowPostEndpointCommand), "social.posts.follow", NoDomainEventReason = "Post follows are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UnfollowPostEndpointCommand), "social.posts.unfollow", NoDomainEventReason = "Post unfollows are observed through the durable generic operation event.")]
