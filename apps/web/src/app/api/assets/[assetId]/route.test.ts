import { NextRequest } from "next/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({ getToken: vi.fn(), fetch: vi.fn() }));
vi.mock("@/auth", () => ({ getToken: mocks.getToken }));

import { DELETE, GET } from "./route";

describe("learning asset item proxy", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("fetch", mocks.fetch);
    vi.stubEnv("API_URL", "http://localhost:8080");
    mocks.getToken.mockResolvedValue("author-token");
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it("forwards authenticated metadata reads", async () => {
    mocks.fetch.mockResolvedValue(new Response("{}", { status: 200 }));
    const request = new NextRequest("http://localhost/api/assets/asset-1?includeContent=true");
    const response = await GET(request, { params: Promise.resolve({ assetId: "asset-1" }) });

    expect(response.status).toBe(200);
    expect(mocks.fetch).toHaveBeenCalledWith(
      "http://localhost:8080/v1/assets/asset-1?includeContent=true",
      expect.objectContaining({
        cache: "no-store",
        headers: { authorization: "Bearer author-token" },
        signal: request.signal,
      }),
    );
  });

  it("forwards authenticated deletion without a browser-supplied actor", async () => {
    mocks.fetch.mockResolvedValue(new Response(null, { status: 204 }));
    const request = new NextRequest("http://localhost/api/assets/asset-1", { method: "DELETE" });
    const response = await DELETE(request, { params: Promise.resolve({ assetId: "asset-1" }) });

    expect(response.status).toBe(204);
    expect(mocks.fetch).toHaveBeenCalledWith(
      "http://localhost:8080/v1/assets/asset-1",
      expect.objectContaining({
        method: "DELETE",
        headers: { authorization: "Bearer author-token" },
        signal: request.signal,
      }),
    );
  });
});

describe("asset proxy with a production internal API", () => {

  beforeEach(() => {
    vi.stubEnv("NODE_ENV", "production");
    vi.stubEnv("API_URL", "http://backend:8080");
    vi.stubEnv("ALLOW_UNSAFE_REMOTE_URL", undefined);
    vi.stubGlobal("fetch", mocks.fetch);
    mocks.fetch.mockReset();
    mocks.getToken.mockResolvedValue("test-access-token");
    mocks.fetch.mockResolvedValue(Response.json({ id: "asset-1" }));
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it.each(["GET", "DELETE"] as const)(
    "proxies %s to the configured origin",
    async (method) => {
      const route = await import("./route");
      const request = new NextRequest(
        "https://gameguild.gg/api/assets/asset-1",
      );
      const response = await route[method](request, {
        params: Promise.resolve({ assetId: "asset-1" }),
      });

      expect(response.status).toBe(200);
      expect(await response.json()).toEqual({ id: "asset-1" });
      expect(mocks.fetch).toHaveBeenCalledOnce();
      expect(mocks.fetch.mock.calls[0][0]).toBe(
        `http://backend:8080/v1/assets/asset-1${method === "GET" ? "?includeContent=true" : ""}`,
      );
      expect(mocks.fetch.mock.calls[0][1].headers).toEqual({
        authorization: "Bearer test-access-token",
      });
    },
  );
});
