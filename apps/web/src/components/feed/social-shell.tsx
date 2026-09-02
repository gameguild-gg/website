import { auth } from "@/auth";
import { BuildStories } from "@/components/feed/build-stories";
import { InfinitePostFeed } from "@/components/feed/infinite-post-feed";
import { SocialComposer } from "@/components/feed/social-composer";
import {
  SocialFeedTabs,
  type SocialFeedTab,
} from "@/components/feed/social-feed-tabs";
import { SocialRail } from "@/components/feed/social-rail";
import {
  getPublicMemberSpotlights,
  getPublicPlaytests,
} from "@/lib/community/public-community-queries";
import {
  demoSocialCreators,
  demoSocialPlaytests,
  demoSocialPosts,
  demoSocialStories,
  type SocialCreatorPreview,
  type SocialPlaytestPreview,
  type SocialStoryPreview,
} from "@/lib/posts/demo";
import {
  loadPosts,
  POSTS_PAGE_SIZE,
  type PostsStream,
} from "@/lib/posts/queries";

const TAB_STREAM: Record<SocialFeedTab, PostsStream> = {
  foryou: "feed",
  following: "feed",
  playtests: "public",
};

function storyAccent(index: number): string {
  return (
    [
      "from-amber-300 via-rose-500 to-violet-600",
      "from-cyan-300 via-blue-500 to-violet-600",
      "from-emerald-300 via-sky-500 to-blue-700",
      "from-fuchsia-400 via-violet-500 to-indigo-700",
    ][index % 4] ?? "from-cyan-300 via-blue-500 to-violet-600"
  );
}

export async function SocialShell({
  tab = "foryou",
}: {
  tab?: SocialFeedTab;
}): Promise<React.JSX.Element> {
  const stream = TAB_STREAM[tab];
  const [session, loadedPosts, publicPlaytests, memberSpotlights] =
    await Promise.all([
      auth(),
      loadPosts(stream, 0),
      getPublicPlaytests(3),
      getPublicMemberSpotlights(5),
    ]);
  const demoEnabled = process.env.SOCIAL_FEED_DEMO === "true";
  const posts =
    loadedPosts.length > 0 ? loadedPosts : demoEnabled ? demoSocialPosts : [];
  const userName =
    session && typeof session !== "function"
      ? session.user.name?.trim() ||
        session.user.email?.split("@")[0] ||
        "GameGuild member"
      : "GameGuild member";
  const currentUserId =
    session && typeof session !== "function" ? session.user.id : null;
  const visibleMemberSpotlights = memberSpotlights.filter(
    (member) => !member.id || member.id !== currentUserId,
  );

  const stories: SocialStoryPreview[] =
    demoEnabled
      ? demoSocialStories
      : visibleMemberSpotlights.length > 0
      ? visibleMemberSpotlights.map((member, index) => ({
          id: member.handle,
          name: member.name,
          handle: member.handle,
          accent: storyAccent(index),
        }))
      : [];
  const playtests: SocialPlaytestPreview[] =
    publicPlaytests.length > 0
      ? publicPlaytests.map((playtest) => ({
          title: playtest.title,
          detail: `${playtest.format} · ${playtest.seats}`,
          date: playtest.date,
          href: playtest.href,
        }))
      : demoEnabled
        ? demoSocialPlaytests
        : [];
  const creators: SocialCreatorPreview[] =
    demoEnabled
      ? demoSocialCreators
      : visibleMemberSpotlights.length > 0
      ? visibleMemberSpotlights.slice(0, 3).map((member) => ({
          name: member.name,
          handle: member.handle,
          focus: member.focus,
        }))
      : [];
  const nextSkip =
    loadedPosts.length === POSTS_PAGE_SIZE ? POSTS_PAGE_SIZE : null;

  return (
    <div
      data-testid="social-shell"
      className="min-h-[calc(100svh-4rem)] bg-[#050914] text-slate-100"
    >
      <div className="mx-auto grid min-h-[calc(100svh-4rem)] w-full max-w-[1260px] grid-cols-1 xl:grid-cols-[minmax(0,1fr)_360px]">
        <div className="min-w-0 border-x border-white/10 bg-[#070a12]">
          <SocialFeedTabs active={tab} />
          <BuildStories userName={userName} stories={stories} />
          <SocialComposer userName={userName} />
          <InfinitePostFeed
            stream={stream}
            initialItems={posts}
            initialNextSkip={nextSkip}
          />
        </div>
        <SocialRail playtests={playtests} creators={creators} />
      </div>
    </div>
  );
}
