import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  followCreator: vi.fn(),
  hydrateSocialPost: vi.fn(),
  savePost: vi.fn(),
  setPostReaction: vi.fn(),
  sharePost: vi.fn(),
}));

vi.mock("@/lib/feed/actions", () => ({
  followCreator: mocks.followCreator,
  hydrateSocialPost: mocks.hydrateSocialPost,
  savePost: mocks.savePost,
  setPostReaction: mocks.setPostReaction,
  sharePost: mocks.sharePost,
}));

import { PostAuthorFollow, PostEngagement } from "./post-engagement";

describe("PostEngagement", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, "", "/pt-BR/social");
    mocks.setPostReaction.mockResolvedValue({ kind: "confirmed", reaction: "Like", reactionsCount: 3 });
    mocks.savePost.mockResolvedValue({ postId: "post-1", isSaved: true });
    mocks.followCreator.mockResolvedValue({ userId: "author-1", isFollowing: true });
    mocks.sharePost.mockResolvedValue({ postId: "post-1", shared: true });
    mocks.hydrateSocialPost.mockResolvedValue({
      id: "post-1",
      kind: "Post",
      createdAt: "2026-09-10T00:00:00Z",
      author: { userId: "author-1", handle: "ada", displayName: "Ada", avatarUrl: null, isVerified: false },
      post: { content: "Post", mediaUrl: null, mediaType: null, visibility: "Public", isEdited: false, editedAt: null, repostedPost: null },
      testingSession: null,
      engagement: { reactionsCount: 11, commentsCount: 0, repostsCount: 0, viewsCount: 0 },
      viewer: { reaction: "Like", isSaved: false, isFollowingAuthor: false, hasReposted: false, canEdit: false, canDelete: false },
      tags: [],
    });
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: vi.fn().mockResolvedValue(undefined) },
    });
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: undefined,
    });
  });

  afterEach(cleanup);

  it("reconciles reaction and save controls with authoritative responses", async () => {
    mocks.setPostReaction.mockResolvedValueOnce({ kind: "confirmed", reaction: "Love", reactionsCount: 9 });
    mocks.savePost.mockResolvedValueOnce({ postId: "post-1", isSaved: false });
    render(
      <PostEngagement
        postId="post-1"
        authorName="Ada"
        initialReaction={null}
        initialReactionCount={2}
        initialSaved={false}
        commentCount={4}
        onOpenComments={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "React to post" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Remove Love reaction" })).toHaveTextContent("9"));

    fireEvent.click(screen.getByRole("button", { name: "Save post" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Save post" })).toHaveAttribute("aria-pressed", "false"));
  });

  it("keeps the committed optimistic reaction when projection hydration is pending", async () => {
    mocks.setPostReaction.mockResolvedValueOnce({ kind: "committed-needs-hydration", reaction: "Like" });
    render(
      <PostEngagement
        postId="post-1"
        authorName="Ada"
        initialReaction={null}
        initialReactionCount={2}
        initialSaved={false}
        commentCount={0}
        onOpenComments={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "React to post" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Remove Like reaction" })).toHaveTextContent("3"));
    expect(await screen.findByRole("status")).toHaveTextContent("Reaction saved");
    fireEvent.click(screen.getByRole("button", { name: "Retry reaction count refresh" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Remove Like reaction" })).toHaveTextContent("11"));
    expect(screen.queryByRole("button", { name: "Retry reaction count refresh" })).not.toBeInTheDocument();
    expect(mocks.hydrateSocialPost).toHaveBeenCalledWith("post-1");
    expect(mocks.setPostReaction).toHaveBeenCalledTimes(1);
  });

  it("rolls a failed reaction back visibly and ignores only duplicate reaction clicks", async () => {
    let rejectReaction!: (reason: Error) => void;
    mocks.setPostReaction.mockImplementationOnce(
      () => new Promise((_resolve, reject) => { rejectReaction = reject; }),
    );
    render(
      <PostEngagement
        postId="post-1"
        authorName="Ada"
        initialReaction={null}
        initialReactionCount={2}
        initialSaved={false}
        commentCount={0}
        onOpenComments={vi.fn()}
      />,
    );

    const reaction = screen.getByRole("button", { name: "React to post" });
    fireEvent.click(reaction);
    fireEvent.click(reaction);
    fireEvent.click(screen.getByRole("button", { name: "Save post" }));

    expect(mocks.setPostReaction).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(mocks.savePost).toHaveBeenCalledTimes(1));
    rejectReaction(new Error("Reaction is offline"));
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Reaction is offline"));
    expect(screen.getByRole("button", { name: "React to post" })).toHaveAttribute("aria-pressed", "false");
  });

  it("copies a locale-aware URL when native sharing rejects", async () => {
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: vi.fn().mockRejectedValue(new Error("dismissed")),
    });
    render(
      <PostEngagement
        postId="post-1"
        authorName="Ada"
        initialReaction={null}
        initialReactionCount={0}
        initialSaved={false}
        commentCount={0}
        onOpenComments={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Share post" }));

    await waitFor(() => expect(navigator.clipboard.writeText).toHaveBeenCalledWith(
      `${window.location.origin}/pt-BR/social/posts/post-1`,
    ));
    expect(mocks.sharePost).toHaveBeenCalledWith("post-1");
  });

  it("keeps save retryable after a visible rollback", async () => {
    mocks.savePost.mockRejectedValueOnce(new Error("Save unavailable"));
    render(
      <PostEngagement
        postId="post-1"
        authorName="Ada"
        initialReaction={null}
        initialReactionCount={0}
        initialSaved={false}
        commentCount={0}
        onOpenComments={vi.fn()}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Save post" }));
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Save unavailable"));
    expect(screen.getByRole("button", { name: "Save post" })).toHaveAttribute("aria-pressed", "false");

    fireEvent.click(screen.getByRole("button", { name: "Save post" }));
    await waitFor(() => expect(mocks.savePost).toHaveBeenCalledTimes(2));
  });

  it("reconciles author follow, rolls failure back and hides self-follow", async () => {
    let releaseFollow!: (value: { userId: string; isFollowing: boolean }) => void;
    mocks.followCreator.mockImplementationOnce(() => new Promise((resolve) => { releaseFollow = resolve; }));
    const view = render(
      <PostAuthorFollow
        authorId="author-1"
        authorName="Ada"
        currentUserId="viewer-1"
        initialFollowing={false}
      />,
    );

    const follow = screen.getByRole("button", { name: "Follow Ada" });
    fireEvent.click(follow);
    fireEvent.click(follow);
    expect(mocks.followCreator).toHaveBeenCalledTimes(1);
    releaseFollow({ userId: "author-1", isFollowing: true });
    await waitFor(() => {
      const settledFollow = screen.getByRole("button", { name: "Unfollow Ada" });
      expect(settledFollow).toHaveAttribute("aria-pressed", "true");
      expect(settledFollow).toBeEnabled();
    });

    mocks.followCreator.mockResolvedValueOnce({ userId: "author-1", isFollowing: false });
    fireEvent.click(screen.getByRole("button", { name: "Unfollow Ada" }));
    await waitFor(() => {
      const settledUnfollow = screen.getByRole("button", { name: "Follow Ada" });
      expect(settledUnfollow).toHaveAttribute("aria-pressed", "false");
      expect(settledUnfollow).toBeEnabled();
    });

    mocks.followCreator.mockRejectedValueOnce(new Error("Follow unavailable"));
    fireEvent.click(screen.getByRole("button", { name: "Follow Ada" }));
    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent("Follow unavailable"));
    expect(screen.getByRole("button", { name: "Follow Ada" })).toHaveAttribute("aria-pressed", "false");

    view.rerender(
      <PostAuthorFollow
        authorId="author-1"
        authorName="Ada"
        currentUserId="author-1"
        initialFollowing={false}
      />,
    );
    expect(screen.queryByRole("button", { name: /follow ada/i })).not.toBeInTheDocument();
  });
});
