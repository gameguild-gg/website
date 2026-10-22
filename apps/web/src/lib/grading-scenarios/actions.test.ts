// @vitest-environment node

import { beforeEach, describe, expect, it, vi } from "vitest";
import type { GradingScenarioState } from "./contracts";

const mocks = vi.hoisted(() => ({
  access: vi.fn(),
  inspect: vi.fn(),
  prepare: vi.fn(),
  reset: vi.fn(),
  verify: vi.fn(),
  revalidatePath: vi.fn(),
}));

vi.mock("./access.server", () => ({
  getGradingScenarioLabAccess: mocks.access,
}));
vi.mock("./service.server", () => ({
  inspectGradingScenario: mocks.inspect,
  prepareGradingScenario: mocks.prepare,
  resetGradingScenario: mocks.reset,
  verifyGradingScenario: mocks.verify,
}));
vi.mock("next/cache", () => ({ revalidatePath: mocks.revalidatePath }));

import {
  inspectGradingScenarioAction,
  prepareGradingScenarioAction,
  resetGradingScenarioAction,
} from "./actions";

const state: GradingScenarioState = {
  adapterKey: "quiz",
  scenarioKey: "review-matrix",
  health: "ready",
  prepared: true,
  runId: "run-1",
  marker: "marker-1",
  declaredCheckpoint: "learner-ready",
  confirmedCheckpoint: "learner-ready",
  nextCheckpoint: "review-ready",
  updatedAt: "2026-10-20T12:00:00.000Z",
  diagnostic: null,
  targets: [],
  personas: [],
  links: [],
  checks: [],
};

describe("grading scenario actions", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubEnv("API_URL", "http://localhost:8080");
    mocks.access.mockResolvedValue({
      allowed: true,
      webBaseUrl: "http://localhost:3000",
      reason: "allowed",
    });
    mocks.inspect.mockResolvedValue(state);
    mocks.prepare.mockResolvedValue({ state, progress: ["prepared"] });
    mocks.reset.mockResolvedValue({
      scenario: "review-matrix",
      removed: ["owned course"],
      skipped: [],
      failures: [],
    });
  });

  it("prepares a deduplicated selection with server-owned endpoints", async () => {
    const result = await prepareGradingScenarioAction({
      adapterKey: " quiz ",
      scenarioKey: " review-matrix ",
      targetKeys: ["automated", "automated"],
      checkpoint: " learner-ready ",
      fresh: true,
    });

    expect(result.success).toBe(true);
    expect(mocks.prepare).toHaveBeenCalledWith("quiz", {
      scenarioKey: "review-matrix",
      targetKeys: ["automated"],
      checkpoint: "learner-ready",
      fresh: true,
      apiBaseUrl: "http://localhost:8080",
      webBaseUrl: "http://localhost:3000",
      onProgress: expect.any(Function),
    });
  });

  it("refreshes state without running verification", async () => {
    const result = await inspectGradingScenarioAction({
      adapterKey: "quiz",
      scenarioKey: "review-matrix",
    });

    expect(result).toMatchObject({ success: true, state });
    expect(mocks.inspect).toHaveBeenCalledWith("quiz", "review-matrix");
    expect(mocks.verify).not.toHaveBeenCalled();
  });

  it("fails closed when the lab is unavailable", async () => {
    mocks.access.mockResolvedValue({
      allowed: false,
      webBaseUrl: null,
      reason: "forbidden",
    });

    await expect(
      inspectGradingScenarioAction({
        adapterKey: "quiz",
        scenarioKey: "review-matrix",
      }),
    ).resolves.toMatchObject({
      success: false,
      error: expect.stringMatching(/unavailable/),
    });
    expect(mocks.inspect).not.toHaveBeenCalled();
  });

  it("keeps the ownership manifest when cleanup reports failures", async () => {
    mocks.reset.mockResolvedValue({
      scenario: "review-matrix",
      removed: [],
      skipped: [],
      failures: ["course deletion failed"],
    });

    const result = await resetGradingScenarioAction({
      adapterKey: "quiz",
      scenarioKey: "review-matrix",
    });

    expect(result).toMatchObject({
      success: false,
      error: expect.stringMatching(/course deletion failed/),
    });
    expect(mocks.inspect).not.toHaveBeenCalled();
  });
});
