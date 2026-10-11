import { afterEach, describe, expect, it, vi } from "vitest";
import {
  AuthServiceUnavailableError,
  CredentialsProvider,
  CredentialsSignInError,
  MfaRequiredError,
} from "@game-guild/client";
import { createMagicLinkCredentialsAuthorize } from "./magic-link-credentials";

const signInResponse = {
  accessToken: "access-token",
  refreshToken: "refresh-token",
  expiresIn: 900,
  userId: "user-123",
  email: "player@example.com",
  sessionId: "session-456",
};

afterEach(() => vi.unstubAllGlobals());

describe("createMagicLinkCredentialsAuthorize", () => {
  it("uses the existing MFA completion authorizer before any stale magic link", async () => {
    const fallback = vi.fn().mockResolvedValue({ user: { id: "completed" } });
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const credentials = { mfaToken: "x".repeat(43), method: "Totp", code: "654321", magicLinkToken: "already-used" };
    const authorize = createMagicLinkCredentialsAuthorize(fallback, "https://api.gameguild.example");
    await expect(authorize(credentials)).resolves.toEqual({ user: { id: "completed" } });
    expect(fallback).toHaveBeenCalledWith(credentials, undefined);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("preserves a limited MFA challenge without accepting an ordinary session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      success: false, requiresMfa: true, mfaToken: "x".repeat(43),
      availableMethods: ["Totp", "BackupCode"], accessToken: "", refreshToken: "",
    }), { status: 200 })));
    const fallback = vi.fn();
    const authorize = createMagicLinkCredentialsAuthorize(fallback, "https://api.gameguild.example");
    const error = await authorize({ magicLinkToken: "verified-link" }).catch((caught: unknown) => caught);
    expect(error).toBeInstanceOf(MfaRequiredError);
    expect(error).toMatchObject({ mfaToken: "x".repeat(43), availableMethods: ["Totp", "BackupCode"] });
    expect(fallback).not.toHaveBeenCalled();
  });

  it("keeps password sign-in on the existing credentials authorizer", async () => {
    const passwordAuthorize = vi
      .fn()
      .mockResolvedValue({ user: { id: "user-123" } });
    const credentials = {
      email: "player@example.com",
      password: "correct horse",
    };
    const request = new Request(
      "https://gameguild.example/api/auth/signin/credentials",
    );
    const authorize = createMagicLinkCredentialsAuthorize(
      passwordAuthorize,
      "https://api.gameguild.example",
    );

    await expect(authorize(credentials, request)).resolves.toEqual({
      user: { id: "user-123" },
    });

    expect(passwordAuthorize).toHaveBeenCalledWith(credentials, request);
  });

  it("consumes the one-time token and maps the backend response into the auth session", async () => {
    const passwordAuthorize = CredentialsProvider().authorize;
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify(signInResponse), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createMagicLinkCredentialsAuthorize(
      passwordAuthorize,
      "https://api.gameguild.example/",
    );

    const result = await authorize(
      {
        magicLinkToken: " one-time-token ",
        tenantId: "tenant-789",
        __apiUrl: "http://127.0.0.1:8080",
      },
      new Request("https://gameguild.example/api/auth/signin/credentials", {
        headers: { "user-agent": "GameGuild test browser" },
      }),
    );

    expect(fetchMock).toHaveBeenCalledWith(
      new URL("https://api.gameguild.example/v1/auth/magic-link:consume"),
      expect.objectContaining({
        method: "POST",
        cache: "no-store",
        credentials: "omit",
        headers: {
          "Content-Type": "application/json",
          "User-Agent": "GameGuild test browser",
        },
        body: JSON.stringify({
          token: "one-time-token",
          tenantId: "tenant-789",
        }),
      }),
    );
    expect(result).toMatchObject({
      user: { id: "user-123", email: "player@example.com" },
      tokens: { accessToken: "access-token", refreshToken: "refresh-token" },
      sessionId: "session-456",
    });
  });

  it("rejects a missing token without calling the API", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createMagicLinkCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        magicLinkToken: " ",
      }),
    ).rejects.toBeInstanceOf(CredentialsSignInError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not expose backend details when a token is invalid or already used", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ detail: "private token validation internals" }),
          { status: 401 },
        ),
      );
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createMagicLinkCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        magicLinkToken: "bad-token",
      }),
    ).rejects.toThrow("This sign-in link is invalid or has expired.");
  });

  it("reports unavailable backend responses without treating the link as valid", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response("", { status: 503 }));
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createMagicLinkCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        magicLinkToken: "valid-token",
      }),
    ).rejects.toBeInstanceOf(AuthServiceUnavailableError);
  });
});
