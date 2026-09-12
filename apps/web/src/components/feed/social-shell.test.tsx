import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { createElement } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  loadSocialFeed: vi.fn(),
  loadStories: vi.fn(),
  loadSocialProfile: vi.fn(),
  searchSocialProfiles: vi.fn(),
  loadTrendingTags: vi.fn(),
}));

vi.mock("@/auth", () => ({ auth: mocks.auth }));
vi.mock("@/lib/feed/queries", () => ({
  loadSocialFeed: mocks.loadSocialFeed,
  loadStories: mocks.loadStories,
  loadSocialProfile: mocks.loadSocialProfile,
  searchSocialProfiles: mocks.searchSocialProfiles,
  loadTrendingTags: mocks.loadTrendingTags,
}));
vi.mock("@/i18n/navigation", () => ({ Link: ({ children, ...props }: React.ComponentProps<"a">) => <a {...props}>{children}</a> }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ refresh: vi.fn() }) }));
vi.mock("next/image", () => ({
  default: ({ alt = "", ...props }: Record<string, unknown>) =>
    createElement("img", { ...props, alt: typeof alt === "string" ? alt : "" }),
}));

import { SocialShell } from "./social-shell";

describe("SocialShell", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.auth.mockResolvedValue({ user: { id: "user-1", name: "Ada", email: "ada@example.com" } });
    mocks.loadSocialFeed.mockResolvedValue({ items: [], nextCursor: null });
    mocks.loadStories.mockResolvedValue([]);
    mocks.loadSocialProfile.mockResolvedValue(null);
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
    render(await SocialShell({ tab }));
    expect(mocks.loadSocialFeed).toHaveBeenCalledWith({ scope, tag: null });
  });

  it("shows a recoverable error without substituting demo posts", async () => {
    mocks.loadSocialFeed.mockRejectedValue(new Error("offline"));
    render(await SocialShell({ tab: "foryou" }));
    expect(screen.getByText(/feed is temporarily unavailable/i)).toBeInTheDocument();
    expect(screen.queryByTestId("post-card")).not.toBeInTheDocument();
  });
});
