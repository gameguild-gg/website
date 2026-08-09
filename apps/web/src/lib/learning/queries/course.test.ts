import { beforeEach, describe, expect, it, vi } from "vitest";

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  clientRequest: vi.fn(),
  getCourses1: vi.fn(),
  getCoursesSlug: vi.fn(),
  getCoursesAnalytics: vi.fn(),
  getCoursesUsers: vi.fn(),
  getCoursesContent: vi.fn(),
  getCoursesContent1: vi.fn(),
  getUsers1: vi.fn(),
  getApiLearningEnrollmentsCourses: vi.fn(),
  getToken: vi.fn(),
}));

vi.mock("@/auth", () => ({
  getToken: mocks.getToken,
}));

vi.mock("@game-guild/client", () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningCoursesProgramModule: class {
      getCourses1 = mocks.getCourses1;
      getCoursesSlug = mocks.getCoursesSlug;
      getCoursesAnalytics = mocks.getCoursesAnalytics;
      getCoursesUsers = mocks.getCoursesUsers;
    },
    LearningCoursesProgramcontentModule: class {
      getCoursesContent = mocks.getCoursesContent;
      getCoursesContent1 = mocks.getCoursesContent1;
    },
    LearningEnrollmentsModule: class {
      getApiLearningEnrollmentsCourses = mocks.getApiLearningEnrollmentsCourses;
    },
    UsersModule: class {
      getUsers1 = mocks.getUsers1;
    },
  },
}));

import {
  getCourse,
  getCourseAnalytics,
  getCourseContent,
  getCourseStudents,
  resolveCourseId,
} from "./course";

describe("course analytics query", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mocks.createServerClient.mockReturnValue({});
    mocks.clientRequest.mockReset();
    mocks.getCourses1.mockReset();
    mocks.getCoursesSlug.mockReset();
    mocks.getToken.mockResolvedValue("access-token");
    mocks.getCoursesSlug.mockReset();
    mocks.getApiLearningEnrollmentsCourses.mockReset();
    mocks.getCoursesUsers.mockReset();
    mocks.getCoursesContent.mockReset();
    mocks.getCoursesContent1.mockReset();
    mocks.getUsers1.mockReset();
  });

  it("resolves dashboard course slugs through the authenticated slug endpoint", async () => {
    mocks.getCoursesSlug.mockResolvedValue({
      ok: true,
      data: {
        id: "08691da8-245e-4d9e-b729-83c9023ba061",
        title: "AI for Boss Encounters",
        description: "Build readable encounter AI.",
        slug: "ai-for-boss-encounters",
        status: "Published",
        visibility: "Public",
        createdAt: "2026-06-01T00:00:00.000Z",
      },
    });

    const course = await getCourse("ai-for-boss-encounters");

    expect(mocks.getCoursesSlug).toHaveBeenCalledWith("ai-for-boss-encounters");
    expect(course).toMatchObject({
      id: "08691da8-245e-4d9e-b729-83c9023ba061",
      slug: "ai-for-boss-encounters",
      title: "AI for Boss Encounters",
      status: "published",
      visibility: "public",
    });
  });

  it("resolves slug-by-author dashboard params through the clean API slug", async () => {
    mocks.getCoursesSlug.mockResolvedValue({
      ok: true,
      data: {
        id: "18691da8-245e-4d9e-b729-83c9023ba062",
        title: "AI for Boss Encounters",
        description: "Build readable encounter AI.",
        slug: "ai-for-boss-encounters",
        status: "Published",
        visibility: "Public",
        createdAt: "2026-06-01T00:00:00.000Z",
      },
    });

    const course = await getCourse("ai-for-boss-encounters-by-ada-lovelace");

    expect(mocks.getCoursesSlug).toHaveBeenCalledWith("ai-for-boss-encounters");
    expect(course?.slug).toBe("ai-for-boss-encounters");
  });

  it("does not retry a missing dashboard slug through the UUID endpoint", async () => {
    mocks.getCoursesSlug.mockResolvedValueOnce({
      ok: false,
      error: { status: 404 },
    });

    await expect(
      getCourse("retired-course-by-ada-lovelace"),
    ).resolves.toBeNull();

    expect(mocks.getCoursesSlug).toHaveBeenCalledTimes(1);
    expect(mocks.getCoursesSlug).toHaveBeenCalledWith("retired-course");
  });

  it("keeps legacy UUID route params working through the course ID endpoint", async () => {
    mocks.getCourses1.mockResolvedValue({
      ok: true,
      data: {
        id: "08691da8-245e-4d9e-b729-83c9023ba061",
        title: "AI for Boss Encounters",
        description: "Build readable encounter AI.",
        slug: "ai-for-boss-encounters",
        status: "Published",
        visibility: "Public",
        createdAt: "2026-06-01T00:00:00.000Z",
      },
    });

    const course = await getCourse("08691da8-245e-4d9e-b729-83c9023ba061");

    expect(mocks.getCourses1).toHaveBeenCalledWith(
      "08691da8-245e-4d9e-b729-83c9023ba061",
    );
    expect(mocks.clientRequest).not.toHaveBeenCalled();
    expect(course?.slug).toBe("ai-for-boss-encounters");
  });

  it("loads authenticated course identity through the no-store HTTP query helper", async () => {
    const courseId = "28691da8-245e-4d9e-b729-83c9023ba063";
    mocks.getCourses1.mockResolvedValue({
      ok: true,
      data: {
        id: courseId,
        title: "Immediately Published Course",
        description: "The latest persisted lifecycle state.",
        slug: "immediately-published-course",
        status: "Published",
        visibility: "Public",
        createdAt: "2026-06-01T00:00:00.000Z",
      },
    });

    const course = await getCourse(courseId);

    expect(mocks.getCourses1).toHaveBeenCalledWith(courseId);
    expect(course?.status).toBe("published");
  });

  it("normalizes numeric archived status returned by compatibility endpoints", async () => {
    const courseId = "38691da8-245e-4d9e-b729-83c9023ba064";
    mocks.getCourses1.mockResolvedValue({
      ok: true,
      data: {
        id: courseId,
        title: "Archived Course",
        description: "Preserved for enrolled students.",
        slug: "archived-course",
        status: 3,
        visibility: 1,
        createdAt: "2026-06-01T00:00:00.000Z",
      },
    });

    const course = await getCourse(courseId);

    expect(course?.status).toBe("archived");
  });

  it("returns the canonical API course ID for a dashboard slug", async () => {
    mocks.getCoursesSlug.mockResolvedValue({
      ok: true,
      data: {
        id: "1caa16bb-6810-4e53-bb0d-91f0d5702333",
        title: "Creature Design Production",
        slug: "creature-design-production",
        status: "Draft",
        visibility: "Private",
      },
    });

    await expect(resolveCourseId("creature-design-production")).resolves.toBe(
      "1caa16bb-6810-4e53-bb0d-91f0d5702333",
    );
  });

  it("deduplicates nested content when the API also returns children in the flat collection", async () => {
    const courseId = "08691da8-245e-4d9e-b729-83c9023ba061";
    const child = {
      id: "28691da8-245e-4d9e-b729-83c9023ba063",
      parentId: "18691da8-245e-4d9e-b729-83c9023ba062",
      title: "Lesson",
      type: "Lesson",
      visibility: "Public",
      sortOrder: 0,
      children: [],
    };
    mocks.getCoursesContent.mockResolvedValue({
      ok: true,
      data: [
        {
          id: "18691da8-245e-4d9e-b729-83c9023ba062",
          parentId: null,
          title: "Module",
          type: "Module",
          visibility: "Public",
          sortOrder: 0,
          children: [child],
        },
        child,
      ],
    });

    const content = await getCourseContent(courseId);

    expect(content.items.map((item) => item.id)).toEqual([
      "18691da8-245e-4d9e-b729-83c9023ba062",
      "28691da8-245e-4d9e-b729-83c9023ba063",
    ]);
    expect(content.total).toBe(2);
  });

  it("maps aggregate API analytics without inventing detail rows", async () => {
    mocks.getCoursesAnalytics.mockResolvedValue({
      ok: true,
      data: {
        totalUsers: 18,
        activeUsers: 7,
        completedUsers: 9,
        completionRate: 50,
        averageCompletionTime: "12:30:00",
        totalViews: 245,
        lastActivity: "2026-06-10T12:00:00.000Z",
      },
    });

    const analytics = await getCourseAnalytics("course-analytics-aggregate");

    expect(analytics).toMatchObject({
      totalUsers: 18,
      activeUsers: 7,
      completedUsers: 9,
      completionRate: 50,
      averageCompletionTime: "12:30:00",
      totalViews: 245,
      lastActivity: "2026-06-10T12:00:00.000Z",
    });
    expect(analytics.enrollments).toEqual([]);
    expect(analytics.ratings).toEqual([]);
    expect(analytics.revenue).toEqual([]);
  });

  it("derives completion rate from aggregate counts when the API omits it", async () => {
    mocks.getCoursesAnalytics.mockResolvedValue({
      ok: true,
      data: {
        totalUsers: 10,
        completedUsers: 3,
      },
    });

    const analytics = await getCourseAnalytics("course-analytics-derived-rate");

    expect(analytics.totalUsers).toBe(10);
    expect(analytics.completedUsers).toBe(3);
    expect(analytics.completionRate).toBe(30);
  });

  it("returns empty aggregate analytics when the API request fails", async () => {
    mocks.getCoursesAnalytics.mockResolvedValue({
      ok: false,
      error: { status: 500, message: "failed" },
    });

    await expect(
      getCourseAnalytics("course-analytics-failure"),
    ).resolves.toEqual({
      totalUsers: 0,
      activeUsers: 0,
      completedUsers: 0,
      completionRate: 0,
      averageCompletionTime: null,
      totalViews: 0,
      lastActivity: null,
      enrollments: [],
      ratings: [],
      revenue: [],
    });
  });

  it("loads canonical course enrollments and joins real user identity data", async () => {
    mocks.getCoursesUsers.mockResolvedValue({
      ok: true,
      data: [
        {
          enrollmentId: "enrollment-1",
          courseId: "course-1",
          userId: "user-1",
          completionPercentage: 42.4,
          startedAt: "2026-06-01T00:00:00.000Z",
          lastAccessedAt: "2026-06-10T00:00:00.000Z",
          completedAt: null,
        },
      ],
    });
    mocks.getUsers1.mockResolvedValue({
      ok: true,
      data: { id: "user-1", name: "Ada Learner", email: "ada@example.com" },
    });

    const result = await getCourseStudents("course-1");

    expect(mocks.getCoursesUsers).toHaveBeenCalledWith("course-1", {
      take: 200,
    });
    expect(mocks.getUsers1).toHaveBeenCalledWith("user-1");
    expect(result).toEqual({
      students: [
        {
          id: "enrollment-1",
          userId: "user-1",
          name: "Ada Learner",
          email: "ada@example.com",
          enrolledAt: "2026-06-01T00:00:00.000Z",
          progress: 42,
          completedAt: null,
          lastActivity: "2026-06-10T00:00:00.000Z",
        },
      ],
      total: 1,
    });
  });
});
