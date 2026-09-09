import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  request: vi.fn(),
  getRequestAuthContext: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getRequestAuthContext: mocks.getRequestAuthContext,
}));
vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
}));

import {
  getDashboardContexts,
  hasAnyDashboardCapability,
} from "./dashboard-contexts";

describe("dashboard contexts query", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getRequestAuthContext.mockResolvedValue({
      token: "reviewer-token",
      tenantId: "reviewer-tenant",
    });
    mocks.createServerClient.mockReturnValue({ request: mocks.request });
  });

  it("loads management capabilities without requesting personal workspace contexts", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: {
        capabilities: ["TestingLab.ManageEvents"],
      },
    });

    const result = await getDashboardContexts();

    expect(mocks.request).toHaveBeenCalledWith({
      method: "GET",
      path: "/v1/access/capabilities",
    });
    const clientOptions = mocks.createServerClient.mock.calls[0]?.[0];
    expect(await clientOptions?.auth.getAccessToken()).toBe("reviewer-token");
    expect(await clientOptions?.tenant.getTenantId()).toBe("reviewer-tenant");
    expect(result.capabilities).toEqual(["TestingLab.ManageEvents"]);
    expect(result.contexts.map((context) => context.type)).toEqual([
      "Workspace",
      "Operations",
    ]);
    expect(result.counts).toEqual({
      teams: 0,
      projects: 0,
      pendingTasks: 0,
      invitations: 0,
    });
  });

  it("fails closed when contexts cannot be loaded", async () => {
    mocks.request.mockResolvedValue({ ok: false, error: { status: 503 } });

    await expect(getDashboardContexts()).resolves.toEqual({
      contexts: [
        { type: "Workspace", id: null, name: "Workspace", route: "/workspace" },
      ],
      capabilities: [],
      counts: { teams: 0, projects: 0, pendingTasks: 0, invitations: 0 },
      navigation: [],
    });
  });

  it("does not expose Operations for non-management capabilities", async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: { capabilities: ["TestingLab.Participate"] },
    });

    const result = await getDashboardContexts();

    expect(result.contexts).toEqual([
      { type: "Workspace", id: null, name: "Workspace", route: "/workspace" },
    ]);
  });

  it("matches management modules without treating participation as management", () => {
    expect(
      hasAnyDashboardCapability(
        ["TestingLab.ManageEvents"],
        "TestingLab.",
      ),
    ).toBe(true);
    expect(
      hasAnyDashboardCapability(
        ["TestingLab.Participate"],
        "TestingLab.Manage",
        "TestingLab.Review",
        "TestingLab.ViewAnalytics",
      ),
    ).toBe(false);
  });
});
