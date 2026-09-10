import { getToken } from "@/auth";
import { createServerClient, type ApiError } from "@game-guild/client";
import type {
  FeedItemKind,
  FeedScope,
  PostComment,
  SocialFeedAuthor,
  SocialFeedItem,
  SocialFeedPage,
  SocialProfile,
  SocialReaction,
  SocialStory,
  TrendingTag,
} from "./contracts";

type UnknownRecord = Record<string, unknown>;

export class SocialFeedRequestError extends Error {
  readonly status: number;
  readonly code: string;
  readonly retryable: boolean;

  constructor(error: Partial<ApiError> & { message?: string }) {
    super(error.message || "The social feed request failed.");
    this.name = "SocialFeedRequestError";
    this.status = error.status ?? 0;
    this.code = error.code ?? "UNKNOWN";
    this.retryable = this.status === 0 || this.status === 429 || this.status >= 500;
  }
}

export const SOCIAL_FEED_PAGE_SIZE = 12;

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
  if (!result.ok) throw new SocialFeedRequestError(result.error);
  return result.data;
}

function record(value: unknown): UnknownRecord {
  return value && typeof value === "object" ? (value as UnknownRecord) : {};
}

function text(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function number(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : 0;
}

function bool(value: unknown) {
  return value === true;
}

function author(value: unknown): SocialFeedAuthor {
  const raw = record(value);
  const displayName = text(raw.displayName, "GameGuild creator");
  return {
    userId: text(raw.userId),
    handle: text(raw.handle, displayName.toLowerCase().replace(/[^a-z0-9]+/g, "")),
    displayName,
    avatarUrl: text(raw.avatarUrl) || null,
    isVerified: bool(raw.isVerified),
  };
}

function nullableText(value: unknown) {
  return text(value) || null;
}

function publicMediaUrl(value: unknown) {
  const mediaUrl = nullableText(value);
  if (!mediaUrl || /^(?:https?:|data:|blob:|\/\/)/i.test(mediaUrl)) return mediaUrl;

  // Authenticated assets must stay on the Web origin so the route handler can
  // exchange the session cookie for an API bearer token before streaming them.
  if (mediaUrl.startsWith("/api/assets/")) return mediaUrl;

  const apiBaseUrl = (
    process.env.NEXT_PUBLIC_API_URL ||
    process.env.API_URL ||
    "http://localhost:8080"
  ).replace(/\/$/, "");
  return new URL(mediaUrl.startsWith("/") ? mediaUrl : `/${mediaUrl}`, `${apiBaseUrl}/`).toString();
}

export function mapSocialFeedItem(value: unknown): SocialFeedItem {
  const raw = record(value);
  const post = record(raw.post);
  const original = record(post.repostedPost);
  const session = record(raw.testingSession);
  const engagement = record(raw.engagement);
  const viewer = record(raw.viewer);
  const kind = ["Post", "Repost", "TestingSession"].includes(text(raw.kind))
    ? (raw.kind as FeedItemKind)
    : "Post";
  const reaction = ["Like", "Love", "Insightful", "Celebrate", "Support", "Curious"].includes(
    text(viewer.reaction),
  )
    ? (viewer.reaction as SocialReaction)
    : null;

  return {
    id: text(raw.id),
    kind,
    createdAt: text(raw.createdAt, new Date(0).toISOString()),
    author: author(raw.author),
    post:
      kind === "TestingSession"
        ? null
        : {
            content: text(post.content),
            mediaUrl: publicMediaUrl(post.mediaUrl),
            mediaType: nullableText(post.mediaType),
            visibility: text(post.visibility, "Public"),
            isEdited: bool(post.isEdited),
            editedAt: nullableText(post.editedAt),
            repostedPost: original.id
              ? {
                  id: text(original.id),
                  author: author(original.author),
                  content: text(original.content),
                  mediaUrl: publicMediaUrl(original.mediaUrl),
                  mediaType: nullableText(original.mediaType),
                  createdAt: text(original.createdAt, new Date(0).toISOString()),
                }
              : null,
          },
    testingSession:
      kind !== "TestingSession"
        ? null
        : {
            name: text(session.name, "Testing Lab session"),
            startsAt: text(session.startsAt),
            endsAt: text(session.endsAt),
            mode: text(session.mode),
            status: text(session.status),
            maxTesters: number(session.maxTesters),
            registeredTesterCount: number(session.registeredTesterCount),
            availableTesterCount: number(session.availableTesterCount),
          },
    engagement: {
      reactionsCount: number(engagement.reactionsCount),
      commentsCount: number(engagement.commentsCount),
      repostsCount: number(engagement.repostsCount),
      viewsCount: number(engagement.viewsCount),
    },
    viewer: {
      reaction,
      isSaved: bool(viewer.isSaved),
      isFollowingAuthor: bool(viewer.isFollowingAuthor),
      hasReposted: bool(viewer.hasReposted),
      canEdit: bool(viewer.canEdit),
      canDelete: bool(viewer.canDelete),
    },
    tags: Array.isArray(raw.tags) ? raw.tags.filter((tag): tag is string => typeof tag === "string") : [],
  };
}

export async function loadSocialFeed(input: {
  scope: FeedScope;
  cursor?: string | null;
  take?: number;
  tag?: string | null;
  signal?: AbortSignal;
}): Promise<SocialFeedPage> {
  const data = record(
    await request<unknown>({
      method: "GET",
      path: "/api/social/feed",
      params: {
        scope: input.scope,
        cursor: input.cursor ?? undefined,
        take: input.take ?? SOCIAL_FEED_PAGE_SIZE,
        tag: input.tag ?? undefined,
      },
      requiresAuth: true,
      ...(input.signal ? { signal: input.signal } : {}),
    }),
  );
  return {
    items: Array.isArray(data.items) ? data.items.map(mapSocialFeedItem) : [],
    nextCursor: nullableText(data.nextCursor),
  };
}

export async function loadSocialPost(postId: string) {
  const data = await request<unknown>({
    method: "GET",
    path: `/api/social/feed/posts/${postId}`,
    requiresAuth: true,
  });
  return data ? mapSocialFeedItem(data) : null;
}

function mapComment(
  value: unknown,
  profiles: Map<string, SocialProfile> = new Map(),
): PostComment {
  const raw = record(value);
  const profile = profiles.get(text(raw.authorId));
  return {
    id: text(raw.id),
    postId: text(raw.postId),
    authorId: text(raw.authorId),
    authorName: text(raw.authorName, profile?.displayName || "GameGuild creator"),
    authorHandle: text(raw.authorHandle, profile?.handle || "creator"),
    authorAvatarUrl: nullableText(raw.authorAvatarUrl) ?? profile?.avatarUrl ?? null,
    parentCommentId: nullableText(raw.parentCommentId),
    content: text(raw.content),
    likesCount: number(raw.likesCount),
    isEdited: bool(raw.isEdited),
    createdAt: text(raw.createdAt),
    updatedAt: nullableText(raw.updatedAt),
    replies: Array.isArray(raw.replies)
      ? raw.replies.map((reply) => mapComment(reply, profiles))
      : [],
  };
}

export async function loadPostComments(postId: string, skip = 0, take = 50) {
  const data = await request<unknown>({
    method: "GET",
    path: `/api/v1/posts/${postId}/comments`,
    params: { skip, take },
    requiresAuth: true,
  });
  if (!Array.isArray(data)) return [];
  const authorIds = [
    ...new Set(
      data
        .map((value) => text(record(value).authorId))
        .filter(Boolean),
    ),
  ];
  const profiles = new Map<string, SocialProfile>();
  await Promise.all(
    authorIds.map(async (authorId) => {
      try {
        const profileData = await request<unknown>({
          method: "GET",
          path: `/api/social/profiles/users/${authorId}`,
          requiresAuth: true,
        });
        if (profileData) profiles.set(authorId, mapProfile(profileData));
      } catch {
        // A missing public profile does not hide the comment itself.
      }
    }),
  );
  const flat = data.map((value) => mapComment(value, profiles));
  const byId = new Map(flat.map((comment) => [comment.id, comment]));
  const roots: PostComment[] = [];
  flat.forEach((comment) => {
    const parent = comment.parentCommentId
      ? byId.get(comment.parentCommentId)
      : null;
    if (parent) parent.replies.push(comment);
    else roots.push(comment);
  });
  return roots;
}

export async function loadStories(): Promise<SocialStory[]> {
  const data = await request<unknown>({ method: "GET", path: "/api/social/stories", requiresAuth: true });
  return Array.isArray(data)
    ? data.map((value) => {
        const raw = record(value);
        return {
          id: text(raw.id),
          assetReferenceId: text(raw.assetReferenceId),
          authorId: text(raw.authorId),
          caption: nullableText(raw.caption),
          createdAt: text(raw.createdAt),
          expiresAt: text(raw.expiresAt),
          isViewed: bool(raw.isViewed),
          mediaType: nullableText(raw.mediaType),
          mediaUrl: publicMediaUrl(raw.mediaUrl),
        };
      })
    : [];
}

function mapProfile(value: unknown): SocialProfile {
  const raw = record(value);
  return {
    id: text(raw.id),
    userId: text(raw.userId, text(raw.id)),
    handle: text(raw.handle),
    displayName: text(raw.displayName, "GameGuild creator"),
    avatarUrl: nullableText(raw.avatarUrl),
    bannerUrl: nullableText(raw.bannerUrl),
    bio: nullableText(raw.bio),
    headline: nullableText(raw.headline),
    location: nullableText(raw.location),
    timeZone: nullableText(raw.timeZone),
    websiteUrl: nullableText(raw.websiteUrl),
    availabilityStatus: nullableText(raw.availabilityStatus),
    isVerified: bool(raw.isVerified) || Boolean(raw.verifiedAt),
    projectCount: number(raw.projectCount),
    postCount: number(raw.postCount),
    followerCount: number(raw.followerCount),
    followingCount: number(raw.followingCount),
    isFollowing: bool(raw.isFollowing),
  };
}

export async function loadSocialProfile(userId: string) {
  const data = await request<unknown>({
    method: "GET",
    path: `/api/social/feed/profiles/users/${userId}`,
    requiresAuth: true,
  });
  return data ? mapProfile(data) : null;
}

async function hydrateFollowState(profiles: SocialProfile[]) {
  const entityIds = profiles.map((profile) => profile.userId).filter(Boolean);
  if (entityIds.length === 0) return profiles;
  try {
    const states = record(
      await request<unknown>({
        method: "POST",
        path: "/api/followers/batch/status",
        body: { entityIds, entityType: "User" },
        requiresAuth: true,
      }),
    );
    return profiles.map((profile) => ({
      ...profile,
      isFollowing: states[profile.userId] === true,
    }));
  } catch {
    return profiles;
  }
}

export async function loadSocialProfileByHandle(handle: string) {
  const data = await request<unknown>({
    method: "GET",
    path: `/api/social/feed/profiles/${encodeURIComponent(handle.replace(/^@/, ""))}`,
    requiresAuth: true,
  });
  if (!data) return null;
  return mapProfile(data);
}

export async function searchSocialProfiles(query = "", take = 5) {
  const data = await request<unknown>({
    method: "GET",
    path: "/api/social/profiles/search",
    params: { query, take },
    requiresAuth: true,
  });
  const profiles = Array.isArray(data) ? data.map(mapProfile) : [];
  return hydrateFollowState(profiles);
}

export async function loadTrendingTags(count = 6): Promise<TrendingTag[]> {
  const data = await request<unknown>({
    method: "GET",
    path: "/api/v1/posts/tags/popular",
    params: { count },
    requiresAuth: true,
  });
  return Array.isArray(data)
    ? data.map((value) => {
        const raw = record(value);
        return {
          name: text(raw.name, text(raw.tag)),
          postCount: number(raw.postCount ?? raw.usageCount ?? raw.count),
        };
      })
    : [];
}
