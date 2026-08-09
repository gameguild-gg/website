import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type LearningAssessmentsAssessment,
  type LearningAssessmentsAssessmentDefinition,
  type LearningAssessmentsAssessmentGroup,
  type LearningAssessmentsAssessmentScoreBucket,
  type LearningAssessmentsCourseAssessmentAnalytics,
  type LearningCertificatesCertificate,
  type LearningCertificatesCertificateTemplate,
  type LearningCertificatesCertificateTemplateDetail,
} from '@game-guild/client';
import { cache } from 'react';
import { resolveCourseId } from './course';

// =============================================================================
// API CLIENT
// =============================================================================

function getApiClient() {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
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

export type AssessmentType = 'Quiz' | 'Assignment' | 'Project' | 'PeerReview' | 'SelfAssessment';
export type AssessmentPresentationMode = 'SingleStep' | 'Continuous';

export interface Assessment {
  id: string;
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
}

export interface AssessmentDefinition {
  assessmentId: string;
  definitionSchemaVersion: number;
  definition: unknown;
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

// =============================================================================
// MAPPERS
// =============================================================================

function mapAssessment(dto: LearningAssessmentsAssessment): Assessment {
  const groupFields = dto as LearningAssessmentsAssessment & AssessmentGroupFields;

  return {
    id: dto.id ?? '',
    courseId: dto.courseId ?? '',
    contentId: dto.contentId ?? null,
    assessmentGroupId: groupFields.assessmentGroupId ?? null,
    assessmentGroupName: groupFields.assessmentGroupName ?? null,
    assessmentGroupWeightPercent: groupFields.assessmentGroupWeightPercent ?? null,
    assessmentGroupOrder: groupFields.assessmentGroupOrder ?? null,
    title: dto.title ?? '',
    description: dto.description ?? null,
    type: normalizeAssessmentType(dto.type),
    maxScore: dto.maxScore ?? 100,
    passingScore: dto.passingScore ?? 70,
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
  };
}

function normalizeAssessmentType(type: string | null | undefined): AssessmentType {
  if (type === 'Exam') return 'Quiz';
  if (
    type === 'Quiz' ||
    type === 'Assignment' ||
    type === 'Project' ||
    type === 'PeerReview' ||
    type === 'SelfAssessment'
  ) {
    return type;
  }

  return 'Quiz';
}

function normalizePresentationMode(mode: string | null | undefined): AssessmentPresentationMode {
  return mode === 'Continuous' ? 'Continuous' : 'SingleStep';
}

function mapAssessmentGroup(dto: LearningAssessmentsAssessmentGroup): AssessmentGroup {
  return {
    id: dto.id ?? '',
    courseId: dto.courseId ?? '',
    name: dto.name ?? '',
    description: dto.description ?? null,
    weightPercent: dto.weightPercent ?? 0,
    order: dto.order ?? 0,
  };
}

function mapScoreBucket(dto: LearningAssessmentsAssessmentScoreBucket): AssessmentScoreBucket {
  return {
    label: dto.label ?? 'Unscored',
    minPercent: dto.minPercent ?? 0,
    maxPercent: dto.maxPercent ?? 0,
    count: dto.count ?? 0,
  };
}

function mapCourseAssessmentAnalytics(
  dto: LearningAssessmentsCourseAssessmentAnalytics,
): CourseAssessmentAnalytics {
  return {
    courseId: dto.courseId ?? '',
    assessmentCount: dto.assessmentCount ?? 0,
    gradedCount: dto.gradedCount ?? 0,
    ungradedCount: dto.ungradedCount ?? 0,
    averagePercent: dto.averagePercent ?? 0,
    passRate: dto.passRate ?? 0,
    distribution: (dto.distribution ?? []).map(mapScoreBucket),
    groups: (dto.groups ?? []).map((group) => ({
      groupId: group.groupId ?? null,
      groupName: group.groupName ?? 'Ungrouped',
      weightPercent: group.weightPercent ?? null,
      assessmentCount: group.assessmentCount ?? 0,
      gradedCount: group.gradedCount ?? 0,
      ungradedCount: group.ungradedCount ?? 0,
      averagePercent: group.averagePercent ?? 0,
      passRate: group.passRate ?? 0,
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
export const getCourseAssessments = cache(async (courseId: string): Promise<CourseAssessments> => {
  try {
    const resolvedCourseId = await resolveCourseId(courseId);
    const assessmentsModule = createAssessmentsModule();
    const result = await assessmentsModule.getAssessmentsCourse(resolvedCourseId);
    if (!result.ok) {
      console.error('Failed to fetch assessments:', result.error);
      return { assessments: [], total: 0 };
    }
    const assessments = (result.data ?? []).map(mapAssessment);
    return { assessments, total: assessments.length };
  } catch (err) {
    console.error('Error fetching course assessments:', err);
    return { assessments: [], total: 0 };
  }
});

export const getCourseAssessmentGroups = cache(async (courseId: string): Promise<AssessmentGroup[]> => {
  try {
    const resolvedCourseId = await resolveCourseId(courseId);
    const result = await createAssessmentsModule().getAssessmentsCourseGroups(resolvedCourseId);
    return result.ok ? (result.data ?? []).map(mapAssessmentGroup) : [];
  } catch (err) {
    console.error('Error fetching course assessment groups:', err);
    return [];
  }
});

export const getCourseAssessmentAnalytics = cache(async (courseId: string): Promise<CourseAssessmentAnalytics | null> => {
  try {
    const resolvedCourseId = await resolveCourseId(courseId);
    const result = await createAssessmentsModule().getAssessmentsCourseAnalytics(resolvedCourseId);
    return result.ok ? mapCourseAssessmentAnalytics(result.data) : null;
  } catch (err) {
    console.error('Error fetching course assessment analytics:', err);
    return null;
  }
});

/**
 * Fetch single assessment by ID.
 */
export const getAssessment = cache(async (assessmentId: string): Promise<Assessment | null> => {
  try {
    const assessmentsModule = createAssessmentsModule();
    const result = await assessmentsModule.getAssessments(assessmentId);
    if (!result.ok) {
      return null;
    }
    return mapAssessment(result.data);
  } catch {
    return null;
  }
});

export const getAssessmentDefinition = cache(async (assessmentId: string): Promise<AssessmentDefinition | null> => {
  try {
    const result = await createAssessmentsModule().getAssessmentsDefinition(assessmentId);
    if (!result.ok) return null;

    const dto: LearningAssessmentsAssessmentDefinition = result.data;
    return {
      assessmentId: dto.assessmentId ?? assessmentId,
      definitionSchemaVersion: dto.definitionSchemaVersion ?? 1,
      definition: dto.definition ?? { order: [], blocks: {} },
    };
  } catch (err) {
    console.error('Error fetching assessment definition:', err);
    return null;
  }
});

export interface CertificateTemplate {
  id: string;
  courseId: string;
  name: string;
  description: string | null;
  status: 'draft' | 'active' | 'archived';
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
  dto: LearningCertificatesCertificateTemplate | LearningCertificatesCertificateTemplateDetail,
  issuedCount = 0,
): CertificateTemplate {
  const createdAt = dto.createdAt ?? new Date().toISOString();

  return {
    id: dto.id ?? '',
    courseId: dto.courseId ?? '',
    name: dto.name ?? '',
    description: dto.description ?? null,
    status: dto.isActive === false ? 'archived' : 'active',
    isDefault: dto.isDefault === true,
    issuedCount,
    createdAt,
    updatedAt: dto.updatedAt ?? createdAt,
  };
}

export const getCourseCertificates = cache(async (courseId: string): Promise<CourseCertificates> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const certificates = createCertificatesModule();
  const [templatesResult, issuedCertificatesResult] = await Promise.all([
    certificates.getApiCertificatesTemplatesCourse(resolvedCourseId),
    certificates.getApiCertificatesCourse(resolvedCourseId),
  ]);

  const templates = templatesResult.ok ? templatesResult.data : [];
  const issuedCertificates: LearningCertificatesCertificate[] = issuedCertificatesResult.ok
    ? issuedCertificatesResult.data
    : [];
  const issuedCount = issuedCertificates.length;
  const mapped = templates.map((template) => mapCertificateTemplate(template, issuedCount));

  return { templates: mapped, total: mapped.length, issuedCount };
});

export const getCertificateTemplate = cache(async (templateId: string): Promise<CertificateTemplateDetail | null> => {
  const result = await createCertificatesModule().getApiCertificatesTemplates(templateId);
  if (!result.ok) return null;

  const template = result.data;
  return {
    ...mapCertificateTemplate(template),
    previewUrl: `/api/certificates/templates/${template.id ?? templateId}`,
    templateHtml: template.templateHtml ?? '',
    templateStyles: template.templateStyles ?? null,
  };
});
