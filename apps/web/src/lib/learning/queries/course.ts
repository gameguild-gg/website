import { auth, getToken } from "@/auth";
import { readContentGradingDefinition } from "@game-guild/grading";
import {
  createServerClient,
  hasRole,
  GeneratedApi,
  type ContentStatus,
  type ContentVisibility,
  type LearningCoursesProgram,
  type LearningCoursesProgramContent,
  type LearningCoursesProgramContentType,
  type LearningCoursesLessonContentFormat,
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
import {
  optionalPercentUnitsToPercentage,
  percentUnitsToPercentage,
} from "@/lib/learning/academic-values";

// Re-export generated types for consumers
export type {
  LearningCoursesProgram,
  LearningCoursesProgramContent,
  LearningCoursesProgramContentType,
};

function getApiClient(tenantId?: string) {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080";
  return createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
    // Sends X-Tenant-Id so the authorization API can resolve the actor tenant.
    ...(tenantId ? { tenant: { getTenantId: async () => tenantId } } : {}),
  });
}

function createCourseModules() {
  const client = getApiClient();

  return {
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    content: new GeneratedApi.LearningCoursesProgramContentModule(client),
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

function normalizeLessonContentFormat(
  format: string | null | undefined,
): LearningCoursesLessonContentFormat | null {
  switch (format) {
    case "Markdown":
    case "Lexical":
    case "RevealJs":
    case "Video":
    case "Html":
    case "ExternalLink":
      return format;
    case null:
    case undefined:
      return null;
    default:
      return "Markdown";
  }
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
    const result = await users.getUsersForGetUsersByUserId(creatorId);
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
    passingScore: optionalPercentUnitsToPercentage(dto.passingScore),
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
    const result = await programs.getCoursesForGetCoursesById(courseId);
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
 * True when the current viewer manages the course (course creator).
 * GUIDs are compared case-insensitively: the API and the auth session
 * may emit the same id with different casing.
 */
export async function canManageCourse(
  courseIdentifier: string,
): Promise<boolean> {
  try {
    const [course, session] = await Promise.all([
      getCourse(courseIdentifier),
      auth(),
    ]);

    return Boolean(
      course?.creatorId &&
        session?.user?.id &&
        course.creatorId.toLowerCase() === session.user.id.toLowerCase(),
    );
  } catch {
    return false;
  }
}

/**
 * True when the current viewer may edit the course: the instructor/course
 * owner (creator), a SystemAdmin, or a viewer holding the
 * `Program.{courseId}.Edit` permission in the 3-layer DAC (platform role →
 * tenant grants → ProgramPermissions).
 *
 * Mirrors the write-side gates exactly: AuthorizationBehavior and
 * ProgramContentController both short-circuit for SystemAdmin before the
 * DAC resolution, and treat the creator as manager; the has-permission
 * endpoint only resolves the DAC layers, so the role and creator checks
 * must run alongside it.
 */
export async function canEditCourse(
  courseIdentifier: string,
): Promise<boolean> {
  try {
    const [session, courseId] = await Promise.all([
      auth(),
      resolveCourseId(courseIdentifier),
    ]);

    if (!session?.user?.id) return false;

    if (hasRole(session, "SystemAdmin")) return true;

    if (session.tenantId) {
      const accessControl = new GeneratedApi.AccessControlResourcePermissionsModule(
        getApiClient(session.tenantId),
      );
      const result = await accessControl.getAuthorizationResourcesHasPermission(
        "Program",
        courseId,
        { tenantId: session.tenantId, permission: "Edit" },
      );
      if (result.ok && result.data.hasPermission) return true;
    }

    return canManageCourse(courseIdentifier);
  } catch {
    return false;
  }
}

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
function readDtoGradingConfig(dto: LearningCoursesProgramContent) {
  return readContentGradingDefinition(dto.jsonBody ?? null);
}

function mapContentDto(
  dto: LearningCoursesProgramContent,
): CourseContentItemViewModel {
  const gradingConfig = readDtoGradingConfig(dto);
  const version = (dto as LearningCoursesProgramContent & { version?: number }).version;

  return {
    id: dto.id!,
    version: version ?? 0,
    slug: dto.slug ?? dto.id!,
    parentId: dto.parentId ?? null,
    order: dto.sortOrder ?? 0,
    type: dto.type ?? "Lesson",
    title: dto.title ?? "",
    description: dto.description ?? null,
    status: dto.visibility === "Public" ? "published" : "draft",
    visibility: dto.visibility ?? "Public",
    duration: dto.estimatedMinutes ?? null,
    estimatedMinutesSource: (dto.estimatedMinutesSource as "Auto" | "Manual" | null) ?? null,
    metadata: {},
    gradingConfig,
    createdAt: dto.createdAt ?? new Date().toISOString(),
    updatedAt: dto.updatedAt ?? dto.createdAt ?? new Date().toISOString(),
  };
}

function mapContentDetailDto(
  dto: LearningCoursesProgramContent,
): CourseContentItemDetailViewModel {
  const gradingConfig = readDtoGradingConfig(dto);

  return {
    ...mapContentDto(dto),
    content: dto.body ?? null,
    jsonBody: dto.jsonBody ?? null,
    settings: {
      isRequired: dto.isRequired,
      gradingConfig,
    },
    lessonFormat: normalizeLessonContentFormat(dto.lessonFormat),
  };
}

function findContentDto(
  dtos: LearningCoursesProgramContent[],
  contentIdOrSlug: string,
): LearningCoursesProgramContent | null {
  for (const dto of dtos) {
    if (dto.id === contentIdOrSlug || dto.slug === contentIdOrSlug) return dto;

    const child = findContentDto(dto.children ?? [], contentIdOrSlug);
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
    contentIdOrSlug: string,
  ): Promise<CourseContentItemDetailViewModel | null> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const { content } = createCourseModules();

      if (isGuid(contentIdOrSlug)) {
        const result = await content.getCoursesContentById(
          resolvedCourseId,
          contentIdOrSlug,
        );
        if (result.ok) return mapContentDetailDto(result.data);
      }

      const courseContent = await content.getCoursesContent(resolvedCourseId);
      const fallbackDto = courseContent.ok
        ? findContentDto(courseContent.data, contentIdOrSlug)
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
              const userResult = await users.getUsersForGetUsersByUserId(dto.userId);
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
            progress: Math.round(
              percentUnitsToPercentage(dto.completionPercentage ?? 0),
            ),
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
