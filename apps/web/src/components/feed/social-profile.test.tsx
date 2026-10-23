import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createElement } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ followCreator: vi.fn() }));
vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("next/image", () => ({
  default: ({ alt = "", ...props }: Record<string, unknown>) =>
    createElement("img", { ...props, alt: typeof alt === "string" ? alt : "" }),
}));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));
vi.mock("next-intl", () => ({
  useLocale: () => "en-US",
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    let message = {
      backToFeed: "Back to feed",
      share: "Share",
      linkCopied: "Profile link copied to clipboard.",
      linkCopyFailed: "Could not copy the profile link.",
      follow: "Follow",
      following: "Following",
      followName: "Follow {name}",
      unfollowName: "Unfollow {name}",
      followFailed: "Follow could not be updated.",
      editProfile: "Edit profile",
      verified: "Verified",
      avatarAlt: "{name} avatar",
      website: "Website",
      collectionsError: "Public work could not be loaded. Refresh to retry.",
      tabsLabel: "Profile work",
      openPost: "Open post",
      emptyProjects: "No public projects yet.",
      emptyPosts: "No public posts yet.",
      "stats.posts": "Posts",
      "stats.projects": "Projects",
      "stats.followers": "Followers",
      "stats.following": "Following",
      "tabs.projects": "Projects",
      "tabs.posts": "Posts",
    }[key] ?? key;
    for (const [name, value] of Object.entries(values ?? {})) {
      message = message.replace(`{${name}}`, String(value));
    }
    return message;
  },
}));

import { SocialProfileView } from "./social-profile";
import type { SocialProfile, SocialProfilePost, SocialProfileProject } from "@/lib/feed/contracts";

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

const posts: SocialProfilePost[] = [
  {
    id: "post-1",
    content: "A persisted launch update",
    mediaUrl: null,
    mediaType: null,
    createdAt: "2026-09-10T00:00:00Z",
  },
];

const projects: SocialProfileProject[] = [
  {
    id: "project-1",
    title: "Skybound",
    slug: "skybound",
    shortDescription: "A persisted public project",
    imageUrl: null,
    publishedAt: "2026-09-09T00:00:00Z",
  },
];

describe("SocialProfileView", () => {
  afterEach(cleanup);

  it("renders real profile metrics and persists follow state", async () => {
    mocks.followCreator.mockResolvedValue({ userId: "user-2", isFollowing: true });
    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={posts} projects={projects} />);

    expect(screen.getAllByText("42").length).toBeGreaterThan(0);
    expect(screen.getByText("Building thoughtful games.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));

    await waitFor(() => expect(mocks.followCreator).toHaveBeenCalledWith("user-2", true));
    expect(screen.getByRole("button", { name: "Unfollow Lin Creator" })).toBeInTheDocument();
    expect(screen.getAllByText("43").length).toBeGreaterThan(0);
  });

  it("does not render a self-follow action and links owners to editing", () => {
    render(<SocialProfileView profile={profile} currentUserId="user-2" posts={posts} projects={projects} />);
    expect(screen.queryByRole("button", { name: /follow lin creator/i })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: /edit profile/i })).toHaveAttribute(
      "href",
      "/workspace/settings/profile",
    );
  });

  it("renders persisted posts and projects with real locale-aware routes", () => {
    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={posts} projects={projects} />);

    expect(screen.getByRole("link", { name: /skybound/i })).toHaveAttribute(
      "href",
      "/projects/skybound",
    );
    // Posts live behind the second tab; activate it before asserting its content.
    fireEvent.click(screen.getByRole("tab", { name: /posts/i }));
    expect(screen.getByText("A persisted launch update")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /open post/i })).toHaveAttribute(
      "href",
      "/social/posts/post-1",
    );
  });

  it("organizes work behind tabs with real counts", () => {
    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={posts} projects={projects} />);

    expect(screen.getByRole("tab", { name: /projects/i })).toHaveTextContent("1");
    expect(screen.getByRole("tab", { name: /posts/i })).toHaveTextContent("1");
  });

  it("renders truthful collection empty states", () => {
    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={[]} projects={[]} />);

    expect(screen.getByText("No public projects yet.")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("tab", { name: /posts/i }));
    expect(screen.getByText("No public posts yet.")).toBeInTheDocument();
  });

  it("renders a recoverable collection error without fake cards", () => {
    render(
      <SocialProfileView
        profile={profile}
        currentUserId="user-1"
        posts={[]}
        projects={[]}
        collectionsFailed
      />,
    );

    expect(screen.getByRole("alert")).toHaveTextContent(
      "Public work could not be loaded. Refresh to retry.",
    );
    expect(screen.queryByText("Demo project")).not.toBeInTheDocument();
  });

  it("copies the profile link to the clipboard on share", async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText } });

    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={[]} projects={[]} />);
    fireEvent.click(screen.getByRole("button", { name: /share/i }));

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(window.location.href));
  });

  it("reconciles follow state with the authoritative response and rolls back failures", async () => {
    mocks.followCreator.mockResolvedValueOnce({ userId: "user-2", isFollowing: false });
    render(<SocialProfileView profile={profile} currentUserId="user-1" posts={[]} projects={[]} />);

    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Follow Lin Creator" })).toHaveAttribute(
        "aria-pressed",
        "false",
      ),
    );
    expect(screen.getAllByText("42").length).toBeGreaterThan(0);

    mocks.followCreator.mockRejectedValueOnce(new Error("Follow unavailable"));
    fireEvent.click(screen.getByRole("button", { name: "Follow Lin Creator" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Follow Lin Creator" })).toHaveAttribute(
        "aria-pressed",
        "false",
      ),
    );
    expect(screen.getAllByText("42").length).toBeGreaterThan(0);
  });
});
