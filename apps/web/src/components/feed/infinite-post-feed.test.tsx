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

  it("does not carry a published item into another scope or tag", async () => {
    intersectionCallback();
    const created = post("created", "Only for you");
    const view = render(<InfinitePostFeed scope="for-you" initialItems={[post("p-1")]} initialNextCursor={null} publishedItem={created} publishedIdentity="for-you:" />);
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
    view.rerender(<InfinitePostFeed scope="saved" initialItems={[post("saved-1")]} initialNextCursor={null} publishedItem={created} publishedIdentity="for-you:" />);
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(1));
    expect(screen.queryByText("Only for you")).not.toBeInTheDocument();
  });

  it("resets the visible items and the dedupe set when the feed identity changes", async () => {
    // PostCard also creates IntersectionObservers, and stale observers (old scope
    // closures) must not fire after disconnect — trigger only connected instances.
    const instances: { cb: IntersectionObserverCallback; connected: boolean }[] = [];
    vi.stubGlobal("IntersectionObserver", class {
      record: { cb: IntersectionObserverCallback; connected: boolean };
      constructor(cb: IntersectionObserverCallback) {
        this.record = { cb, connected: true };
        instances.push(this.record);
      }
      observe = vi.fn();
      disconnect = () => { this.record.connected = false; };
    });
    const trigger = () => act(() => {
      for (const { cb, connected } of [...instances]) if (connected) cb([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
    });

    // B's pagination returns an ID A already showed: proves the dedupe (seenRef) reset, not just the item list.
    mocks.loadSocialFeedPage
      .mockResolvedValueOnce({ items: [post("a-3", "Shared across scopes")], nextCursor: null })
      .mockResolvedValueOnce({ items: [post("a-3", "Shared across scopes")], nextCursor: null });

    const view = render(
      <InfinitePostFeed scope="following" initialItems={[post("a-1", "A only"), post("a-2", "A second")]} initialNextCursor="cursor-a" />,
    );
    expect(screen.getAllByTestId("post-card")).toHaveLength(2);
    trigger();
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(3));

    view.rerender(<InfinitePostFeed scope="community" initialItems={[post("b-1", "B only")]} initialNextCursor="cursor-b" />);

    await waitFor(() => expect(screen.queryByText("A only")).not.toBeInTheDocument());
    expect(screen.queryByText("A second")).not.toBeInTheDocument();
    expect(screen.getByText("B only")).toBeInTheDocument();
    expect(screen.getAllByTestId("post-card")).toHaveLength(1);

    trigger();
    await waitFor(() => expect(mocks.loadSocialFeedPage).toHaveBeenCalledWith(expect.objectContaining({ scope: "community", cursor: "cursor-b" })));
    await waitFor(() => expect(screen.getAllByTestId("post-card")).toHaveLength(2));
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
