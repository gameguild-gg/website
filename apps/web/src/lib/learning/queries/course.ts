import { getToken } from "@/auth";
import {
  createServerClient,
  GeneratedApi,
  type ContentStatus,
  type ContentVisibility,
  type LearningCoursesProgram,
  type LearningCoursesProgramContent,
  type LearningCoursesProgramContentType,
} from "@game-guild/client";
import { cache } from "react";

// Dashboard view models are explicitly derived from generated DTOs so client components do not import server-only modules.
export type {
  CourseContentItemViewModel,
  CourseContentItemDetailViewModel,
  CourseAnalyticsViewModel,
  CourseContentViewModel,
  CourseDeliveryMode,
  CourseViewModel,
  CourseFeaturesViewModel,
  CoursePricingModel,
  CourseStudentsViewModel,
} from "@/lib/learning/view-models";

import type {
  CourseContentItemViewModel,
  CourseContentItemDetailViewModel,
  CourseAnalyticsViewModel,
  CourseContentViewModel,
  CourseViewModel,
  CourseStudentsViewModel,
} from "@/lib/learning/view-models";
import {
  getCourseLookupSlug,
  slugifyRoutePart,
} from "@/lib/learning/course-route";

// Re-export generated types for consumers
export type {
  LearningCoursesProgram,
  LearningCoursesProgramContent,
  LearningCoursesProgramContentType,
};

function getApiClient() {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080";
  return createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });
}

function createCourseModules() {
  const client = getApiClient();

  return {
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    content: new GeneratedApi.LearningCoursesProgramcontentModule(client),
    users: new GeneratedApi.UsersModule(client),
  };
}

function emptyCourseAnalytics(): CourseAnalyticsViewModel {
  return {
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
  };
}

// Compatibility endpoints may serialize enums as either names or numeric values.
function mapStatus(
  s: ContentStatus | number | string | undefined,
): "draft" | "published" | "archived" {
  if (s === 2 || s === "2" || s === "Published" || s === "published")
    return "published";
  if (s === 3 || s === "3" || s === "Archived" || s === "archived")
    return "archived";
  return "draft";
}

// Map ContentVisibility string union to simplified frontend visibility
function mapVisibility(
  v: ContentVisibility | undefined,
): "public" | "private" | "unlisted" {
  if (v === "Public") return "public";
  return "private";
}

type LegacyProgramContentType =
  LearningCoursesProgramContentType | "Page" | "Challenge";

function normalizeProgramContentType(
  type: LegacyProgramContentType | undefined,
): LearningCoursesProgramContentType {
  if (type === "Page") return "Lesson";
  if (type === "Challenge") return "Assignment";
  return type ?? "Lesson";
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value.trim(),
  );
}

async function resolveCreatorHandle(
  creatorId: string | null | undefined,
): Promise<string | null> {
  if (!creatorId) return null;

  const fallback = slugifyRoutePart(creatorId).slice(0, 12) || null;
  const { users } = createCourseModules();

  try {
    const result = await users.getUsers1(creatorId);
    if (!result.ok) return fallback;

    return (
      slugifyRoutePart(result.data.name ?? "") ||
      slugifyRoutePart(result.data.email?.split("@")[0] ?? "") ||
      fallback
    );
  } catch {
    return fallback;
  }
}

function mapProgramDtoToCourseViewModel(
  dto: LearningCoursesProgram,
  creatorHandle: string | null = null,
): CourseViewModel {
  return {
    id: dto.id!,
    creatorId: dto.creatorId ?? null,
    creatorHandle,
    title: dto.title ?? "",
    description: dto.description ?? "",
    metadata: dto.metadata ?? null,
    slug: dto.slug ?? "",
    status: mapStatus(dto.status),
    visibility: mapVisibility(dto.visibility),
    thumbnail: dto.thumbnail ?? null,
    videoShowcaseUrl: dto.videoShowcaseUrl ?? null,
    estimatedHours: dto.estimatedHours ?? null,
    category: dto.category ?? "GeneralEducation",
    difficulty: dto.difficulty ?? "Beginner",
    skillsRequired: dto.skillsRequired ?? null,
    skillsProvided: dto.skillsProvided ?? null,
    enrollmentStatus: dto.enrollmentStatus ?? "Open",
    maxEnrollments: dto.maxEnrollments ?? null,
    enrollmentDeadline: dto.enrollmentDeadline ?? null,
    currentEnrollments: dto.currentEnrollments ?? 0,
    averageRating: dto.averageRating ?? 0,
    totalRatings: dto.totalRatings ?? 0,
    isEnrollmentOpen: dto.isEnrollmentOpen ?? true,
    deliveryMode: "on-demand",
    pricingModel: "free",
    features: {
      hasClasses: true,
      hasRecordings: true,
      hasSchedule: true,
      hasOnDemandContent: true,
      hasPricing: true,
      hasCertificate: true,
      hasAssessments: true,
      hasDiscussions: true,
    },
    createdAt: dto.createdAt ?? new Date().toISOString(),
    updatedAt: dto.updatedAt ?? dto.createdAt ?? new Date().toISOString(),
  };
}

async function fetchCourseBySlug(
  slug: string,
): Promise<CourseViewModel | null> {
  try {
    const { programs } = createCourseModules();
    const result = await programs.getCoursesSlug(slug);
    if (!result.ok) return null;

    return mapProgramDtoToCourseViewModel(
      result.data,
      await resolveCreatorHandle(result.data.creatorId),
    );
  } catch {
    return null;
  }
}

async function fetchCourseById(
  courseId: string,
): Promise<CourseViewModel | null> {
  try {
    const { programs } = createCourseModules();
    const result = await programs.getCourses1(courseId);
    if (!result.ok) return null;

    return mapProgramDtoToCourseViewModel(
      result.data,
      await resolveCreatorHandle(result.data.creatorId),
    );
  } catch {
    return null;
  }
}

/**
 * Fetch course details from the API by canonical ID or dashboard slug.
 */
export const getCourse = cache(
  async (courseIdentifier: string): Promise<CourseViewModel | null> => {
    const identifier = courseIdentifier.trim();
    if (!identifier) return null;

    if (isGuid(identifier)) {
      return fetchCourseById(identifier);
    }

    const slug = getCourseLookupSlug(identifier);
    return fetchCourseBySlug(slug);
  },
);

export const resolveCourseId = cache(
  async (courseIdentifier: string): Promise<string> => {
    if (isGuid(courseIdentifier)) return courseIdentifier;

    const course = await getCourse(courseIdentifier);
    return course?.id ?? courseIdentifier;
  },
);

/**
 * Fetch course analytics data from the API.
 */
export const getCourseAnalytics = cache(
  async (courseId: string): Promise<CourseAnalyticsViewModel> => {
    const empty = emptyCourseAnalytics();
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const { programs } = createCourseModules();
      const result = await programs.getCoursesAnalytics(resolvedCourseId);

      if (!result.ok) return empty;

      const dto = result.data;
      const totalUsers = Math.max(0, dto.totalUsers ?? 0);
      const completedUsers = Math.max(0, dto.completedUsers ?? 0);
      const completionRate =
        dto.completionRate ??
        (totalUsers > 0 ? (completedUsers / totalUsers) * 100 : 0);

      return {
        totalUsers,
        activeUsers: Math.max(0, dto.activeUsers ?? 0),
        completedUsers,
        completionRate: Math.max(0, Math.min(100, completionRate)),
        averageCompletionTime: dto.averageCompletionTime ?? null,
        totalViews: Math.max(0, dto.totalViews ?? 0),
        lastActivity: dto.lastActivity ?? null,
        enrollments: [],
        ratings: [],
        revenue: [],
      };
    } catch {
      return empty;
    }
  },
);

/**
 * Map a LearningCoursesProgramContent DTO to the frontend CourseContentItemViewModel shape.
 */
function mapContentDto(
  dto: LearningCoursesProgramContent,
): CourseContentItemViewModel {
  return {
    id: dto.id!,
    parentId: dto.parentId ?? null,
    order: dto.sortOrder ?? 0,
    type: normalizeProgramContentType(
      dto.type as LegacyProgramContentType | undefined,
    ),
    title: dto.title ?? "",
    description: dto.description ?? null,
    status: dto.visibility === "Public" ? "published" : "draft",
    duration: dto.estimatedMinutes ?? null,
    metadata: {},
    createdAt: dto.createdAt ?? new Date().toISOString(),
    updatedAt: dto.updatedAt ?? dto.createdAt ?? new Date().toISOString(),
  };
}

function serializeContentBody(body: unknown): string | null {
  if (body == null) return null;
  return typeof body === "string" ? body : JSON.stringify(body);
}

function mapContentDetailDto(
  dto: LearningCoursesProgramContent,
): CourseContentItemDetailViewModel {
  return {
    ...mapContentDto(dto),
    content: serializeContentBody(dto.body),
    settings: {
      isRequired: dto.isRequired,
      gradingMethod: dto.gradingMethod ?? null,
      maxPoints: dto.maxPoints ?? null,
    },
  };
}

function findContentDto(
  dtos: LearningCoursesProgramContent[],
  contentId: string,
): LearningCoursesProgramContent | null {
  for (const dto of dtos) {
    if (dto.id === contentId) return dto;

    const child = findContentDto(dto.children ?? [], contentId);
    if (child) return child;
  }

  return null;
}

/**
 * Fetch course content items from the API (flat list for tree rendering).
 */
export const getCourseContent = cache(
  async (courseId: string): Promise<CourseContentViewModel> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const { content } = createCourseModules();
      const result = await content.getCoursesContent(resolvedCourseId);

      if (!result.ok) return { items: [], total: 0 };

      const items: CourseContentItemViewModel[] = [];
      const seenIds = new Set<string>();
      const visit = (dto: LearningCoursesProgramContent) => {
        if (dto.id && !seenIds.has(dto.id)) {
          seenIds.add(dto.id);
          items.push(mapContentDto(dto));
        }

        for (const child of dto.children ?? []) visit(child);
      };

      for (const dto of result.data) {
        visit(dto);
      }

      return { items, total: items.length };
    } catch {
      return { items: [], total: 0 };
    }
  },
);

/**
 * Fetch single content item detail.
 */
export const getContentItem = cache(
  async (
    courseId: string,
    contentId: string,
  ): Promise<CourseContentItemDetailViewModel | null> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const { content } = createCourseModules();
      const result = await content.getCoursesContent1(
        resolvedCourseId,
        contentId,
      );

      if (result.ok) return mapContentDetailDto(result.data);

      const courseContent = await content.getCoursesContent(resolvedCourseId);
      const fallbackDto = courseContent.ok
        ? findContentDto(courseContent.data, contentId)
        : null;
      return fallbackDto ? mapContentDetailDto(fallbackDto) : null;
    } catch {
      return null;
    }
  },
);

/**
 * Fetch course students from the API.
 */
export const getCourseStudents = cache(
  async (courseId: string): Promise<CourseStudentsViewModel> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const { programs } = createCourseModules();
      const { users } = createCourseModules();
      const result = await programs.getCoursesUsers(resolvedCourseId, {
        take: 200,
      });

      if (!result.ok) return { students: [], total: 0 };

      const students = await Promise.all(
        result.data.map(async (dto, i) => {
          const userId = dto.userId ?? `user-${i}`;
          let identity: { name?: string | null; email?: string | null } | null =
            null;

          if (dto.userId) {
            try {
              const userResult = await users.getUsers1(dto.userId);
              if (userResult.ok) identity = userResult.data;
            } catch {
              // The roster remains usable if an individual identity lookup fails.
            }
          }

          return {
            id: dto.enrollmentId ?? userId,
            userId,
            name:
              identity?.name?.trim() ||
              identity?.email?.split("@")[0] ||
              `Student ${i + 1}`,
            email: identity?.email ?? "",
            enrolledAt: dto.startedAt ?? new Date().toISOString(),
            progress: Math.round(dto.completionPercentage ?? 0),
            completedAt: dto.completedAt ?? null,
            lastActivity:
              dto.lastAccessedAt ?? dto.startedAt ?? new Date().toISOString(),
          };
        }),
      );

      return { students, total: students.length };
    } catch {
      return { students: [], total: 0 };
    }
  },
);
