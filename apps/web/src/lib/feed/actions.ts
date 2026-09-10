"use server";

import { getSession, getToken } from "@/auth";
import {
  createServerClient,
  type AssetsSocialMediaSocialMediaAssetDescriptor,
  type SocialFollowsControllersFollowDto,
  type SocialReactionsReactionDto,
} from "@game-guild/client";
import { FeedMutationError } from "./errors";
import type {
  DeletedPostComment,
  DeletedSocialPost,
  DeletedStoryState,
  PostComment,
  SavedPostState,
  SharedPostState,
  SocialFollowState,
  SocialFeedPage,
  SocialMediaAsset,
  SocialProfile,
  SocialPostItem,
  SocialPostMutation,
  SocialReaction,
  SocialStory,
  ViewedPostState,
  ViewedStoryState,
} from "./contracts";
import {
  loadPostComments,
  loadSocialFeed,
  mapSocialFeedItem,
  type SocialFeedRequestError,
} from "./queries";

function createClient() {
  return createServerClient({
    baseUrl:
      process.env.API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:8080",
    auth: { getAccessToken: getToken },
  });
}

async function request<T>(input: Parameters<ReturnType<typeof createClient>["request"]>[0]) {
  const result = await createClient().request<T>(input);
  if (!result.ok) throw new FeedMutationError(result.error);
  return result.data;
}

// The generated Social Posts declarations are `void`, while the deployed API
// returns post/comment projections. Keep that generator mismatch narrow and
// never expose `unknown` to Task 8 callers.
function postMutation(value: SocialPostMutation): SocialPostMutation {
  return {
    id: value.id,
    content: value.content,
    createdAt: value.createdAt,
    visibility: value.visibility,
  };
}

export class SocialPostHydrationError extends Error {
  constructor(readonly postId: string) {
    super("Your post was published, but it is still being prepared for the feed.");
    this.name = "SocialPostHydrationError";
  }
}

export async function hydrateSocialPost(postId: string): Promise<SocialPostItem> {
  const item = await request<unknown>({ method: "GET", path: `/api/social/feed/posts/${postId}`, requiresAuth: true });
  return mapSocialFeedItem(item);
}

function mediaAsset(value: AssetsSocialMediaSocialMediaAssetDescriptor): SocialMediaAsset {
  return {
    assetReferenceId: value.assetReferenceId ?? "",
    deliveryUrl: value.deliveryUrl ?? null,
    mimeType: value.mimeType ?? null,
    sizeBytes: value.sizeBytes ?? 0,
    state: value.state ?? "Processing",
  };
}

function defaultHandle(email: string | null | undefined, userId: string) {
  const localPart = email?.split("@")[0]?.toLowerCase().replace(/[^a-z0-9_-]/g, "-")
    .replace(/^-+|-+$/g, "") || "member";
  return `${localPart.slice(0, 62)}-${userId.replace(/-/g, "").slice(0, 8)}`;
}

async function ensureCurrentSocialProfile() {
  const session = await getSession();
  const userId = session?.user?.id;
  if (!userId) {
    throw new FeedMutationError({
      status: 401,
      code: "AUTHENTICATION_ERROR",
      message: "Sign in to publish to the community.",
    });
  }

  const existing = await request<SocialProfile | null>({
    method: "GET",
    path: `/api/social/profiles/users/${userId}`,
    requiresAuth: true,
  });
  if (existing) return existing;

  const email = session.user.email;
  return request<SocialProfile>({
    method: "PUT",
    path: `/api/social/profiles/users/${userId}`,
    body: {
      handle: defaultHandle(email, userId),
      displayName: session.user.name?.trim() || email?.split("@")[0] || "GameGuild member",
      socialLinksJson: "{}",
    },
    requiresAuth: true,
  });
}

export async function loadSocialFeedAction(
  ...args: Parameters<typeof loadSocialFeed>
): Promise<SocialFeedPage> {
  return loadSocialFeed(...args).catch((error: SocialFeedRequestError) => {
    throw error;
  });
}

export async function loadPostCommentsAction(postId: string) {
  return loadPostComments(postId);
}

export async function createSocialPost(input: {
  content: string;
  visibility?: "Public" | "Followers" | "Private" | "Unlisted";
  assetReferenceId?: string | null;
  tags?: string[];
}): Promise<SocialPostItem> {
  await ensureCurrentSocialProfile();
  const data = await request<SocialPostMutation>({
    method: "POST",
    path: "/api/v1/posts",
    body: {
      content: input.content.trim(),
      visibility: input.visibility ?? "Public",
      assetReferenceId: input.assetReferenceId ?? null,
      tags: input.tags ?? [],
    },
    requiresAuth: true,
  });
  const post = postMutation(data);
  try {
    return await hydrateSocialPost(post.id);
  } catch {
    throw new SocialPostHydrationError(post.id);
  }
}

export async function updateSocialPost(postId: string, content: string): Promise<SocialPostMutation> {
  const data = await request<SocialPostMutation>({
    method: "PUT",
    path: `/api/v1/posts/${postId}`,
    body: { content: content.trim() },
    requiresAuth: true,
  });
  return postMutation(data);
}

export async function deleteSocialPost(postId: string): Promise<DeletedSocialPost> {
  await request<void>({ method: "DELETE", path: `/api/v1/posts/${postId}`, requiresAuth: true });
  return { postId, deleted: true };
}

export async function setPostReaction(
  postId: string,
  reaction: SocialReaction | null,
): Promise<SocialReactionsReactionDto | null> {
  if (!reaction) {
    await request<void>({
      method: "DELETE",
      path: "/api/social/reactions",
      body: { targetType: "Post", targetId: postId },
      requiresAuth: true,
    });
    return null;
  }
  return request<SocialReactionsReactionDto>({
    method: "PUT",
    path: "/api/social/reactions",
    body: { targetType: "Post", targetId: postId, type: reaction },
    requiresAuth: true,
  });
}

export async function createPostComment(
  postId: string,
  input: { content: string; parentCommentId?: string | null },
): Promise<PostComment> {
  const data = await request<PostComment>({
    method: "POST",
    path: `/api/v1/posts/${postId}/comments`,
    body: { content: input.content.trim(), parentCommentId: input.parentCommentId ?? null },
    requiresAuth: true,
  });
  return data;
}

export async function updatePostComment(postId: string, commentId: string, content: string): Promise<PostComment> {
  return request<PostComment>({
    method: "PUT",
    path: `/api/v1/posts/${postId}/comments/${commentId}`,
    body: { content: content.trim() },
    requiresAuth: true,
  });
}

export async function deletePostComment(postId: string, commentId: string): Promise<DeletedPostComment> {
  await request<void>({
    method: "DELETE",
    path: `/api/v1/posts/${postId}/comments/${commentId}`,
    requiresAuth: true,
  });
  return { postId, commentId, deleted: true };
}

export async function repostPost(postId: string, content = ""): Promise<SocialPostMutation> {
  const data = await request<SocialPostMutation>({
    method: "POST",
    path: `/api/v1/posts/${postId}/reposts`,
    body: { content: content.trim() || null },
    requiresAuth: true,
  });
  return postMutation(data);
}

export async function savePost(postId: string, save: boolean): Promise<SavedPostState> {
  if (save) {
    const state = await request<SavedPostState>({
      method: "PUT",
      path: `/api/social/saved-posts/${postId}`,
      requiresAuth: true,
    });
    return state;
  }
  await request<void>({
    method: "DELETE",
    path: `/api/social/saved-posts/${postId}`,
    requiresAuth: true,
  });
  return { postId, isSaved: false };
}

export async function followCreator(userId: string, follow: boolean): Promise<SocialFollowState> {
  if (!follow) {
    await request<void>({
      method: "DELETE",
      path: "/api/followers/unfollow",
      params: { entityId: userId, entityType: "User" },
      requiresAuth: true,
    });
    return { userId, isFollowing: false };
  }
  await request<SocialFollowsControllersFollowDto>({
    method: "POST",
    path: "/api/followers/follow",
    body: { entityId: userId, entityType: "User", notificationsEnabled: true },
    requiresAuth: true,
  });
  return { userId, isFollowing: true };
}

export async function sharePost(postId: string): Promise<SharedPostState> {
  await request<void>({ method: "POST", path: `/api/v1/posts/${postId}/share`, requiresAuth: true });
  return { postId, shared: true };
}

export async function recordPostView(postId: string): Promise<ViewedPostState> {
  await request<void>({ method: "POST", path: `/api/v1/posts/${postId}/view`, requiresAuth: true });
  return { postId, viewed: true };
}

export async function uploadSocialMedia(formData: FormData): Promise<SocialMediaAsset> {
  const asset = await request<AssetsSocialMediaSocialMediaAssetDescriptor>({
    method: "POST",
    path: "/v1/assets/social-media",
    body: formData,
    requiresAuth: true,
  });
  return mediaAsset(asset);
}

export async function getSocialMediaStatus(assetReferenceId: string): Promise<SocialMediaAsset> {
  const asset = await request<AssetsSocialMediaSocialMediaAssetDescriptor>({
    method: "GET",
    path: `/v1/assets/social-media/${assetReferenceId}`,
    requiresAuth: true,
  });
  return mediaAsset(asset);
}

export async function createStory(assetReferenceId: string, caption?: string | null): Promise<SocialStory> {
  await ensureCurrentSocialProfile();
  const data = await request<SocialStory>({
    method: "POST",
    path: "/api/social/stories",
    body: { assetReferenceId, caption: caption?.trim() || null },
    requiresAuth: true,
  });
  return data;
}

export async function markStoryViewed(storyId: string): Promise<ViewedStoryState> {
  await request<void>({
    method: "POST",
    path: `/api/social/stories/${storyId}/views`,
    requiresAuth: true,
  });
  return { storyId, viewed: true };
}

export async function deleteStory(storyId: string): Promise<DeletedStoryState> {
  await request<void>({ method: "DELETE", path: `/api/social/stories/${storyId}`, requiresAuth: true });
  return { storyId, deleted: true };
}
