import { auth, getToken } from '@/auth';
import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';
import { cache } from 'react';

export type LaunchPlanStatus = 'Draft' | 'Preparing' | 'Ready' | 'Launched' | 'Paused' | number;

export interface LaunchChecklistItem {
  id: string;
  title: string;
  category: string;
  isRequired: boolean;
  isComplete: boolean;
  completedAt?: string | null;
}

export interface LaunchProjectSummary {
  id: string;
  title?: string | null;
  name?: string | null;
  slug?: string | null;
  status?: string | number | null;
  visibility?: string | number | null;
}

export interface LaunchPlan {
  id: string;
  projectId: string;
  project?: LaunchProjectSummary | null;
  name: string;
  positioning?: string | null;
  targetLaunchAt?: string | null;
  launchedAt?: string | null;
  status: LaunchPlanStatus;
  readinessPercent?: number;
  channels?: string[] | null;
  checklistItems?: LaunchChecklistItem[] | null;
}

export interface LaunchProjectOption {
  id: string;
  title: string;
  slug?: string | null;
  status?: string | number | null;
}

async function launchPadApiGet<T>(path: string, revalidate = 30): Promise<T | null> {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const token = await getToken();
  const tenantId = (await auth().catch(() => null))?.tenantId;
  // SSRF (Codacy rule-node-ssrf) — false positive: the target origin is pinned
  // to the env-configured API base by assertSafeServiceUrl; request data only
  // becomes encoded path segments and can never select another origin.
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}${path}`, apiUrl), {
    redirect: 'error',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
    },
    next: { revalidate },
  });

  if (response.status === 401 || response.status === 403 || response.status === 404) return null;

  if (!response.ok) {
    console.error(`[launchPadApiGet] ${path} failed with ${response.status}`);
    return null;
  }

  return (await response.json()) as T;
}

export type LaunchPadEventStatus = 'Draft' | 'ApplicationsOpen' | 'ApplicationsClosed' | 'Scheduled' | 'Active' | 'Completed' | 'Cancelled' | 'Archived' | number;
export type LaunchPadApplicationStatus = 'Draft' | 'Submitted' | 'UnderReview' | 'Waitlisted' | 'Approved' | 'Rejected' | 'Withdrawn' | number;
export type LaunchPadParticipantStatus = 'Registered' | 'Waitlisted' | 'CheckedIn' | 'Attended' | 'Completed' | 'Cancelled' | 'NoShow' | number;

export interface LaunchPadEvent {
  id: string;
  name: string;
  description?: string | null;
  startsAt: string;
  endsAt: string;
  status: LaunchPadEventStatus;
  applicationsOpenAt?: string | null;
  applicationsCloseAt?: string | null;
}

export interface LaunchPadSlot {
  id: string;
  eventId: string;
  name: string;
  role: string | number;
  capacity: number;
  reservedCount: number;
  startsAt: string;
  endsAt: string;
}

export interface LaunchPadEventDetail {
  event: LaunchPadEvent;
  slots: LaunchPadSlot[];
}

export interface LaunchPadApplication {
  id: string;
  eventId: string;
  projectId: string;
  projectVersionId: string;
  submittedByUserId: string;
  status: LaunchPadApplicationStatus;
  pitch?: string | null;
  submittedAt: string;
}

export interface LaunchPadRegistration {
  id: string;
  slotId: string;
  userId: string;
  status: LaunchPadParticipantStatus;
  registeredAt: string;
}

export interface LaunchPadAnalytics {
  events: number;
  completedEvents: number;
  applications: number;
  approvedApplications: number;
  registrations: number;
  completedRegistrations: number;
}

export interface LaunchPadSettings {
  id?: string;
  tenantId?: string | null;
  versionSubmissionPolicy?: 'ReadyMutableUntilReview' | 'ReleasedImmutable';
}

export const getPublicLaunchPadEvents = cache(async (): Promise<LaunchPadEvent[]> =>
  (await launchPadApiGet<LaunchPadEvent[]>('/v1/launch-pad/events/public', 30)) ?? []);

export const getPublicLaunchPadEvent = cache(async (eventId: string): Promise<LaunchPadEventDetail | null> =>
  launchPadApiGet<LaunchPadEventDetail>(`/v1/launch-pad/events/public/${eventId}`, 15));

export const getManagedLaunchPadEvents = cache(async (): Promise<LaunchPadEvent[]> =>
  (await launchPadApiGet<LaunchPadEvent[]>('/v1/launch-pad/events/management', 0)) ?? []);

export const getManagedLaunchPadEvent = cache(async (eventId: string): Promise<LaunchPadEventDetail | null> =>
  launchPadApiGet<LaunchPadEventDetail>(`/v1/launch-pad/events/${eventId}/management`, 0));

export const getMyLaunchPadApplications = cache(async (): Promise<LaunchPadApplication[]> =>
  (await launchPadApiGet<LaunchPadApplication[]>('/v1/launch-pad/events/applications/me', 0)) ?? []);

export const getMyLaunchPadRegistrations = cache(async (): Promise<LaunchPadRegistration[]> =>
  (await launchPadApiGet<LaunchPadRegistration[]>('/v1/launch-pad/events/registrations/me', 0)) ?? []);

export const getManagedLaunchPadApplications = cache(async (eventIds: string[]): Promise<LaunchPadApplication[]> =>
  (await Promise.all(eventIds.map((eventId) => launchPadApiGet<LaunchPadApplication[]>(`/v1/launch-pad/events/${eventId}/applications/management`, 0))))
    .flatMap((items) => items ?? []));

export const getManagedLaunchPadRegistrations = cache(async (eventIds: string[]): Promise<LaunchPadRegistration[]> =>
  (await Promise.all(eventIds.map((eventId) => launchPadApiGet<LaunchPadRegistration[]>(`/v1/launch-pad/events/${eventId}/registrations/management`, 0))))
    .flatMap((items) => items ?? []));

export const getLaunchPadAnalytics = cache(async (): Promise<LaunchPadAnalytics | null> =>
  launchPadApiGet<LaunchPadAnalytics>('/v1/launch-pad/events/analytics', 30));

export const getLaunchPadSettings = cache(async (): Promise<LaunchPadSettings | null> =>
  launchPadApiGet<LaunchPadSettings>('/v1/launch-pad/settings', 0));

export const getLaunchPadDashboard = cache(async (): Promise<LaunchPlan[]> => {
  return (await launchPadApiGet<LaunchPlan[]>('/v1/launch-pad', 30)) ?? [];
});

export const getLaunchProjectOptions = cache(async (): Promise<LaunchProjectOption[]> => {
  const projects = await launchPadApiGet<LaunchProjectSummary[]>('/v1/projects?take=50&sortBy=UpdatedAt&sortDirection=DESC', 60);

  return (projects ?? []).map((project) => ({
    id: project.id,
    title: project.title ?? project.name ?? project.slug ?? project.id,
    slug: project.slug,
    status: project.status,
  }));
});

export function normalizeLaunchStatus(status: LaunchPlanStatus): string {
  if (typeof status === 'string') return status;

  return ['Draft', 'Preparing', 'Ready', 'Launched', 'Paused'][status] ?? 'Preparing';
}

export function getPlanReadiness(plan: LaunchPlan): number {
  if (typeof plan.readinessPercent === 'number') return Math.max(0, Math.min(100, Math.round(plan.readinessPercent)));

  const items = plan.checklistItems ?? [];
  if (items.length === 0) return 0;

  return Math.round((items.filter((item) => item.isComplete).length / items.length) * 100);
}
