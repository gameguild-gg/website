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
    const incoming = new FormData();
    incoming.append("file", new Blob(["png"], { type: "image/png" }), "build.png");
    incoming.append("actorId", "attacker");
    incoming.append("tenantId", "other-tenant");
    vi.spyOn(request, "formData").mockResolvedValue(incoming);
    const response = await POST(request);
    expect(mocks.fetch).toHaveBeenCalledWith(new URL("http://localhost:8080/v1/assets/social-media"), expect.objectContaining({
      method: "POST",
      headers: { authorization: "Bearer viewer-token" },
      cache: "no-store",
    }));
    expect(response.status).toBe(201);
    const upstreamBody = mocks.fetch.mock.calls[0]?.[1].body as FormData;
    expect([...upstreamBody.keys()]).toEqual(["file"]);
    expect((upstreamBody.get("file") as File).name).toBe("build.png");
    expect(mocks.fetch.mock.calls[0]?.[1].signal).toBe(request.signal);
  });

  it("rejects duplicate file fields without forwarding them", async () => {
    const request = new NextRequest("http://localhost/api/social/media", { method: "POST" });
    const incoming = new FormData();
    incoming.append("file", new Blob(["one"]), "one.png");
    incoming.append("file", new Blob(["two"]), "two.png");
    vi.spyOn(request, "formData").mockResolvedValue(incoming);
    const response = await POST(request);
    expect(response.status).toBe(400);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });

  it("sanitizes aborted upstream uploads with a private no-store response", async () => {
    mocks.fetch.mockRejectedValue(new DOMException("internal details", "AbortError"));
    const request = new NextRequest("http://localhost/api/social/media", { method: "POST" });
    const incoming = new FormData();
    incoming.append("file", new Blob(["png"]), "build.png");
    vi.spyOn(request, "formData").mockResolvedValue(incoming);
    const response = await POST(request);
    expect(response.status).toBe(499);
    expect(response.headers.get("cache-control")).toBe("private, no-store");
    await expect(response.json()).resolves.toEqual({ error: "Upload cancelled." });
    expect(mocks.fetch.mock.calls[0]?.[1].signal).toBe(request.signal);
  });

  it("rejects anonymous uploads without contacting the API", async () => {
    mocks.getToken.mockResolvedValue(null);
    const response = await POST(new NextRequest("http://localhost/api/social/media", { method: "POST", body: new FormData() }));
    expect(response.status).toBe(401);
    expect(mocks.fetch).not.toHaveBeenCalled();
  });
});
