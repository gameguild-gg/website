import { describe, expect, it } from "vitest";
import {
  validateAssessmentExecutionDelivery,
  validateAssessmentExecutionManifest,
  validateAssessmentExecutionPolicy,
  validateAssessmentExecutionSnapshot,
} from "./execution-contracts";

const policy = {
  schemaVersion: 1,
  passingScore: 100,
  maxAttempts: 2,
  attemptContribution: { mode: "highest-finalized" },
  availability: { allowLateSubmissions: false },
  completion: { mode: "on-release-and-pass" },
  resultRelease: { mode: "manual" },
  presentation: { mode: "continuous" },
  review: { schemaVersion: 1, methods: 12, instructor: { requireOverrideReason: true } },
};

const manifest = {
  schemaVersion: 1,
  items: [{
    itemId: "q1",
    itemType: "TRUE_FALSE",
    adapterKey: "quiz-assessment-type",
    adapterVersion: "1",
  }],
  stages: [
    { method: "AutomatedReview", handlerKey: "quiz-automated-review", handlerVersion: "1" },
    { method: "InstructorReview", handlerKey: "instructor-review", handlerVersion: "1" },
  ],
  policies: [],
};

describe("execution contracts", () => {
  it("validates a strict policy and requires contribution for repeated attempts", () => {
    expect(validateAssessmentExecutionPolicy(policy)).toEqual(policy);
    expect(() => validateAssessmentExecutionPolicy({ ...policy, attemptContribution: undefined })).toThrow(/attemptContribution/);
    expect(() => validateAssessmentExecutionPolicy({ ...policy, maxScore: 200 })).toThrow(/not allowed/);
    expect(() => validateAssessmentExecutionPolicy({
      ...policy,
      review: { schemaVersion: 1, methods: 8 },
    })).toThrow(/instructor is required/);
    expect(() => validateAssessmentExecutionPolicy({
      ...policy,
      review: { schemaVersion: 1, methods: 8, instructor: { requireOverrideReason: "yes" } },
    })).toThrow(/must be boolean/);
  });

  it("validates canonical manifest order and paired executable versions", () => {
    expect(validateAssessmentExecutionManifest(manifest)).toEqual(manifest);
    expect(() => validateAssessmentExecutionManifest({
      ...manifest,
      stages: [{ ...manifest.stages[0], algorithmVersion: "1" }],
    })).toThrow(/not allowed/);
    expect(() => validateAssessmentExecutionManifest({
      ...manifest,
      stages: [...manifest.stages].reverse(),
    })).toThrow(/final review stage/);
  });

  it("requires snapshot projections and explicit delivery order to match items", () => {
    const snapshot = {
      schemaVersion: 1,
      authoringSource: { schemaVersion: 1, contentType: "quiz", content: {}, grading: { schemaVersion: 2, items: { q1: {} } }, policy },
      manifest,
      itemProjections: {
        q1: {
          schemaVersion: 1,
          itemId: "q1",
          itemType: "TRUE_FALSE",
          maxScore: 100,
          source: { contentType: "quiz", itemId: "q1" },
        },
      },
    };
    expect(validateAssessmentExecutionSnapshot(snapshot)).toEqual(snapshot);
    expect(() => validateAssessmentExecutionSnapshot({ ...snapshot, itemProjections: {} })).toThrow(/same item IDs/);
    expect(() => validateAssessmentExecutionSnapshot({
      ...snapshot,
      authoringSource: {
        ...snapshot.authoringSource,
        grading: { schemaVersion: 2, items: { q2: {} } },
      },
    })).toThrow(/authoring grading items/);
    expect(() => validateAssessmentExecutionSnapshot({
      ...snapshot,
      itemProjections: {
        q1: { ...snapshot.itemProjections.q1, itemType: "ESSAY" },
      },
    })).toThrow(/manifest item type/);
    expect(() => validateAssessmentExecutionSnapshot({
      ...snapshot,
      authoringSource: {
        ...snapshot.authoringSource,
        policy: {
          ...policy,
          review: { schemaVersion: 1, methods: 8, instructor: { requireOverrideReason: true } },
        },
      },
    })).toThrow(/exactly match policy/);

    const delivery = {
      schemaVersion: 1,
      definitionRevisionId: "00000000-0000-4000-8000-000000000001",
      executionSnapshotHash: "0000000000000000000000000000000000000000000000000000000000000000",
      itemOrder: ["q1"],
      items: { q1: { adapterKey: "quiz-assessment-type", adapterVersion: "1", learnerPayload: {} } },
    };
    expect(validateAssessmentExecutionDelivery(delivery)).toEqual(delivery);
    expect(() => validateAssessmentExecutionDelivery({ ...delivery, executionSnapshotHash: "hash" })).toThrow(/SHA-256/);
    expect(() => validateAssessmentExecutionDelivery({ ...delivery, itemOrder: [] })).toThrow(/every delivery item/);
    expect(() => validateAssessmentExecutionDelivery({
      ...delivery,
      items: { q1: { adapterKey: "quiz-assessment-type", adapterVersion: "1" } },
    })).toThrow(/learnerPayload is required/);
  });
});
