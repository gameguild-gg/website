import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  createServerClient: vi.fn(),
  getToken: vi.fn(),
}));

vi.mock("@/auth", () => ({ getToken: mocks.getToken }));
vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
}));

import {
  SocialFeedRequestError,
  loadPostComments,
  loadSocialFeed,
  loadSocialPost,
  loadSocialProfileByHandle,
  loadStories,
  searchSocialProfiles,
} from "./queries";

describe("social feed queries", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ request: mocks.request });
  });

  it("forwards scope, opaque cursor, page size and tag unchanged", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: { items: [], nextCursor: "next-opaque" },
    });

    const page = await loadSocialFeed({
      scope: "following",
      cursor: "opaque+/=",
      take: 12,
      tag: "indiedev",
    });

    expect(mocks.request).toHaveBeenCalledWith({
      method: "GET",
      path: "/api/social/feed",
      params: {
        scope: "following",
        cursor: "opaque+/=",
        take: 12,
        tag: "indiedev",
      },
      requiresAuth: true,
    });
    expect(page).toEqual({ items: [], nextCursor: "next-opaque" });
  });

  it("maps optional API data into a stable feed contract", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: {
        items: [
          {
            id: "post-1",
            kind: "Post",
            createdAt: "2026-09-10T00:00:00Z",
            author: { userId: "user-1", displayName: "Ada", handle: "ada" },
            post: { content: "Hello", visibility: "Public" },
            engagement: { reactionsCount: 2 },
            viewer: { isSaved: true, canEdit: false },
            tags: ["indiedev"],
          },
        ],
        nextCursor: null,
      },
    });

    const page = await loadSocialFeed({ scope: "for-you" });

    expect(page.items[0]).toMatchObject({
      id: "post-1",
      author: { userId: "user-1", displayName: "Ada", handle: "ada" },
      post: { content: "Hello" },
      engagement: { reactionsCount: 2, commentsCount: 0, repostsCount: 0 },
      viewer: { isSaved: true, canEdit: false, canDelete: false },
    });
  });

  it("throws a typed request error instead of silently returning demo feed", async () => {
    mocks.request.mockResolvedValue({
      ok: false,
      error: { status: 503, code: "SERVER_ERROR", message: "offline" },
    });

    const request = loadSocialFeed({ scope: "saved" });

    await expect(request).rejects.toEqual(
      expect.objectContaining<Partial<SocialFeedRequestError>>({
        name: "SocialFeedRequestError",
        status: 503,
        code: "SERVER_ERROR",
        retryable: true,
      }),
    );
  });

  it("loads a permalink through the viewer-aware feed projection", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: {
        id: "post-1",
        kind: "Post",
        author: { userId: "user-2", displayName: "Lin" },
        post: { content: "Permalink" },
      },
    });

    const post = await loadSocialPost("post-1");

    expect(mocks.request).toHaveBeenCalledWith({
      method: "GET",
      path: "/api/social/feed/posts/post-1",
      requiresAuth: true,
    });
    expect(post).toMatchObject({ id: "post-1", post: { content: "Permalink" } });
  });

  it("resolves comment authors and nests replies", async () => {
    mocks.request
      .mockResolvedValueOnce({
        ok: true,
        data: [
          { id: "c-1", postId: "post-1", authorId: "user-1", content: "Root", createdAt: "2026-09-10T00:00:00Z" },
          { id: "c-2", postId: "post-1", authorId: "user-2", parentCommentId: "c-1", content: "Reply", createdAt: "2026-09-10T00:01:00Z" },
        ],
      })
      .mockResolvedValueOnce({ ok: true, data: { userId: "user-1", displayName: "Ada", handle: "ada" } })
      .mockResolvedValueOnce({ ok: true, data: { userId: "user-2", displayName: "Lin", handle: "lin" } });

    const comments = await loadPostComments("post-1");

    expect(comments).toHaveLength(1);
    expect(comments[0]).toMatchObject({ authorName: "Ada", replies: [{ authorName: "Lin", content: "Reply" }] });
  });

  it("hydrates creator suggestions with the viewer follow state", async () => {
    mocks.request
      .mockResolvedValueOnce({
        ok: true,
        data: [{ id: "profile-1", userId: "user-2", displayName: "Lin", handle: "lin" }],
      })
      .mockResolvedValueOnce({ ok: true, data: { "user-2": true } });

    const profiles = await searchSocialProfiles("", 5);

    expect(mocks.request).toHaveBeenLastCalledWith({
      method: "POST",
      path: "/api/followers/batch/status",
      body: { entityIds: ["user-2"], entityType: "User" },
      requiresAuth: true,
    });
    expect(profiles[0]?.isFollowing).toBe(true);
  });

  it("loads the viewer-aware aggregate social profile by handle", async () => {
    mocks.request.mockResolvedValueOnce({
      ok: true,
      data: { userId: "user-2", handle: "lin", displayName: "Lin", projectCount: 3, isFollowing: true },
    });

    const profile = await loadSocialProfileByHandle("@lin");

    expect(mocks.request).toHaveBeenNthCalledWith(1, {
      method: "GET",
      path: "/api/social/feed/profiles/lin",
      requiresAuth: true,
    });
    expect(profile).toMatchObject({ handle: "lin", displayName: "Lin", projectCount: 3, isFollowing: true });
  });

  it("keeps authenticated asset paths on the Web origin for session-aware proxying", async () => {
    process.env.NEXT_PUBLIC_API_URL = "https://api.example.test/";
    mocks.request.mockResolvedValueOnce({
      ok: true,
      data: [{
        id: "story-1",
        assetReferenceId: "asset-1",
        authorId: "user-1",
        mediaType: "image/png",
        mediaUrl: "/api/assets/asset-1/content",
      }],
    });

    try {
      const stories = await loadStories();
      expect(stories[0]?.mediaUrl).toBe("/api/assets/asset-1/content");
    } finally {
      delete process.env.NEXT_PUBLIC_API_URL;
    }
  });
});
