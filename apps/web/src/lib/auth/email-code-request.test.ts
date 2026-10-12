import { afterEach, describe, expect, it, vi } from "vitest";
import { requestEmailCode } from "./email-code-request";

afterEach(() => vi.unstubAllGlobals());

describe("requestEmailCode", () => {
  it("posts the normalized email to the public endpoint without cookies and discards the response body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          success: true,
          message: "If an account with that email exists...",
        }),
        {
          status: 200,
        },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await expect(
      requestEmailCode(
        " player@example.com ",
        "https://api.gameguild.example/",
      ),
    ).resolves.toBe(true);

    expect(fetchMock).toHaveBeenCalledWith(
      "https://api.gameguild.example/v1/auth/email-code:request",
      expect.objectContaining({
        method: "POST",
        credentials: "omit",
        cache: "no-store",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email: "player@example.com" }),
      }),
    );
  });

  it("returns false when the API rejects the request", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(new Response("", { status: 429 })),
    );

    await expect(
      requestEmailCode("player@example.com", "https://api.gameguild.example"),
    ).resolves.toBe(false);
  });

  it("propagates network failures to the caller", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("network down")),
    );

    await expect(
      requestEmailCode("player@example.com", "https://api.gameguild.example"),
    ).rejects.toThrow("network down");
  });
});
