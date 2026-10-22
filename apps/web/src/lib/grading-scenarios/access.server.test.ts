// @vitest-environment node

import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  getSession: vi.fn(),
  hasRole: vi.fn(),
  headers: vi.fn(),
}));

vi.mock("@/auth", () => ({ getSession: mocks.getSession }));
vi.mock("@game-guild/client", () => ({ hasRole: mocks.hasRole }));
vi.mock("next/headers", () => ({ headers: mocks.headers }));

import { getGradingScenarioLabAccess } from "./access.server";

describe("grading scenario lab server access", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv("NODE_ENV", "test");
    vi.stubEnv("GRADING_SCENARIO_LAB_ENABLED", "1");
    mocks.headers.mockResolvedValue(
      new Headers({ host: "localhost:3000", "x-forwarded-proto": "http" }),
    );
    mocks.getSession.mockResolvedValue({ user: { roles: ["SystemAdmin"] } });
    mocks.hasRole.mockReturnValue(true);
  });

  it("fails closed in production even with an explicit opt-in", async () => {
    vi.stubEnv("NODE_ENV", "production");

    await expect(getGradingScenarioLabAccess()).resolves.toEqual({
      allowed: false,
      webBaseUrl: null,
      reason: "disabled",
    });
    expect(mocks.getSession).not.toHaveBeenCalled();
  });

  it("rejects a non-local request before reading the session", async () => {
    mocks.headers.mockResolvedValue(new Headers({ host: "example.com" }));

    await expect(getGradingScenarioLabAccess()).resolves.toEqual({
      allowed: false,
      webBaseUrl: null,
      reason: "non-local",
    });
    expect(mocks.getSession).not.toHaveBeenCalled();
  });

  it("rejects an unauthenticated request", async () => {
    mocks.getSession.mockResolvedValue(null);

    await expect(getGradingScenarioLabAccess()).resolves.toMatchObject({
      allowed: false,
      reason: "unauthenticated",
    });
  });

  it("rejects an authenticated user without SystemAdmin", async () => {
    mocks.hasRole.mockReturnValue(false);

    await expect(getGradingScenarioLabAccess()).resolves.toMatchObject({
      allowed: false,
      reason: "forbidden",
    });
  });

  it("accepts a local SystemAdmin request", async () => {
    await expect(getGradingScenarioLabAccess()).resolves.toEqual({
      allowed: true,
      webBaseUrl: "http://localhost:3000",
      reason: "allowed",
    });
  });
});
