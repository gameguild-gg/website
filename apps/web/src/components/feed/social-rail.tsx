"use client";

import { cn } from "@game-guild/ui/lib/utils";
import { Link } from "@/i18n/navigation";
import { followCreator } from "@/lib/feed/actions";
import type {
  SocialFeedTestingSession,
  SocialProfile,
  TrendingTag,
} from "@/lib/feed/contracts";
import { Button, buttonVariants } from "@game-guild/ui/components/button";
import { ArrowUpRight, CalendarDays, Users } from "lucide-react";
import * as React from "react";
import { formatSocialDateTime } from "@/lib/feed/format";
import { toast } from "sonner";
import type { SocialFeedTab } from "./social-feed-tabs";

export interface SocialSessionPreview extends SocialFeedTestingSession {
  id: string;
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

function FollowButton({ profile }: { profile: SocialProfile }) {
  const [following, setFollowing] = React.useState(profile.isFollowing);
  const [pending, startTransition] = React.useTransition();
  return (
    <Button
      type="button"
      variant={following ? "secondary" : "outline"}
      size="xs"
      disabled={pending}
      aria-label={`${following ? "Unfollow" : "Follow"} ${profile.displayName}`}
      aria-pressed={following}
      onClick={() => {
        const previous = following;
        const next = !following;
        setFollowing(next);
        startTransition(async () => {
          try {
            const state = await followCreator(profile.userId, next);
            setFollowing(state.isFollowing);
          } catch (error) {
            setFollowing(previous);
            toast.error(
              error instanceof Error ? error.message : "Follow could not be updated.",
            );
          }
        });
      }}
    >
      {following ? "Following" : "Follow"}
    </Button>
  );
}

export function SocialRail({
  currentProfile,
  sessions,
  creators,
  tags,
  activeTab,
}: {
  currentProfile: SocialProfile | null;
  sessions: SocialSessionPreview[];
  creators: SocialProfile[];
  tags: TrendingTag[];
  activeTab: SocialFeedTab;
}): React.JSX.Element {
  return (
    <aside aria-label="Around GameGuild" className="mt-6 grid h-fit grid-cols-1 gap-3 sm:grid-cols-2 xl:sticky xl:top-5 xl:mt-0 xl:block xl:space-y-3">
      {currentProfile ? (
        <section className="hidden rounded-xl bg-card p-4 text-card-foreground xl:block">
          <div className="flex items-start gap-3">
            <span className="flex size-14 shrink-0 items-center justify-center rounded-full bg-highlight/15 text-sm font-bold text-foreground">
              {initials(currentProfile.displayName)}
            </span>
            <div className="min-w-0 flex-1 pt-0.5">
              <p className="truncate text-sm font-semibold text-foreground">
                {currentProfile.displayName}
              </p>
              <p className="truncate text-xs text-muted-foreground">
                @{currentProfile.handle}
              </p>
              {currentProfile.headline ? (
                <p className="mt-1 line-clamp-2 text-xs leading-4 text-muted-foreground">
                  {currentProfile.headline}
                </p>
              ) : null}
            </div>
          </div>
          <div className="mt-4 grid grid-cols-3 divide-x divide-border/40 text-center">
            {[
              [currentProfile.postCount, "Posts"],
              [currentProfile.followerCount, "Followers"],
              [currentProfile.followingCount, "Following"],
            ].map(([value, label]) => (
              <div key={label}>
                <p className="text-sm font-semibold text-foreground">{value}</p>
                <p className="mt-0.5 text-[10px] text-muted-foreground">{label}</p>
              </div>
            ))}
          </div>
          <Link
            href={`/social/profiles/${currentProfile.handle}`}
            className={cn(
              buttonVariants({
                variant: "secondary",
                size: "sm",
                className: "mt-4 w-full",
              }),
            )}
          >
            View profile
          </Link>
        </section>
      ) : null}

      <section className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-center justify-between gap-3">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-foreground">
            <CalendarDays className="size-4 text-primary" aria-hidden="true" />
            Upcoming community sessions
          </h2>
          <Link href="/testing-lab" className="text-xs font-medium text-primary hover:text-foreground">
            View all
          </Link>
        </div>
        <div className="mt-3 space-y-1">
          {sessions.length === 0 ? (
            <p className="py-3 text-sm leading-6 text-muted-foreground">
              New sessions will appear here when registration opens.
            </p>
          ) : (
            sessions.slice(0, 3).map((session) => (
              <Link key={session.id} href={`/testing-lab/events/${session.id}`} className="block rounded-lg px-2 py-2.5 transition hover:bg-accent">
                <span className="block truncate text-sm font-semibold text-foreground">{session.name}</span>
                <span className="mt-0.5 block text-xs text-muted-foreground">{formatSocialDateTime(session.startsAt)}</span>
                <span className="mt-1 block text-xs text-primary">
                  {session.availableTesterCount > 0 ? `${session.availableTesterCount} spots left` : "Waitlist open"} · {session.mode}
                </span>
              </Link>
            ))
          )}
        </div>
      </section>

      <section className="hidden rounded-xl bg-card p-4 text-card-foreground xl:block">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-foreground">
          <Users className="size-4 text-highlight" aria-hidden="true" />
          Suggested creators
        </h2>
        <div className="mt-4 space-y-4">
          {creators.length === 0 ? (
            <p className="text-sm leading-6 text-muted-foreground">
              Suggestions improve as creators publish and connect.
            </p>
          ) : (
            creators.slice(0, 4).map((creator) => (
              <div key={creator.userId} className="flex items-center gap-3">
                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/15 text-[11px] font-bold text-foreground">
                  {initials(creator.displayName)}
                </span>
                <Link href={`/social/profiles/${creator.handle}`} className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-foreground">{creator.displayName}</p>
                  <p className="truncate text-xs text-muted-foreground">@{creator.handle}</p>
                </Link>
                <FollowButton profile={creator} />
              </div>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-center justify-between gap-3">
          <h2 className="text-sm font-semibold text-foreground">Trending now</h2>
          <Link
            href="/explore"
            className="inline-flex min-h-8 items-center gap-1 rounded-md px-2 text-xs font-medium text-primary transition-colors hover:bg-primary/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            Explore all
            <ArrowUpRight className="size-3.5" aria-hidden="true" />
          </Link>
        </div>
        {tags.length > 0 ? (
          <div className="mt-3 grid grid-cols-2 gap-2">
            {tags.map((tag) => (
              <Link
                key={tag.name}
                href={`${activeTab === "foryou" ? "/feed?" : `/feed?tab=${activeTab}&`}tag=${encodeURIComponent(tag.name)}`}
                className="rounded-lg bg-accent/50 px-3 py-2 text-xs transition hover:bg-accent"
              >
                <span className="block truncate font-medium text-foreground">#{tag.name}</span>
                <span className="mt-0.5 block text-[10px] text-muted-foreground">{tag.postCount} posts</span>
              </Link>
            ))}
          </div>
        ) : (
          <p className="mt-3 text-sm leading-6 text-muted-foreground">No trending tags yet.</p>
        )}
      </section>
    </aside>
  );
}
