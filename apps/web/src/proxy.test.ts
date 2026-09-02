// @vitest-environment node

import { NextRequest } from "next/server";
import { describe, expect, it } from "vitest";

import { routeRequest } from "./proxy";

describe("GameGuild internationalization proxy", () => {
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

  it("serves the authenticated social feed from the unprefixed root", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/?tab=following"),
      true,
    );

    expect(response.headers.get("x-middleware-rewrite")).toBe(
      "https://gameguild.gg/en-US/social?tab=following",
    );
    expect(response.headers.get("location")).toBeNull();
  });

  it("canonicalizes the legacy authenticated social URL to root", () => {
    const response = routeRequest(
      new NextRequest("https://gameguild.gg/social?tab=playtests"),
      true,
    );

    expect(response.headers.get("location")).toBe(
      "https://gameguild.gg/?tab=playtests",
    );
  });
});
