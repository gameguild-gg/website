import { afterEach, describe, expect, it, vi } from "vitest";
import { requestMagicLink } from "./magic-link-request";

afterEach(() => vi.unstubAllGlobals());

describe("requestMagicLink", () => {
  it("posts the normalized email to the public endpoint without cookies and discards the response body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          developmentPreviewToken: "must-not-be-returned",
        }),
        {
          status: 200,
        },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      requestMagicLink(
        " player@example.com ",
        "https://api.gameguild.example/",
        "/workspace?tab=files",
        "pt-BR",
      ),
    ).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.gameguild.example/v1/auth/magic-link:request",
      expect.objectContaining({
        method: "POST",
        credentials: "omit",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          email: "player@example.com",
          redirectTo: "/workspace?tab=files",
          locale: "pt-BR",
        }),
      }),
    );
  });

  it("returns false when the API rejects the request", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("", { status: 429 })),
    );

    await expect(
      requestMagicLink("player@example.com", "https://api.gameguild.example"),
    ).resolves.toBe(false);
  });
});
