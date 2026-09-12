import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { createElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createStory: vi.fn(),
  deleteStory: vi.fn(),
  getSocialMediaStatus: vi.fn(),
  markStoryViewed: vi.fn(),
  uploadSocialMedia: vi.fn(),
  refresh: vi.fn(),
}));

vi.mock("@/lib/feed/actions", () => mocks);
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: mocks.refresh }) }));
vi.mock("next/image", () => ({
  default: ({ alt = "", ...props }: Record<string, unknown>) =>
    createElement("img", { ...props, alt: typeof alt === "string" ? alt : "" }),
}));

import { BuildStories, type SocialStoryPreview } from "./build-stories";

const stories: SocialStoryPreview[] = [
  {
    id: "story-1",
    assetReferenceId: "asset-1",
    authorId: "user-2",
    caption: "First build",
    createdAt: "2026-09-10T00:00:00Z",
    expiresAt: "2026-09-11T00:00:00Z",
    isViewed: false,
    mediaType: "image/png",
    mediaUrl: "https://cdn.example/story-1.png",
    authorName: "Ada",
    authorHandle: "ada",
    authorAvatarUrl: null,
    isOwn: false,
  },
  {
    id: "story-2",
    assetReferenceId: "asset-2",
    authorId: "user-1",
    caption: "My build",
    createdAt: "2026-09-10T01:00:00Z",
    expiresAt: "2026-09-11T01:00:00Z",
    isViewed: true,
    mediaType: "image/png",
    mediaUrl: "https://cdn.example/story-2.png",
    authorName: "Me",
    authorHandle: "me",
    authorAvatarUrl: null,
    isOwn: true,
  },
];

describe("BuildStories", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.markStoryViewed.mockResolvedValue(undefined);
    mocks.deleteStory.mockResolvedValue(undefined);
    Object.defineProperty(window, "matchMedia", {
      configurable: true,
      value: vi.fn().mockReturnValue({ matches: true }),
    });
  });
  afterEach(cleanup);

  it("records a view and supports previous/next story navigation", async () => {
    render(<BuildStories userName="Me" stories={stories} />);

    fireEvent.click(screen.getByRole("button", { name: "View Ada's story" }));
    await waitFor(() => expect(mocks.markStoryViewed).toHaveBeenCalledWith("story-1"));
    expect(screen.getByRole("heading", { name: "Ada" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Next story" }));
    expect(screen.getByRole("heading", { name: "Me" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Previous story" })).toBeEnabled();
  });

  it("removes an owned story from the visible list after deletion", async () => {
    render(<BuildStories userName="Me" stories={stories} />);
    fireEvent.click(screen.getByRole("button", { name: "View Me's story" }));
    fireEvent.click(screen.getByRole("button", { name: "Delete story" }));

    await waitFor(() => expect(mocks.deleteStory).toHaveBeenCalledWith("story-2"));
    expect(screen.queryByRole("button", { name: "View Me's story" })).not.toBeInTheDocument();
  });
});
