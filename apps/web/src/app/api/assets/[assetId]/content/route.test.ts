import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const mocks = vi.hoisted(() => ({
  getToken: vi.fn(),
  fetch: vi.fn(),
}));

vi.mock("@/auth", () => ({ getToken: mocks.getToken }));

import { GET } from "./route";

describe("authenticated asset proxy", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", mocks.fetch);
    vi.stubEnv("API_URL", "http://localhost:8080");
    mocks.getToken.mockResolvedValue("viewer-token");
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    vi.clearAllMocks();
  });

  it("forwards the viewer token, range, and safe media headers", async () => {
    mocks.fetch.mockResolvedValue(new Response(new Uint8Array([1, 2, 3]), {
      status: 206,
      headers: {
        "content-type": "image/png",
        "content-range": "bytes 0-2/3",
        "accept-ranges": "bytes",
      },
    }));

    const response = await GET(
      new NextRequest("http://localhost/api/assets/asset-1/content?transform=width%3D320", {
        headers: { range: "bytes=0-2" },
      }),
      { params: Promise.resolve({ assetId: "asset-1" }) },
    );

    expect(mocks.fetch).toHaveBeenCalledWith(
      new URL("http://localhost:8080/api/assets/asset-1/content?transform=width%3D320"),
      expect.objectContaining({
        cache: "no-store",
        redirect: "follow",
        headers: {
          authorization: "Bearer viewer-token",
          range: "bytes=0-2",
        },
      }),
    );
    expect(response.status).toBe(206);
    expect(response.headers.get("content-type")).toBe("image/png");
    expect(response.headers.get("cache-control")).toBe("private, max-age=300");
    expect(Array.from(new Uint8Array(await response.arrayBuffer()))).toEqual([1, 2, 3]);
  });

  it("rejects anonymous media requests without contacting the API", async () => {
    mocks.getToken.mockResolvedValue(null);

    const response = await GET(
      new NextRequest("http://localhost/api/assets/asset-1/content"),
      { params: Promise.resolve({ assetId: "asset-1" }) },
    );

    expect(response.status).toBe(401);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });
});
