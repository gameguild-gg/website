import { auth } from "@/auth";
import {
  BuildStories,
  type SocialStoryPreview,
} from "@/components/feed/build-stories";
import { FeedSkeleton } from "@/components/feed/feed-skeleton";
import { SocialFeedClient } from "@/components/feed/social-feed-client";
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
  loadCreatorSuggestions,
  loadStories,
  loadTrendingTags,
} from "@/lib/feed/queries";
import { AlertCircle } from "lucide-react";
import { Suspense } from "react";

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

export function SocialShell({
  tab = "foryou",
  tag = null,
}: {
  tab?: SocialFeedTab;
  tag?: string | null;
}): React.JSX.Element {
  return (
    <Suspense fallback={<FeedSkeleton />}>
      <SocialFeed tab={tab} tag={tag} />
    </Suspense>
  );
}

export async function SocialFeed({
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

  const emptyFeed = { items: [], nextCursor: null };
  // Captured, not awaited: a rejection must not kill the parallel batch below.
  let primaryError = false;
  const primaryPromise = loadSocialFeed({ scope, tag }).catch(() => {
    primaryError = true;
    return emptyFeed;
  });

  const [primary, storiesTrack, currentProfile, suggestedProfiles, trendingTags, community] =
    await Promise.all([
      primaryPromise,
      optional(
        loadStories().then(async (stories) => {
          const authorProfiles = await Promise.all(
            [...new Set(stories.map((story) => story.authorId))].map((id) =>
              optional(loadSocialProfile(id), null),
            ),
          );
          return { stories, authorProfiles };
        }),
        { stories: [], authorProfiles: [] },
      ),
      currentUserId
        ? optional(loadSocialProfile(currentUserId), null)
        : Promise.resolve(null),
      currentUserId
        ? optional(loadCreatorSuggestions(currentUserId, 8), [])
        : Promise.resolve([]),
      optional(loadTrendingTags(6), []),
      scope === "community"
        ? primaryPromise
        : optional(loadSocialFeed({ scope: "community", take: 8 }), emptyFeed),
    ]);

  const { stories, authorProfiles } = storiesTrack;
  const storyAuthorIds = [...new Set(stories.map((story) => story.authorId))];
  const storyProfiles = new Map<string, SocialProfile>();
  storyAuthorIds.forEach((authorId, index) => {
    const profile = authorProfiles[index];
    if (profile) storyProfiles.set(authorId, profile);
  });
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
      <div className="mx-auto grid min-h-[calc(100svh-4rem)] w-full max-w-[1260px] grid-cols-1 gap-0 xl:grid-cols-[minmax(0,820px)_360px] xl:gap-6">
        <div className="min-w-0">
          <SocialFeedTabs active={tab} />
          <BuildStories
            key={storyPreviews.map((story) => story.id).join(":")}
            userName={userName}
            stories={storyPreviews}
            currentProfile={currentProfile}
          />
          {primaryError ? (
            <div role="alert" className="mx-4 my-8 flex items-start gap-3 rounded-xl bg-card px-5 py-6 sm:mx-6">
              <AlertCircle className="mt-0.5 size-5 shrink-0 text-destructive" />
              <div>
                <p className="font-semibold text-foreground">The feed is temporarily unavailable</p>
                <p className="mt-1 text-sm text-muted-foreground">No placeholder posts were substituted. Refresh to retry the live feed.</p>
              </div>
            </div>
          ) : (
            <SocialFeedClient
              userName={userName}
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
          activeTab={tab}
        />
      </div>
    </div>
  );
}
