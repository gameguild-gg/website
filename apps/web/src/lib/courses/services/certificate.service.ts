import { assertSafeRemoteUrl } from '@/lib/security/safe-remote-url';

export interface CertificateStatus {
  eligible: boolean;
  generated: boolean;
  url: string | null;
  certificateId?: string;
  certificateNumber?: string;
  templateId?: string;
  enrollmentId?: string;
  userId?: string;
}

export interface CertificateGenerationOptions {
  templateId?: string | null;
  enrollmentId?: string | null;
}

export interface CertificateGenerationResult {
  success: boolean;
  certificateId?: string;
  certificateUrl: string | null;
  error?: string;
}

export interface CertificateCompletionResult {
  showCertificateNotification: boolean;
  eligibility: {
    eligible?: boolean;
    reason?: string;
    courseId?: string;
    courseTitle?: string;
    completedAt?: Date;
    finalGrade?: number;
    templateId?: string;
    enrollmentId?: string;
    userId?: string;
    certificateId?: string;
    certificateNumber?: string;
    certificateUrl?: string | null;
  };
}

const CERTIFICATE_CONTEXT_ERROR = 'Certificate generation requires a completed enrollment and active certificate template.';

async function parseError(response: Response): Promise<string> {
  try {
    const data = await response.json();
    if (typeof data?.error === 'string') return data.error;
    if (typeof data?.message === 'string') return data.message;
    if (typeof data?.title === 'string') return data.title;
  } catch {
    // Fall through to status text.
  }

  return response.statusText || 'Certificate request failed.';
}

function buildCertificateUrl(certificateNumber: unknown, certificateId: unknown): string | null {
  if (typeof certificateNumber === 'string' && certificateNumber.length > 0) {
    return `/certificates/${certificateNumber}`;
  }

  if (typeof certificateId === 'string' && certificateId.length > 0) {
    return `/certificates/${certificateId}`;
  }

  return null;
}

export async function generateCertificate(
  courseId: string,
  userId: string,
  options: CertificateGenerationOptions = {},
): Promise<CertificateGenerationResult> {
  const learnerId = userId.trim();
  const templateId = options.templateId?.trim();
  const enrollmentId = options.enrollmentId?.trim();

  if (!learnerId || !templateId || !enrollmentId) {
    return {
      success: false,
      error: CERTIFICATE_CONTEXT_ERROR,
      certificateUrl: null,
    };
  }

  const response = await fetch('/api/courses/certificates', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      courseId,
      userId: learnerId,
      templateId,
      enrollmentId,
    }),
  });

  if (!response.ok) {
    return {
      success: false,
      error: await parseError(response),
      certificateUrl: null,
    };
  }

  const data = await response.json();
  const certificateId = typeof data?.id === 'string' ? data.id : undefined;

  return {
    success: true,
    certificateId,
    certificateUrl: buildCertificateUrl(data?.certificateNumber, certificateId),
  };
}

export async function getCertificateById(certificateId: string) {
  const response = await fetch(`/api/courses/certificates?mode=certificate&certificateId=${encodeURIComponent(certificateId)}`, {
    method: 'GET',
  });

  if (!response.ok) {
    return { data: null, error: await parseError(response) };
  }

  return { data: await response.json(), error: null };
}

export async function verifyCertificate(certificateNumber: string) {
  const response = await fetch(`/api/courses/certificates?mode=verify&certificateNumber=${encodeURIComponent(certificateNumber)}`, {
    method: 'GET',
  });

  if (!response.ok) {
    return { valid: false, error: await parseError(response) };
  }

  const data = await response.json();
  return { valid: Boolean(data?.isValid ?? data?.valid), data };
}

export async function downloadCertificate(certificateId: string) {
  const certificate = await getCertificateById(certificateId);
  if (certificate.error || !certificate.data) {
    return { url: null, error: certificate.error ?? 'Certificate not found.' };
  }

  return {
    url: buildCertificateUrl(certificate.data.certificateNumber, certificate.data.id),
    error: null,
  };
}

export async function getCertificateStatus(courseId: string): Promise<CertificateStatus> {
  const response = await fetch(assertSafeRemoteUrl(`/api/courses/certificates?mode=status&courseId=${encodeURIComponent(courseId)}`), {
    redirect: 'error',
    method: 'GET',
  });

  if (!response.ok) {
    return { eligible: false, generated: false, url: null };
  }

  const data = await response.json();
  return {
    eligible: Boolean(data?.eligible),
    generated: Boolean(data?.generated),
    url: typeof data?.url === 'string' ? data.url : null,
    certificateId: typeof data?.certificateId === 'string' ? data.certificateId : undefined,
    certificateNumber: typeof data?.certificateNumber === 'string' ? data.certificateNumber : undefined,
    templateId: typeof data?.templateId === 'string' ? data.templateId : undefined,
    enrollmentId: typeof data?.enrollmentId === 'string' ? data.enrollmentId : undefined,
    userId: typeof data?.userId === 'string' ? data.userId : undefined,
  };
}

export async function handleCourseCompletion(
  courseId: string,
  courseData: { title?: string; overallProgress?: number; finalGrade?: number; score?: number },
  _studentName: string,
): Promise<CertificateCompletionResult> {
  if ((courseData.overallProgress ?? 0) < 100) {
    return {
      showCertificateNotification: false,
      eligibility: { eligible: false, reason: 'Course is not complete yet.' },
    };
  }

  const status = await getCertificateStatus(courseId);
  if (!status.eligible) {
    return {
      showCertificateNotification: false,
      eligibility: { eligible: false, reason: 'No active certificate template is available for this course.' },
    };
  }

  const eligibility: CertificateCompletionResult['eligibility'] = {
    courseId,
    courseTitle: courseData.title ?? 'Course',
    completedAt: new Date(),
    finalGrade: courseData.finalGrade ?? courseData.score,
  };

  if (status.templateId) eligibility.templateId = status.templateId;
  if (status.enrollmentId) eligibility.enrollmentId = status.enrollmentId;
  if (status.userId) eligibility.userId = status.userId;
  if (status.certificateId) eligibility.certificateId = status.certificateId;
  if (status.certificateNumber) eligibility.certificateNumber = status.certificateNumber;
  if (status.url) eligibility.certificateUrl = status.url;

  return {
    showCertificateNotification: true,
    eligibility,
  };
}

export const CourseCompletionCertificateService = {
  generateCertificate,
  getCertificateById,
  verifyCertificate,
  downloadCertificate,
  getCertificateStatus,
  generate: generateCertificate,
  check: getCertificateStatus,
  handleCourseCompletion,
};

export const certificateService = CourseCompletionCertificateService;

export default certificateService;
