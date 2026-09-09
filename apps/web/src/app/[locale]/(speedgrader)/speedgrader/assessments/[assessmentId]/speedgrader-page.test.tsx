import "@testing-library/jest-dom/vitest";
import { beforeEach, describe, expect, it, vi } from "vitest";

const clientMocks = vi.hoisted(() => ({
  request: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getToken: async () => "test-token",
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: () => ({ request: clientMocks.request }),
}));

import { fetchGradingQueue } from "./grading-queue";

const rubriclessQueue = {
  assessment: {
    id: "assessment-1",
    title: "Plain assignment",
    type: "Assignment",
    maxScore: 10_000,
    reviewMethods: 8,
    groupSetId: null,
    peerReviewsRequiredCount: 0,
    hasRubric: false,
    rubric: null,
  },
  items: [
    {
      submissionId: "sub-1",
      canonicalSubmissionId: "sub-1",
      userId: "user-1",
      displayName: "Ada Lovelace",
      attemptNumber: 1,
      status: "Submitted",
      isLate: false,
      submittedAt: "2026-08-01T10:00:00Z",
      isGroup: false,
    },
  ],
  total: 1,
  needsGrading: 1,
};

const rubricQueue = {
  ...rubriclessQueue,
  assessment: {
    ...rubriclessQueue.assessment,
    title: "Rubric assignment",
    hasRubric: true,
    rubric: {
      id: "rubric-1",
      title: "Rubric",
      criteria: [{ id: "c1", description: "Correctness", points: 6_000, order: 0 }],
    },
  },
};

describe("fetchGradingQueue raw channel", () => {
  beforeEach(() => {
    clientMocks.request.mockReset();
  });

  it("accepts a rubric-less queue (rubric: null) and passes it through unchanged", async () => {
    clientMocks.request.mockResolvedValue({ ok: true, data: rubriclessQueue });

    const result = await fetchGradingQueue("assessment-1");

    expect(result).toEqual({ ok: true, data: rubriclessQueue });
  });

  it("accepts a rubric-bearing queue and passes it through unchanged", async () => {
    clientMocks.request.mockResolvedValue({ ok: true, data: rubricQueue });

    const result = await fetchGradingQueue("assessment-1");

    expect(result).toEqual({ ok: true, data: rubricQueue });
  });

  it("requests the grading-queue endpoint with auth via the raw channel", async () => {
    clientMocks.request.mockResolvedValue({ ok: true, data: rubriclessQueue });

    await fetchGradingQueue("assessment-1");

    expect(clientMocks.request).toHaveBeenCalledWith({
      method: "GET",
      path: "/v1.0/assessments/assessment-1/grading-queue",
      requiresAuth: true,
    });
  });

  it("surfaces API errors as an error result", async () => {
    clientMocks.request.mockResolvedValue({
      ok: false,
      error: { status: 403, message: "Forbidden" },
    });

    const result = await fetchGradingQueue("assessment-1");

    expect(result).toEqual({
      ok: false,
      status: 403,
      message: "You do not have permission to grade this assessment.",
    });
  });
});
