"use server";

import { getToken } from "@/auth";
import { getCourseRouteParam } from "@/lib/learning/course-route";
import type {
  AssessmentPresentationMode,
  AssessmentType,
} from "@/lib/learning/queries/assessments";
import type {
  CourseIntegrationSettings,
  CourseNotificationSettings,
} from "@/lib/learning/queries/settings";

import {
  createServerClient,
  GeneratedApi,
  type LearningAssessmentsCreateAssessmentInput,
  type LearningAssessmentsUpdateAssessmentInput,
  type LearningCoursesMonetization,
  type LearningCoursesCloneProgram,
  type LearningCoursesCreateProgram,
  type LearningCoursesCreateProgramContent,
  type LearningCoursesUpdateProgram,
  type LearningCoursesUpdateProgramContent,
  type LearningCoursesProgramContentType,
} from "@game-guild/client";
import { revalidatePath } from "next/cache";

type ActionResult<T> =
  { success: true; data: T } | { success: false; error: string };

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

async function resolveCourseMutationId(courseId: string): Promise<string> {
  const { resolveCourseId } = await import("@/lib/learning/queries/course");
  return resolveCourseId(courseId);
}

function revalidateCoursePath(
  courseId: string,
  resolvedCourseId: string,
  segment = "",
) {
  const suffix = segment ? `/${segment.replace(/^\/+/, "")}` : "";

  revalidatePath(`/dashboard/learning/courses/${courseId}${suffix}`);
  if (resolvedCourseId !== courseId) {
    revalidatePath(`/dashboard/learning/courses/${resolvedCourseId}${suffix}`);
  }
}

function revalidateCourseContentPaths(
  courseId: string,
  resolvedCourseId: string,
) {
  revalidateCoursePath(courseId, resolvedCourseId);
  revalidateCoursePath(courseId, resolvedCourseId, "content");
  revalidateCoursePath(courseId, resolvedCourseId, "overview");
}

function revalidateCourseAssessmentPaths(
  courseId: string,
  resolvedCourseId: string,
) {
  revalidateCoursePath(courseId, resolvedCourseId);
  revalidateCoursePath(courseId, resolvedCourseId, "assessments");
  revalidateCoursePath(courseId, resolvedCourseId, "overview");
}

function createCourseModules() {
  const client = getApiClient();

  return {
    client,
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    content: new GeneratedApi.LearningCoursesProgramcontentModule(client),
    lifecycle: new GeneratedApi.LearningCoursesProgramlifecycleModule(client),
    assessments: new GeneratedApi.LearningAssessmentsModule(client),
    enrollments: new GeneratedApi.LearningEnrollmentsModule(client),
    students: new GeneratedApi.LearningCoursesStudentsModule(client),
    supportTickets: new GeneratedApi.LearningCoursesSupportticketsModule(
      client,
    ),
    certificates: new GeneratedApi.LearningCertificatesModule(client),
    discussions: new GeneratedApi.LearningExperienceSocialDiscussionsModule(
      client,
    ),
    replies: new GeneratedApi.LearningExperienceSocialRepliesModule(client),
    reviews: new GeneratedApi.LearningExperienceSocialReviewsModule(client),
    users: new GeneratedApi.UsersModule(client),
  };
}

function extractError(err: unknown): string {
  const e = err as
    { status?: number; message?: string; detail?: string } | undefined;
  return e?.detail || e?.message || "An unexpected error occurred.";
}

function formatUnexpectedError(err: unknown): string {
  if (err instanceof Error) return err.message;
  if (typeof err === "string") return err;

  try {
    return JSON.stringify(err);
  } catch {
    return String(err);
  }
}

// ── Content actions ──

export interface AddContentInput {
  courseId: string;
  parentId?: string;
  title: string;
  description?: string;
  type: LearningCoursesProgramContentType;
  sortOrder?: number;
}

export async function addContent(
  input: AddContentInput,
): Promise<ActionResult<{ id: string }>> {
  const { courseId, parentId, title, type, description, sortOrder } = input;

  if (!title || title.trim().length < 1) {
    return { success: false, error: "Title is required." };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const contentBody: LearningCoursesCreateProgramContent = {
      programId: resolvedCourseId,
      title: title.trim(),
      description: (description ?? "").trim(),
      type,
      sortOrder: sortOrder ?? 0,
      isRequired: true,
      visibility: "Public",
      ...(parentId ? { parentId } : {}),
    };

    const { content } = createCourseModules();
    const result = await content.postCoursesContent(
      resolvedCourseId,
      contentBody,
    );

    if (result.ok && result.data.id) {
      revalidateCourseContentPaths(courseId, resolvedCourseId);
      return { success: true, data: { id: result.data.id! } };
    }

    return {
      success: false,
      error: result.ok
        ? "Content was created, but the API did not return its ID."
        : extractError(result.error),
    };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${formatUnexpectedError(e)}`,
    };
  }
}

export async function deleteContent(
  courseId: string,
  contentId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { content } = createCourseModules();
    const result = await content.deleteCoursesContent(
      resolvedCourseId,
      contentId,
    );

    if (result.ok) {
      revalidateCourseContentPaths(courseId, resolvedCourseId);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateContentInput {
  courseId: string;
  contentId: string;
  title?: string;
  description?: string;
  type?: LearningCoursesProgramContentType;
  body?: string;
  sortOrder?: number;
  isRequired?: boolean;
  estimatedMinutes?: number;
  visibility?: string;
}

export async function updateContent(
  input: UpdateContentInput,
): Promise<ActionResult<null>> {
  const { courseId, contentId, ...fields } = input;

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const body = {
      id: contentId,
      ...fields,
    } as LearningCoursesUpdateProgramContent;
    const { content } = createCourseModules();
    const result = await content.putCoursesContent(
      resolvedCourseId,
      contentId,
      body,
    );

    if (result.ok) {
      revalidateCourseContentPaths(courseId, resolvedCourseId);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${formatUnexpectedError(e)}`,
    };
  }
}

export async function reorderContent(
  courseId: string,
  contentIds: string[],
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { programs } = createCourseModules();
    const result = await programs.postCoursesContentReorder(resolvedCourseId, {
      contentIds,
    });

    if (result.ok) {
      revalidateCourseContentPaths(courseId, resolvedCourseId);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

// ── Course CRUD actions ──

export interface CreateCourseInput {
  title: string;
  description: string;
  slug: string;
}

export async function createCourse(
  input: CreateCourseInput,
): Promise<ActionResult<{ id: string; slug: string; routeParam: string }>> {
  const { title, description, slug } = input;

  if (!title || title.trim().length < 3) {
    return { success: false, error: "Title must be at least 3 characters." };
  }
  if (!description || description.trim().length < 10) {
    return {
      success: false,
      error: "Description must be at least 10 characters.",
    };
  }
  if (!slug || slug.trim().length < 1) {
    return { success: false, error: "Slug is required." };
  }

  try {
    const { programs } = createCourseModules();
    const result = await programs.postCourses({
      title: title.trim(),
      description: description.trim(),
      slug: slug.trim(),
    } satisfies LearningCoursesCreateProgram);

    if (result.ok) {
      const id = result.data.id!;
      const createdSlug = result.data.slug?.trim() || slug.trim();

      revalidatePath("/dashboard/learning/courses");
      return {
        success: true,
        data: {
          id,
          slug: createdSlug,
          routeParam: getCourseRouteParam({
            id,
            slug: createdSlug,
            creatorId: result.data.creatorId ?? null,
          }),
        },
      };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateCourseInput {
  courseId: string;
  title?: string;
  description?: string;
  metadata?: string | null;
  slug?: string;
  thumbnail?: string;
  videoShowcaseUrl?: string;
  estimatedHours?: number;
  visibility?: string;
  category?: string;
  difficulty?: string;
  skillsRequired?: string;
  skillsProvided?: string;
  enrollmentStatus?: string;
  maxEnrollments?: number | null;
  enrollmentDeadline?: string | null;
}

export async function updateCourse(
  input: UpdateCourseInput,
): Promise<ActionResult<null>> {
  const { courseId, ...fields } = input;
  const updateFields: LearningCoursesUpdateProgram & {
    clearMaxEnrollments?: boolean;
    clearEnrollmentDeadline?: boolean;
  } = { ...fields } as LearningCoursesUpdateProgram;

  if (input.maxEnrollments === null) {
    delete updateFields.maxEnrollments;
    updateFields.clearMaxEnrollments = true;
  }
  if (input.enrollmentDeadline === null) {
    delete updateFields.enrollmentDeadline;
    updateFields.clearEnrollmentDeadline = true;
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { programs } = createCourseModules();
    const result = await programs.putCourses(resolvedCourseId, updateFields);

    if (result.ok) {
      revalidatePath(`/dashboard/learning/courses/${courseId}`);
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function publishCourse(
  courseId: string,
): Promise<ActionResult<null>> {
  try {
    const [{ getCourse, getCourseContent }, { deriveCourseLaunchSummary }] =
      await Promise.all([
        import("@/lib/learning/queries/course"),
        import("@/lib/learning/course-launch"),
      ]);
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const [course, content] = await Promise.all([
      getCourse(courseId),
      getCourseContent(resolvedCourseId),
    ]);

    if (!course) {
      return {
        success: false,
        error: "Course could not be loaded before publishing.",
      };
    }

    const launchSummary = deriveCourseLaunchSummary(course, content);
    if (launchSummary.blockers.length > 0) {
      return {
        success: false,
        error: `Course cannot be published until readiness is complete: ${launchSummary.blockers.join(", ")}.`,
      };
    }

    const { lifecycle } = createCourseModules();
    const result = await lifecycle.postCoursesPublish(resolvedCourseId);

    if (result.ok) {
      revalidateCoursePath(courseId, resolvedCourseId);
      revalidateCoursePath(courseId, resolvedCourseId, "overview");
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function unpublishCourse(
  courseId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { lifecycle } = createCourseModules();
    const result = await lifecycle.postCoursesUnpublish(resolvedCourseId);

    if (result.ok) {
      revalidateCoursePath(courseId, resolvedCourseId);
      revalidateCoursePath(courseId, resolvedCourseId, "overview");
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function restoreCourse(
  courseId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { lifecycle } = createCourseModules();
    const result = await lifecycle.postCoursesRestore(resolvedCourseId);

    if (result.ok) {
      revalidateCoursePath(courseId, resolvedCourseId);
      revalidateCoursePath(courseId, resolvedCourseId, "overview");
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function transferCourseOwnership(
  courseId: string,
  ownerReference: string,
): Promise<ActionResult<null>> {
  const reference = ownerReference.trim();
  if (!reference) {
    return {
      success: false,
      error: "New owner email, name, or user ID is required.",
    };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const resolvedUser = await resolveEnrollmentUserId(reference);
    if (!resolvedUser.success) {
      return resolvedUser;
    }

    const { programs } = createCourseModules();
    const result = await programs.putCourses(resolvedCourseId, {
      creatorId: resolvedUser.data.userId,
    });

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCoursePath(courseId, resolvedCourseId);
    revalidateCoursePath(courseId, resolvedCourseId, "settings/danger");
    revalidatePath("/dashboard/learning/courses");
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function archiveCourse(
  courseId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { lifecycle } = createCourseModules();
    const result = await lifecycle.postCoursesArchive(resolvedCourseId);

    if (result.ok) {
      revalidateCoursePath(courseId, resolvedCourseId);
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function deleteCourse(
  courseId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { programs } = createCourseModules();
    const result = await programs.deleteCourses(resolvedCourseId);

    if (result.ok) {
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface ManualEnrollStudentInput {
  courseId: string;
  userId: string;
  cohortId?: string | null;
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value.trim(),
  );
}

async function resolveEnrollmentUserId(
  reference: string,
): Promise<ActionResult<{ userId: string }>> {
  const value = reference.trim();
  if (isGuid(value)) {
    return { success: true, data: { userId: value } };
  }

  const { users } = createCourseModules();
  const query = value.includes("@")
    ? { email: value, limit: 5 }
    : { q: value, limit: 5 };
  const result = await users.getUsers(query);

  if (!result.ok) {
    return { success: false, error: extractError(result.error) };
  }

  const normalized = value.toLowerCase();
  const matches = result.data.items ?? [];
  const match =
    matches.find(
      (user) =>
        user.email?.toLowerCase() === normalized ||
        user.name?.toLowerCase() === normalized,
    ) ?? matches[0];

  if (!match?.id) {
    return {
      success: false,
      error: "No user matched that email, name, or user ID.",
    };
  }

  return { success: true, data: { userId: match.id } };
}

export async function manualEnrollStudent(
  input: ManualEnrollStudentInput,
): Promise<ActionResult<{ id: string | null }>> {
  const courseId = input.courseId.trim();
  const userReference = input.userId.trim();
  const cohortId = input.cohortId?.trim() || null;

  if (!courseId) {
    return { success: false, error: "Course is required." };
  }

  if (!userReference) {
    return {
      success: false,
      error: "Student email, name, or user ID is required.",
    };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const resolvedUser = await resolveEnrollmentUserId(userReference);
    if (!resolvedUser.success) {
      return resolvedUser;
    }

    const { programs, enrollments } = createCourseModules();
    const rosterResult = await programs.postCoursesUsers(
      resolvedCourseId,
      resolvedUser.data.userId,
    );
    if (!rosterResult.ok) {
      return { success: false, error: extractError(rosterResult.error) };
    }

    if (cohortId) {
      const cohortResult = await enrollments.postApiLearningEnrollments({
        courseId: resolvedCourseId,
        userId: resolvedUser.data.userId,
        cohortId,
      });

      if (!cohortResult.ok) {
        await programs.deleteCoursesUsers(
          resolvedCourseId,
          resolvedUser.data.userId,
        );
        return { success: false, error: extractError(cohortResult.error) };
      }
    }

    revalidateCoursePath(courseId, resolvedCourseId, "students");
    revalidateCoursePath(courseId, resolvedCourseId);
    return {
      success: true,
      data: { id: rosterResult.data.enrollmentId ?? null },
    };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function removeCourseStudents(
  courseId: string,
  userIds: string[],
): Promise<ActionResult<{ removed: number }>> {
  const uniqueUserIds = [
    ...new Set(userIds.map((userId) => userId.trim()).filter(Boolean)),
  ];
  if (uniqueUserIds.length === 0) {
    return { success: false, error: "Select at least one student to remove." };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { programs } = createCourseModules();
    const results = await Promise.all(
      uniqueUserIds.map((userId) =>
        programs.deleteCoursesUsers(resolvedCourseId, userId),
      ),
    );
    const failed = results.find((result) => !result.ok);
    if (failed && !failed.ok)
      return { success: false, error: extractError(failed.error) };

    revalidateCoursePath(courseId, resolvedCourseId, "students");
    revalidateCoursePath(courseId, resolvedCourseId);
    return { success: true, data: { removed: uniqueUserIds.length } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface SendCourseStudentMessageInput {
  courseId: string;
  userIds: string[];
  subject: string;
  message: string;
}

export async function sendCourseStudentMessage(
  input: SendCourseStudentMessageInput,
): Promise<ActionResult<{ sent: number }>> {
  const subject = input.subject.trim();
  const message = input.message.trim();
  const userIds = [
    ...new Set(input.userIds.map((userId) => userId.trim()).filter(Boolean)),
  ];

  if (userIds.length === 0)
    return { success: false, error: "Select at least one student." };
  if (subject.length < 3)
    return { success: false, error: "Subject must be at least 3 characters." };
  if (message.length < 2)
    return { success: false, error: "Message is required." };

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { students } = createCourseModules();
    const result = await students.postCoursesStudentsMessage(resolvedCourseId, {
      userIds,
      subject,
      message,
    });
    return result.ok
      ? { success: true, data: { sent: result.data.sent ?? 0 } }
      : { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

interface LandingFaqInput {
  question: string;
  answer: string;
  category?: string;
}

interface LandingProjectInput {
  title: string;
  summary: string;
  image?: string;
  skills?: string | string[];
  deliverable: string;
  moduleLabel?: string;
}

function parseMetadata(
  raw: string | null | undefined,
): Record<string, unknown> {
  if (!raw) return {};

  try {
    const parsed = JSON.parse(raw) as unknown;
    return parsed && typeof parsed === "object" && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

function normalizeStringList(value: string | string[] | undefined): string[] {
  if (Array.isArray(value)) {
    return value.map((item) => item.trim()).filter(Boolean);
  }

  if (typeof value === "string") {
    return value
      .split(/[,;\n]/)
      .map((item) => item.trim())
      .filter(Boolean);
  }

  return [];
}

type NotificationSettingsInput = Omit<
  CourseNotificationSettings,
  "courseId" | "updatedAt"
>;
type IntegrationSettingsInput = Omit<
  CourseIntegrationSettings,
  "courseId" | "updatedAt"
>;

async function updateCourseMetadataSection(
  courseId: string,
  key: "notificationSettings" | "integrationSettings",
  value: unknown,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { programs } = createCourseModules();
    const courseResult = await programs.getCourses1(resolvedCourseId);
    if (!courseResult.ok)
      return { success: false, error: extractError(courseResult.error) };

    const metadata = parseMetadata(courseResult.data.metadata);
    metadata[key] = value;
    const result = await programs.putCourses(resolvedCourseId, {
      metadata: JSON.stringify(metadata),
    } satisfies LearningCoursesUpdateProgram);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCoursePath(
      courseId,
      resolvedCourseId,
      `settings/${key === "notificationSettings" ? "notifications" : "integrations"}`,
    );
    return { success: true, data: null };
  } catch (error) {
    return {
      success: false,
      error: `Unexpected error: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}

export async function updateCourseNotificationSettings(
  courseId: string,
  input: NotificationSettingsInput,
): Promise<ActionResult<null>> {
  const classReminders = [...new Set(input.studentNotifications.classReminders)]
    .filter(
      (minutes) => Number.isFinite(minutes) && minutes >= 0 && minutes <= 10080,
    )
    .sort((a, b) => b - a);
  const lowRatingThreshold = Math.min(
    5,
    Math.max(1, Math.round(input.instructorNotifications.lowRatingThreshold)),
  );
  const templates = input.templates
    .map((template) => ({
      id: template.id.trim(),
      type: template.type.trim(),
      subject: template.subject.trim(),
      enabled: Boolean(template.enabled),
    }))
    .filter((template) => template.id && template.type && template.subject)
    .slice(0, 20);

  return updateCourseMetadataSection(courseId, "notificationSettings", {
    studentNotifications: {
      ...input.studentNotifications,
      classReminders,
    },
    instructorNotifications: {
      ...input.instructorNotifications,
      lowRatingThreshold,
    },
    templates,
  });
}

export async function updateCourseIntegrationSettings(
  courseId: string,
  input: IntegrationSettingsInput,
): Promise<ActionResult<null>> {
  for (const webhook of input.webhooks) {
    try {
      const url = new URL(webhook.url);
      if (!["http:", "https:"].includes(url.protocol))
        throw new Error("invalid protocol");
    } catch {
      return { success: false, error: "Webhook URLs must use http or https." };
    }
  }

  const integrations = input.integrations
    .slice(0, 20)
    .map((integration) => ({
      ...integration,
      id: integration.id.trim(),
      name: integration.name.trim(),
      enabled: Boolean(integration.enabled),
      status: integration.enabled
        ? integration.status
        : ("disconnected" as const),
    }))
    .filter((integration) => integration.id && integration.name);
  const webhooks = input.webhooks.slice(0, 20).map((webhook) => ({
    id: webhook.id.trim(),
    url: webhook.url.trim(),
    events: [
      ...new Set(webhook.events.map((event) => event.trim()).filter(Boolean)),
    ],
    enabled: Boolean(webhook.enabled),
  }));

  return updateCourseMetadataSection(courseId, "integrationSettings", {
    integrations,
    webhooks,
  });
}

export async function updateCourseReviewModeration(
  courseId: string,
  reviewId: string,
  isApproved: boolean,
  isFeatured: boolean,
): Promise<ActionResult<null>> {
  const { reviews } = createCourseModules();
  const result = await reviews.patchApiSocialReviewsModeration(reviewId, {
    isApproved,
    isFeatured,
  });

  if (!result.ok) return { success: false, error: extractError(result.error) };

  revalidatePath(
    `/dashboard/learning/courses/${courseId}/listing/testimonials`,
  );
  revalidatePath(`/courses/${courseId}`);
  return { success: true, data: null };
}

export async function updateCourseFaq(
  courseId: string,
  items: LandingFaqInput[],
): Promise<ActionResult<null>> {
  const sanitizedItems = items
    .map((item) => ({
      question: item.question.trim(),
      answer: item.answer.trim(),
      category: item.category?.trim() || "Course details",
    }))
    .filter((item) => item.question.length > 0 && item.answer.length > 0)
    .slice(0, 12);

  try {
    const course = await fetchCourse(courseId);
    if (!course) return { success: false, error: "Course not found." };

    const metadata = parseMetadata(course.metadata);
    metadata.landingFaq = sanitizedItems;

    return updateCourse({
      courseId,
      metadata: JSON.stringify(metadata),
    });
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function updateCourseLandingProjects(
  courseId: string,
  items: LandingProjectInput[],
): Promise<ActionResult<null>> {
  const sanitizedItems = items
    .map((item, index) => ({
      title: item.title.trim(),
      summary: item.summary.trim(),
      image: item.image?.trim() || null,
      skills: normalizeStringList(item.skills),
      deliverable: item.deliverable.trim(),
      moduleLabel:
        item.moduleLabel?.trim() ||
        `Project ${String(index + 1).padStart(2, "0")}`,
    }))
    .filter(
      (item) =>
        item.title.length > 0 &&
        item.summary.length > 0 &&
        item.deliverable.length > 0,
    )
    .slice(0, 6);

  try {
    const course = await fetchCourse(courseId);
    if (!course) return { success: false, error: "Course not found." };

    const metadata = parseMetadata(course.metadata);
    metadata.landingProjects = sanitizedItems;

    return updateCourse({
      courseId,
      metadata: JSON.stringify(metadata),
    });
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateCoursePricingInput {
  courseId: string;
  isMonetizationEnabled: boolean;
  price: number;
  currency: string;
  isSubscription: boolean;
  subscriptionDurationDays?: number | null;
}

export async function updateCoursePricing(
  input: UpdateCoursePricingInput,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { programs } = createCourseModules();

    if (!input.isMonetizationEnabled) {
      const result =
        await programs.postCoursesDisableMonetization(resolvedCourseId);

      if (result.ok) {
        revalidatePath(
          `/dashboard/learning/courses/${input.courseId}/listing/pricing`,
        );
        revalidatePath(`/dashboard/learning/courses/${input.courseId}/listing`);
        return { success: true, data: null };
      }

      return { success: false, error: extractError(result.error) };
    }

    if (!Number.isFinite(input.price) || input.price < 0) {
      return { success: false, error: "Price must be zero or greater." };
    }

    const result = await programs.postCoursesMonetize(resolvedCourseId, {
      price: input.price,
      currency: input.currency.trim().toUpperCase() || "USD",
      isSubscription: input.isSubscription,
      subscriptionDurationDays: input.isSubscription
        ? (input.subscriptionDurationDays ?? null)
        : null,
    } satisfies LearningCoursesMonetization);

    if (result.ok) {
      revalidatePath(
        `/dashboard/learning/courses/${input.courseId}/listing/pricing`,
      );
      revalidatePath(`/dashboard/learning/courses/${input.courseId}/listing`);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function cloneCourse(
  courseId: string,
  newTitle: string,
): Promise<ActionResult<{ id: string }>> {
  try {
    const { programs } = createCourseModules();
    const result = await programs.postCoursesClone(courseId, {
      newTitle,
    } satisfies LearningCoursesCloneProgram);

    if (result.ok) {
      revalidatePath("/dashboard/learning/courses");
      return { success: true, data: { id: result.data.id! } };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

// ── Assessment actions ──

export interface CreateAssessmentInput {
  courseId: string;
  title: string;
  description?: string;
  type: AssessmentType;
  assessmentGroupId?: string | null;
  maxScore?: number;
  passingScore?: number;
  timeLimitMinutes?: number;
  maxAttempts?: number;
  isRequired?: boolean;
  availableFrom?: string;
  availableUntil?: string;
  presentationMode?: AssessmentPresentationMode;
}

export async function createAssessment(
  input: CreateAssessmentInput,
): Promise<ActionResult<{ id: string }>> {
  const { courseId, title, ...rest } = input;

  if (!title || title.trim().length < 1) {
    return { success: false, error: "Title is required." };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const body: LearningAssessmentsCreateAssessmentInput & {
      assessmentGroupId?: string | null;
    } = {
      courseId: resolvedCourseId,
      title: title.trim(),
      description: rest.description?.trim() ?? null,
      type: rest.type,
      assessmentGroupId: rest.assessmentGroupId ?? null,
      maxScore: rest.maxScore ?? 100,
      passingScore: rest.passingScore ?? 70,
      timeLimitMinutes: rest.timeLimitMinutes ?? null,
      maxAttempts: rest.maxAttempts ?? null,
      isRequired: rest.isRequired ?? true,
      availableFrom: rest.availableFrom ?? null,
      availableUntil: rest.availableUntil ?? null,
      presentationMode:
        rest.presentationMode ??
        (rest.type === "Quiz" ? "Continuous" : "SingleStep"),
    };

    const { assessments } = createCourseModules();
    const result = await assessments.postAssessments(body);

    if (result.ok) {
      revalidateCourseAssessmentPaths(courseId, resolvedCourseId);
      return { success: true, data: { id: result.data.id! } };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface CreateAssessmentGroupInput {
  courseId: string;
  name: string;
  weightPercent: number;
  order?: number;
  description?: string;
}

export interface UpdateAssessmentGroupInput {
  courseId: string;
  groupId: string;
  name: string;
  weightPercent: number;
  order?: number;
  description?: string | null;
}

function validateAssessmentGroup(
  name: string,
  weightPercent: number,
): string | null {
  if (name.trim().length < 1) {
    return "Group name is required.";
  }

  if (
    !Number.isFinite(weightPercent) ||
    weightPercent < 0 ||
    weightPercent > 100
  ) {
    return "Weight must be between 0 and 100.";
  }

  return null;
}

export async function createAssessmentGroup(
  input: CreateAssessmentGroupInput,
): Promise<ActionResult<{ id: string }>> {
  const name = input.name.trim();

  const validationError = validateAssessmentGroup(name, input.weightPercent);
  if (validationError) {
    return { success: false, error: validationError };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { assessments } = createCourseModules();
    const result = await assessments.postAssessmentsGroups({
      courseId: resolvedCourseId,
      name,
      weightPercent: input.weightPercent,
      order: input.order ?? 0,
      description: input.description?.trim() || null,
    });

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidatePath(`/dashboard/learning/courses/${input.courseId}`);
    revalidatePath(`/dashboard/learning/courses/${input.courseId}/assessments`);
    return { success: true, data: { id: result.data.id! } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function updateAssessmentGroup(
  input: UpdateAssessmentGroupInput,
): Promise<ActionResult<{ id: string }>> {
  const name = input.name.trim();

  const validationError = validateAssessmentGroup(name, input.weightPercent);
  if (validationError) {
    return { success: false, error: validationError };
  }

  if (!input.groupId.trim()) {
    return { success: false, error: "Group id is required." };
  }

  try {
    const { assessments } = createCourseModules();
    const result = await assessments.putAssessmentsGroups(input.groupId, {
      name,
      description: input.description?.trim() || null,
      weightPercent: input.weightPercent,
      order: input.order ?? 0,
    });

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidatePath(`/dashboard/learning/courses/${input.courseId}`);
    revalidatePath(`/dashboard/learning/courses/${input.courseId}/assessments`);
    return { success: true, data: { id: result.data.id! } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function deleteAssessmentGroup(
  courseId: string,
  groupId: string,
): Promise<ActionResult<null>> {
  if (!groupId.trim()) {
    return { success: false, error: "Group id is required." };
  }

  try {
    const { assessments } = createCourseModules();
    const result = await assessments.deleteAssessmentsGroups(groupId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidatePath(`/dashboard/learning/courses/${courseId}`);
    revalidatePath(`/dashboard/learning/courses/${courseId}/assessments`);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateAssessmentInput {
  courseId: string;
  assessmentId: string;
  title?: string;
  description?: string;
  maxScore?: number;
  passingScore?: number;
  timeLimitMinutes?: number | null;
  maxAttempts?: number | null;
  isRequired?: boolean;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  clearContentId?: boolean;
  assessmentGroupId?: string | null;
  clearAssessmentGroupId?: boolean;
  presentationMode?: AssessmentPresentationMode;
}

export async function updateAssessment(
  input: UpdateAssessmentInput,
): Promise<ActionResult<null>> {
  const { courseId, assessmentId, ...fields } = input;

  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const body: LearningAssessmentsUpdateAssessmentInput & {
      assessmentGroupId?: string | null;
      clearAssessmentGroupId?: boolean;
    } = {
      title: fields.title?.trim() ?? null,
      description: fields.description?.trim() ?? null,
      maxScore: fields.maxScore ?? null,
      passingScore: fields.passingScore ?? null,
      timeLimitMinutes: fields.timeLimitMinutes ?? null,
      maxAttempts: fields.maxAttempts ?? null,
      isRequired: fields.isRequired ?? null,
      availableFrom: fields.availableFrom ?? null,
      availableUntil: fields.availableUntil ?? null,
      contentId: fields.contentId ?? null,
      clearContentId: fields.clearContentId ?? false,
      assessmentGroupId: fields.assessmentGroupId ?? null,
      clearAssessmentGroupId: fields.clearAssessmentGroupId ?? false,
      presentationMode: fields.presentationMode,
    };

    const { assessments } = createCourseModules();
    const result = await assessments.putAssessments(assessmentId, body);

    if (result.ok) {
      revalidateCourseAssessmentPaths(courseId, resolvedCourseId);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateAssessmentDefinitionInput {
  courseId: string;
  assessmentId: string;
  definition: unknown;
  definitionSchemaVersion?: number;
}

function normalizeAssessmentDefinitionPayload(definition: unknown): unknown {
  if (typeof definition !== "string") {
    return definition ?? { order: [], blocks: {} };
  }

  const trimmed = definition.trim();
  return trimmed ? JSON.parse(trimmed) : { order: [], blocks: {} };
}

export async function updateAssessmentDefinition(
  input: UpdateAssessmentDefinitionInput,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const definition = normalizeAssessmentDefinitionPayload(input.definition);
    const { assessments } = createCourseModules();
    const result = await assessments.putAssessmentsDefinition(
      input.assessmentId,
      {
        definitionSchemaVersion: input.definitionSchemaVersion ?? 1,
        definition: definition as Record<string, unknown>,
      },
    );

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseAssessmentPaths(input.courseId, resolvedCourseId);
    revalidatePath(
      `/dashboard/learning/courses/${input.courseId}/assessments/${input.assessmentId}`,
    );
    if (resolvedCourseId !== input.courseId) {
      revalidatePath(
        `/dashboard/learning/courses/${resolvedCourseId}/assessments/${input.assessmentId}`,
      );
    }
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function deleteAssessment(
  courseId: string,
  assessmentId: string,
): Promise<ActionResult<null>> {
  try {
    const resolvedCourseId = await resolveCourseMutationId(courseId);
    const { assessments } = createCourseModules();
    const result = await assessments.deleteAssessments(assessmentId);

    if (result.ok) {
      revalidateCourseAssessmentPaths(courseId, resolvedCourseId);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

// ── Certificate template actions ──

export interface CreateCertificateTemplateInput {
  courseId: string;
  name: string;
  templateHtml?: string;
}

const defaultCertificateTemplateHtml = `
<section style="font-family: Inter, Arial, sans-serif; padding: 48px; border: 12px solid #111827; text-align: center;">
  <p style="letter-spacing: 0.18em; text-transform: uppercase; color: #6b7280;">Certificate of Completion</p>
  <h1 style="font-size: 42px; margin: 24px 0;">{{recipientName}}</h1>
  <p style="font-size: 18px; color: #374151;">has successfully completed</p>
  <h2 style="font-size: 30px; margin: 18px 0;">{{courseName}}</h2>
  <p style="color: #6b7280;">Issued on {{issuedAt}} - Certificate {{certificateNumber}}</p>
</section>
`.trim();

export async function createCertificateTemplate(
  input: CreateCertificateTemplateInput,
): Promise<ActionResult<{ id: string }>> {
  const name = input.name.trim();
  const templateHtml =
    input.templateHtml?.trim() || defaultCertificateTemplateHtml;

  if (name.length < 3) {
    return {
      success: false,
      error: "Template name must be at least 3 characters.",
    };
  }

  if (templateHtml.length < 20) {
    return {
      success: false,
      error: "Template HTML must be at least 20 characters.",
    };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { certificates } = createCourseModules();
    const result = await certificates.postApiCertificatesTemplates({
      courseId: resolvedCourseId,
      name,
      templateHtml,
    });

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidatePath(
      `/dashboard/learning/courses/${input.courseId}/certificates`,
    );
    return { success: true, data: { id: result.data.id! } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface UpdateCertificateTemplateInput {
  courseId: string;
  templateId: string;
  name: string;
  description?: string | null;
  templateHtml: string;
  templateStyles?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export async function updateCertificateTemplate(
  input: UpdateCertificateTemplateInput,
): Promise<ActionResult<null>> {
  const name = input.name.trim();
  const templateHtml = input.templateHtml.trim();

  if (name.length < 3) {
    return {
      success: false,
      error: "Template name must be at least 3 characters.",
    };
  }

  if (templateHtml.length < 20) {
    return {
      success: false,
      error: "Template HTML must be at least 20 characters.",
    };
  }

  try {
    const { certificates } = createCourseModules();
    const result = await certificates.putApiCertificatesTemplates(
      input.templateId,
      {
        name,
        description: input.description?.trim() || null,
        templateHtml,
        templateStyles: input.templateStyles?.trim() || null,
        isDefault: input.isDefault,
        isActive: input.isActive,
      },
    );

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidatePath(
      `/dashboard/learning/courses/${input.courseId}/certificates`,
    );
    revalidatePath(
      `/dashboard/learning/courses/${input.courseId}/certificates/${input.templateId}`,
    );
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function deleteCertificateTemplate(
  courseId: string,
  templateId: string,
): Promise<ActionResult<null>> {
  try {
    const { certificates } = createCourseModules();
    const result =
      await certificates.deleteApiCertificatesTemplates(templateId);

    if (result.ok) {
      revalidatePath(`/dashboard/learning/courses/${courseId}/certificates`);
      return { success: true, data: null };
    }

    return { success: false, error: extractError(result.error) };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

// ── Course support/discussion actions ──

export interface CreateCourseDiscussionInput {
  courseId: string;
  title: string;
  content: string;
  contentId?: string | null;
}

export interface CreateDiscussionReplyInput {
  courseId: string;
  discussionId: string;
  content: string;
  parentReplyId?: string | null;
}

function revalidateCourseSupport(courseId: string, discussionId?: string) {
  revalidatePath(`/dashboard/learning/courses/${courseId}/support`);
  revalidatePath(`/dashboard/learning/courses/${courseId}/support/tickets`);
  revalidatePath(`/dashboard/learning/courses/${courseId}/support/discussions`);

  if (discussionId) {
    revalidatePath(
      `/dashboard/learning/courses/${courseId}/support/tickets/${discussionId}`,
    );
    revalidatePath(
      `/dashboard/learning/courses/${courseId}/support/discussions/${discussionId}`,
    );
  }
}

export interface AddCourseSupportTicketMessageInput {
  courseId: string;
  ticketId: string;
  message: string;
}

export async function addCourseSupportTicketMessage(
  input: AddCourseSupportTicketMessageInput,
): Promise<ActionResult<null>> {
  const message = input.message.trim();
  if (message.length < 2)
    return { success: false, error: "Reply is required." };

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { supportTickets } = createCourseModules();
    const result = await supportTickets.postCoursesSupportTicketsMessages(
      resolvedCourseId,
      input.ticketId,
      { message },
    );
    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(input.courseId, input.ticketId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export interface ResolveCourseSupportTicketInput {
  courseId: string;
  ticketId: string;
  summary: string;
}

export async function resolveCourseSupportTicket(
  input: ResolveCourseSupportTicketInput,
): Promise<ActionResult<null>> {
  const summary = input.summary.trim();
  if (summary.length < 3)
    return {
      success: false,
      error: "Resolution summary must be at least 3 characters.",
    };

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { supportTickets } = createCourseModules();
    const result = await supportTickets.postCoursesSupportTicketsResolve(
      resolvedCourseId,
      input.ticketId,
      { summary },
    );
    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(input.courseId, input.ticketId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function createCourseDiscussion(
  input: CreateCourseDiscussionInput,
): Promise<ActionResult<{ id: string }>> {
  const title = input.title.trim();
  const content = input.content.trim();

  if (title.length < 5) {
    return {
      success: false,
      error: "Discussion title must be at least 5 characters.",
    };
  }

  if (content.length < 10) {
    return {
      success: false,
      error: "Discussion content must be at least 10 characters.",
    };
  }

  try {
    const resolvedCourseId = await resolveCourseMutationId(input.courseId);
    const { discussions } = createCourseModules();
    const result = await discussions.postApiSocialDiscussions({
      courseId: resolvedCourseId,
      title,
      content,
      contentId: input.contentId?.trim() || null,
    });

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(input.courseId, result.data.id);
    return { success: true, data: { id: result.data.id! } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function createDiscussionReply(
  input: CreateDiscussionReplyInput,
): Promise<ActionResult<{ id: string }>> {
  const content = input.content.trim();

  if (content.length < 2) {
    return { success: false, error: "Reply content is required." };
  }

  try {
    const { replies } = createCourseModules();
    const result = await replies.postApiSocialDiscussionsReplies(
      input.discussionId,
      {
        discussionId: input.discussionId,
        content,
        parentReplyId: input.parentReplyId?.trim() || null,
      },
    );

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(input.courseId, input.discussionId);
    return { success: true, data: { id: result.data.id! } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function updateDiscussionPin(
  courseId: string,
  discussionId: string,
  pinned: boolean,
): Promise<ActionResult<null>> {
  try {
    const { discussions } = createCourseModules();
    const result = pinned
      ? await discussions.postApiSocialDiscussionsPin(discussionId)
      : await discussions.postApiSocialDiscussionsUnpin(discussionId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(courseId, discussionId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function resolveDiscussion(
  courseId: string,
  discussionId: string,
): Promise<ActionResult<null>> {
  try {
    const { discussions } = createCourseModules();
    const result =
      await discussions.postApiSocialDiscussionsResolve(discussionId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(courseId, discussionId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function deleteDiscussion(
  courseId: string,
  discussionId: string,
): Promise<ActionResult<null>> {
  try {
    const { discussions } = createCourseModules();
    const result = await discussions.deleteApiSocialDiscussions(discussionId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(courseId, discussionId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function acceptDiscussionReply(
  courseId: string,
  discussionId: string,
  replyId: string,
): Promise<ActionResult<null>> {
  try {
    const { replies } = createCourseModules();
    const result = await replies.postApiSocialRepliesAccept(replyId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(courseId, discussionId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

export async function upvoteDiscussionReply(
  courseId: string,
  discussionId: string,
  replyId: string,
): Promise<ActionResult<null>> {
  try {
    const { replies } = createCourseModules();
    const result = await replies.postApiSocialRepliesUpvote(replyId);

    if (!result.ok)
      return { success: false, error: extractError(result.error) };

    revalidateCourseSupport(courseId, discussionId);
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

// =============================================================================
// SERVER-SIDE DATA FETCHING ACTIONS
// =============================================================================
// These wrap query functions so 'use client' components can fetch data via RPC
// instead of dynamic-importing server-only modules (which breaks Turbopack).
// =============================================================================

import type { CourseViewModel } from "@/lib/learning/view-models";

/**
 * Fetch course details. Safe to call from client components via server action RPC.
 */
export async function fetchCourse(
  courseId: string,
): Promise<CourseViewModel | null> {
  const { getCourse } = await import("@/lib/learning/queries/course");
  return getCourse(courseId);
}
