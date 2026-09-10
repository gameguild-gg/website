import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ followCreator: vi.fn() }));
vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("next/image", () => ({ default: (props: Record<string, unknown>) => <img {...props} /> }));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));

import { SocialProfileView } from "./social-profile";
import type { SocialProfile } from "@/lib/feed/contracts";

const profile: SocialProfile = {
  id: "profile-1",
  userId: "user-2",
  handle: "lin",
  displayName: "Lin Creator",
  avatarUrl: null,
  bannerUrl: null,
  bio: "Building thoughtful games.",
  headline: "Independent designer",
  location: "Toronto",
  timeZone: "America/Toronto",
  websiteUrl: "https://example.com",
  availabilityStatus: "AvailableForCollaboration",
  isVerified: true,
  projectCount: 3,
  postCount: 12,
  followerCount: 42,
  followingCount: 8,
  isFollowing: false,
};

describe("SocialProfileView", () => {
  afterEach(cleanup);

  it("renders real profile metrics and persists follow state", async () => {
    mocks.followCreator.mockResolvedValue(undefined);
    render(<SocialProfileView profile={profile} currentUserId="user-1" />);

    expect(screen.getByText("42")).toBeInTheDocument();
    expect(screen.getByText("Building thoughtful games.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));

    await waitFor(() => expect(mocks.followCreator).toHaveBeenCalledWith("user-2", true));
    expect(screen.getByRole("button", { name: "Unfollow Lin Creator" })).toBeInTheDocument();
    expect(screen.getByText("43")).toBeInTheDocument();
  });

  it("does not render a self-follow action", () => {
    render(<SocialProfileView profile={profile} currentUserId="user-2" />);
    expect(screen.queryByRole("button", { name: /follow lin creator/i })).not.toBeInTheDocument();
  });
});
