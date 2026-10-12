// @vitest-environment node

import { NextRequest } from "next/server";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { routeRequest } from "./proxy";
import nextConfig from "../next.config";

describe("GameGuild internationalization proxy", () => {
  it("preserves the RSC headers needed by the production proxy", () => {
    expect(nextConfig.skipProxyUrlNormalize).toBe(true);
  });
  it("keeps the default locale internal for an unprefixed route", () => {
    const response = routeRequest(
      new NextRequest(
        "https://gameguild.gg/learn/courses/game-ai/content?module=2",
        { headers: { "accept-language": "en-US" } },
      ),
    );

    expect(response.headers.get("x-middleware-rewrite")).toBe(
      "https://gameguild.gg/en-US/learn/courses/game-ai/content?module=2",
    );
    expect(response.headers.get("location")).toBeNull();
  });

  it("removes an explicit default-locale prefix", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/en-US/projects?view=grid"),
    );

    expect(response.headers.get("location")).toBe(
      "https://gameguild.gg/projects?view=grid",
    );
  });

  it.each([
    "/en-US?_rsc=public-root",
    "/en-US/sign-in?redirectTo=%2Ffeed&_rsc=public-sign-in",
    "/en-US/projects?_rsc=public-projects",
  ])("serves the internal default-locale RSC request %s without a canonical redirect", (path) => {
    const response = routeRequest(new NextRequest(`https://gameguild.gg${path}`, {
      headers: { rsc: "1", "next-router-prefetch": "1" },
    }));

    expect(response.headers.get("location")).toBeNull();
    expect(response.headers.get("x-middleware-rewrite")).toBe(`https://gameguild.gg${path}`);
    expect(response.headers.get("x-middleware-request-x-next-intl-locale")).toBe("en-US");
    expect(response.headers.get("x-middleware-request-x-gameguild-internal-locale-rewrite")).toBe("1");
  });

  it.each(["0", "true"])("keeps HTML canonicalization when the RSC header is %s", (rsc) => {
    const response = routeRequest(new NextRequest("https://gameguild.gg/en-US/projects?_rsc=query-only", {
      headers: { rsc },
    }));

    expect(response.headers.get("location")).toBe("https://gameguild.gg/projects?_rsc=query-only");
  });

  it.each([
    "/workspace/teams/new-team",
    "/workspace/projects/new-project",
  ])("rewrites a Server Action redirect to %s despite the inherited marker", (path) => {
    const response = routeRequest(
      new NextRequest(`https://gameguild.gg${path}?created=1`, {
        headers: {
          "x-gameguild-internal-locale-rewrite": "1",
          "x-next-intl-locale": "en-US",
        },
      }),
    );

    expect(response.headers.get("x-middleware-rewrite")).toBe(
      `https://gameguild.gg/en-US${path}?created=1`,
    );
    expect(response.headers.get("location")).toBeNull();
  });

  it("does not canonicalize an already rewritten internal locale route", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/en-US/workspace/teams/new-team", {
        headers: { "x-gameguild-internal-locale-rewrite": "1" },
      }),
    );

    expect(response.headers.get("x-middleware-next")).toBe("1");
    expect(response.headers.get("location")).toBeNull();
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
  });

  it("preserves an explicitly selected non-default locale", () => {
    const response = routeRequest(
      new NextRequest(
        "https://gameguild.gg/pt-BR/learn/courses/game-ai/grades",
      ),
    );

    expect(response.headers.get("x-middleware-next")).toBe("1");
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
    expect(response.headers.get("location")).toBeNull();
  });

  it("keeps the root on the public page so signed-in members get the /feed redirect", () => {
    const response = routeRequest(new NextRequest("https://gameguild.gg/"));

    expect(response.headers.get("x-middleware-rewrite")).toBe(
      "https://gameguild.gg/en-US",
    );
    expect(response.headers.get("location")).toBeNull();
  });

  it("serves the social feed at /feed through an internal default-locale rewrite", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/feed?tab=following"),
    );

    expect(response.headers.get("x-middleware-rewrite")).toBe(
      "https://gameguild.gg/en-US/feed?tab=following",
    );
    expect(response.headers.get("location")).toBeNull();
  });

  it("canonicalizes the legacy social URL to /feed", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/social?tab=playtests"),
    );

    expect(response.headers.get("location")).toBe(
      "https://gameguild.gg/feed?tab=playtests",
    );
  });

  it("preserves the legacy default-locale social redirect for RSC", () => {
    const response = routeRequest(new NextRequest("https://gameguild.gg/en-US/social?tab=playtests", {
      headers: { rsc: "1", "next-router-prefetch": "1" },
    }));

    expect(response.headers.get("location")).toBe("https://gameguild.gg/feed?tab=playtests");
    expect(response.headers.get("x-middleware-rewrite")).toBeNull();
  });

  describe("request origin preservation with URL normalization disabled", () => {
    beforeEach(() => {
      vi.stubEnv("__NEXT_NO_MIDDLEWARE_URL_NORMALIZE", "true");
    });

    afterEach(() => vi.unstubAllEnvs());

    it.each(["http://127.0.0.1:44025", "http://[::1]:44025"])(
      "keeps the browser and its host-only session cookie on %s during canonicalization",
      (origin) => {
        const request = new NextRequest(`${origin}/en-US/testing-lab/events/test?joinAs=developer`, {
          headers: { cookie: "gameguild.session-token=opaque-session" },
        });
        expect(request.nextUrl.hostname).toBe("localhost");
        expect(new URL(request.url).origin).toBe(origin);

        const response = routeRequest(request);
        expect(response.headers.get("location")).toBe(`${origin}/testing-lab/events/test?joinAs=developer`);
        expect(response.headers.get("set-cookie")).toBeNull();
        expect(request.headers.get("cookie")).toBe("gameguild.session-token=opaque-session");
      },
    );

    it.each(["http://127.0.0.1:44025", "http://[::1]:44025"])(
      "preserves %s for internal RSC rewrites",
      (origin) => {
        const path = "/en-US/testing-lab/events/test?_rsc=key";
        const response = routeRequest(new NextRequest(`${origin}${path}`, {
          headers: { rsc: "1", "next-router-prefetch": "1" },
        }));

        expect(response.headers.get("location")).toBeNull();
        expect(response.headers.get("x-middleware-rewrite")).toBe(`${origin}${path}`);
      },
    );

    it.each(["http://127.0.0.1:44025", "http://[::1]:44025"])(
      "preserves %s for unprefixed HTML rewrites",
      (origin) => {
        const response = routeRequest(new NextRequest(`${origin}/testing-lab/events/test?joinAs=developer`));

        expect(response.headers.get("location")).toBeNull();
        expect(response.headers.get("x-middleware-rewrite")).toBe(`${origin}/en-US/testing-lab/events/test?joinAs=developer`);
      },
    );

    it("uses the actual request origin rather than client-provided forwarding headers", () => {
      const response = routeRequest(new NextRequest("http://127.0.0.1:44025/en-US/projects?view=grid", {
        headers: { "x-forwarded-host": "attacker.example", "x-forwarded-proto": "https" },
      }));

      expect(response.headers.get("location")).toBe("http://127.0.0.1:44025/projects?view=grid");
    });
  });
});
