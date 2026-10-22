// @vitest-environment node

import { describe, expect, it } from "vitest";
import { getGradingScenarioLabModel } from "./service.server";

describe("grading scenario service", () => {
  it("loads quiz through the adapter registry without exposing it in the core service", async () => {
    const model = await getGradingScenarioLabModel();
    const adapter = model.adapters.find(
      (candidate) => candidate.key === "quiz",
    );

    expect(adapter).toBeDefined();
    expect(adapter?.defaultScenarioKey).toBe("review-matrix");
    expect(adapter?.scenarios.length).toBeGreaterThan(0);
    expect(
      adapter?.scenarios
        .find((scenario) => scenario.key === "review-matrix")
        ?.targets.map((target) => target.key),
    ).toContain("automated-instructor");
    expect(
      adapter?.capabilityCatalog.some(
        (entry) => entry.id === "QG-REVIEW-AUTOMATED-INSTRUCTOR-001",
      ),
    ).toBe(true);
    expect(model.states.some((state) => state.adapterKey === "quiz")).toBe(
      true,
    );
  });
});
