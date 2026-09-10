import { beforeEach, describe, expect, it, vi } from "vitest";
import { NextRequest } from "next/server";

const mocks = vi.hoisted(() => ({ getToken: vi.fn(), fetch: vi.fn() }));
vi.mock("@/auth", () => ({ getToken: mocks.getToken }));

import { POST } from "./route";

describe("social media upload proxy", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal("fetch", mocks.fetch);
    vi.stubEnv("API_URL", "http://localhost:8080");
    mocks.getToken.mockResolvedValue("viewer-token");
  });

  it("forwards only the browser file with the server-derived viewer token", async () => {
    mocks.fetch.mockResolvedValue(new Response(JSON.stringify({ assetReferenceId: "asset-1", state: "Ready" }), { status: 201, headers: { "content-type": "application/json" } }));
    const request = new NextRequest("http://localhost/api/social/media", { method: "POST" });
    vi.spyOn(request, "formData").mockResolvedValue({ get: () => new Blob(["png"], { type: "image/png" }) } as FormData);
    const response = await POST(request);
    expect(mocks.fetch).toHaveBeenCalledWith(new URL("http://localhost:8080/v1/assets/social-media"), expect.objectContaining({
      method: "POST",
      headers: { authorization: "Bearer viewer-token" },
      cache: "no-store",
    }));
    expect(response.status).toBe(201);
  });

  it("rejects anonymous uploads without contacting the API", async () => {
    mocks.getToken.mockResolvedValue(null);
    const response = await POST(new NextRequest("http://localhost/api/social/media", { method: "POST", body: new FormData() }));
    expect(response.status).toBe(401);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });
});
