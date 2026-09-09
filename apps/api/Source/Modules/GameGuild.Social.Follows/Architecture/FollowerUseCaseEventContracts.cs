using GameGuild;
using GameGuild.Social.Follows.Commands;

[assembly: UseCaseEventContract(typeof(FollowEntityEndpointCommand), "social.follows.follow", NoDomainEventReason = "Follow creation is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UnfollowEntityEndpointCommand), "social.follows.unfollow", NoDomainEventReason = "Follow removal is observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateFollowNotificationsEndpointCommand), "social.follows.notifications.update", NoDomainEventReason = "Follow-notification changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UpdateFollowPrivacyEndpointCommand), "social.follows.privacy.update", NoDomainEventReason = "Follow-privacy changes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(BlockUserEndpointCommand), "social.follows.block", NoDomainEventReason = "User blocks are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UnblockUserEndpointCommand), "social.follows.unblock", NoDomainEventReason = "User unblocks are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(MuteUserEndpointCommand), "social.follows.mute", NoDomainEventReason = "User mutes are observed through the durable generic operation event.")]
[assembly: UseCaseEventContract(typeof(UnmuteUserEndpointCommand), "social.follows.unmute", NoDomainEventReason = "User unmutes are observed through the durable generic operation event.")]
