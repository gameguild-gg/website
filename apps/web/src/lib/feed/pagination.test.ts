import { beforeEach, describe, expect, it, vi } from "vitest";

import { loadSocialFeedPage } from "./pagination";

describe("social feed pagination transport", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("forwards the opaque cursor, tag, and abort signal to the session-aware feed route", async () => {
    const signal = new AbortController().signal;
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(JSON.stringify({ items: [], nextCursor: null }), { status: 200 }),
    );

    await loadSocialFeedPage({ scope: "following", cursor: "opaque+/=", tag: "indiedev", signal });

    expect(fetchMock).toHaveBeenCalledWith(
      "/api/social/feed?scope=following&cursor=opaque%2B%2F%3D&tag=indiedev",
      expect.objectContaining({ signal, headers: { Accept: "application/json" } }),
    );
  });
});
