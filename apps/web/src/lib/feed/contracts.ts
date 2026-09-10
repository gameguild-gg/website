export type FeedScope = "for-you" | "following" | "community" | "saved";

export type FeedItemKind = "Post" | "Repost" | "TestingSession";

export interface SocialFeedAuthor {
  userId: string;
  handle: string;
  displayName: string;
  avatarUrl: string | null;
  isVerified: boolean;
}

export interface SocialFeedOriginalPost {
  id: string;
  author: SocialFeedAuthor;
  content: string;
  mediaUrl: string | null;
  mediaType: string | null;
  createdAt: string;
}

export interface SocialFeedPost {
  content: string;
  mediaUrl: string | null;
  mediaType: string | null;
  visibility: string;
  isEdited: boolean;
  editedAt: string | null;
  repostedPost: SocialFeedOriginalPost | null;
}

export interface SocialFeedTestingSession {
  name: string;
  startsAt: string;
  endsAt: string;
  mode: string;
  status: string;
  maxTesters: number;
  registeredTesterCount: number;
  availableTesterCount: number;
}

export interface SocialFeedEngagement {
  reactionsCount: number;
  commentsCount: number;
  repostsCount: number;
  viewsCount: number;
}

export interface SocialFeedViewerState {
  reaction: SocialReaction | null;
  isSaved: boolean;
  isFollowingAuthor: boolean;
  hasReposted: boolean;
  canEdit: boolean;
  canDelete: boolean;
}

export interface SocialFeedItem {
  id: string;
  kind: FeedItemKind;
  createdAt: string;
  author: SocialFeedAuthor;
  post: SocialFeedPost | null;
  testingSession: SocialFeedTestingSession | null;
  engagement: SocialFeedEngagement;
  viewer: SocialFeedViewerState;
  tags: string[];
}

/** The complete persisted feed projection returned after publishing a post. */
export type SocialPostItem = SocialFeedItem;

/** Serializable Server Action result that preserves a committed post receipt. */
export type SocialPostPublicationResult =
  | { kind: "published"; post: SocialPostItem }
  | { kind: "needs-hydration"; postId: string };

export interface SocialFeedPage {
  items: SocialFeedItem[];
  nextCursor: string | null;
}

export type SocialReaction =
  | "Like"
  | "Love"
  | "Insightful"
  | "Celebrate"
  | "Support"
  | "Curious";

export interface SocialPostMutation {
  id: string;
  content: string;
  createdAt?: string;
  visibility?: string;
}

export interface DeletedSocialPost { postId: string; deleted: true; }
export interface DeletedPostComment { postId: string; commentId: string; deleted: true; }
export interface SocialFollowState { userId: string; isFollowing: boolean; }
export interface SharedPostState { postId: string; shared: true; }
export interface ViewedPostState { postId: string; viewed: true; }
export interface ViewedStoryState { storyId: string; viewed: true; }
export interface DeletedStoryState { storyId: string; deleted: true; }

export interface PostComment {
  id: string;
  postId: string;
  authorId: string;
  authorName: string;
  authorHandle: string;
  authorAvatarUrl: string | null;
  parentCommentId: string | null;
  content: string;
  likesCount: number;
  isEdited: boolean;
  createdAt: string;
  updatedAt: string | null;
  replies: PostComment[];
}

export interface SavedPostState {
  postId: string;
  isSaved: boolean;
}

export interface SocialStory {
  id: string;
  assetReferenceId: string;
  authorId: string;
  caption: string | null;
  createdAt: string;
  expiresAt: string;
  isViewed: boolean;
  mediaType: string | null;
  mediaUrl: string | null;
}

export type SocialMediaProcessingState = "Processing" | "Ready" | "Rejected";

export interface SocialMediaAsset {
  assetReferenceId: string;
  deliveryUrl: string | null;
  mimeType: string | null;
  sizeBytes: number;
  state: SocialMediaProcessingState;
}

export interface SocialProfile {
  id: string;
  userId: string;
  handle: string;
  displayName: string;
  avatarUrl: string | null;
  bannerUrl: string | null;
  bio: string | null;
  headline: string | null;
  location: string | null;
  timeZone: string | null;
  websiteUrl: string | null;
  availabilityStatus: string | null;
  isVerified: boolean;
  projectCount: number;
  postCount: number;
  followerCount: number;
  followingCount: number;
  isFollowing: boolean;
}

export interface TrendingTag {
  name: string;
  postCount: number;
}
