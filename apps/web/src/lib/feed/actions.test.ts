import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  createServerClient: vi.fn(),
  getSession: vi.fn(),
  getToken: vi.fn(),
}));

vi.mock("@/auth", () => ({ getSession: mocks.getSession, getToken: mocks.getToken }));
vi.mock("@game-guild/client", () => ({ createServerClient: mocks.createServerClient }));

import {
  createPostComment,
  createSocialPost,
  createStory,
  deletePostComment,
  deleteSocialPost,
  deleteStory,
  FeedMutationError,
  followCreator,
  getSocialMediaStatus,
  markStoryViewed,
  recordPostView,
  repostPost,
  savePost,
  sharePost,
  setPostReaction,
  updatePostComment,
  updateSocialPost,
  uploadSocialMedia,
} from "./actions";

describe("social feed actions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.request.mockReset();
    mocks.getSession.mockResolvedValue({
      user: { id: "user-1", email: "builder@example.com", name: "Ari Builder" },
    });
    mocks.getToken.mockResolvedValue("access-token");
    mocks.createServerClient.mockReturnValue({ request: mocks.request });
  });

  it("creates posts without accepting a caller-supplied actor or tenant", async () => {
    mocks.request.mockResolvedValue({ ok: true, data: { id: "post-1", content: "Ship it" } });

    const post = await createSocialPost({ content: " Ship it ", tags: ["release"] });

    expect(mocks.request).toHaveBeenNthCalledWith(2, expect.objectContaining({
      method: "POST",
      path: "/api/v1/posts",
      body: { content: "Ship it", visibility: "Public", assetReferenceId: null, tags: ["release"] },
    }));
    expect(mocks.request.mock.calls[1]?.[0].body).not.toHaveProperty("userId");
    expect(mocks.request.mock.calls[1]?.[0].body).not.toHaveProperty("tenantId");
    expect(post.id).toBe("post-1");
  });

  it("provisions a social profile before a user's first post", async () => {
    mocks.request
      .mockResolvedValueOnce({ ok: true, data: null })
      .mockResolvedValueOnce({ ok: true, data: { id: "profile-1", userId: "user-1" } })
      .mockResolvedValueOnce({ ok: true, data: { id: "post-1", content: "First post" } });

    await createSocialPost({ content: "First post" });

    expect(mocks.request).toHaveBeenNthCalledWith(2, {
      method: "PUT",
      path: "/api/social/profiles/users/user-1",
      body: {
        handle: "builder-user1",
        displayName: "Ari Builder",
        socialLinksJson: "{}",
      },
      requiresAuth: true,
    });
  });

  it("returns authoritative reaction, comment, repost and save responses", async () => {
    mocks.request
      .mockResolvedValueOnce({ ok: true, data: { id: "r-1", type: "Love", targetId: "post-1" } })
      .mockResolvedValueOnce({ ok: true, data: { id: "c-1", postId: "post-1", content: "Nice" } })
      .mockResolvedValueOnce({ ok: true, data: { id: "rp-1", repostOfPostId: "post-1", content: "Boost" } })
      .mockResolvedValueOnce({ ok: true, data: { postId: "post-1", isSaved: true } });

    await expect(setPostReaction("post-1", "Love")).resolves.toMatchObject({ type: "Love" });
    await expect(createPostComment("post-1", { content: "Nice" })).resolves.toMatchObject({ id: "c-1" });
    await expect(repostPost("post-1", "Boost")).resolves.toMatchObject({ id: "rp-1" });
    await expect(savePost("post-1", true)).resolves.toEqual({ postId: "post-1", isSaved: true });
  });

  it("uses the actor-safe follow contract", async () => {
    mocks.request.mockResolvedValue({ ok: true, data: undefined });

    await followCreator("user-2", false);

    expect(mocks.request).toHaveBeenCalledWith({
      method: "DELETE",
      path: "/api/followers/unfollow",
      params: { entityId: "user-2", entityType: "User" },
      requiresAuth: true,
    });
  });

  it("returns the persisted saved state after saving a post", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: { postId: "post-1", isSaved: true },
    });

    const saved = await savePost("post-1", true);

    expect(saved).toEqual({ postId: "post-1", isSaved: true });
  });

  it("returns an explicit unfollow state when the generated endpoint has no response body", async () => {
    mocks.request.mockResolvedValue({ ok: true, data: undefined });

    await expect(followCreator("user-2", false)).resolves.toEqual({
      userId: "user-2",
      isFollowing: false,
    });
  });

  it("maps each remaining mutation to a concrete authoritative response", async () => {
    mocks.request
      .mockResolvedValueOnce({ ok: true, data: { id: "post-1", content: "Updated" } })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: { id: "comment-1", postId: "post-1", content: "Updated comment" } })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: { assetReferenceId: "asset-1", state: "Ready", sizeBytes: 1 } })
      .mockResolvedValueOnce({ ok: true, data: { assetReferenceId: "asset-1", state: "Ready", sizeBytes: 1 } })
      .mockResolvedValueOnce({ ok: true, data: { id: "profile-1", userId: "user-1" } })
      .mockResolvedValueOnce({ ok: true, data: { id: "story-1", assetReferenceId: "asset-1" } })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: undefined });

    await expect(updateSocialPost("post-1", " Updated ")).resolves.toEqual({ id: "post-1", content: "Updated" });
    await expect(deleteSocialPost("post-1")).resolves.toEqual({ postId: "post-1", deleted: true });
    await expect(updatePostComment("post-1", "comment-1", " Updated comment ")).resolves.toEqual({ id: "comment-1", postId: "post-1", content: "Updated comment" });
    await expect(deletePostComment("post-1", "comment-1")).resolves.toEqual({ postId: "post-1", commentId: "comment-1", deleted: true });
    await expect(sharePost("post-1")).resolves.toEqual({ postId: "post-1", shared: true });
    await expect(recordPostView("post-1")).resolves.toEqual({ postId: "post-1", viewed: true });
    await expect(uploadSocialMedia(new FormData())).resolves.toEqual({ assetReferenceId: "asset-1", deliveryUrl: null, mimeType: null, sizeBytes: 1, state: "Ready" });
    await expect(getSocialMediaStatus("asset-1")).resolves.toEqual({ assetReferenceId: "asset-1", deliveryUrl: null, mimeType: null, sizeBytes: 1, state: "Ready" });
    await expect(createStory("asset-1")).resolves.toMatchObject({ id: "story-1", assetReferenceId: "asset-1" });
    await expect(markStoryViewed("story-1")).resolves.toEqual({ storyId: "story-1", viewed: true });
    await expect(deleteStory("story-1")).resolves.toEqual({ storyId: "story-1", deleted: true });
  });

  it("normalizes every API failure to the public typed mutation error", async () => {
    mocks.request.mockResolvedValue({
      ok: false,
      error: { status: 403, code: "FORBIDDEN", message: "No permission" },
    });

    await expect(setPostReaction("post-1", "Like")).rejects.toEqual(
      expect.objectContaining<Partial<FeedMutationError>>({
        name: "FeedMutationError",
        status: 403,
        code: "FORBIDDEN",
      }),
    );
  });

  it("returns concrete states for reaction removal, unsaving, and following", async () => {
    mocks.request
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: undefined })
      .mockResolvedValueOnce({ ok: true, data: { id: "follow-1", followedEntityId: "user-2" } });

    await expect(setPostReaction("post-1", null)).resolves.toBeNull();
    await expect(savePost("post-1", false)).resolves.toEqual({ postId: "post-1", isSaved: false });
    await expect(followCreator("user-2", true)).resolves.toEqual({ userId: "user-2", isFollowing: true });
  });

  it("maps failures from every Task 8 mutation wrapper to FeedMutationError", async () => {
    const failure = { ok: false as const, error: { status: 503, code: "OFFLINE", message: "offline" } };
    const mutations: Array<() => Promise<unknown>> = [
      () => createSocialPost({ content: "Post" }),
      () => updateSocialPost("post-1", "Post"),
      () => deleteSocialPost("post-1"),
      () => setPostReaction("post-1", "Like"),
      () => createPostComment("post-1", { content: "Comment" }),
      () => updatePostComment("post-1", "comment-1", "Comment"),
      () => deletePostComment("post-1", "comment-1"),
      () => repostPost("post-1"),
      () => savePost("post-1", true),
      () => savePost("post-1", false),
      () => followCreator("user-2", true),
      () => followCreator("user-2", false),
      () => sharePost("post-1"),
      () => recordPostView("post-1"),
      () => uploadSocialMedia(new FormData()),
      () => getSocialMediaStatus("asset-1"),
      () => createStory("asset-1"),
      () => markStoryViewed("story-1"),
      () => deleteStory("story-1"),
    ];

    for (const mutate of mutations) {
      mocks.request.mockResolvedValueOnce(failure);
      await expect(mutate()).rejects.toEqual(expect.objectContaining({
        name: "FeedMutationError",
        status: 503,
        code: "OFFLINE",
      }));
    }
  });
});
