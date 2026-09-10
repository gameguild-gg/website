import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  setPostReaction: vi.fn(),
  savePost: vi.fn(),
  repostPost: vi.fn(),
  sharePost: vi.fn(),
  createPostComment: vi.fn(),
  deleteSocialPost: vi.fn(),
  updateSocialPost: vi.fn(),
  loadPostCommentsAction: vi.fn(),
  recordPostView: vi.fn(),
}));

vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("next/image", () => ({ default: (props: Record<string, unknown>) => <img alt="" {...props} /> }));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));

import { PostCard } from "./post-card";
import type { SocialFeedItem } from "@/lib/feed/contracts";

const item: SocialFeedItem = {
  id: "post-1",
  kind: "Post",
  createdAt: "2026-09-10T00:00:00Z",
  author: { userId: "user-2", displayName: "Ada Builder", handle: "ada", avatarUrl: null, isVerified: true },
  post: { content: "Ship the build", mediaUrl: null, mediaType: null, visibility: "Public", isEdited: false, editedAt: null, repostedPost: null },
  testingSession: null,
  engagement: { reactionsCount: 2, commentsCount: 0, repostsCount: 0, viewsCount: 1 },
  viewer: { reaction: null, isSaved: false, isFollowingAuthor: false, hasReposted: false, canEdit: true, canDelete: true },
  tags: ["release"],
};

describe("PostCard", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.setPostReaction.mockResolvedValue({ type: "Like" });
    mocks.savePost.mockResolvedValue({ postId: "post-1", isSaved: true });
    mocks.repostPost.mockResolvedValue({ id: "repost-1" });
    mocks.sharePost.mockResolvedValue(undefined);
    mocks.createPostComment.mockResolvedValue({ id: "comment-1", content: "Nice" });
    mocks.loadPostCommentsAction.mockResolvedValue([]);
    mocks.recordPostView.mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) }, share: undefined });
  });
  afterEach(cleanup);

  it("uses authoritative reaction and save actions", async () => {
    render(<PostCard item={item} />);

    fireEvent.click(screen.getByRole("button", { name: /react to post/i }));
    await waitFor(() => expect(mocks.setPostReaction).toHaveBeenCalledWith("post-1", "Like"));
    expect(screen.getByRole("button", { name: /remove reaction/i })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /save post/i }));
    await waitFor(() => expect(mocks.savePost).toHaveBeenCalledWith("post-1", true));
    expect(screen.getByRole("button", { name: /remove saved post/i })).toBeInTheDocument();
  });

  it("loads comments on demand and submits a comment", async () => {
    render(<PostCard item={item} />);
    fireEvent.click(screen.getByRole("button", { name: /comments/i }));
    await waitFor(() => expect(mocks.loadPostCommentsAction).toHaveBeenCalledWith("post-1"));

    fireEvent.change(screen.getByPlaceholderText(/add a comment/i), { target: { value: "Nice" } });
    fireEvent.submit(screen.getByPlaceholderText(/add a comment/i).closest("form")!);
    await waitFor(() => expect(mocks.createPostComment).toHaveBeenCalledWith("post-1", { content: "Nice" }));
  });

  it("records a share before using the browser clipboard fallback", async () => {
    render(<PostCard item={item} />);
    fireEvent.click(screen.getByRole("button", { name: /share post/i }));
    await waitFor(() => expect(mocks.sharePost).toHaveBeenCalledWith("post-1"));
    expect(navigator.clipboard.writeText).toHaveBeenCalled();
  });

  it("rolls a failed reaction back to the authoritative viewer state", async () => {
    mocks.setPostReaction.mockRejectedValueOnce(new Error("offline"));
    render(<PostCard item={item} />);

    const reaction = screen.getByRole("button", { name: /react to post/i });
    fireEvent.click(reaction);

    await waitFor(() => expect(reaction).toHaveAttribute("aria-pressed", "false"));
    expect(screen.queryByRole("button", { name: /remove reaction/i })).not.toBeInTheDocument();
  });

  it("prevents duplicate repost submissions while the mutation is pending", async () => {
    let release!: (value: { id: string }) => void;
    mocks.repostPost.mockImplementationOnce(
      () => new Promise((resolve) => { release = resolve; }),
    );
    render(<PostCard item={item} />);

    const repost = screen.getByRole("button", { name: "Repost" });
    fireEvent.click(repost);
    fireEvent.click(repost);

    expect(mocks.repostPost).toHaveBeenCalledTimes(1);
    release({ id: "repost-1" });
    await waitFor(() => expect(repost).toHaveAttribute("aria-pressed", "true"));
  });
});
