import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getLearningMeDashboard: vi.fn(),
  getToken: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getToken: mocks.getToken,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningCoursesProgramModule: class {},
    LearningCoursesProgramContentModule: class {},
    LearningWorkspacesLearnerWorkspaceModule: class {
      getLearningMeDashboard = mocks.getLearningMeDashboard;
    },
  },
}));

vi.mock("next/navigation", () => ({
  usePathname: () => '/workspace/learning',
  unstable_rethrow: vi.fn(),
}));

import {
  getLearnerDashboard,
  getMyLearningCourses,
  mapLearnerCourseSummary,
} from "./courses";

describe("learner course aggregate adapter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    process.env.AUTH_SECRET = "test-auth-secret";
    mocks.createServerClient.mockReturnValue({ kind: "learner-client" });
    mocks.getToken.mockResolvedValue("access-token");
  });

  it("maps aggregate summaries and preserves the current lesson", () => {
    expect(
      mapLearnerCourseSummary({
        courseId: "course-1",
        enrollmentId: "enrollment-1",
        title: "Game Production",
        slug: "game-production",
        description: "Ship a playable game.",
        progressPercentage: 4_260,
        totalItems: 10,
        completedItems: 4,
        remainingMinutes: 75,
        currentContentId: "lesson-2",
        currentContentTitle: "Prototype loop",
        currentContentType: "Assignment",
      }),
    ).toMatchObject({
      id: "course-1",
      enrollmentId: "enrollment-1",
      overallProgress: 43,
      currentItem: {
        id: "lesson-2",
        title: "Prototype loop",
        type: "assignment",
        status: "in-progress",
      },
    });
  });

  it("loads enrolled courses with one dashboard request", async () => {
    mocks.getLearningMeDashboard.mockResolvedValue({
      ok: true,
      data: {
        courses: [
          {
            courseId: "course-1",
            enrollmentId: "enrollment-1",
            title: "Game Production",
            slug: "game-production",
            progressPercentage: 2_500,
          },
        ],
      },
    });

    await expect(getMyLearningCourses()).resolves.toEqual([
      expect.objectContaining({ id: "course-1", slug: "game-production" }),
    ]);
    expect(mocks.getLearningMeDashboard).toHaveBeenCalledTimes(1);
    expect(mocks.createServerClient).toHaveBeenCalledTimes(1);
  });

  it("uses the shared auth session when development relies on the auth fallback secret", async () => {
    delete process.env.AUTH_SECRET;
    mocks.getLearningMeDashboard.mockResolvedValue({
      ok: true,
      data: { courses: [] },
    });

    await expect(getLearnerDashboard()).resolves.toMatchObject({ courses: [] });
    expect(mocks.getToken).toHaveBeenCalledTimes(1);
    expect(mocks.getLearningMeDashboard).toHaveBeenCalledTimes(1);
  });

  it("returns no dashboard when the session has no token", async () => {
    mocks.getToken.mockResolvedValue(null);

    await expect(getLearnerDashboard()).resolves.toBeNull();
    expect(mocks.getLearningMeDashboard).not.toHaveBeenCalled();
  });

  it("returns null when the dashboard endpoint fails", async () => {
    mocks.getLearningMeDashboard.mockResolvedValue({
      ok: false,
      error: { message: "Unavailable" },
    });
    const errorSpy = vi
      .spyOn(console, "error")
      .mockImplementation(() => undefined);

    await expect(getLearnerDashboard()).resolves.toBeNull();
    expect(errorSpy).toHaveBeenCalled();
  });
});
