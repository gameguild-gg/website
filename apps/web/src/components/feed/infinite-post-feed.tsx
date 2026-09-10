"use client";

import { PostCard } from "@/components/feed/post-card";
import { loadSocialFeedPage } from "@/lib/feed/pagination";
import type { FeedScope, SocialFeedItem } from "@/lib/feed/contracts";
import { Bookmark, Gamepad2, Loader2, Users } from "lucide-react";
import * as React from "react";

const EMPTY = {
  saved: {
    title: "No saved posts yet",
    detail: "Posts you save will appear here.",
    icon: Bookmark,
  },
  following: {
    title: "Your following feed is quiet",
    detail: "Follow creators to see their latest work here.",
    icon: Users,
  },
  "for-you": {
    title: "Your feed is ready for its first build",
    detail:
      "Follow creators or share what you are making. Project updates and community sessions will appear here.",
    icon: Gamepad2,
  },
  community: {
    title: "No community activity yet",
    detail: "Public builds and Testing Lab sessions will appear here.",
    icon: Gamepad2,
  },
} satisfies Record<
  FeedScope,
  { title: string; detail: string; icon: typeof Gamepad2 }
>;

export function InfinitePostFeed({
  scope,
  tag = null,
  initialItems,
  initialNextCursor,
  currentUserId,
  publishedItem = null,
  publishedIdentity,
}: {
  scope: FeedScope;
  tag?: string | null;
  initialItems: SocialFeedItem[];
  initialNextCursor: string | null;
  currentUserId?: string | null;
  publishedItem?: SocialFeedItem | null;
  publishedIdentity?: string;
}): React.JSX.Element {
  const feedIdentity = `${scope}:${tag ?? ""}`;
  const [items, setItems] = React.useState(initialItems);
  const [nextCursor, setNextCursor] = React.useState(initialNextCursor);
  const [loading, setLoading] = React.useState(false);
  const [error, setError] = React.useState(false);
  const sentinelRef = React.useRef<HTMLDivElement | null>(null);
  const cursorRef = React.useRef(initialNextCursor);
  const loadingRef = React.useRef(false);
  const seenRef = React.useRef(new Set(initialItems.map((item) => item.id)));
  const requestRef = React.useRef(0);
  const activeControllerRef = React.useRef<AbortController | null>(null);
  const publishedItemRef = React.useRef<SocialFeedItem | null>(null);
  const identityRef = React.useRef(feedIdentity);

  React.useEffect(() => {
    activeControllerRef.current?.abort();
    activeControllerRef.current = null;
    loadingRef.current = false;
    setLoading(false);
    if (identityRef.current !== feedIdentity) {
      identityRef.current = feedIdentity;
      publishedItemRef.current = null;
    }
    const retained = publishedItemRef.current;
    setItems(retained && !initialItems.some((item) => item.id === retained.id) ? [retained, ...initialItems] : initialItems);
    setNextCursor(initialNextCursor);
    setError(false);
    cursorRef.current = initialNextCursor;
    seenRef.current = new Set([...(retained ? [retained] : []), ...initialItems].map((item) => item.id));
    requestRef.current += 1;
  }, [feedIdentity, initialItems, initialNextCursor, scope, tag]);

  React.useEffect(() => {
    if (!publishedItem || (publishedIdentity ?? feedIdentity) !== feedIdentity || seenRef.current.has(publishedItem.id)) return;
    publishedItemRef.current = publishedItem;
    seenRef.current.add(publishedItem.id);
    setItems((current) => [publishedItem, ...current]);
  }, [feedIdentity, publishedIdentity, publishedItem]);

  React.useEffect(
    () => () => activeControllerRef.current?.abort(),
    [],
  );

  const loadMore = React.useCallback(() => {
    if (loadingRef.current || cursorRef.current === null) return;
    const cursor = cursorRef.current;
    const requestId = ++requestRef.current;
    const controller = new AbortController();
    activeControllerRef.current = controller;
    loadingRef.current = true;
    setLoading(true);
    setError(false);
    void loadSocialFeedPage({ scope, cursor, tag, signal: controller.signal })
      .then((page) => {
        if (requestId !== requestRef.current) return;
        const fresh = page.items.filter(
          (item) => !seenRef.current.has(item.id),
        );
        fresh.forEach((item) => seenRef.current.add(item.id));
        setItems((current) => [...current, ...fresh]);
        cursorRef.current = page.nextCursor;
        setNextCursor(page.nextCursor);
      })
      .catch(() => {
        if (controller.signal.aborted || requestId !== requestRef.current) return;
        setError(true);
      })
      .finally(() => {
        if (controller.signal.aborted || requestId !== requestRef.current) return;
        if (activeControllerRef.current === controller) activeControllerRef.current = null;
        loadingRef.current = false;
        setLoading(false);
      });
  }, [scope, tag]);

  React.useEffect(() => {
    const node = sentinelRef.current;
    if (!node || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) loadMore();
      },
      { rootMargin: "600px 0px" },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [loadMore]);

  const empty = EMPTY[scope];
  const EmptyIcon = empty.icon;
  return (
    <div className="divide-y divide-border/35">
      {items.map((item) => (
        <PostCard
          key={`${item.kind}-${item.id}`}
          item={item}
          currentUserId={currentUserId}
        />
      ))}

      {items.length === 0 && !loading ? (
        <div className="mx-4 my-8 flex flex-col items-center rounded-2xl bg-card px-6 py-12 text-center text-card-foreground sm:mx-6">
          <span className="flex size-11 items-center justify-center rounded-xl bg-primary/10 text-primary">
            <EmptyIcon className="size-5" aria-hidden="true" />
          </span>
          <p className="mt-4 text-sm font-semibold text-foreground">
            {empty.title}
          </p>
          <p className="mt-1 max-w-sm text-sm leading-6 text-muted-foreground">
            {empty.detail}
          </p>
        </div>
      ) : null}

      <div ref={sentinelRef} aria-hidden="true" className="h-px" />

      {loading ? (
        <p className="flex items-center justify-center gap-2 py-5 text-sm text-muted-foreground">
          <Loader2 className="size-4 animate-spin" aria-hidden="true" />
          Loading more…
        </p>
      ) : error ? (
        <div className="flex justify-center py-5">
          <button
            type="button"
            onClick={loadMore}
            className="rounded-lg bg-accent px-3 py-2 text-sm font-semibold text-foreground hover:bg-accent/80"
          >
            Try again
          </button>
        </div>
      ) : nextCursor === null && items.length > 0 ? (
        <p className="py-5 text-center text-xs text-muted-foreground/70">
          You&apos;re all caught up.
        </p>
      ) : null}
    </div>
  );
}
