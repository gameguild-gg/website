import { assertSafeRemoteUrl } from '@/lib/security/safe-remote-url';

export interface ContentReport {
  id: string;
  contentType: string;
  contentId: string;
  contentTitle?: string;
  reportType?: string;
  reason: string;
  description?: string;
  status: 'pending' | 'reviewed' | 'resolved' | 'dismissed';
  createdAt: string;
  userId: string;
}

type ReportPayload = Omit<ContentReport, 'id' | 'status' | 'createdAt' | 'userId'> & {
  userId?: string;
};

interface ContentReportSuccess {
  success: true;
  message: string;
  reportId?: string;
}

interface ContentReportFailure {
  success: false;
  error: string;
}

type ContentReportResult = ContentReportSuccess | ContentReportFailure;

async function parseError(response: Response): Promise<string> {
  try {
    const data = await response.json();
    if (typeof data?.error === 'string') return data.error;
    if (typeof data?.message === 'string') return data.message;
    if (typeof data?.title === 'string') return data.title;
  } catch {
    // Ignore JSON parse failures and use the status line below.
  }

  return response.statusText || 'Unable to submit content report.';
}

export async function reportContent(data: ReportPayload): Promise<ContentReportResult> {
  const response = await fetch('/api/courses/content-reports', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      contentType: data.contentType,
      contentId: data.contentId,
      contentTitle: data.contentTitle,
      reportType: data.reportType,
      reason: data.reason,
      description: data.description,
    }),
  });

  if (!response.ok) {
    return { success: false, error: await parseError(response) };
  }

  const result = await response.json();
  return {
    success: true,
    message: 'Report submitted for moderation.',
    reportId: typeof result?.id === 'string' ? result.id : typeof result?.reportId === 'string' ? result.reportId : undefined,
  };
}

export async function getContentReports(contentType: string, contentId: string) {
  const query = new URLSearchParams({ contentType, contentId });
  const response = await fetch(assertSafeRemoteUrl(`/api/courses/content-reports?${query.toString()}`), {
    redirect: 'error',
    method: 'GET',
  });

  if (!response.ok) {
    return { data: [] as ContentReport[], error: await parseError(response) };
  }

  const data = await response.json();
  return { data: Array.isArray(data) ? data as ContentReport[] : [], error: null };
}

export async function updateReportStatus(reportId: string, status: ContentReport['status']) {
  const response = await fetch('/api/courses/content-reports', {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reportId, status }),
  });

  if (!response.ok) {
    return { success: false, error: await parseError(response) };
  }

  return { success: true };
}

export const ContentReportService = {
  reportContent,
  getContentReports,
  updateReportStatus,
  report: reportContent,
  getReports: getContentReports,
  updateStatus: updateReportStatus,
  createReport: reportContent,
};

export const contentReportService = ContentReportService;

export default contentReportService;
