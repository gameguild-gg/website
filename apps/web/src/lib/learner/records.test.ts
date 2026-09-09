import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getLearningCoursesWorkspace: vi.fn(),
  getApiSocialDiscussion: vi.fn(),
  getApiSocialDiscussionReplies: vi.fn(),
  getApiCertificatesMy: vi.fn(),
  getProjectsCreator: vi.fn(),
  getToken: vi.fn(),
  getLearnerDashboard: vi.fn(),
  mapLearnerCourseSummary: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getToken: mocks.getToken,
}));

vi.mock("@/lib/learner/courses", () => ({
  getLearnerDashboard: mocks.getLearnerDashboard,
  mapLearnerCourseSummary: mocks.mapLearnerCourseSummary,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningWorkspacesLearnerWorkspaceModule: class {
      getLearningCoursesWorkspace = mocks.getLearningCoursesWorkspace;
    },
    LearningExperienceSocialDiscussionsModule: class {
      getApiSocialDiscussions = mocks.getApiSocialDiscussion;
    },
    LearningExperienceSocialRepliesModule: class {
      getApiSocialDiscussionsReplies = mocks.getApiSocialDiscussionReplies;
    },
    LearningCertificatesModule: class {
      getApiCertificatesMy = mocks.getApiCertificatesMy;
    },
    ProjectsModule: class {
      getProjectsCreator = mocks.getProjectsCreator;
    },
  },
}));

import {
  getCourseDiscussionThread,
  getCourseLearnerContext,
  getMyCertificates,
  getMyLearnerRecords,
  getMyProjects,
} from "./records";

describe("learner workspace record adapter", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.createServerClient.mockReturnValue({ kind: "learner-client" });
    mocks.getToken.mockResolvedValue("access-token");
    mocks.mapLearnerCourseSummary.mockImplementation((summary) => ({
      id: summary.courseId,
      title: summary.title,
      slug: summary.slug,
      description: "",
      thumbnail: null,
      modules: [],
      overallProgress: summary.progressPercentage ?? 0,
      totalItems: 0,
      completedItems: 0,
      remainingMinutes: 0,
      enrollmentId: summary.enrollmentId,
    }));
  });

  it("loads and maps a course workspace with one API request", async () => {
    mocks.getLearningCoursesWorkspace.mockResolvedValue({
      ok: true,
      data: {
        course: { enrollmentId: "enrollment-1" },
        cohort: {
          cohortId: "cohort-1",
          name: "Evening cohort",
          status: "Active",
        },
        calendar: [
          {
            cohortId: "cohort-1",
            scheduleItemId: "schedule-1",
            title: "Live critique",
            type: "LiveSession",
            status: "Scheduled",
          },
        ],
        assessmentGroups: [
          {
            groupId: "group-1",
            name: "Final project",
            description: "Capstone",
            weightPercent: 4_000,
            order: 2,
          },
        ],
        assessments: [
          {
            assessmentId: "assessment-1",
            groupId: "group-1",
            title: "Playable build",
            type: "Exam",
            maxScore: 10_000,
          },
        ],
        submissions: [
          {
            submissionId: "submission-1",
            assessmentId: "assessment-1",
            enrollmentId: "enrollment-1",
            status: "Graded",
          },
        ],
        discussions: [
          {
            discussionId: "discussion-1",
            title: "Weekly critique",
          },
        ],
        certificates: [
          {
            certificateId: "certificate-1",
            courseId: "course-1",
            verificationUrl: "https://gameguild.gg/verify/certificate-1",
          },
        ],
      },
    });

    await expect(getCourseLearnerContext("course-1")).resolves.toMatchObject({
      assessmentGroups: [
        {
          id: "group-1",
          name: "Final project",
          description: "Capstone",
          weightPercent: 40,
          order: 2,
        },
      ],
      enrollmentId: "enrollment-1",
      cohort: { id: "cohort-1", name: "Evening cohort" },
      calendar: [{ itemId: "schedule-1", title: "Live critique" }],
      assessments: [
        {
          id: "assessment-1",
          type: "Quiz",
          assessmentGroupName: "Final project",
        },
      ],
      submissions: [{ id: "submission-1" }],
      discussions: [{ id: "discussion-1" }],
      certificates: [
        {
          id: "certificate-1",
          verificationUrl: "https://gameguild.gg/verify/certificate-1",
        },
      ],
    });
    expect(mocks.getLearningCoursesWorkspace).toHaveBeenCalledTimes(1);
    expect(mocks.getLearningCoursesWorkspace).toHaveBeenCalledWith("course-1");
  });

  it("returns an empty context without authentication or after an API failure", async () => {
    mocks.getToken.mockResolvedValueOnce(null);
    await expect(getCourseLearnerContext("course-1")).resolves.toMatchObject({
      enrollmentId: null,
      assessments: [],
    });

    mocks.getLearningCoursesWorkspace.mockResolvedValueOnce({ ok: false });
    await expect(getCourseLearnerContext("course-1")).resolves.toMatchObject({
      enrollmentId: null,
      assessments: [],
    });
  });

  it("builds global records from the dashboard without workspace fanout", async () => {
    mocks.getLearnerDashboard.mockResolvedValue({
      courses: [
        {
          courseId: "course-1",
          enrollmentId: "enrollment-1",
          title: "Game Production",
          slug: "game-production",
        },
      ],
      upcoming: [
        {
          courseId: "course-1",
          scheduleItemId: "schedule-1",
          title: "Live critique",
        },
      ],
      deadlines: [],
      grades: [
        {
          courseId: "course-1",
          gradedAssessments: 1,
          totalAssessments: 2,
          percentage: 8_800,
          groups: [
            {
              groupId: "group-1",
              name: "Projects",
              weightPercent: 10_000,
              order: 1,
            },
          ],
          items: [
            {
              assessmentId: "assessment-1",
              groupId: "group-1",
              title: "Playable build",
              type: "Project",
              maxScore: 10_000,
              passingScore: 7_000,
              submissionStatus: "Graded",
              score: 8_800,
              passed: true,
              feedback: "Strong iteration.",
            },
          ],
        },
      ],
      announcements: [],
      certificates: [],
    });

    await expect(getMyLearnerRecords()).resolves.toEqual([
      expect.objectContaining({
        course: expect.objectContaining({ id: "course-1" }),
        context: expect.objectContaining({
          assessmentGroups: [
            expect.objectContaining({ id: "group-1", name: "Projects" }),
          ],
          assessments: [
            expect.objectContaining({
              id: "assessment-1",
              assessmentGroupId: "group-1",
            }),
          ],
          submissions: [
            expect.objectContaining({
              assessmentId: "assessment-1",
              score: 88,
              feedback: "Strong iteration.",
            }),
          ],
          gradeSummary: expect.objectContaining({ percentage: 88 }),
        }),
      }),
    ]);
    expect(mocks.getLearningCoursesWorkspace).not.toHaveBeenCalled();
  });

  it("loads a discussion and its replies as one learner thread", async () => {
    mocks.getApiSocialDiscussion.mockResolvedValue({
      ok: true,
      data: { id: "discussion-1", title: "Testing approach" },
    });
    mocks.getApiSocialDiscussionReplies.mockResolvedValue({
      ok: true,
      data: [{ id: "reply-1", content: "Start with onboarding." }],
    });

    await expect(getCourseDiscussionThread("discussion-1")).resolves.toEqual({
      discussion: { id: "discussion-1", title: "Testing approach" },
      replies: [{ id: "reply-1", content: "Start with onboarding." }],
    });
    expect(mocks.getApiSocialDiscussionReplies).toHaveBeenCalledWith(
      "discussion-1",
      { take: 200 },
    );
  });

  it("keeps certificate and project helpers authenticated", async () => {
    mocks.getApiCertificatesMy.mockResolvedValue({
      ok: true,
      data: [{ id: "certificate-1" }],
    });
    mocks.getProjectsCreator.mockResolvedValue({
      ok: true,
      data: [{ id: "project-1" }],
    });

    await expect(getMyCertificates()).resolves.toEqual([
      { id: "certificate-1" },
    ]);
    await expect(getMyProjects("user-1")).resolves.toEqual([
      { id: "project-1" },
    ]);
    expect(mocks.getProjectsCreator).toHaveBeenCalledWith("user-1", {
      take: 100,
    });
  });
});
