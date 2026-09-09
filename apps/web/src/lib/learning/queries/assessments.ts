import { getToken } from "@/auth";
import {
  createServerClient,
  GeneratedApi,
  type LearningAssessmentsAssessment,
  type LearningAssessmentsAssessmentGroup,
  type LearningAssessmentsAssessmentPresentationMode,
  type LearningAssessmentsAssessmentScoreBucket,
  type LearningAssessmentsAssessmentSubmission,
  type LearningAssessmentsAssessmentType,
  type LearningAssessmentsCourseAssessmentAnalytics,
  type LearningAssessmentsGradingContractsAttemptContributionMode,
  type LearningAssessmentsGradingContractsContentCompletionMode,
  type LearningAssessmentsGradingContractsResultReleaseMode,
  type LearningCertificatesCertificate,
  type LearningCertificatesCertificateTemplate,
  type LearningCertificatesCertificateTemplateDetail,
} from "@game-guild/client";
import {
  ACADEMIC_VALUE_SCALE,
  parsePercentValue,
  parseReviewMethods,
  parseScoreValue,
  type ReviewMethods,
} from "@game-guild/grading";
import { cache } from "react";
import { resolveCourseId } from "./course";

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
    value.trim(),
  );
}

// =============================================================================
// API CLIENT
// =============================================================================

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

function createAssessmentsModule() {
  return new GeneratedApi.LearningAssessmentsModule(getApiClient());
}

// =============================================================================
// TYPES (re-exported for convenience)
// =============================================================================

export type AssessmentType = LearningAssessmentsAssessmentType;
export type AssessmentPresentationMode =
  LearningAssessmentsAssessmentPresentationMode;

export interface Assessment {
  id: string;
  slug: string;
  courseId: string;
  contentId: string | null;
  assessmentGroupId: string | null;
  assessmentGroupName: string | null;
  assessmentGroupWeightPercent: number | null;
  assessmentGroupOrder: number | null;
  title: string;
  description: string | null;
  type: AssessmentType;
  maxScore: number;
  passingScore: number;
  timeLimitMinutes: number | null;
  maxAttempts: number | null;
  isRequired: boolean;
  order: number;
  availableFrom: string | null;
  availableUntil: string | null;
  presentationMode: AssessmentPresentationMode;
  dueAt: string | null;
  allowLateSubmissions: boolean;
  lateSubmissionDeadline: string | null;
  isAvailable: boolean;
  reviewMethods: ReviewMethods;
  groupSetId: string | null;
  publishedDefinitionRevisionId: string | null;
  reviewConfigurationCanonicalJson: string | null;
  attemptContributionMode: LearningAssessmentsGradingContractsAttemptContributionMode | null;
  contentCompletionMode: LearningAssessmentsGradingContractsContentCompletionMode;
  resultReleaseMode: LearningAssessmentsGradingContractsResultReleaseMode;
  resultReleaseScheduledFor: string | null;
  version: number;
}

export interface AssessmentRevisionState {
  revisionId: string;
  revisionNumber: number;
  authoringSourceHash: string;
  executionSnapshotHash: string;
  createdAt: string;
}

export interface AssessmentCapabilityState {
  available: boolean;
  code: string | null;
  message: string | null;
}

export interface AssessmentAuthoringState {
  assessmentId: string;
  assessmentVersion: number;
  lifecycle: "draft" | "candidate" | "published" | "changes-pending";
  currentAuthoringSourceHash: string;
  candidate: AssessmentRevisionState | null;
  published: AssessmentRevisionState | null;
  candidateMatchesDraft: boolean;
  publishedMatchesDraft: boolean;
  prepare: AssessmentCapabilityState;
  publish: AssessmentCapabilityState;
}

export interface CourseAssessments {
  assessments: Assessment[];
  total: number;
}

export interface AssessmentGroup {
  id: string;
  courseId: string;
  name: string;
  description: string | null;
  weightPercent: number;
  order: number;
}

export interface AssessmentScoreBucket {
  label: string;
  minPercent: number;
  maxPercent: number;
  count: number;
}

export interface AssessmentGroupAnalytics {
  groupId: string | null;
  groupName: string;
  weightPercent: number | null;
  assessmentCount: number;
  gradedCount: number;
  ungradedCount: number;
  averagePercent: number;
  passRate: number;
  distribution: AssessmentScoreBucket[];
}

export interface CourseAssessmentAnalytics {
  courseId: string;
  assessmentCount: number;
  gradedCount: number;
  ungradedCount: number;
  averagePercent: number;
  passRate: number;
  distribution: AssessmentScoreBucket[];
  groups: AssessmentGroupAnalytics[];
}

type AssessmentGroupFields = {
  assessmentGroupId?: string | null;
  assessmentGroupName?: string | null;
  assessmentGroupWeightPercent?: number | null;
  assessmentGroupOrder?: number | null;
};

type AssessmentWireFields = {
  maxScore?: unknown;
  passingScore?: unknown;
  reviewMethods?: unknown;
  publishedDefinitionRevisionId?: string | null;
  reviewConfigurationCanonicalJson?: string | null;
  attemptContributionMode?: LearningAssessmentsGradingContractsAttemptContributionMode | null;
  contentCompletionMode?: LearningAssessmentsGradingContractsContentCompletionMode | null;
  resultReleaseMode?: LearningAssessmentsGradingContractsResultReleaseMode | null;
  resultReleaseScheduledFor?: string | null;
  version?: number;
};

function scoreToNumber(value: unknown, fallbackUnits: number): number {
  return parseScoreValue(value ?? fallbackUnits) / ACADEMIC_VALUE_SCALE;
}

function percentToNumber(value: unknown, fallbackUnits: number): number {
  return parsePercentValue(value ?? fallbackUnits) / ACADEMIC_VALUE_SCALE;
}

// =============================================================================
// MAPPERS
// =============================================================================

function mapAssessment(dto: LearningAssessmentsAssessment): Assessment {
  const groupFields = dto as LearningAssessmentsAssessment &
    AssessmentGroupFields;
  const wire = dto as LearningAssessmentsAssessment & AssessmentWireFields;

  return {
    id: dto.id ?? "",
    slug: dto.slug ?? dto.id ?? "",
    courseId: dto.courseId ?? "",
    contentId: dto.contentId ?? null,
    assessmentGroupId: groupFields.assessmentGroupId ?? null,
    assessmentGroupName: groupFields.assessmentGroupName ?? null,
    assessmentGroupWeightPercent:
      groupFields.assessmentGroupWeightPercent == null
        ? null
        : percentToNumber(groupFields.assessmentGroupWeightPercent, 0),
    assessmentGroupOrder: groupFields.assessmentGroupOrder ?? null,
    title: dto.title ?? "",
    description: dto.description ?? null,
    type: normalizeAssessmentType(dto.type),
    maxScore: scoreToNumber(wire.maxScore, 10_000),
    passingScore: scoreToNumber(wire.passingScore, 7_000),
    timeLimitMinutes: dto.timeLimitMinutes ?? null,
    maxAttempts: dto.maxAttempts ?? null,
    isRequired: dto.isRequired ?? true,
    order: dto.order ?? 0,
    availableFrom: dto.availableFrom ?? null,
    availableUntil: dto.availableUntil ?? null,
    presentationMode: normalizePresentationMode(dto.presentationMode),
    dueAt: dto.dueAt ?? null,
    allowLateSubmissions: dto.allowLateSubmissions ?? false,
    lateSubmissionDeadline: dto.lateSubmissionDeadline ?? null,
    isAvailable: dto.isAvailable ?? true,
    reviewMethods: parseReviewMethods(wire.reviewMethods ?? 8, {
      allowDraft: true,
    }),
    groupSetId: dto.groupSetId ?? null,
    publishedDefinitionRevisionId: wire.publishedDefinitionRevisionId ?? null,
    reviewConfigurationCanonicalJson:
      wire.reviewConfigurationCanonicalJson ?? null,
    attemptContributionMode: wire.attemptContributionMode ?? null,
    contentCompletionMode: wire.contentCompletionMode ?? "on-release-and-pass",
    resultReleaseMode: wire.resultReleaseMode ?? "manual",
    resultReleaseScheduledFor: wire.resultReleaseScheduledFor ?? null,
    version: wire.version ?? 0,
  };
}

function normalizeAssessmentType(
  type: string | null | undefined,
): AssessmentType {
  if (type === "Exam") return "Quiz";
  if (
    type === "Quiz" ||
    type === "Assignment" ||
    type === "Project" ||
    type === "PeerReview" ||
    type === "SelfAssessment"
  ) {
    return type;
  }

  return "Quiz";
}

function normalizePresentationMode(
  mode: string | null | undefined,
): AssessmentPresentationMode {
  return mode === "Continuous" ? "Continuous" : "SingleStep";
}

function mapAssessmentGroup(
  dto: LearningAssessmentsAssessmentGroup,
): AssessmentGroup {
  const wire = dto as LearningAssessmentsAssessmentGroup & {
    weightPercent?: unknown;
  };
  return {
    id: dto.id ?? "",
    courseId: dto.courseId ?? "",
    name: dto.name ?? "",
    description: dto.description ?? null,
    weightPercent: percentToNumber(wire.weightPercent, 0),
    order: dto.order ?? 0,
  };
}

function mapScoreBucket(
  dto: LearningAssessmentsAssessmentScoreBucket,
): AssessmentScoreBucket {
  return {
    label: dto.label ?? "Unscored",
    minPercent: dto.minPercent ?? 0,
    maxPercent: dto.maxPercent ?? 0,
    count: dto.count ?? 0,
  };
}

function mapCourseAssessmentAnalytics(
  dto: LearningAssessmentsCourseAssessmentAnalytics,
): CourseAssessmentAnalytics {
  return {
    courseId: dto.courseId ?? "",
    assessmentCount: dto.assessmentCount ?? 0,
    gradedCount: dto.gradedCount ?? 0,
    ungradedCount: dto.ungradedCount ?? 0,
    averagePercent: percentToNumber(dto.averagePercent, 0),
    passRate: percentToNumber(dto.passRate, 0),
    distribution: (dto.distribution ?? []).map(mapScoreBucket),
    groups: (dto.groups ?? []).map((group) => ({
      groupId: group.groupId ?? null,
      groupName: group.groupName ?? "Ungrouped",
      weightPercent:
        group.weightPercent == null
          ? null
          : percentToNumber(group.weightPercent, 0),
      assessmentCount: group.assessmentCount ?? 0,
      gradedCount: group.gradedCount ?? 0,
      ungradedCount: group.ungradedCount ?? 0,
      averagePercent: percentToNumber(group.averagePercent, 0),
      passRate: percentToNumber(group.passRate, 0),
      distribution: (group.distribution ?? []).map(mapScoreBucket),
    })),
  };
}

// =============================================================================
// FETCH FUNCTIONS
// =============================================================================

/**
 * Fetch course assessments (conditional: hasAssessments).
 */
export const getCourseAssessments = cache(
  async (courseId: string): Promise<CourseAssessments> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const assessmentsModule = createAssessmentsModule();
      const result =
        await assessmentsModule.getAssessmentsCourse(resolvedCourseId);
      if (!result.ok) {
        console.error("Failed to fetch assessments:", result.error);
        return { assessments: [], total: 0 };
      }
      const assessments = (result.data ?? []).map(mapAssessment);
      return { assessments, total: assessments.length };
    } catch (err) {
      console.error("Error fetching course assessments:", err);
      return { assessments: [], total: 0 };
    }
  },
);

export const getCourseAssessmentGroups = cache(
  async (courseId: string): Promise<AssessmentGroup[]> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const result =
        await createAssessmentsModule().getAssessmentsCourseGroups(
          resolvedCourseId,
        );
      return result.ok ? (result.data ?? []).map(mapAssessmentGroup) : [];
    } catch (err) {
      console.error("Error fetching course assessment groups:", err);
      return [];
    }
  },
);

export const getCourseAssessmentAnalytics = cache(
  async (courseId: string): Promise<CourseAssessmentAnalytics | null> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const result =
        await createAssessmentsModule().getAssessmentsCourseAnalytics(
          resolvedCourseId,
        );
      return result.ok ? mapCourseAssessmentAnalytics(result.data) : null;
    } catch (err) {
      console.error("Error fetching course assessment analytics:", err);
      return null;
    }
  },
);

/**
 * Fetch single assessment by ID.
 */
export const getAssessment = cache(
  async (
    courseId: string,
    assessmentIdOrSlug: string,
  ): Promise<Assessment | null> => {
    try {
      if (isGuid(assessmentIdOrSlug)) {
        const assessmentsModule = createAssessmentsModule();
        const result =
          await assessmentsModule.getAssessments(assessmentIdOrSlug);
        if (!result.ok) {
          return null;
        }
        return mapAssessment(result.data);
      }

      const assessments = await getCourseAssessments(courseId);
      return (
        assessments.assessments.find((a) => a.slug === assessmentIdOrSlug) ??
        null
      );
    } catch {
      return null;
    }
  },
);

export const getAssessmentAuthoringState = cache(
  async (assessmentId: string): Promise<AssessmentAuthoringState | null> => {
    try {
      const result = await getApiClient().request<AssessmentAuthoringState>({
        method: "GET",
        path: `/v1.0/assessments/${assessmentId}/authoring-state`,
        requiresAuth: true,
      });
      return result.ok ? result.data : null;
    } catch {
      return null;
    }
  },
);

/**
 * Fetch all submissions for an assessment (instructor view).
 *
 * Backend (`AssessmentService.GetAssessmentSubmissionsAsync`) returns the
 * list ordered by `StartedAt` descending. Returned raw (typed via the
 * generated DTO) — caller renders.
 */
export const getAssessmentSubmissions = cache(
  async (
    assessmentId: string,
  ): Promise<LearningAssessmentsAssessmentSubmission[]> => {
    try {
      const result =
        await createAssessmentsModule().getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions(
          assessmentId,
        );
      if (!result.ok) {
        console.error("Failed to fetch assessment submissions:", result.error);
        return [];
      }
      return result.data ?? [];
    } catch (err) {
      console.error("Error fetching assessment submissions:", err);
      return [];
    }
  },
);

export interface CertificateTemplate {
  id: string;
  courseId: string;
  name: string;
  description: string | null;
  status: "draft" | "active" | "archived";
  isDefault: boolean;
  issuedCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CourseCertificates {
  templates: CertificateTemplate[];
  total: number;
  issuedCount: number;
}

export interface CertificateTemplateDetail extends CertificateTemplate {
  previewUrl: string;
  templateHtml: string;
  templateStyles: string | null;
}

function createCertificatesModule() {
  return new GeneratedApi.LearningCertificatesModule(getApiClient());
}

function mapCertificateTemplate(
  dto:
    | LearningCertificatesCertificateTemplate
    | LearningCertificatesCertificateTemplateDetail,
  issuedCount = 0,
): CertificateTemplate {
  const createdAt = dto.createdAt ?? new Date().toISOString();

  return {
    id: dto.id ?? "",
    courseId: dto.courseId ?? "",
    name: dto.name ?? "",
    description: dto.description ?? null,
    status: dto.isActive === false ? "archived" : "active",
    isDefault: dto.isDefault === true,
    issuedCount,
    createdAt,
    updatedAt: dto.updatedAt ?? createdAt,
  };
}

export const getCourseCertificates = cache(
  async (courseId: string): Promise<CourseCertificates> => {
    const resolvedCourseId = await resolveCourseId(courseId);
    const certificates = createCertificatesModule();
    const [templatesResult, issuedCertificatesResult] = await Promise.all([
      certificates.getApiCertificatesTemplatesCourse(resolvedCourseId),
      certificates.getApiCertificatesCourse(resolvedCourseId),
    ]);

    const templates = templatesResult.ok ? templatesResult.data : [];
    const issuedCertificates: LearningCertificatesCertificate[] =
      issuedCertificatesResult.ok ? issuedCertificatesResult.data : [];
    const issuedCount = issuedCertificates.length;
    const mapped = templates.map((template) =>
      mapCertificateTemplate(template, issuedCount),
    );

    return { templates: mapped, total: mapped.length, issuedCount };
  },
);

export const getCertificateTemplate = cache(
  async (templateId: string): Promise<CertificateTemplateDetail | null> => {
    const result =
      await createCertificatesModule().getApiCertificatesTemplates(templateId);
    if (!result.ok) return null;

    const template = result.data;
    return {
      ...mapCertificateTemplate(template),
      previewUrl: `/api/certificates/templates/${template.id ?? templateId}`,
      templateHtml: template.templateHtml ?? "",
      templateStyles: template.templateStyles ?? null,
    };
  },
);

// =============================================================================
// CODING DEFINITION (public — hidden cases already stripped server-side)
// =============================================================================

/** Shape returned by GET /v1.0/assessments/{id}/coding-definition/public. */
export interface CodingDefinition {
  kind: string;
  language: string;
  workspaceConfig: Record<string, unknown> | null;
  testPlan: Record<string, unknown> | null;
  maxScore: number;
  passingScore: number;
}

/**
 * Fetch the public coding definition for an assessment (enrollment-gated).
 * Hidden test cases are stripped server-side. Returns null when absent.
 */
export const getCodingDefinitionPublic = cache(
  async (assessmentId: string): Promise<CodingDefinition | null> => {
    try {
      const client = getApiClient();
      const result = await client.request<CodingDefinition>({
        method: "GET",
        path: `/v1.0/assessments/${assessmentId}/coding-definition/public`,
      });
      if (!result.ok) return null;
      return result.data;
    } catch (err) {
      console.error("Error fetching public coding definition:", err);
      return null;
    }
  },
);

// =============================================================================
// COURSE GROUP SETS (todo 4 endpoints)
// =============================================================================

export interface CourseGroupSetSummary {
  id: string;
  name: string;
  groups: { id: string; name: string; capacity: number; memberCount: number }[];
}

export interface CourseGroupDetail {
  id: string;
  name: string;
  capacity: number;
  memberCount: number;
  members: { userId: string; displayName: string }[];
}

export interface CourseGroupSetView extends CourseGroupSetSummary {
  groups: CourseGroupDetail[];
}

export const getCourseGroupSets = cache(
  async (courseId: string): Promise<CourseGroupSetSummary[]> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const module = new GeneratedApi.LearningAssessmentsGroupSetsModule(
        getApiClient(),
      );
      const result = await module.getCoursesGroupSets(resolvedCourseId);
      if (!result.ok) {
        console.error("Failed to fetch course group sets:", result.error);
        return [];
      }
      return (result.data ?? []).map((set) => ({
        id: set.id ?? "",
        name: set.name ?? "",
        groups: (set.groups ?? []).map((group) => ({
          id: group.id ?? "",
          name: group.name ?? "",
          capacity: group.capacity ?? 0,
          memberCount: group.memberCount ?? 0,
        })),
      }));
    } catch (err) {
      console.error("Error fetching course group sets:", err);
      return [];
    }
  },
);

export const getGroupSetGroups = cache(
  async (courseId: string, setId: string): Promise<CourseGroupDetail[]> => {
    try {
      const resolvedCourseId = await resolveCourseId(courseId);
      const module = new GeneratedApi.LearningAssessmentsGroupSetsModule(
        getApiClient(),
      );
      const result = await module.getCoursesGroupSetsGroups(
        resolvedCourseId,
        setId,
      );
      if (!result.ok) {
        console.error("Failed to fetch group set groups:", result.error);
        return [];
      }
      return (result.data ?? []).map((group) => ({
        id: group.id ?? "",
        name: group.name ?? "",
        capacity: group.capacity ?? 0,
        memberCount: group.memberCount ?? 0,
        members: (group.members ?? []).map((member) => ({
          userId: member.userId ?? "",
          displayName: member.displayName ?? member.userId ?? "",
        })),
      }));
    } catch (err) {
      console.error("Error fetching group set groups:", err);
      return [];
    }
  },
);

export const getCourseGroupSetViews = cache(
  async (courseId: string): Promise<CourseGroupSetView[]> => {
    const sets = await getCourseGroupSets(courseId);
    const details = await Promise.all(
      sets.map((set) => getGroupSetGroups(courseId, set.id)),
    );
    return sets.map((set, index) => ({ ...set, groups: details[index] ?? [] }));
  },
);

// =============================================================================
// ASSESSMENT RUBRIC (todo 6 endpoints)
// =============================================================================

export interface AssessmentRubricView {
  id: string;
  title: string;
  criteria: { description: string; points: number; order: number }[];
}

export const getAssessmentRubric = cache(
  async (
    assessmentId: string,
  ): Promise<{ rubric: AssessmentRubricView | null; locked: boolean }> => {
    try {
      const module = new GeneratedApi.LearningAssessmentsRubricsModule(
        getApiClient(),
      );
      const result = await module.getAssessmentsRubric(assessmentId);
      if (result.ok) {
        const rubric = result.data;
        return {
          rubric: {
            id: rubric.id ?? "",
            title: rubric.title ?? "",
            criteria: (rubric.criteria ?? []).map((criterion) => ({
              description: criterion.description ?? "",
              points: scoreToNumber(criterion.points, 0),
              order: criterion.order ?? 0,
            })),
          },
          locked: false,
        };
      }
      // 404 = no rubric assigned yet; 409 = locked after grading started.
      const status = (result.error as { status?: number } | undefined)?.status;
      if (status === 409) {
        return { rubric: null, locked: true };
      }
      return { rubric: null, locked: false };
    } catch (err) {
      console.error("Error fetching assessment rubric:", err);
      return { rubric: null, locked: false };
    }
  },
);
