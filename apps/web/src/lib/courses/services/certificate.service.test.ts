import { beforeEach, describe, expect, it, vi } from 'vitest';

import { CourseCompletionCertificateService } from './certificate.service';

describe('CourseCompletionCertificateService', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('loads certificate status for the current learner through the Next API route', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({
        eligible: true,
        generated: true,
        certificateId: 'cert-1',
        certificateNumber: 'CERT-20260612-ABCDEF12',
        url: '/certificates/CERT-20260612-ABCDEF12',
      }),
    } as Response);

    const status = await CourseCompletionCertificateService.getCertificateStatus('course-1');

    expect(status).toEqual({
      eligible: true,
      generated: true,
      certificateId: 'cert-1',
      certificateNumber: 'CERT-20260612-ABCDEF12',
      url: '/certificates/CERT-20260612-ABCDEF12',
    });
    expect(fetchMock).toHaveBeenCalledWith('/api/courses/certificates?mode=status&courseId=course-1', {
      method: 'GET',
      redirect: 'error',
    });
  });

  it('does not fake certificate generation when enrollment or template context is missing', async () => {
    const fetchMock = vi.spyOn(global, 'fetch');

    const result = await CourseCompletionCertificateService.generateCertificate('course-1', 'user-1');

    expect(result).toEqual({
      success: false,
      error: 'Certificate generation requires a completed enrollment and active certificate template.',
      certificateUrl: null,
    });
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('issues certificates through the Next API route when required ids are available', async () => {
    const fetchMock = vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({
        id: 'cert-1',
        certificateNumber: 'CERT-20260612-ABCDEF12',
      }),
    } as Response);

    const result = await CourseCompletionCertificateService.generateCertificate('course-1', 'user-1', {
      templateId: 'template-1',
      enrollmentId: 'enrollment-1',
    });

    expect(result).toEqual({
      success: true,
      certificateId: 'cert-1',
      certificateUrl: '/certificates/CERT-20260612-ABCDEF12',
    });
    expect(fetchMock).toHaveBeenCalledWith('/api/courses/certificates', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        courseId: 'course-1',
        userId: 'user-1',
        templateId: 'template-1',
        enrollmentId: 'enrollment-1',
      }),
    });
  });

  it('asks for a certificate notification after a completed course becomes eligible', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValue({
      ok: true,
      json: async () => ({
        eligible: true,
        generated: false,
        templateId: 'template-1',
      }),
    } as Response);

    const result = await CourseCompletionCertificateService.handleCourseCompletion(
      'course-1',
      {
        title: 'Intro to Game Design',
        overallProgress: 100,
      },
      'Alice',
    );

    expect(result).toEqual({
      showCertificateNotification: true,
      eligibility: {
        courseId: 'course-1',
        courseTitle: 'Intro to Game Design',
        completedAt: expect.any(Date),
        finalGrade: undefined,
        templateId: 'template-1',
      },
    });
  });
});
