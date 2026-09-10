import "@testing-library/jest-dom/vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ loadSocialFeedPage: vi.fn() }));

vi.mock("@/lib/feed/pagination", () => ({ loadSocialFeedPage: mocks.loadSocialFeedPage }));
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
    mocks.loadSocialFeedPage.mockResolvedValue({ items: [post("p-2")], nextCursor: "cursor-2" });

    render(<InfinitePostFeed scope="following" initialItems={[post("p-1")]} initialNextCursor="opaque+/=" />);

    expect(screen.getAllByTestId("post-card")).toHaveLength(1);
    act(() => io.trigger(true));
    await waitFor(() => expect(mocks.loadSocialFeedPage).toHaveBeenCalledWith(expect.objectContaining({ scope: "following", cursor: "opaque+/=" })));
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
  });

  it("deduplicates items and stops when the API closes the cursor", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedPage.mockResolvedValue({ items: [post("p-1"), post("p-2")], nextCursor: null });

    render(<InfinitePostFeed scope="community" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    act(() => io.trigger(true));

    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
    expect(screen.getByText(/all caught up/i)).toBeInTheDocument();
    act(() => io.trigger(true));
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(mocks.loadSocialFeedPage).toHaveBeenCalledTimes(1);
  });

  it("shows a retry action instead of fake content when pagination fails", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedPage.mockRejectedValue(new Error("offline"));

    render(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    act(() => io.trigger(true));

    expect(await screen.findByRole("button", { name: /try again/i })).toBeInTheDocument();
  });

  it("shows a scope-specific empty state", () => {
    intersectionCallback();
    render(<InfinitePostFeed scope="saved" initialItems={[]} initialNextCursor={null} />);
    expect(screen.getByText(/posts you save will appear here/i)).toBeInTheDocument();
  });

  it("inserts an authoritative published item at the top once", async () => {
    intersectionCallback();
    const created = post("created", "Authoritative post");
    const view = render(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor={null} />);

    view.rerender(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor={null} publishedItem={created} />);
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
    expect(screen.getAllByTestId("post-card")[0]).toHaveTextContent("Authoritative post");

    view.rerender(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor={null} publishedItem={created} />);
    expect(screen.getAllByTestId("post-card")).toHaveLength(2);
  });

  it("keeps the SSR tag on every subsequent page request", async () => {
    const io = intersectionCallback();
    mocks.loadSocialFeedPage.mockResolvedValue({ items: [post("p-2")], nextCursor: null });

    render(<InfinitePostFeed scope="community" tag="indiedev" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    act(() => io.trigger(true));

    await waitFor(() => expect(mocks.loadSocialFeedPage).toHaveBeenCalledWith(expect.objectContaining({
      scope: "community",
      cursor: "cursor-1",
      tag: "indiedev",
    })));
  });

  it("aborts an in-flight page and clears loading before a changed feed can load", async () => {
    const io = intersectionCallback();
    let firstSignal: AbortSignal | undefined;
    mocks.loadSocialFeedPage.mockImplementationOnce(({ signal }) => {
      firstSignal = signal;
      return new Promise((_resolve, reject) => {
        signal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError")), { once: true });
      });
    });

    const view = render(<InfinitePostFeed scope="following" tag="old" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    act(() => io.trigger(true));
    await waitFor(() => expect(firstSignal).toBeDefined());
    expect(screen.getByText(/loading more/i)).toBeInTheDocument();

    view.rerender(<InfinitePostFeed scope="saved" tag="new" initialItems={[post("p-3")]} initialNextCursor="cursor-3" />);

    await waitFor(() => expect(firstSignal?.aborted).toBe(true));
    await waitFor(() => expect(screen.queryByText(/loading more/i)).not.toBeInTheDocument());
    expect(screen.getAllByTestId("post-card")).toHaveLength(1);
  });

  it("aborts an in-flight page when unmounted", async () => {
    const io = intersectionCallback();
    let signal: AbortSignal | undefined;
    mocks.loadSocialFeedPage.mockImplementationOnce(({ signal: requestSignal }) => {
      signal = requestSignal;
      return new Promise((_resolve, reject) => {
        requestSignal.addEventListener("abort", () => reject(new DOMException("Aborted", "AbortError")), { once: true });
      });
    });

    const view = render(<InfinitePostFeed scope="following" initialItems={[post("p-1")]} initialNextCursor="cursor-1" />);
    act(() => io.trigger(true));
    await waitFor(() => expect(signal).toBeDefined());
    view.unmount();

    expect(signal?.aborted).toBe(true);
  });
});
