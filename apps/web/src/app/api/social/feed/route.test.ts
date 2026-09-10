import { describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const mocks = vi.hoisted(() => ({ loadSocialFeed: vi.fn() }));

vi.mock("@/lib/feed/queries", () => ({ loadSocialFeed: mocks.loadSocialFeed }));

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
  });
});
