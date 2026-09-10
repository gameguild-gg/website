import { auth } from "@/auth";
import {
  BuildStories,
  type SocialStoryPreview,
} from "@/components/feed/build-stories";
import { InfinitePostFeed } from "@/components/feed/infinite-post-feed";
import { SocialComposer } from "@/components/feed/social-composer";
import {
  SocialFeedTabs,
  type SocialFeedTab,
} from "@/components/feed/social-feed-tabs";
import {
  SocialRail,
  type SocialSessionPreview,
} from "@/components/feed/social-rail";
import type { FeedScope, SocialProfile } from "@/lib/feed/contracts";
import {
  loadSocialFeed,
  loadSocialProfile,
  loadStories,
  loadTrendingTags,
  searchSocialProfiles,
} from "@/lib/feed/queries";
import { AlertCircle } from "lucide-react";

const TAB_SCOPE: Record<SocialFeedTab, FeedScope> = {
  foryou: "for-you",
  following: "following",
  community: "community",
  saved: "saved",
};

async function optional<T>(operation: Promise<T>, fallback: T): Promise<T> {
  try {
    return await operation;
  } catch {
    return fallback;
  }
}

export async function SocialShell({
  tab = "foryou",
  tag = null,
}: {
  tab?: SocialFeedTab;
  tag?: string | null;
}): Promise<React.JSX.Element> {
  const scope = TAB_SCOPE[tab];
  const session = await auth();
  const user = session && typeof session !== "function" ? session.user : null;
  const userName =
    user?.name?.trim() || user?.email?.split("@")[0] || "GameGuild member";
  const currentUserId = user?.id ?? null;

  let primary;
  let primaryError = false;
  try {
    primary = await loadSocialFeed({ scope, tag });
  } catch {
    primary = { items: [], nextCursor: null };
    primaryError = true;
  }

  const [stories, currentProfile, suggestedProfiles, trendingTags, community] =
    await Promise.all([
      optional(loadStories(), []),
      currentUserId
        ? optional(loadSocialProfile(currentUserId), null)
        : Promise.resolve(null),
      optional(searchSocialProfiles("", 8), []),
      optional(loadTrendingTags(6), []),
      scope === "community"
        ? Promise.resolve(primary)
        : optional(loadSocialFeed({ scope: "community", take: 8 }), {
            items: [],
            nextCursor: null,
          }),
    ]);

  const storyAuthorIds = [...new Set(stories.map((story) => story.authorId))];
  const storyProfiles = new Map<string, SocialProfile>();
  await Promise.all(
    storyAuthorIds.map(async (authorId) => {
      const profile = await optional(loadSocialProfile(authorId), null);
      if (profile) storyProfiles.set(authorId, profile);
    }),
  );
  const storyPreviews: SocialStoryPreview[] = stories.map((story) => {
    const profile = storyProfiles.get(story.authorId);
    const ownStory = story.authorId === currentUserId;
    return {
      ...story,
      authorName: profile?.displayName || (ownStory ? userName : "GameGuild creator"),
      authorHandle: profile?.handle || "creator",
      authorAvatarUrl: profile?.avatarUrl ?? null,
      isOwn: ownStory,
    };
  });
  const sessions: SocialSessionPreview[] = community.items
    .filter((item) => item.kind === "TestingSession" && item.testingSession)
    .map((item) => ({ id: item.id, ...item.testingSession! }));
  const creators = suggestedProfiles.filter(
    (profile) => profile.userId && profile.userId !== currentUserId,
  );

  return (
    <div
      data-testid="social-shell"
      className="min-h-[calc(100svh-4rem)] bg-background text-foreground"
    >
      <div className="mx-auto grid min-h-[calc(100svh-4rem)] w-full max-w-[1260px] grid-cols-1 gap-0 xl:grid-cols-[minmax(0,820px)_360px] xl:gap-6 xl:px-5">
        <div className="min-w-0">
          <SocialFeedTabs active={tab} />
          <BuildStories
            key={storyPreviews.map((story) => story.id).join(":")}
            userName={userName}
            stories={storyPreviews}
          />
          <SocialComposer userName={userName} />
          {primaryError ? (
            <div role="alert" className="mx-4 my-8 flex items-start gap-3 rounded-xl bg-card px-5 py-6 sm:mx-6">
              <AlertCircle className="mt-0.5 size-5 shrink-0 text-destructive" />
              <div>
                <p className="font-semibold text-foreground">The feed is temporarily unavailable</p>
                <p className="mt-1 text-sm text-muted-foreground">No placeholder posts were substituted. Refresh to retry the live feed.</p>
              </div>
            </div>
          ) : (
            <InfinitePostFeed
              scope={scope}
              tag={tag}
              initialItems={primary.items}
              initialNextCursor={primary.nextCursor}
              currentUserId={currentUserId}
            />
          )}
        </div>
        <SocialRail
          currentProfile={currentProfile}
          sessions={sessions}
          creators={creators}
          tags={trendingTags}
        />
      </div>
    </div>
  );
}
