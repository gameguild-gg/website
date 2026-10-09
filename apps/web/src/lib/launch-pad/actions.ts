'use server';

import { auth, getToken } from '@/auth';
import type { LaunchPadApplication, LaunchPadEvent, LaunchPadRegistration, LaunchPadSettings, LaunchPadSlot, LaunchPlan } from './queries';
import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';
import { revalidatePath } from 'next/cache';

type ActionResult<T> = { success: true; data: T } | { success: false; error: string };

const defaultChecklist = [
  { title: 'Landing page copy and media approved', category: 'Storefront', isRequired: true },
  { title: 'Release build or playable package smoke tested', category: 'Quality', isRequired: true },
  { title: 'Distribution channels and launch date confirmed', category: 'Distribution', isRequired: true },
  { title: 'Support intake and known-issues process ready', category: 'Support', isRequired: true },
  { title: 'Launch metrics and post-launch review prepared', category: 'Analytics', isRequired: true },
];

async function launchPadApiRequest<T>(path: string, init: RequestInit): Promise<ActionResult<T>> {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const token = await getToken();
  const tenantId = (await auth().catch(() => null))?.tenantId;
  // SSRF (Codacy rule-node-ssrf) — false positive: the target origin is pinned
  // to the env-configured API base by assertSafeServiceUrl; request data only
  // becomes encoded path segments and can never select another origin.
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}${path}`, apiUrl), {
    ...init,
    redirect: 'error',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
      ...(init.headers ?? {}),
    },
  });

  if (!response.ok) {
    let error = response.statusText || 'Launch Pad request failed.';
    try {
      const body = await response.json() as { detail?: string; message?: string; description?: string; code?: string };
      error = body.detail || body.message || body.description || body.code || error;
    } catch {
      // Keep the HTTP status text when no JSON error payload is available.
    }

    return { success: false, error };
  }

  return { success: true, data: await response.json() as T };
}

function value(formData: FormData, key: string) {
  return String(formData.get(key) ?? '').trim();
}

function revalidateLaunchPad(eventId?: string) {
  revalidatePath('/console/community/launch-pad');
  revalidatePath('/launch-pad');
  revalidatePath('/launch-pad/events');
  revalidatePath('/launch-pad/participation');
  if (eventId) revalidatePath(`/launch-pad/events/${eventId}`);
}

export async function createLaunchPadEvent(formData: FormData): Promise<ActionResult<LaunchPadEvent>> {
  const name = value(formData, 'name');
  const startsAt = value(formData, 'startsAt');
  const endsAt = value(formData, 'endsAt');
  if (!name || !startsAt || !endsAt) return { success: false, error: 'Name, start, and end are required.' };
  const result = await launchPadApiRequest<LaunchPadEvent>('/v1/launch-pad/events', {
    method: 'POST',
    body: JSON.stringify({
      name,
      description: value(formData, 'description') || null,
      startsAt: new Date(startsAt).toISOString(),
      endsAt: new Date(endsAt).toISOString(),
      applicationsOpenAt: value(formData, 'applicationsOpenAt') ? new Date(value(formData, 'applicationsOpenAt')).toISOString() : null,
      applicationsCloseAt: value(formData, 'applicationsCloseAt') ? new Date(value(formData, 'applicationsCloseAt')).toISOString() : null,
    }),
  });
  if (result.success) revalidateLaunchPad(result.data.id);
  return result;
}

export async function transitionLaunchPadEvent(formData: FormData): Promise<ActionResult<LaunchPadEvent>> {
  const eventId = value(formData, 'eventId');
  const status = value(formData, 'status');
  if (!eventId || !status) return { success: false, error: 'Event and status are required.' };
  const result = await launchPadApiRequest<LaunchPadEvent>(`/v1/launch-pad/events/${eventId}:transition`, {
    method: 'POST', body: JSON.stringify({ status }),
  });
  if (result.success) revalidateLaunchPad(eventId);
  return result;
}

export async function createLaunchPadSlot(formData: FormData): Promise<ActionResult<LaunchPadSlot>> {
  const eventId = value(formData, 'eventId');
  const result = await launchPadApiRequest<LaunchPadSlot>(`/v1/launch-pad/events/${eventId}/slots`, {
    method: 'POST',
    body: JSON.stringify({
      name: value(formData, 'name'), role: value(formData, 'role'), capacity: Number(value(formData, 'capacity')),
      startsAt: new Date(value(formData, 'startsAt')).toISOString(), endsAt: new Date(value(formData, 'endsAt')).toISOString(),
    }),
  });
  if (result.success) revalidateLaunchPad(eventId);
  return result;
}

export async function reviewLaunchPadApplication(formData: FormData): Promise<ActionResult<LaunchPadApplication>> {
  const applicationId = value(formData, 'applicationId');
  const status = value(formData, 'status');
  const result = await launchPadApiRequest<LaunchPadApplication>(`/v1/launch-pad/events/applications/${applicationId}:review`, {
    method: 'POST',
    body: JSON.stringify({ status, launchPlanName: value(formData, 'launchPlanName') || null }),
  });
  if (result.success) revalidateLaunchPad(value(formData, 'eventId'));
  return result;
}

export async function transitionLaunchPadRegistration(formData: FormData): Promise<ActionResult<LaunchPadRegistration>> {
  const registrationId = value(formData, 'registrationId');
  const status = value(formData, 'status');
  const result = await launchPadApiRequest<LaunchPadRegistration>(`/v1/launch-pad/events/registrations/${registrationId}:transition`, {
    method: 'POST', body: JSON.stringify({ status }),
  });
  if (result.success) revalidateLaunchPad(value(formData, 'eventId'));
  return result;
}

export async function submitLaunchPadApplication(formData: FormData): Promise<ActionResult<LaunchPadApplication>> {
  const eventId = value(formData, 'eventId');
  const result = await launchPadApiRequest<LaunchPadApplication>(`/v1/launch-pad/events/${eventId}/applications`, {
    method: 'POST',
    body: JSON.stringify({ projectId: value(formData, 'projectId'), projectVersionId: value(formData, 'projectVersionId'), pitch: value(formData, 'pitch') || null }),
  });
  if (result.success) revalidateLaunchPad(eventId);
  return result;
}

export async function registerLaunchPadSlot(formData: FormData): Promise<ActionResult<LaunchPadRegistration>> {
  const eventId = value(formData, 'eventId');
  const slotId = value(formData, 'slotId');
  const result = await launchPadApiRequest<LaunchPadRegistration>(`/v1/launch-pad/events/slots/${slotId}/registrations`, { method: 'POST' });
  if (result.success) revalidateLaunchPad(eventId);
  return result;
}

export async function withdrawLaunchPadApplication(formData: FormData): Promise<ActionResult<LaunchPadApplication>> {
  const result = await launchPadApiRequest<LaunchPadApplication>(`/v1/launch-pad/events/applications/${value(formData, 'applicationId')}:withdraw`, { method: 'POST' });
  if (result.success) revalidateLaunchPad(value(formData, 'eventId'));
  return result;
}

export async function updateLaunchPadApplication(formData: FormData): Promise<ActionResult<LaunchPadApplication>> {
  const applicationId = value(formData, 'applicationId');
  const result = await launchPadApiRequest<LaunchPadApplication>(`/v1/launch-pad/events/applications/${applicationId}`, {
    method: 'PUT',
    body: JSON.stringify({ projectVersionId: value(formData, 'projectVersionId'), pitch: value(formData, 'pitch') || null }),
  });
  if (result.success) revalidateLaunchPad(value(formData, 'eventId'));
  return result;
}

export async function cancelLaunchPadRegistration(formData: FormData): Promise<ActionResult<LaunchPadRegistration>> {
  const result = await launchPadApiRequest<LaunchPadRegistration>(`/v1/launch-pad/events/registrations/${value(formData, 'registrationId')}:cancel`, { method: 'POST' });
  if (result.success) revalidateLaunchPad(value(formData, 'eventId'));
  return result;
}

export async function createLaunchPadEventForm(formData: FormData): Promise<void> { await createLaunchPadEvent(formData); }
export async function transitionLaunchPadEventForm(formData: FormData): Promise<void> { await transitionLaunchPadEvent(formData); }
export async function createLaunchPadSlotForm(formData: FormData): Promise<void> { await createLaunchPadSlot(formData); }
export async function reviewLaunchPadApplicationForm(formData: FormData): Promise<void> { await reviewLaunchPadApplication(formData); }
export async function transitionLaunchPadRegistrationForm(formData: FormData): Promise<void> { await transitionLaunchPadRegistration(formData); }
export async function submitLaunchPadApplicationForm(formData: FormData): Promise<void> { await submitLaunchPadApplication(formData); }
export async function registerLaunchPadSlotForm(formData: FormData): Promise<void> { await registerLaunchPadSlot(formData); }
export async function withdrawLaunchPadApplicationForm(formData: FormData): Promise<void> { await withdrawLaunchPadApplication(formData); }
export async function updateLaunchPadApplicationForm(formData: FormData): Promise<void> { await updateLaunchPadApplication(formData); }
export async function cancelLaunchPadRegistrationForm(formData: FormData): Promise<void> { await cancelLaunchPadRegistration(formData); }

function parseChecklist(raw: string) {
  const parsed = raw
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [category, ...titleParts] = line.includes(':') ? line.split(':') : ['Readiness', line];
      return {
        category: category.trim() || 'Readiness',
        title: titleParts.join(':').trim() || line,
        isRequired: true,
      };
    });

  return parsed.length > 0 ? parsed : defaultChecklist;
}

export async function createLaunchPlan(formData: FormData): Promise<void> {
  const projectId = String(formData.get('projectId') ?? '').trim();
  const name = String(formData.get('name') ?? '').trim();
  const positioning = String(formData.get('positioning') ?? '').trim();
  const targetLaunchAt = String(formData.get('targetLaunchAt') ?? '').trim();
  const channels = formData.getAll('channels').map(String).map((channel) => channel.trim()).filter(Boolean);
  const checklist = parseChecklist(String(formData.get('checklist') ?? ''));

  if (!projectId || !name) return;

  const result = await launchPadApiRequest<LaunchPlan>('/v1/launch-pad', {
    method: 'POST',
    body: JSON.stringify({
      projectId,
      name,
      positioning: positioning || null,
      targetLaunchAt: targetLaunchAt ? new Date(targetLaunchAt).toISOString() : null,
      channels,
      checklistItems: checklist,
    }),
  });

  if (result.success) revalidatePath('/console/community/launch-pad');
}

export async function completeLaunchChecklistItem(formData: FormData): Promise<void> {
  const planId = String(formData.get('planId') ?? '').trim();
  const itemId = String(formData.get('itemId') ?? '').trim();
  if (!planId || !itemId) return;

  const result = await launchPadApiRequest<LaunchPlan>(`/v1/launch-pad/${planId}/checklist/${itemId}:complete`, {
    method: 'POST',
    body: JSON.stringify({}),
  });

  if (result.success) revalidatePath('/console/community/launch-pad');
}

export async function publishLaunchPlan(formData: FormData): Promise<void> {
  const planId = String(formData.get('planId') ?? '').trim();
  if (!planId) return;

  const result = await launchPadApiRequest<LaunchPlan>(`/v1/launch-pad/${planId}:publish`, {
    method: 'POST',
    body: JSON.stringify({}),
  });

  if (result.success) revalidatePath('/console/community/launch-pad');
}

export async function updateLaunchPadSettings(formData: FormData): Promise<ActionResult<LaunchPadSettings>> {
  const versionSubmissionPolicy = value(formData, 'versionSubmissionPolicy');
  if (!['ReadyMutableUntilReview', 'ReleasedImmutable'].includes(versionSubmissionPolicy))
    return { success: false, error: 'Choose a valid project version policy.' };
  const result = await launchPadApiRequest<LaunchPadSettings>('/v1/launch-pad/settings', {
    method: 'PUT',
    body: JSON.stringify({ versionSubmissionPolicy }),
  });
  if (result.success) revalidatePath('/console/community/launch-pad/settings');
  return result;
}
