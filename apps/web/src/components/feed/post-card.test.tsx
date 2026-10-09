import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  setPostReaction: vi.fn(),
  savePost: vi.fn(),
  repostPost: vi.fn(),
  sharePost: vi.fn(),
  followCreator: vi.fn(),
  hydrateSocialPost: vi.fn(),
  createPostComment: vi.fn(),
  deletePostComment: vi.fn(),
  updatePostComment: vi.fn(),
  deleteSocialPost: vi.fn(),
  updateSocialPost: vi.fn(),
  loadPostCommentsPageAction: vi.fn(),
  loadPostCommentRepliesPageAction: vi.fn(),
  recordPostView: vi.fn(),
}));

vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("@/lib/testing-lab/public-event-hydration", () => ({
  hydratePublicTestingEvent: vi.fn(),
}));
vi.mock("next/image", () => ({
  default: ({ alt = "", ...props }: Record<string, unknown>) =>
    createElement("img", { ...props, alt: typeof alt === "string" ? alt : "" }),
}));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));

import { PostCard } from "./post-card";
import { hydratePublicTestingEvent } from "@/lib/testing-lab/public-event-hydration";
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
    vi.mocked(hydratePublicTestingEvent).mockResolvedValue(null);
    mocks.setPostReaction.mockResolvedValue({ kind: "confirmed", reaction: "Like", reactionsCount: 3 });
    mocks.savePost.mockResolvedValue({ postId: "post-1", isSaved: true });
    mocks.repostPost.mockResolvedValue({ id: "repost-1" });
    mocks.sharePost.mockResolvedValue(undefined);
    mocks.followCreator.mockResolvedValue({ userId: "user-2", isFollowing: true });
    mocks.createPostComment.mockResolvedValue({
      id: "comment-1",
      postId: "post-1",
      authorId: "user-1",
      authorName: "Viewer",
      authorHandle: "viewer",
      authorAvatarUrl: null,
      parentCommentId: null,
      content: "Nice",
      likesCount: 0,
      isEdited: false,
      createdAt: "2026-09-10T00:00:00Z",
      updatedAt: null,
      replies: [],
    });
    mocks.loadPostCommentsPageAction.mockResolvedValue({ items: [], nextSkip: null });
    mocks.loadPostCommentRepliesPageAction.mockResolvedValue({ items: [], nextSkip: null });
    mocks.hydrateSocialPost.mockResolvedValue(item);
    mocks.recordPostView.mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: vi.fn().mockResolvedValue(undefined) }, share: undefined });
    Object.defineProperty(window, "innerWidth", { configurable: true, value: 1024 });
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      value: vi.fn().mockReturnValue({
        matches: false,
        media: "(max-width: 767px)",
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        addListener: vi.fn(),
        removeListener: vi.fn(),
        dispatchEvent: vi.fn(),
      }),
    });
  });
  afterEach(cleanup);

  it("uses authoritative reaction and save actions", async () => {
    render(<PostCard item={item} />);

    fireEvent.click(screen.getByRole("button", { name: /react to post/i }));
    await waitFor(() => expect(mocks.setPostReaction).toHaveBeenCalledWith("post-1", "Like"));
    expect(screen.getByRole("button", { name: /remove like reaction/i })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /save post/i }));
    await waitFor(() => expect(mocks.savePost).toHaveBeenCalledWith("post-1", true));
    expect(screen.getByRole("button", { name: /remove saved post/i })).toBeInTheDocument();
  });

  it("loads comments on demand and submits a comment", async () => {
    render(<PostCard item={item} />);
    fireEvent.click(screen.getByRole("button", { name: /comments/i }));
    await waitFor(() => expect(mocks.loadPostCommentsPageAction).toHaveBeenCalledWith("post-1", 0, 10));
    const comments = screen.getByRole("region", { name: "Comments" });
    expect(screen.getByTestId("post-card")).toContainElement(comments);
    expect(screen.queryByRole("dialog", { name: "Comments" })).not.toBeInTheDocument();

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
    expect(screen.queryByRole("button", { name: /remove like reaction/i })).not.toBeInTheDocument();
  });

  it("prevents duplicate repost submissions while the mutation is pending", async () => {
    let release!: (value: { id: string }) => void;
    mocks.repostPost.mockImplementationOnce(
      () => new Promise((resolve) => { release = resolve; }),
    );
    render(<PostCard item={item} />);

    fireEvent.click(screen.getByRole("button", { name: "Repost" }));
    const repost = screen.getByRole("button", { name: "Publish repost" });
    fireEvent.click(repost);
    fireEvent.click(repost);

    expect(mocks.repostPost).toHaveBeenCalledTimes(1);
    release({ id: "repost-1" });
    await waitFor(() => expect(screen.getByRole("button", { name: "Reposted" })).toHaveAttribute("aria-pressed", "true"));
  });

  it("renders testing session posts as event cards with dual sign-up paths", () => {
    render(
      <PostCard
        item={{
          ...item,
          id: "session-9",
          kind: "TestingSession",
          post: null,
          testingSession: {
            name: "Autumn cozy playtest",
            startsAt: "2026-09-25T18:00:00Z",
            endsAt: "2026-09-25T20:00:00Z",
            mode: "Online",
            status: "ApplicationsOpen",
            maxTesters: 20,
            registeredTesterCount: 8,
            availableTesterCount: 12,
          },
        }}
      />,
    );

    expect(screen.getByText("Autumn cozy playtest")).toBeInTheDocument();
    expect(screen.getByText("12 spots left")).toBeInTheDocument();
    expect(screen.getByText("8/20 testers signed in")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Join" })).toHaveAttribute("href", "/testing-lab/events/session-9");
    expect(screen.queryByRole("link", { name: "Sign in your product" })).not.toBeInTheDocument();
  });

  it("appends the designed event card to testing event announcements", async () => {
    vi.mocked(hydratePublicTestingEvent).mockResolvedValue({
      name: "Teste!",
      description: "Final gate playtest for the autumn jam.",
      startsAt: "2026-09-18T21:00:00.000Z",
      endsAt: "2026-09-18T23:00:00.000Z",
      mode: "Online",
      status: "ApplicationsOpen",
      registeredTesterCount: 0,
      maxTesters: 8,
      availableTesterCount: 8,
      games: [
        {
          projectId: "mothlight-id",
          title: "Mothlight",
          description: "A little forest spirit restores the valley.",
          imageUrl: "/testing-lab/seeded-games/mothlight.svg",
        },
        {
          projectId: "hollow-signal-id",
          title: "Hollow Signal",
          description: "Follow a radio signal across the island.",
          imageUrl: "/testing-lab/seeded-games/hollow-signal.svg",
        },
      ],
      approvedGameCount: 2,
      availableGameCount: 2,
      gameImages: ["/testing-lab/seeded-games/mothlight.svg", "/testing-lab/seeded-games/hollow-signal.svg"],
    });
    const announcement =
      "🧪 New testing event: Teste! Event starts Sep 18, 21:00 UTC. Details: /testing-lab/events/e112d20d-43d6-4016-bbac-5626f209053d";
    render(<PostCard item={{ ...item, content: announcement, post: { ...item.post!, content: announcement } }} />);

    expect(screen.getByText("Teste!")).toBeInTheDocument();
    expect(screen.queryByText(announcement)).not.toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.getByText(
          "Have a build ready for feedback? Join the playtest and submit your game.",
        ),
      ).toBeInTheDocument(),
    );
    expect(screen.getByText("Sep 18, 2026, 9:00 PM UTC")).toBeInTheDocument();
    expect(screen.getByText("Looking for games to test")).toBeInTheDocument();
    expect(screen.getByText("2 game spots open")).toBeInTheDocument();
    expect(screen.queryByText("0/8 testers signed in")).not.toBeInTheDocument();
    const eventBanner = screen.getByRole("link", { name: "Join playtest" }).closest("div.rounded-xl");
    expect(eventBanner?.querySelector('img[src="/testing-lab/seeded-games/mothlight.svg"]')).toBeInTheDocument();
    expect(eventBanner?.querySelector('img[src="/testing-lab/seeded-games/hollow-signal.svg"]')).toBeInTheDocument();
    expect(screen.queryByText("#playtest")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Join playtest" })).toHaveAttribute(
      "href",
      "/testing-lab/events/e112d20d-43d6-4016-bbac-5626f209053d",
    );
    expect(screen.queryByRole("link", { name: "Sign in product" })).not.toBeInTheDocument();
  });

  it("turns game-joined announcements into a game-focused playtest card", async () => {
    vi.mocked(hydratePublicTestingEvent).mockResolvedValue({
      name: "Game Jam Sprint Playtest",
      description: "Community playtest for this month's builds.",
      startsAt: "2026-09-18T21:00:00.000Z",
      endsAt: "2026-09-18T23:00:00.000Z",
      mode: "Online",
      status: "ApplicationsOpen",
      registeredTesterCount: 0,
      maxTesters: 12,
      availableTesterCount: 12,
      games: [
        {
          projectId: "mothlight-id",
          title: "Mothlight",
          description: "Find your way through a greenhouse that rearranges itself at night.",
          imageUrl: "/testing-lab/seeded-games/mothlight.svg",
        },
      ],
      approvedGameCount: 1,
      availableGameCount: 3,
      gameImages: ["/testing-lab/seeded-games/mothlight.svg"],
    });
    const announcement =
      "🎮 'Mothlight' just joined the testing event Game Jam Sprint Playtest! Follow the build and share your feedback: /testing-lab/events/490dc4af-4480-41cb-89a1-41dd5e555d62";

    render(
      <PostCard
        item={{
          ...item,
          content: announcement,
          post: { ...item.post!, content: announcement },
        }}
      />,
    );

    expect(screen.queryByText(announcement)).not.toBeInTheDocument();
    expect(screen.getByText("Mothlight joined the playtest")).toBeInTheDocument();
    expect(screen.getByText("Game Jam Sprint Playtest")).toBeInTheDocument();
    await waitFor(() =>
      expect(
        screen.getByText(
          "Find your way through a greenhouse that rearranges itself at night.",
        ),
      ).toBeInTheDocument(),
    );
    expect(screen.getByRole("link", { name: "View playtest" })).toHaveAttribute(
      "href",
      "/testing-lab/events/490dc4af-4480-41cb-89a1-41dd5e555d62",
    );
    expect(
      screen.getByRole("link", { name: "View playtest" }).closest("div.rounded-xl")
        ?.querySelector('img[src="/testing-lab/seeded-games/mothlight.svg"]'),
    ).toBeInTheDocument();
  });

  it("renders blog post announcements as a clickable embed card", () => {
    const announcement = "📝 New blog post: Devlog September 2026! Read it here: /blogs/tolstenko/devlog-september-2026";
    render(<PostCard item={{ ...item, post: { ...item.post!, content: announcement } }} />);

    expect(screen.getByText("📝 Blog post")).toBeInTheDocument();
    expect(screen.getByText("Devlog September 2026")).toBeInTheDocument();
    expect(screen.queryByText(/Read it here/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Read post" })).toHaveAttribute(
      "href",
      "/blogs/tolstenko/devlog-september-2026",
    );
  });

  it("renders blog post announcements with an excerpt", () => {
    const announcement =
      "📝 New blog post: Devlog September 2026! Behind the scenes of the autumn jam. Read it here: /blogs/tolstenko/devlog-september-2026";
    render(<PostCard item={{ ...item, post: { ...item.post!, content: announcement } }} />);

    expect(screen.getByText("Devlog September 2026")).toBeInTheDocument();
    expect(screen.getByText("Behind the scenes of the autumn jam.")).toBeInTheDocument();
    expect(screen.queryByText(/Read it here/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Read post" })).toHaveAttribute(
      "href",
      "/blogs/tolstenko/devlog-september-2026",
    );
  });

  it("still renders the caption for normal posts", () => {
    render(<PostCard item={item} />);

    expect(screen.getByText("Ship the build")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Read post" })).not.toBeInTheDocument();
  });
});
