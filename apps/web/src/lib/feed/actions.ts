"use server";

import { getSession, getToken } from "@/auth";
import { createServerClient, type ApiError } from "@game-guild/client";
import type {
  PostComment,
  SavedPostState,
  SocialFeedPage,
  SocialMediaAsset,
  SocialProfile,
  SocialPostMutation,
  SocialReaction,
  SocialStory,
} from "./contracts";
import {
  loadPostComments,
  loadSocialFeed,
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

class FeedMutationError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(error: Partial<ApiError> & { message?: string }) {
    super(error.message || "The social action failed.");
    this.name = "FeedMutationError";
    this.status = error.status ?? 0;
    this.code = error.code ?? "UNKNOWN";
  }
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
}) {
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
  return data;
}

export async function updateSocialPost(postId: string, content: string) {
  const data = await request<SocialPostMutation>({
    method: "PUT",
    path: `/api/v1/posts/${postId}`,
    body: { content: content.trim() },
    requiresAuth: true,
  });
  return data;
}

export async function deleteSocialPost(postId: string) {
  await request<void>({ method: "DELETE", path: `/api/v1/posts/${postId}`, requiresAuth: true });
}

export async function setPostReaction(postId: string, reaction: SocialReaction | null) {
  if (!reaction) {
    await request<void>({
      method: "DELETE",
      path: "/api/social/reactions",
      body: { targetType: "Post", targetId: postId },
      requiresAuth: true,
    });
    return null;
  }
  return request<{ id: string; type: SocialReaction; targetId: string }>({
    method: "PUT",
    path: "/api/social/reactions",
    body: { targetType: "Post", targetId: postId, type: reaction },
    requiresAuth: true,
  });
}

export async function createPostComment(
  postId: string,
  input: { content: string; parentCommentId?: string | null },
) {
  const data = await request<PostComment>({
    method: "POST",
    path: `/api/v1/posts/${postId}/comments`,
    body: { content: input.content.trim(), parentCommentId: input.parentCommentId ?? null },
    requiresAuth: true,
  });
  return data;
}

export async function updatePostComment(postId: string, commentId: string, content: string) {
  return request<PostComment>({
    method: "PUT",
    path: `/api/v1/posts/${postId}/comments/${commentId}`,
    body: { content: content.trim() },
    requiresAuth: true,
  });
}

export async function deletePostComment(postId: string, commentId: string) {
  await request<void>({
    method: "DELETE",
    path: `/api/v1/posts/${postId}/comments/${commentId}`,
    requiresAuth: true,
  });
}

export async function repostPost(postId: string, content = "") {
  const data = await request<SocialPostMutation>({
    method: "POST",
    path: `/api/v1/posts/${postId}/reposts`,
    body: { content: content.trim() || null },
    requiresAuth: true,
  });
  return data;
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

export async function followCreator(userId: string, follow: boolean) {
  if (!follow) {
    const result = await request<unknown>({
      method: "DELETE",
      path: "/api/followers/unfollow",
      params: { entityId: userId, entityType: "User" },
      requiresAuth: true,
    });
    return result;
  }
  const result = await request<unknown>({
    method: "POST",
    path: "/api/followers/follow",
    body: { entityId: userId, entityType: "User", notificationsEnabled: true },
    requiresAuth: true,
  });
  return result;
}

export async function sharePost(postId: string) {
  await request<void>({ method: "POST", path: `/api/v1/posts/${postId}/share`, requiresAuth: true });
}

export async function recordPostView(postId: string) {
  await request<void>({ method: "POST", path: `/api/v1/posts/${postId}/view`, requiresAuth: true });
}

export async function uploadSocialMedia(formData: FormData): Promise<SocialMediaAsset> {
  return request<SocialMediaAsset>({
    method: "POST",
    path: "/v1/assets/social-media",
    body: formData,
    requiresAuth: true,
  });
}

export async function getSocialMediaStatus(assetReferenceId: string): Promise<SocialMediaAsset> {
  return request<SocialMediaAsset>({
    method: "GET",
    path: `/v1/assets/social-media/${assetReferenceId}`,
    requiresAuth: true,
  });
}

export async function createStory(assetReferenceId: string, caption?: string | null) {
  await ensureCurrentSocialProfile();
  const data = await request<SocialStory>({
    method: "POST",
    path: "/api/social/stories",
    body: { assetReferenceId, caption: caption?.trim() || null },
    requiresAuth: true,
  });
  return data;
}

export async function markStoryViewed(storyId: string) {
  await request<void>({
    method: "POST",
    path: `/api/social/stories/${storyId}/views`,
    requiresAuth: true,
  });
}

export async function deleteStory(storyId: string) {
  await request<void>({ method: "DELETE", path: `/api/social/stories/${storyId}`, requiresAuth: true });
}
