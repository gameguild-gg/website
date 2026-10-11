import { afterEach, describe, expect, it, vi } from "vitest";
import {
  AuthServiceUnavailableError,
  CredentialsProvider,
  CredentialsSignInError,
  MfaRequiredError,
} from "@game-guild/client";
import { createEmailCodeCredentialsAuthorize } from "./email-code-credentials";

const signInResponse = {
  accessToken: "access-token",
  refreshToken: "refresh-token",
  expiresIn: 900,
  userId: "user-123",
  email: "player@example.com",
  sessionId: "session-456",
};

afterEach(() => vi.unstubAllGlobals());

describe("createEmailCodeCredentialsAuthorize", () => {
  it("keeps MFA completion on the existing authorizer even with stale email-code fields", async () => {
    const fallback = vi.fn().mockResolvedValue({ user: { id: "completed" } });
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const credentials = {
      mfaToken: "x".repeat(43), method: "Totp", code: "654321",
      emailCode: "123456", emailCodeEmail: "player@example.com",
    };
    const authorize = createEmailCodeCredentialsAuthorize(fallback, "https://api.gameguild.example");
    await expect(authorize(credentials)).resolves.toEqual({ user: { id: "completed" } });
    expect(fallback).toHaveBeenCalledWith(credentials, undefined);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("returns a typed limited MFA challenge without creating an ordinary session", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      success: false, requiresMfa: true, mfaToken: "x".repeat(43),
      availableMethods: ["Totp", "BackupCode"],
      accessToken: "", refreshToken: "",
    }), { status: 200 })));
    const fallback = vi.fn();
    const authorize = createEmailCodeCredentialsAuthorize(fallback, "https://api.gameguild.example");
    const error = await authorize({ emailCode: "123456", emailCodeEmail: "player@example.com" })
      .catch((caught: unknown) => caught);
    expect(error).toBeInstanceOf(MfaRequiredError);
    expect(error).toMatchObject({
      mfaToken: "x".repeat(43), availableMethods: ["Totp", "BackupCode"],
    });
    expect(fallback).not.toHaveBeenCalled();
  });

  it("keeps other credentials on the fallback authorizer", async () => {
    const fallbackAuthorize = vi
      .fn()
      .mockResolvedValue({ user: { id: "user-123" } });
    const credentials = {
      email: "player@example.com",
      password: "correct horse",
    };
    const request = new Request(
      "https://gameguild.example/api/auth/signin/credentials",
    );
    const authorize = createEmailCodeCredentialsAuthorize(
      fallbackAuthorize,
      "https://api.gameguild.example",
    );

    await expect(authorize(credentials, request)).resolves.toEqual({
      user: { id: "user-123" },
    });

    expect(fallbackAuthorize).toHaveBeenCalledWith(credentials, request);
  });

  it("consumes the one-time code and maps the backend response into the auth session", async () => {
    const fallbackAuthorize = CredentialsProvider().authorize;
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify(signInResponse), { status: 200 }),
      );
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createEmailCodeCredentialsAuthorize(
      fallbackAuthorize,
      "https://api.gameguild.example/",
    );

    const result = await authorize(
      {
        emailCode: " 123456 ",
        emailCodeEmail: " player@example.com ",
        tenantId: "tenant-789",
      },
      new Request("https://gameguild.example/api/auth/signin/credentials", {
        headers: { "user-agent": "GameGuild test browser" },
      }),
    );

    expect(fetchMock).toHaveBeenCalledWith(
      new URL("https://api.gameguild.example/v1/auth/email-code:consume"),
      expect.objectContaining({
        method: "POST",
        cache: "no-store",
        credentials: "omit",
        headers: {
          "Content-Type": "application/json",
          "User-Agent": "GameGuild test browser",
        },
        body: JSON.stringify({
          email: "player@example.com",
          code: "123456",
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

  it("rejects a missing code without calling the API", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createEmailCodeCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        emailCode: " ",
        emailCodeEmail: "player@example.com",
      }),
    ).rejects.toBeInstanceOf(CredentialsSignInError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("rejects a missing email without calling the API", async () => {
    const fetchMock = vi.fn();
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createEmailCodeCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        emailCode: "123456",
        emailCodeEmail: " ",
      }),
    ).rejects.toBeInstanceOf(CredentialsSignInError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("does not expose backend details when a code is invalid or already used", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(
        new Response(
          JSON.stringify({ detail: "private code validation internals" }),
          { status: 401 },
        ),
      );
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createEmailCodeCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        emailCode: "000000",
        emailCodeEmail: "player@example.com",
      }),
    ).rejects.toThrow("This sign-in code is invalid or has expired.");
  });

  it("reports unavailable backend responses without treating the code as valid", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValue(new Response("", { status: 503 }));
    vi.stubGlobal("fetch", fetchMock);
    const authorize = createEmailCodeCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        emailCode: "123456",
        emailCodeEmail: "player@example.com",
      }),
    ).rejects.toBeInstanceOf(AuthServiceUnavailableError);
  });

  it("reports an unreachable authentication service as unavailable", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("network down")),
    );
    const authorize = createEmailCodeCredentialsAuthorize(
      CredentialsProvider().authorize,
      "https://api.gameguild.example",
    );

    await expect(
      authorize({
        emailCode: "123456",
        emailCodeEmail: "player@example.com",
      }),
    ).rejects.toBeInstanceOf(AuthServiceUnavailableError);
  });
});
