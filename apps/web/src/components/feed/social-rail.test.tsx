import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ followCreator: vi.fn(), toastError: vi.fn() }));
vi.mock("@/lib/feed/actions", () => ({ followCreator: mocks.followCreator }));
vi.mock("sonner", () => ({ toast: { error: mocks.toastError } }));
vi.mock("@/i18n/navigation", () => ({
  Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a>,
}));

import type { SocialProfile } from "@/lib/feed/contracts";
import { SocialRail } from "./social-rail";

const creator: SocialProfile = {
  id: "profile-2",
  userId: "creator-2",
  handle: "lin",
  displayName: "Lin Creator",
  avatarUrl: null,
  bannerUrl: null,
  bio: null,
  headline: null,
  location: null,
  timeZone: null,
  websiteUrl: null,
  availabilityStatus: null,
  isVerified: false,
  projectCount: 1,
  postCount: 2,
  followerCount: 3,
  followingCount: 4,
  isFollowing: false,
};

describe("SocialRail", () => {
  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it("renders a truthful empty state when no tags are trending", () => {
    render(
      <SocialRail currentProfile={null} sessions={[]} creators={[]} tags={[]} activeTab="foryou" />,
    );

    expect(screen.getByRole("heading", { name: "Trending now" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Explore all" })).toHaveAttribute(
      "href",
      "/explore",
    );
    expect(screen.getByText("No trending tags yet.")).toBeInTheDocument();
  });

  it("keeps tag links in the active locale-aware feed route", () => {
    render(
      <SocialRail
        currentProfile={null}
        sessions={[]}
        creators={[]}
        tags={[{ name: "indie dev", postCount: 7 }]}
        activeTab="community"
      />,
    );

    expect(screen.getByRole("link", { name: /#indie dev/i })).toHaveAttribute(
      "href",
      "/feed?tab=community&tag=indie%20dev",
    );
  });

  it("keeps the for-you trending tag on the default feed route without a redundant tab param", () => {
    render(
      <SocialRail
        currentProfile={null}
        sessions={[]}
        creators={[]}
        tags={[{ name: "indie dev", postCount: 7 }]}
        activeTab="foryou"
      />,
    );

    expect(screen.getByRole("link", { name: /#indie dev/i })).toHaveAttribute(
      "href",
      "/feed?tag=indie%20dev",
    );
  });

  it("reconciles follow state with the authoritative response and rolls back failures", async () => {
    mocks.followCreator.mockResolvedValueOnce({ userId: creator.userId, isFollowing: false });
    render(
      <SocialRail currentProfile={null} sessions={[]} creators={[creator]} tags={[]} activeTab="foryou" />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));
    await waitFor(() => {
      expect(mocks.followCreator).toHaveBeenCalledTimes(1);
      expect(screen.getByRole("button", { name: "Follow Lin Creator" })).toBeEnabled();
      expect(screen.getByRole("button", { name: "Follow Lin Creator" })).toHaveAttribute(
        "aria-pressed",
        "false",
      );
    });

    mocks.followCreator.mockRejectedValueOnce(new Error("Follow unavailable"));
    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));
    await waitFor(() => {
      expect(mocks.followCreator).toHaveBeenCalledTimes(2);
      expect(mocks.toastError).toHaveBeenCalledWith("Follow unavailable");
    });
    expect(screen.getByRole("button", { name: "Follow Lin Creator" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });
});
