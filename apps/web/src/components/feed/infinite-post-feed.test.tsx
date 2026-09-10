import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ loadSocialFeedAction: vi.fn() }));

vi.mock("@/lib/feed/actions", () => ({ loadSocialFeedAction: mocks.loadSocialFeedAction }));
vi.mock("next/image", () => ({ default: (props: Record<string, unknown>) => <img alt="" {...props} /> }));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));

import { InfinitePostFeed } from "./infinite-post-feed";
import type { SocialFeedItem } from "@/lib/feed/contracts";

function post(id: string, content = id): SocialFeedItem {
  return {
    id,
    kind: "Post",
    createdAt: new Date().toISOString(),
    author: { userId: "author-1", displayName: "Ada Builder", handle: "ada", avatarUrl: null, isVerified: false },
    post: { content, mediaUrl: null, mediaType: null, visibility: "Public", isEdited: false, editedAt: null, repostedPost: null },
    testingSession: null,
    engagement: { reactionsCount: 3, commentsCount: 1, repostsCount: 0, viewsCount: 0 },
    viewer: { reaction: null, isSaved: false, isFollowingAuthor: false, hasReposted: false, canEdit: false, canDelete: false },
    tags: [],
  };
}

function intersectionCallback() {
  let callback: IntersectionObserverCallback | undefined;
  const observe = vi.fn();
  const stub = class {
    constructor(cb: IntersectionObserverCallback) { callback = cb; }
    observe = observe;
    disconnect = vi.fn();
  };
  vi.stubGlobal("IntersectionObserver", stub);
  return {
    trigger(intersects: boolean) {
      callback?.([{ isIntersecting: intersects } as IntersectionObserverEntry], {} as IntersectionObserver);
    },
    observe,
  };
}

describe("InfinitePostFeed", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(() => { cleanup(); vi.unstubAllGlobals(); });

  it("renders the server page and requests the next opaque cursor", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedAction.mockResolvedValue({ items: [post("p-2")], nextCursor: "cursor-2" });

    render(<InfinitePostFeed scope="following" initialItems={[post("p-1")]} initialNextCursor="opaque+/=" />);

    expect(screen.getAllByTestId("post-card")).toHaveLength(1);
    io.trigger(true);
    await waitFor(() => expect(mocks.loadSocialFeedAction).toHaveBeenCalledWith({ scope: "following", cursor: "opaque+/=" }));
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
  });

  it("deduplicates items and stops when the API closes the cursor", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedAction.mockResolvedValue({ items: [post("p-1"), post("p-2")], nextCursor: null });

    render(<InfinitePostFeed scope="community" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    io.trigger(true);

    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
    expect(screen.getByText(/all caught up/i)).toBeInTheDocument();
    io.trigger(true);
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(mocks.loadSocialFeedAction).toHaveBeenCalledTimes(1);
  });

  it("shows a retry action instead of fake content when pagination fails", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedAction.mockRejectedValue(new Error("offline"));

    render(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    io.trigger(true);

    expect(await screen.findByRole("button", { name: /try again/i })).toBeInTheDocument();
  });

  it("shows a scope-specific empty state", () => {
    intersectionCallback();
    render(<InfinitePostFeed scope="saved" initialItems={[]} initialNextCursor={null} />);
    expect(screen.getByText(/posts you save will appear here/i)).toBeInTheDocument();
  });
});
