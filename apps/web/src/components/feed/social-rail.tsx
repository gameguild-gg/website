"use client";

import { Link } from "@/i18n/navigation";
import { followCreator } from "@/lib/feed/actions";
import type {
  SocialFeedTestingSession,
  SocialProfile,
  TrendingTag,
} from "@/lib/feed/contracts";
import { Button } from "@game-guild/ui/components/button";
import { CalendarDays, Users } from "lucide-react";
import * as React from "react";
import { formatSocialDateTime } from "@/lib/feed/format";
import { toast } from "sonner";

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
      onClick={() => {
        const next = !following;
        setFollowing(next);
        startTransition(async () => {
          try {
            await followCreator(profile.userId, next);
          } catch (error) {
            setFollowing(!next);
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
}: {
  currentProfile: SocialProfile | null;
  sessions: SocialSessionPreview[];
  creators: SocialProfile[];
  tags: TrendingTag[];
}): React.JSX.Element {
  return (
    <aside className="sticky top-5 hidden h-fit space-y-3 xl:block">
      {currentProfile ? (
        <section className="rounded-xl bg-card p-4 text-card-foreground">
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
          <Button asChild variant="secondary" size="sm" className="mt-4 w-full">
            <Link href={`/social/profiles/${currentProfile.handle}`}>View profile</Link>
          </Button>
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
                <span className="mt-1 block text-xs text-primary">{session.availableTesterCount} spots · {session.mode}</span>
              </Link>
            ))
          )}
        </div>
      </section>

      <section className="rounded-xl bg-card p-4 text-card-foreground">
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

      {tags.length > 0 ? (
        <section className="rounded-xl bg-card p-4 text-card-foreground">
          <h2 className="text-sm font-semibold text-foreground">Trending now</h2>
          <div className="mt-3 grid grid-cols-2 gap-2">
            {tags.map((tag) => (
              <Link key={tag.name} href={`/?tag=${encodeURIComponent(tag.name)}`} className="rounded-lg bg-accent/50 px-3 py-2 text-xs transition hover:bg-accent">
                <span className="block truncate font-medium text-foreground">#{tag.name}</span>
                <span className="mt-0.5 block text-[10px] text-muted-foreground">{tag.postCount} posts</span>
              </Link>
            ))}
          </div>
        </section>
      ) : null}
    </aside>
  );
}
