import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  getAssessmentDefinition,
  getCertificateTemplate,
  getCourseAssessmentAnalytics,
  getCourseAssessmentGroups,
  getCourseCertificates,
} from './assessments';

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getToken: vi.fn(),
  resolveCourseId: vi.fn(),
  getAssessmentsCourseGroups: vi.fn(),
  getAssessmentsCourseAnalytics: vi.fn(),
  getAssessmentsDefinition: vi.fn(),
  getApiCertificatesTemplatesCourse: vi.fn(),
  getApiCertificatesCourse: vi.fn(),
  getApiCertificatesTemplates: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: mocks.getToken }));

vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningAssessmentsModule: class {
      getAssessmentsCourseGroups = mocks.getAssessmentsCourseGroups;
      getAssessmentsCourseAnalytics = mocks.getAssessmentsCourseAnalytics;
      getAssessmentsDefinition = mocks.getAssessmentsDefinition;
    },
    LearningCertificatesModule: class {
      getApiCertificatesTemplatesCourse = mocks.getApiCertificatesTemplatesCourse;
      getApiCertificatesCourse = mocks.getApiCertificatesCourse;
      getApiCertificatesTemplates = mocks.getApiCertificatesTemplates;
    },
  },
}));

vi.mock('./course', () => ({ resolveCourseId: mocks.resolveCourseId }));

describe('assessment queries', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.createServerClient.mockReturnValue({});
    mocks.getToken.mockResolvedValue('token');
    mocks.resolveCourseId.mockResolvedValue('course-1');
  });

  it('maps grade groups and analytics from generated assessment contracts', async () => {
    mocks.getAssessmentsCourseGroups.mockResolvedValue({
      ok: true,
      data: [{ id: 'group-1', courseId: 'course-1', name: 'Quizzes', weightPercent: 30, order: 2 }],
    });
    mocks.getAssessmentsCourseAnalytics.mockResolvedValue({
      ok: true,
      data: {
        courseId: 'course-1', assessmentCount: 2, gradedCount: 8, ungradedCount: 2,
        averagePercent: 77.5, passRate: 80,
        distribution: [{ label: '70-79', minPercent: 70, maxPercent: 79, count: 3 }],
        groups: [{
          groupId: 'group-1', groupName: 'Quizzes', weightPercent: 30,
          assessmentCount: 2, gradedCount: 8, ungradedCount: 2, averagePercent: 77.5, passRate: 80,
          distribution: [{ label: '70-79', minPercent: 70, maxPercent: 79, count: 3 }],
        }],
      },
    });

    const [groups, analytics] = await Promise.all([
      getCourseAssessmentGroups('course-slug'),
      getCourseAssessmentAnalytics('course-slug'),
    ]);

    expect(mocks.getAssessmentsCourseGroups).toHaveBeenCalledWith('course-1');
    expect(mocks.getAssessmentsCourseAnalytics).toHaveBeenCalledWith('course-1');
    expect(groups).toEqual([expect.objectContaining({ id: 'group-1', name: 'Quizzes', weightPercent: 30 })]);
    expect(analytics).toMatchObject({
      courseId: 'course-1',
      averagePercent: 77.5,
      groups: [expect.objectContaining({ groupName: 'Quizzes', distribution: [expect.objectContaining({ count: 3 })] })],
    });
  });

  it('loads definitions and certificates through generated modules', async () => {
    mocks.getAssessmentsDefinition.mockResolvedValue({
      ok: true,
      data: { assessmentId: 'assessment-1', definitionSchemaVersion: 2, definition: { order: ['block-1'] } },
    });
    mocks.getApiCertificatesTemplatesCourse.mockResolvedValue({
      ok: true,
      data: [{
        id: 'template-1', courseId: 'course-1', name: 'Completion', isActive: true, isDefault: true,
        createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z',
      }],
    });
    mocks.getApiCertificatesCourse.mockResolvedValue({ ok: true, data: [{ id: 'certificate-1' }] });
    mocks.getApiCertificatesTemplates.mockResolvedValue({
      ok: true,
      data: {
        id: 'template-1', courseId: 'course-1', name: 'Completion', isActive: true, isDefault: true,
        templateHtml: '<main>Certificate</main>', templateStyles: '.certificate {}',
        createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z',
      },
    });

    const [definition, certificates, template] = await Promise.all([
      getAssessmentDefinition('assessment-1'),
      getCourseCertificates('course-slug'),
      getCertificateTemplate('template-1'),
    ]);

    expect(mocks.getAssessmentsDefinition).toHaveBeenCalledWith('assessment-1');
    expect(mocks.getApiCertificatesTemplatesCourse).toHaveBeenCalledWith('course-1');
    expect(mocks.getApiCertificatesCourse).toHaveBeenCalledWith('course-1');
    expect(mocks.getApiCertificatesTemplates).toHaveBeenCalledWith('template-1');
    expect(definition).toMatchObject({ assessmentId: 'assessment-1', definitionSchemaVersion: 2 });
    expect(certificates).toMatchObject({ total: 1, issuedCount: 1 });
    expect(template).toMatchObject({ id: 'template-1', templateHtml: '<main>Certificate</main>' });
  });
});
