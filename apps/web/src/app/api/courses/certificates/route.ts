import { getToken } from '@/auth';
import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';
import { NextRequest, NextResponse } from 'next/server';

const DEFAULT_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

function getApiUrl(): string {
  return DEFAULT_API_URL.replace(/\/$/, '');
}

function jsonError(message: string, status: number): NextResponse {
  return NextResponse.json({ error: message }, { status });
}

async function getBackendError(response: Response): Promise<string> {
  try {
    const body = await response.json();
    if (typeof body?.title === 'string') return body.title;
    if (typeof body?.error === 'string') return body.error;
    if (typeof body?.message === 'string') return body.message;
  } catch {
    // Use the HTTP status text below.
  }

  return response.statusText || 'Backend request failed.';
}

async function getAuthHeader(): Promise<Record<string, string> | NextResponse> {
  const token = await getToken();
  if (!token) {
    return jsonError('You must be signed in to manage certificates.', 401);
  }

  return { Authorization: `Bearer ${token}` };
}

async function getJson<T>(path: string, authHeader?: Record<string, string>): Promise<T | null> {
  const response = await fetch(assertSafeServiceUrl(`${getApiUrl()}${path}`, getApiUrl()), {
    redirect: 'error',
    method: 'GET',
    headers: authHeader,
    cache: 'no-store',
  });

  if (response.status === 404 || response.status === 403 || response.status === 401) {
    return null;
  }

  if (!response.ok) {
    throw new Error(await getBackendError(response));
  }

  return (await response.json()) as T;
}

interface CertificateTemplateDto {
  id?: string;
  isActive?: boolean;
}

interface CertificateDto {
  id?: string;
  templateId?: string;
  enrollmentId?: string;
  userId?: string;
  courseId?: string;
  certificateNumber?: string;
  status?: string;
}

interface UserProgressDto {
  enrollmentId?: string;
  courseId?: string;
  userId?: string;
  completionPercentage?: number;
  completedAt?: string | null;
}

function toCertificateUrl(certificate: CertificateDto | undefined): string | null {
  if (!certificate) return null;
  if (certificate.certificateNumber) return `/certificates/${certificate.certificateNumber}`;
  if (certificate.id) return `/certificates/${certificate.id}`;
  return null;
}

async function getStatus(request: NextRequest): Promise<NextResponse> {
  const authHeader = await getAuthHeader();
  if (authHeader instanceof NextResponse) return authHeader;

  const courseId = request.nextUrl.searchParams.get('courseId')?.trim();
  if (!courseId) {
    return jsonError('A course id is required.', 400);
  }

  const [templates, certificates] = await Promise.all([
    getJson<CertificateTemplateDto[]>(`/api/certificates/templates/course/${encodeURIComponent(courseId)}`, authHeader),
    getJson<CertificateDto[]>('/api/certificates/my', authHeader),
  ]);
  const progress = await getJson<UserProgressDto>(`/v1/courses/${encodeURIComponent(courseId)}/me/progress`, authHeader);

  const activeTemplate = (templates ?? []).find((template) => template.isActive !== false);
  const certificate = (certificates ?? []).find(
    (candidate) => candidate.courseId === courseId && candidate.status?.toLowerCase() !== 'revoked',
  );

  return NextResponse.json({
    eligible: Boolean(activeTemplate),
    generated: Boolean(certificate),
    templateId: activeTemplate?.id ?? null,
    enrollmentId: certificate?.enrollmentId ?? progress?.enrollmentId ?? null,
    userId: certificate?.userId ?? progress?.userId ?? null,
    certificateId: certificate?.id ?? null,
    certificateNumber: certificate?.certificateNumber ?? null,
    url: toCertificateUrl(certificate),
  });
}

async function getCertificate(request: NextRequest): Promise<NextResponse> {
  const authHeader = await getAuthHeader();
  if (authHeader instanceof NextResponse) return authHeader;

  const certificateId = request.nextUrl.searchParams.get('certificateId')?.trim();
  if (!certificateId) {
    return jsonError('A certificate id is required.', 400);
  }

  const certificate = await getJson<CertificateDto>(`/api/certificates/${encodeURIComponent(certificateId)}`, authHeader);
  if (!certificate) {
    return jsonError('Certificate not found.', 404);
  }

  return NextResponse.json(certificate);
}

async function verifyCertificate(request: NextRequest): Promise<NextResponse> {
  const certificateNumber = request.nextUrl.searchParams.get('certificateNumber')?.trim();
  if (!certificateNumber) {
    return jsonError('A certificate number is required.', 400);
  }

  const verification = await getJson(`/api/certificates/verify/${encodeURIComponent(certificateNumber)}`);
  if (!verification) {
    return jsonError('Certificate could not be verified.', 404);
  }

  return NextResponse.json(verification);
}

export async function GET(request: NextRequest): Promise<NextResponse> {
  try {
    const mode = request.nextUrl.searchParams.get('mode') ?? 'status';
    if (mode === 'verify') return verifyCertificate(request);
    if (mode === 'certificate') return getCertificate(request);
    return getStatus(request);
  } catch (error) {
    return jsonError(error instanceof Error ? error.message : 'Certificate request failed.', 502);
  }
}

export async function POST(request: NextRequest): Promise<NextResponse> {
  const authHeader = await getAuthHeader();
  if (authHeader instanceof NextResponse) return authHeader;

  const body = await request.json();
  const templateId = typeof body?.templateId === 'string' ? body.templateId.trim() : '';
  const enrollmentId = typeof body?.enrollmentId === 'string' ? body.enrollmentId.trim() : '';
  const userId = typeof body?.userId === 'string' ? body.userId.trim() : '';
  const courseId = typeof body?.courseId === 'string' ? body.courseId.trim() : '';

  if (!templateId || !enrollmentId || !userId || !courseId) {
    return jsonError('templateId, enrollmentId, userId, and courseId are required to issue a certificate.', 400);
  }

  const response = await fetch(assertSafeServiceUrl(`${getApiUrl()}/api/certificates/issue`, getApiUrl()), {
    redirect: 'error',
    method: 'POST',
    headers: {
      ...authHeader,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ templateId, enrollmentId, userId, courseId }),
    cache: 'no-store',
  });

  if (!response.ok) {
    return jsonError(await getBackendError(response), response.status);
  }

  return NextResponse.json(await response.json(), { status: response.status });
}
