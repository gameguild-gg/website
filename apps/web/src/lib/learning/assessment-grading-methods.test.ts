import { describe, expect, it } from "vitest";

import {
  buildReviewWorkflow,
  hasReviewMethod,
  isPrimaryReviewMethodAvailable,
  readReviewWorkflow,
  REVIEW_METHOD_FLAGS,
} from "./assessment-grading-methods";

describe("assessment grading method helpers", () => {
  it("reads a canonical two-stage review workflow", () => {
    expect(
      readReviewWorkflow(
        REVIEW_METHOD_FLAGS.AutomatedReview |
          REVIEW_METHOD_FLAGS.InstructorReview,
      ),
    ).toEqual({
      methods: 12,
      primary: "AutomatedReview",
      requiresInstructorReview: true,
    });
    expect(readReviewWorkflow(0)).toEqual({
      methods: 0,
      primary: null,
      requiresInstructorReview: false,
    });
  });

  it("builds and inspects canonical review flags", () => {
    const methods = buildReviewWorkflow("PeerReview", true);
    expect(methods).toBe(9);
    expect(hasReviewMethod(methods, "PeerReview")).toBe(true);
    expect(hasReviewMethod(methods, "InstructorReview")).toBe(true);
    expect(hasReviewMethod(methods, "AIReview")).toBe(false);
    expect(buildReviewWorkflow("InstructorReview", true)).toBe(8);
  });

  it("keeps implemented human reviews available and AI review unavailable without a provider", () => {
    expect(isPrimaryReviewMethodAvailable("AutomatedReview")).toBe(true);
    expect(isPrimaryReviewMethodAvailable("InstructorReview")).toBe(true);
    expect(isPrimaryReviewMethodAvailable("SelfReview")).toBe(true);
    expect(isPrimaryReviewMethodAvailable("PeerReview")).toBe(true);
    expect(isPrimaryReviewMethodAvailable("AIReview")).toBe(false);
  });
});
