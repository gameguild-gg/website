import {
  createServerClient,
  GeneratedApi,
  type LearningCoursesContentProgress,
  type LearningCoursesProgramContent,
  type LearningCoursesProgressStatus,
  type LearningWorkspacesLearnerCourseSummary,
  type LearningWorkspacesLearnerDashboard,
} from "@game-guild/client";
import { collectHiddenContentIds, flattenUniqueContent } from "@/lib/learner/content-tree";
import { percentUnitsToPercentage } from "@/lib/learning/academic-values";
import { unstable_rethrow } from "next/navigation";

export interface LearningCourseSummary {
  id: string;
  title: string;
  slug: string;
  description: string;
  thumbnail: string | null;
  category: string;
  difficulty: string;
  estimatedHours: number | null;
  currentEnrollments: number;
  averageRating: number;
  isEnrollmentOpen: boolean;
}

export interface CourseAttendanceItem {
  id: string;
  slug?: string;
  title: string;
  type: "lesson" | "activity" | "quiz" | "assignment" | "peer-review";
  status: "locked" | "available" | "in-progress" | "completed";
  duration?: number;
  description?: string;
  order: number;
  isRequired: boolean;
  content?: unknown;
  contentType?: LearningCoursesProgramContent["type"];
  lessonFormat?: LearningCoursesProgramContent["lessonFormat"];
  activitySettings?: LearningCoursesProgramContent["activitySettings"];
}

export interface CourseAttendanceModule {
  id: string;
  title: string;
  description: string;
  order: number;
  items: CourseAttendanceItem[];
  progress: number;
}

export type CourseAccessState =
  | { kind: "ready"; course: CourseAttendanceData }
  | { kind: "enrollment-required"; course: LearningCourseSummary }
  | {
      kind: "payment-required";
      course: LearningCourseSummary;
      price: number | null;
      currency: string;
    }
  | { kind: "enrollment-closed"; course: LearningCourseSummary }
  | { kind: "unavailable"; course?: LearningCourseSummary; message: string }
  | { kind: "not-found" };

export interface CourseAttendanceData {
  id: string;
  title: string;
  slug: string;
  description: string;
  thumbnail: string | null;
  modules: CourseAttendanceModule[];
  overallProgress: number;
  totalItems: number;
  completedItems: number;
  currentItem?: CourseAttendanceItem;
  remainingMinutes: number;
  enrollmentId?: string;
}

interface CourseAttendanceModuleSource {
  id: string;
  title: string;
  description: string;
  order: number;
  items: Array<LearningCoursesProgramContent & { id: string }>;
}

type ProgressItemStatus = "not-started" | "in-progress" | "completed";

async function getOptionalToken(): Promise<string | null> {
  const { getToken } = await import("@/auth");
  return getToken();
}

function getApiClient(getAccessToken?: () => Promise<string | null>) {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    "http://localhost:8080";

  return createServerClient({
    baseUrl: apiUrl,
    auth: getAccessToken ? { getAccessToken } : undefined,
  });
}

function createCourseModules(getAccessToken?: () => Promise<string | null>) {
  const client = getApiClient(getAccessToken);

  return {
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    content: new GeneratedApi.LearningCoursesProgramContentModule(client),
    workspaces: new GeneratedApi.LearningWorkspacesLearnerWorkspaceModule(
      client,
    ),
  };
}

function mapCourse(
  program: GeneratedApi.LearningCoursesProgram,
): LearningCourseSummary {
  return {
    id: program.id ?? "",
    title: program.title ?? "Untitled course",
    slug: program.slug ?? "",
    description: program.description ?? "",
    thumbnail: program.thumbnail ?? null,
    category: String(program.category ?? "General"),
    difficulty: String(program.difficulty ?? "Beginner"),
    estimatedHours: program.estimatedHours ?? null,
    currentEnrollments: program.currentEnrollments ?? 0,
    averageRating: program.averageRating ?? 0,
    isEnrollmentOpen: program.isEnrollmentOpen ?? false,
  };
}

function mapWorkspaceItemType(
  type?: string | null,
): CourseAttendanceItem["type"] {
  switch (type) {
    case "Assignment":
      return "assignment";
    case "Questionnaire":
    case "Quiz":
      return "quiz";
    case "Discussion":
      return "peer-review";
    case "Code":
    case "Project":
    case "Reflection":
    case "Survey":
      return "activity";
    default:
      return "lesson";
  }
}

export function mapLearnerCourseSummary(
  course: LearningWorkspacesLearnerCourseSummary,
): CourseAttendanceData {
  const progress = Math.max(
    0,
    Math.min(
      100,
      Math.round(percentUnitsToPercentage(course.progressPercentage ?? 0)),
    ),
  );
  const currentItem = course.currentContentId
    ? {
        id: course.currentContentId,
        title: course.currentContentTitle || "Continue course",
        type: mapWorkspaceItemType(course.currentContentType),
        status:
          progress > 0 ? ("in-progress" as const) : ("available" as const),
        order: 0,
        isRequired: true,
      }
    : undefined;

  return {
    id: course.courseId ?? "",
    title: course.title ?? "Untitled course",
    slug: course.slug ?? "",
    description: course.description ?? "",
    thumbnail: course.thumbnail ?? null,
    modules: [],
    overallProgress: progress,
    totalItems: course.totalItems ?? 0,
    completedItems: course.completedItems ?? 0,
    currentItem,
    remainingMinutes: course.remainingMinutes ?? 0,
    enrollmentId: course.enrollmentId,
  };
}

function getContentProgressMap(
  contentProgress: LearningCoursesContentProgress[] | null | undefined,
) {
  return new Map(
    (contentProgress ?? [])
      .filter(
        (
          entry,
        ): entry is LearningCoursesContentProgress & { contentId: string } =>
          Boolean(entry.contentId),
      )
      .map((entry) => [entry.contentId, entry]),
  );
}

function mapProgressStatus(
  status?: LearningCoursesProgressStatus,
): ProgressItemStatus {
  switch (status) {
    case "Completed":
    case "Submitted":
      return "completed";
    case "InProgress":
      return "in-progress";
    case "NotStarted":
    default:
      return "not-started";
  }
}

function mapAttendanceStatus(
  progressStatus: ProgressItemStatus | undefined,
  unlocked: boolean,
): CourseAttendanceItem["status"] {
  if (progressStatus === "completed") {
    return "completed";
  }

  if (progressStatus === "in-progress") {
    return "in-progress";
  }

  return unlocked ? "available" : "locked";
}

function mapItemType(
  type: LearningCoursesProgramContent["type"],
): CourseAttendanceItem["type"] {
  switch (type) {
    case "Assignment":
      return "assignment";
    case "Questionnaire":
      return "quiz";
    case "Discussion":
      return "peer-review";
    case "Code":
    case "Project":
    case "Reflection":
    case "Survey":
      return "activity";
    case "Lesson":
    default:
      return "lesson";
  }
}

export async function getPublicCourses(): Promise<LearningCourseSummary[]> {
  try {
    const { programs } = createCourseModules();
    const result = await programs.getCoursesPublic();

    if (!result.ok || !Array.isArray(result.data)) {
      return [];
    }

    return result.data.map(mapCourse);
  } catch (error) {
    console.error("[learning] Failed to fetch public courses", error);
    return [];
  }
}

export async function getPublicCourseBySlug(
  slug: string,
): Promise<LearningCourseSummary | null> {
  try {
    const { programs } = createCourseModules();
    const result = await programs.getCoursesSlug(encodeURIComponent(slug));

    if (!result.ok) {
      return null;
    }

    return mapCourse(result.data);
  } catch (error) {
    console.error("[learning] Failed to fetch course by slug", error);
    return null;
  }
}

export async function getCourseAttendanceData(
  courseSlug: string,
  options?: { includeProgress?: boolean },
): Promise<CourseAttendanceData | null> {
  try {
    const includeProgress = options?.includeProgress ?? false;
    const token = includeProgress ? await getOptionalToken() : null;

    if (includeProgress && !token) {
      return null;
    }

    const publicModules = createCourseModules();
    const authenticatedModules = token
      ? createCourseModules(async () => token)
      : null;
    const programs = authenticatedModules?.programs ?? publicModules.programs;
    const content = authenticatedModules?.content ?? publicModules.content;
    const courseResult = await programs.getCoursesSlug(
      encodeURIComponent(courseSlug),
    );

    if (!courseResult.ok || !courseResult.data.id) {
      return null;
    }

    const course = mapCourse(courseResult.data);
    const courseId = course.id;
    const [contentResult, progressResult] = await Promise.all([
      content.getCoursesContent(courseId),
      authenticatedModules?.programs
        ? authenticatedModules.programs.getCoursesMeProgress(courseId)
        : Promise.resolve(undefined),
    ]);

    if (includeProgress && (!progressResult || !progressResult.ok)) {
      return null;
    }

    if (!contentResult.ok) {
      if (includeProgress) {
        return null;
      }

      return {
        ...course,
        modules: [],
        overallProgress: 0,
        totalItems: 0,
        completedItems: 0,
        remainingMinutes: 0,
      };
    }

    const hiddenContentIds = collectHiddenContentIds(contentResult.data);
    const flatContent = flattenUniqueContent(contentResult.data)
      .filter(
        (item): item is LearningCoursesProgramContent & { id: string } =>
          item.id !== undefined && !hiddenContentIds.has(item.id),
      );
    const progress =
      progressResult && progressResult.ok ? progressResult.data : undefined;
    const progressByContentId = getContentProgressMap(
      progress?.contentProgress,
    );
    const topLevelContent = flatContent
      .filter((item) => !item.parentId)
      .sort((left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0));
    const hasNestedContent = flatContent.some((item) => Boolean(item.parentId));

    const moduleSources: CourseAttendanceModuleSource[] = hasNestedContent
      ? topLevelContent.map((module) => ({
          id: module.id,
          title: module.title ?? "Untitled module",
          description: module.description ?? "",
          order: module.sortOrder ?? 0,
          items: flatContent
            .filter((item) => item.parentId === module.id)
            .sort(
              (left, right) => (left.sortOrder ?? 0) - (right.sortOrder ?? 0),
            ),
        }))
      : [
          {
            id: `${courseId}-content`,
            title: "Course Content",
            description: course.description,
            order: 0,
            items: topLevelContent,
          },
        ];

    let nextUnlockedAssigned = false;
    const modules = moduleSources.map((module) => {
      const items = module.items.map((item) => {
        const itemProgress = progressByContentId.get(item.id);
        const progressStatus = mapProgressStatus(itemProgress?.status);
        const unlocked = !nextUnlockedAssigned;
        const status = mapAttendanceStatus(progressStatus, unlocked);

        if (status === "available" || status === "in-progress") {
          nextUnlockedAssigned = true;
        }

        return {
          id: item.id,
          slug: item.slug ?? undefined,
          title: item.title ?? "Untitled content",
          type: mapItemType(item.type),
          status,
          duration: item.estimatedMinutes ?? undefined,
          description: item.description ?? undefined,
          order: item.sortOrder ?? 0,
          isRequired: item.isRequired ?? false,
          content: item.jsonBody ?? item.body ?? undefined,
          contentType: item.type,
          lessonFormat: item.lessonFormat,
          activitySettings: item.activitySettings,
        } satisfies CourseAttendanceItem;
      });

      const completedItems = items.filter(
        (item) => item.status === "completed",
      ).length;

      return {
        id: module.id,
        title: module.title,
        description: module.description,
        order: module.order,
        items,
        progress:
          items.length > 0
            ? Math.round((completedItems / items.length) * 100)
            : 0,
      } satisfies CourseAttendanceModule;
    });

    const allItems = modules.flatMap((module) => module.items);
    const completedItems = allItems.filter(
      (item) => item.status === "completed",
    ).length;
    const remainingMinutes = allItems
      .filter((item) => item.status !== "completed")
      .reduce((total, item) => total + (item.duration ?? 0), 0);
    const currentItem = allItems.find(
      (item) => item.status === "in-progress" || item.status === "available",
    );

    return {
      ...course,
      modules,
      overallProgress:
        allItems.length > 0
          ? Math.round((completedItems / allItems.length) * 100)
          : 0,
      totalItems: allItems.length,
      completedItems,
      currentItem,
      remainingMinutes,
      enrollmentId: progress?.enrollmentId,
    };
  } catch (error) {
    console.error("[learning] Failed to build course attendance data", error);
    return null;
  }
}

function getApiErrorStatus(error: unknown): number | undefined {
  return (error as { status?: number } | undefined)?.status;
}

export async function getCourseAccessData(
  courseSlug: string,
): Promise<CourseAccessState> {
  try {
    const publicModules = createCourseModules();
    const courseResult = await publicModules.programs.getCoursesSlug(
      encodeURIComponent(courseSlug),
    );

    if (!courseResult.ok || !courseResult.data.id) {
      return getApiErrorStatus(
        courseResult.ok ? undefined : courseResult.error,
      ) === 404
        ? { kind: "not-found" }
        : {
            kind: "unavailable",
            message: "Course access could not be verified. Try again.",
          };
    }

    const course = mapCourse(courseResult.data);
    const token = await getOptionalToken();
    if (!token) {
      return {
        kind: "unavailable",
        course,
        message: "Your session expired. Sign in again to continue.",
      };
    }

    const authenticatedModules = createCourseModules(async () => token);
    const progressResult =
      await authenticatedModules.programs.getCoursesMeProgress(course.id);

    if (progressResult.ok) {
      const attendance = await getCourseAttendanceData(courseSlug, {
        includeProgress: true,
      });
      return attendance
        ? { kind: "ready", course: attendance }
        : {
            kind: "unavailable",
            course,
            message: "The classroom is temporarily unavailable.",
          };
    }

    if (getApiErrorStatus(progressResult.error) !== 404) {
      return {
        kind: "unavailable",
        course,
        message: "Your enrollment could not be verified. Try again.",
      };
    }

    if (!course.isEnrollmentOpen) {
      return { kind: "enrollment-closed", course };
    }

    const productsResult = await publicModules.programs.getCoursesProducts(
      course.id,
    );
    const productIds = productsResult.ok
      ? productsResult.data.filter((productId): productId is string =>
          Boolean(productId),
        )
      : [];

    if (productIds.length === 0) {
      return { kind: "enrollment-required", course };
    }

    const pricingResult = await authenticatedModules.programs.getCoursesPricing(
      course.id,
    );
    const pricing = pricingResult.ok ? pricingResult.data : undefined;

    return {
      kind: "payment-required",
      course,
      price: pricing?.price ?? null,
      currency: pricing?.currency ?? "USD",
    };
  } catch (error) {
    console.error("[learning] Failed to resolve course access", error);
    return {
      kind: "unavailable",
      message: "Course access could not be verified. Try again.",
    };
  }
}

export async function getLearnerDashboard(): Promise<LearningWorkspacesLearnerDashboard | null> {
  const token = await getOptionalToken();
  if (!token) return null;

  const { workspaces } = createCourseModules(async () => token);
  const result = await workspaces.getLearningMeDashboard();
  if (!result.ok) {
    console.error("[learning] Failed to fetch learner dashboard", result.error);
    return null;
  }

  return result.data;
}

export async function getMyLearningCourses(): Promise<CourseAttendanceData[]> {
  try {
    const dashboard = await getLearnerDashboard();
    return (dashboard?.courses ?? [])
      .map(mapLearnerCourseSummary)
      .filter((course) => Boolean(course.id && course.slug));
  } catch (error) {
    unstable_rethrow(error);
    console.error("[learning] Failed to build learner dashboard", error);
    return [];
  }
}
