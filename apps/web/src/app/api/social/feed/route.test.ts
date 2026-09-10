import { describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const mocks = vi.hoisted(() => {
  class SocialFeedRequestError extends Error {
    readonly status: number;
    readonly code: string;

    constructor(status: number, code: string, message: string) {
      super(message);
      this.name = "SocialFeedRequestError";
      this.status = status;
      this.code = code;
    }
  }

  return { loadSocialFeed: vi.fn(), SocialFeedRequestError };
});

vi.mock("@/lib/feed/queries", () => ({
  loadSocialFeed: mocks.loadSocialFeed,
  SocialFeedRequestError: mocks.SocialFeedRequestError,
}));

import { GET } from "./route";

describe("social feed pagination route", () => {
  it("forwards validated pagination values and the caller abort signal to the feed query", async () => {
    mocks.loadSocialFeed.mockResolvedValue({ items: [], nextCursor: null });
    const request = new NextRequest("http://localhost/api/social/feed?scope=following&cursor=opaque%2B%2F%3D&tag=indiedev");

    const response = await GET(request);

    expect(mocks.loadSocialFeed).toHaveBeenCalledWith({
      scope: "following",
      cursor: "opaque+/=",
      take: undefined,
      tag: "indiedev",
      signal: request.signal,
    });
    expect(await response.json()).toEqual({ items: [], nextCursor: null });
    expect(response.headers.get("cache-control")).toBe("private, no-store");
  });

  it.each([
    [401, "AUTHENTICATION_ERROR"],
    [403, "FORBIDDEN"],
    [422, "INVALID_CURSOR"],
    [503, "UPSTREAM_UNAVAILABLE"],
  ])("maps typed feed failures to sanitized responses", async (status, code) => {
    mocks.loadSocialFeed.mockRejectedValue(
      new mocks.SocialFeedRequestError(status, code, "backend secret must not reach the browser"),
    );

    const response = await GET(new NextRequest("http://localhost/api/social/feed?scope=following"));

    expect(response.status).toBe(status);
    expect(response.headers.get("cache-control")).toBe("private, no-store");
    await expect(response.json()).resolves.toEqual({ error: "The social feed is unavailable." });
  });

  it("maps unexpected route errors to sanitized no-store 500 responses", async () => {
    mocks.loadSocialFeed.mockRejectedValue(new Error("database password"));

    const response = await GET(new NextRequest("http://localhost/api/social/feed?scope=following"));

    expect(response.status).toBe(500);
    expect(response.headers.get("cache-control")).toBe("private, no-store");
    await expect(response.json()).resolves.toEqual({ error: "The social feed is unavailable." });
  });
});
