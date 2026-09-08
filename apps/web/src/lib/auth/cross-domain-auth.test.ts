import { describe, expect, it } from "vitest";

import {
  createSharedAuthCookieConfig,
  resolveAllowedAuthRedirect,
} from "./cross-domain-auth";

describe("cross-domain auth", () => {
  it("uses a secure shared cookie only when explicitly configured", () => {
    expect(
      createSharedAuthCookieConfig({
        authCookieDomain: ".gameguild.gg",
        nodeEnv: "production",
      }),
    ).toEqual({
      domain: ".gameguild.gg",
      httpOnly: true,
      name: "gameguild",
      path: "/",
      sameSite: "lax",
      secure: true,
    });

    expect(
      createSharedAuthCookieConfig({
        authCookieDomain: "",

        nodeEnv: "development",
      }),
    ).toEqual({
      domain: undefined,

      httpOnly: true,

      name: "gameguild",

      path: "/",

      sameSite: "lax",

      secure: false,
    });

    expect(
      createSharedAuthCookieConfig({
        authCookieDomain: ".gameguild.127.0.0.1.sslip.io",
        authCookieSecure: "false",
        nodeEnv: "production",
      }),
    ).toEqual({
      domain: ".gameguild.127.0.0.1.sslip.io",
      httpOnly: true,
      name: "gameguild",
      path: "/",
      sameSite: "lax",
      secure: false,
    });
  });

  it("allows local application routes", () => {
    expect(resolveAllowedAuthRedirect(undefined)).toBe("/social");
    expect(resolveAllowedAuthRedirect("/dashboard")).toBe("/dashboard");
    expect(
      resolveAllowedAuthRedirect(
        "/pt-BR/learn/courses/game-ai/lessons/intro?mode=focus",
      ),
    ).toBe("/pt-BR/learn/courses/game-ai/lessons/intro?mode=focus");
  });

  it("rejects protocol-relative, absolute, malformed, and non-string redirects", () => {
    const options = { fallback: "/dashboard" };

    expect(resolveAllowedAuthRedirect("//evil.example/path", options)).toBe(
      "/dashboard",
    );
    expect(
      resolveAllowedAuthRedirect(
        ["/dashboard", "https://evil.example"],
        options,
      ),
    ).toBe("/dashboard");
    expect(
      resolveAllowedAuthRedirect("https://evil.example/path", options),
    ).toBe("/dashboard");
    expect(
      resolveAllowedAuthRedirect(
        "https://gameguild.gg/learn/courses/game-ai",
        options,
      ),
    ).toBe("/dashboard");
    expect(resolveAllowedAuthRedirect("not a url", options)).toBe("/dashboard");
  });
});
