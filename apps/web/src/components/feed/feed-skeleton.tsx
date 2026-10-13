import React from "react";

/**
 * Static skeleton mirroring the /feed left column (social-shell.tsx):
 * sticky tab bar (h-14) → stories strip (h-24 row of circles) → composer
 * (rounded card h-20) → post cards. Used as the nested Suspense fallback so
 * cold-load streaming shows structure without flashing on soft tab switches
 * (searchParams-only navigations keep the page shell mounted).
 * CSS animation only (animate-pulse), no runtime hooks.
 */
export function FeedSkeleton(): React.JSX.Element {
  return (
    <div
      data-testid="feed-loading-skeleton"
      role="status"
      aria-label="Loading feed"
      className="mx-auto w-full max-w-[1260px] xl:grid xl:grid-cols-[minmax(0,820px)_360px] xl:gap-6"
    >
      <div className="min-w-0">
        {/* Tab bar placeholder — matches SocialFeedTabs sticky h-14 */}
        <div className="sticky top-0 z-20 flex h-14 items-end gap-8 bg-background/95 px-4 backdrop-blur-xl sm:px-6">
          {Array.from({ length: 4 }).map((_, i) => (
            <div
              key={i}
              className="h-4 w-14 animate-pulse rounded bg-muted"
            />
          ))}
        </div>

        {/* Stories strip placeholder — matches BuildStories px-4 py-4 */}
        <section
          aria-hidden="true"
          className="overflow-hidden px-4 py-4 sm:px-6"
        >
          <div className="flex h-24 gap-4 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="flex w-[4.5rem] shrink-0 flex-col items-center gap-2"
              >
                <div className="size-[3.25rem] animate-pulse rounded-full bg-muted" />
                <div className="h-2.5 w-12 animate-pulse rounded bg-muted" />
              </div>
            ))}
          </div>
        </section>

        {/* Composer placeholder — matches SocialComposer px-4 py-3 card */}
        <section aria-hidden="true" className="px-4 py-3 sm:px-6">
          <div className="flex h-20 items-center gap-3 rounded-xl bg-card p-2.5">
            <div className="size-9 shrink-0 animate-pulse rounded-full bg-muted" />
            <div className="h-8 min-w-0 flex-1 animate-pulse rounded-lg bg-muted" />
          </div>
        </section>

        {/* Post card placeholders — mirror PostCard article rhythm */}
        {Array.from({ length: 3 }).map((_, i) => (
          <article
            key={i}
            aria-hidden="true"
            data-testid="feed-skeleton-post"
            className="border-b border-border/35 bg-card py-2"
          >
            <div className="flex items-center gap-3 px-4 py-4 pb-2 sm:px-6">
              <div className="size-10 shrink-0 animate-pulse rounded-full bg-muted" />
              <div className="min-w-0 flex-1 space-y-2">
                <div className="h-3.5 w-32 animate-pulse rounded bg-muted" />
                <div className="h-2.5 w-24 animate-pulse rounded bg-muted" />
              </div>
            </div>
            <div className="space-y-2 px-4 pb-4 pt-4 sm:px-6">
              <div className="h-3 w-full animate-pulse rounded bg-muted" />
              <div className="h-3 w-4/5 animate-pulse rounded bg-muted" />
            </div>
          </article>
        ))}
      </div>
      {/* Right rail exists only on xl; omitted — scoped to left column per plan */}
    </div>
  );
}
