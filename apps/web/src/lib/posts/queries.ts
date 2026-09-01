import { auth, getToken } from "@/auth";
import { createServerClient, GeneratedApi } from "@game-guild/client";

export interface PostCardData {
  id: string;
  authorId: string;
  authorName: string | null;
  authorHandle?: string | null;
  authorAvatarUrl?: string | null;
  communityLabel?: string | null;
  content: string;
  mediaUrl: string | null;
  mediaType: string | null;
  likesCount: number;
  commentsCount: number;
  sharesCount: number;
  isEdited: boolean;
  isPinned: boolean;
  createdAt: string;
  project?: {
    title: string;
    version?: string | null;
    platform?: string | null;
    status?: string | null;
    href?: string | null;
    buildHref?: string | null;
    playtestHref?: string | null;
  } | null;
  commentPreview?: Array<{
    id: string;
    authorName: string;
    authorHandle?: string | null;
    content: string;
    likesCount?: number;
  }>;
}

export type PostsStream = "feed" | "public" | "trending";

interface PostDto {
  id?: string;
  authorId?: string;
  content?: string;
  mediaUrl?: string | null;
  mediaType?: string | null;
  likesCount?: number;
  commentsCount?: number;
  sharesCount?: number;
  isEdited?: boolean;
  isPinned?: boolean;
  createdAt?: string;
}

interface UserDto {
  name?: string | null;
  email?: string | null;
}

export const POSTS_PAGE_SIZE = 6;

function createClient() {
  return createServerClient({
    baseUrl:
      process.env.API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      "http://localhost:8080",
    auth: { getAccessToken: getToken },
  });
}

function streamPath(stream: PostsStream): {
  path: string;
  requiresAuth: boolean;
} {
  if (stream === "trending")
    return { path: "/api/v1/posts/trending", requiresAuth: true };
  if (stream === "feed")
    return { path: "/api/v1/posts/feed", requiresAuth: true };
  return { path: "/api/v1/posts", requiresAuth: true };
}

async function resolveAuthorNames(
  client: ReturnType<typeof createServerClient>,
  authorIds: string[],
): Promise<Map<string, string | null>> {
  const users = new GeneratedApi.UsersModule(client);
  const unique = [...new Set(authorIds)];
  const entries = await Promise.all(
    unique.map(async (id) => {
      const result = await users
        .getUsersForGetUsersByUserId(id)
        .catch(() => null);
      const name = result?.ok
        ? ((result.data as UserDto).name ??
          (result.data as UserDto).email?.split("@")[0] ??
          null)
        : null;
      return [id, name] as const;
    }),
  );
  return new Map(entries);
}

export async function loadPosts(
  stream: PostsStream,
  skip: number,
  take = POSTS_PAGE_SIZE,
): Promise<PostCardData[]> {
  const session = await auth().catch(() => null);
  if (!session || typeof session === "function") return [];

  try {
    const client = createClient();
    const target = streamPath(stream);
    const result = await client.request<PostDto[]>({
      method: "GET",
      path: target.path,
      params: { skip, take },
      requiresAuth: target.requiresAuth,
    });
    if (!result.ok) return [];

    const raw = Array.isArray(result.data) ? result.data : [];
    const names = await resolveAuthorNames(
      client,
      raw.map((post) => String(post.authorId ?? "")),
    );

    return raw.map((post) => ({
      id: String(post.id ?? ""),
      authorId: String(post.authorId ?? ""),
      authorName: names.get(String(post.authorId ?? "")) ?? null,
      content: post.content ?? "",
      mediaUrl: post.mediaUrl ?? null,
      mediaType: post.mediaType ?? null,
      likesCount: post.likesCount ?? 0,
      commentsCount: post.commentsCount ?? 0,
      sharesCount: post.sharesCount ?? 0,
      isEdited: Boolean(post.isEdited),
      isPinned: Boolean(post.isPinned),
      createdAt: post.createdAt ?? new Date().toISOString(),
    }));
  } catch {
    return [];
  }
}
