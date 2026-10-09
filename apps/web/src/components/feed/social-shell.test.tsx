import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { createElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  loadSocialFeed: vi.fn(),
  loadStories: vi.fn(),
  loadSocialProfile: vi.fn(),
  loadCreatorSuggestions: vi.fn(),
  searchSocialProfiles: vi.fn(),
  loadTrendingTags: vi.fn(),
}));

vi.mock("@/auth", () => ({ auth: mocks.auth }));
vi.mock("@/lib/feed/queries", () => ({
  loadSocialFeed: mocks.loadSocialFeed,
  loadStories: mocks.loadStories,
  loadSocialProfile: mocks.loadSocialProfile,
  loadCreatorSuggestions: mocks.loadCreatorSuggestions,
  searchSocialProfiles: mocks.searchSocialProfiles,
  loadTrendingTags: mocks.loadTrendingTags,
}));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));
vi.mock("next/image", () => ({
  default: ({ alt = "", ...props }: Record<string, unknown>) =>
    createElement("img", { ...props, alt: typeof alt === "string" ? alt : "" }),
}));

import { Suspense } from "react";
import { SocialFeed, SocialShell } from "./social-shell";
import type { SocialFeedItem } from "@/lib/feed/contracts";

function postItem(id: string): SocialFeedItem {
  return {
    id,
    kind: "Post",
    createdAt: "2026-09-10T00:00:00Z",
    author: { userId: "user-2", displayName: "Ada Builder", handle: "ada", avatarUrl: null, isVerified: false },
    post: { content: id, mediaUrl: null, mediaType: null, visibility: "Public", isEdited: false, editedAt: null, repostedPost: null },
    testingSession: null,
    engagement: { reactionsCount: 0, commentsCount: 0, repostsCount: 0, viewsCount: 0 },
    viewer: { reaction: null, isSaved: false, isFollowingAuthor: false, hasReposted: false, canEdit: false, canDelete: false },
    tags: [],
  };
}

async function renderResolvedFeed(props: { tab?: "foryou" | "following" | "community" | "saved" }) {
  const feed = await SocialFeed(props);
  return render(<Suspense fallback={<div data-testid="feed-loading-skeleton" />}>{feed}</Suspense>);
}

describe("SocialShell", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue({ user: { id: "user-1", name: "Ada", email: "ada@example.com" } });
    mocks.loadSocialFeed.mockResolvedValue({ items: [], nextCursor: null });
    mocks.loadStories.mockResolvedValue([]);
    mocks.loadSocialProfile.mockResolvedValue(null);
    mocks.loadCreatorSuggestions.mockResolvedValue([]);
    mocks.searchSocialProfiles.mockResolvedValue([]);
    mocks.loadTrendingTags.mockResolvedValue([]);
  });
  afterEach(cleanup);

  it.each([
    ["foryou", "for-you"],
    ["following", "following"],
    ["community", "community"],
    ["saved", "saved"],
  ] as const)("maps %s to the %s API scope", async (tab, scope) => {
    await renderResolvedFeed({ tab });
    expect(mocks.loadSocialFeed).toHaveBeenCalledWith({ scope, tag: null });
  });

  it("shows a recoverable error without substituting demo posts", async () => {
    mocks.loadSocialFeed.mockRejectedValue(new Error("offline"));
    await renderResolvedFeed({ tab: "foryou" });
    expect(screen.getByText(/feed is temporarily unavailable/i)).toBeInTheDocument();
    expect(screen.queryByTestId("post-card")).not.toBeInTheDocument();
  });

  it("loads moderation-aware creator suggestions for the current actor", async () => {
    await renderResolvedFeed({ tab: "foryou" });

    expect(mocks.loadCreatorSuggestions).toHaveBeenCalledWith("user-1", 8);
  });

  it("streams behind the FeedSkeleton fallback", async () => {
    mocks.loadSocialFeed.mockReturnValue(new Promise(() => {}));

    render(<SocialShell tab="foryou" />);

    expect(screen.getByTestId("feed-loading-skeleton")).toBeInTheDocument();
  });

  it("runs the primary feed and the trending-tags rail in one parallel round", async () => {
    const timeline: string[] = [];
    const gate = { primary: false, tags: false };
    const primary = async () => {
      timeline.push("primary:start");
      await new Promise<void>((resolve) => {
        const tick = () => (gate.primary ? resolve() : setTimeout(tick, 0));
        tick();
      });
      timeline.push("primary:end");
      return { items: [], nextCursor: null };
    };
    const tags = async () => {
      timeline.push("tags:start");
      await new Promise<void>((resolve) => {
        const tick = () => (gate.tags ? resolve() : setTimeout(tick, 0));
        tick();
      });
      timeline.push("tags:end");
      return [];
    };
    mocks.loadSocialFeed.mockImplementation(({ scope }: { scope: string }) =>
      scope === "community" ? Promise.resolve({ items: [], nextCursor: null }) : primary(),
    );
    mocks.loadTrendingTags.mockImplementation(tags);

    const feedPromise = SocialFeed({ tab: "foryou" });
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(timeline).toEqual(["primary:start", "tags:start"]);
    gate.tags = true;
    await new Promise((resolve) => setTimeout(resolve, 0));
    gate.primary = true;
    await feedPromise;

    expect(timeline).toEqual(["primary:start", "tags:start", "tags:end", "primary:end"]);
  });

  it("still renders feed items when the trending-tags rail rejects", async () => {
    mocks.loadTrendingTags.mockRejectedValue(new Error("tags down"));
    mocks.loadSocialFeed.mockImplementation(({ scope }: { scope: string }) =>
      Promise.resolve(
        scope === "community"
          ? { items: [], nextCursor: null }
          : { items: [postItem("post-1")], nextCursor: null },
      ),
    );

    await renderResolvedFeed({ tab: "foryou" });

    expect(screen.getAllByTestId("post-card")).toHaveLength(1);
    expect(screen.getByText("Trending now")).toBeInTheDocument();
  });
});
