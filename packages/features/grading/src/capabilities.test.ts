import { describe, expect, it } from "vitest";
import { ReviewCapabilityRegistry, type AssessmentExecutionManifestV1 } from "./index";

const manifest: AssessmentExecutionManifestV1 = {
  schemaVersion: 1,
  items: [{
    itemId: "q1",
    itemType: "example",
    adapterKey: "adapter",
    adapterVersion: "1",
  }],
  stages: [{
    method: "AutomatedReview",
    handlerKey: "handler",
    handlerVersion: "1",
  }],
  policies: [{ policyKey: "policy", policyVersion: "1" }],
};

describe("review capability registry", () => {
  it("resolves exact versions and execution contexts", () => {
    const registry = new ReviewCapabilityRegistry();
    for (const [kind, key] of [
      ["assessment-type-adapter", "adapter"],
      ["execution-policy", "policy"],
    ] as const) {
      registry.registerComponent({ kind, key, version: "1", contexts: ["author-test"] });
    }
    registry.registerReview({
      method: "AutomatedReview",
      handlerKey: "handler",
      handlerVersion: "1",
      contexts: ["author-test"],
    });

    expect(registry.validateManifest(manifest, "author-test")).toEqual([]);
    expect(registry.validateManifest(manifest, "official-submission")).toHaveLength(3);
    expect(registry.resolveComponent("assessment-type-adapter", "adapter", "2", "author-test")).toBeNull();
  });
});
