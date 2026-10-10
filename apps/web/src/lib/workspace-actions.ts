'use server';

import { auth, getToken } from '@/auth';
import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';
import { createServerClient } from '@game-guild/client';
import { revalidatePath } from 'next/cache';
import { redirect as nextRedirect } from 'next/navigation';
import { getLocale } from 'next-intl/server';
import { getPathname, redirect as localizedRedirect } from '@/i18n/navigation';

type WorkspaceMethod = 'POST' | 'PUT' | 'DELETE';

async function request<T>(method: WorkspaceMethod, path: string, body?: unknown) {
  const session = await auth().catch(() => null);
  const client = createServerClient({
    baseUrl: process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080',
    auth: { getAccessToken: () => getToken() },
    tenant: { getTenantId: async () => session?.tenantId ?? null },
  });
  const result = await client.request<T>({ method, path, body, requiresAuth: true });
  if (!result.ok) {
    throw new Error(`Workspace request failed (${result.error.status}): ${result.error.message}`);
  }
  return result.data;
}

const text = (data: FormData, key: string) => String(data.get(key) ?? '').trim();

function isoDate(data: FormData, key: string): string | null {
  const raw = text(data, key);
  if (!raw) return null;
  const wallClock = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?$/.test(raw)
    ? `${raw}Z`
    : raw;
  const value = new Date(wallClock);
  return Number.isNaN(value.valueOf()) ? null : value.toISOString();
}

function projectTaskPriority(data: FormData): string {
  const value = text(data, 'priority');
  if (value === 'Medium') return 'Normal';
  if (value === 'Critical') return 'Urgent';
  return value || 'Normal';
}

export async function createTeamForm(data: FormData): Promise<void> {
  const name = text(data, 'name');
  const slug = (text(data, 'slug') || name)
    .normalize('NFKD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  if (!name || !slug) return;
  const result = await request<{ id: string; slug: string }>('POST', '/v1/teams', {
    name,
    slug,
    visibility: text(data, 'visibility') || 'Private',
    description: text(data, 'description') || null,
    ownerUserId: text(data, 'ownerUserId') || null,
  });
  const management = text(data, 'surface') === 'admin';
  const locale = await getLocale();
  const listPath = management ? '/console/community/teams' : '/workspace/teams';
  const destination = management ? `/console/community/teams/${result.id}` : `/workspace/teams/${result.slug}`;
  revalidatePath(getPathname({ href: listPath, locale }));
  localizedRedirect({ href: destination, locale });
}

export async function createProjectForm(data: FormData): Promise<void> {
  const title = text(data, 'title');
  if (!title) return;
  const ownerTeamId = text(data, 'ownerTeamId');
  const result = await request<{ id: string; slug: string }>('POST', '/v1/projects', {
    title, description: text(data, 'description') || null, shortDescription: text(data, 'shortDescription') || null,
    type: text(data, 'type') || 'Game', visibility: text(data, 'visibility') || 'Private', status: 'Draft',
    ownerTeamId: ownerTeamId || null,
  });
  const management = text(data, 'surface') === 'admin';
  const locale = await getLocale();
  const listPath = management ? '/console/community/projects' : '/workspace/projects';
  const destination = management ? `/console/community/projects/${result.id}` : `/workspace/projects/${result.slug}`;
  revalidatePath(getPathname({ href: listPath, locale }));
  localizedRedirect({ href: destination, locale });
}

export async function createProjectVersionForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const versionNumber = text(data, 'versionNumber');
  if (!projectId || !versionNumber) return;
  await request('POST', `/v1/projects/${projectId}/versions`, { versionNumber, status: text(data, 'status') || 'draft', releaseNotes: text(data, 'releaseNotes') || null });
  revalidatePath(text(data, 'returnPath'));
}

export async function transitionProjectVersionForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const versionId = text(data, 'versionId');
  const action = text(data, 'versionAction');
  if (!projectId || !versionId || !['ready', 'release', 'archive'].includes(action)) return;
  await request('POST', `/v1/projects/${projectId}/versions/${versionId}:${action}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function updateTeamForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  const name = text(data, 'name');
  const slug = text(data, 'slug').toLowerCase();
  if (!teamId || !name || !slug) return;
  await request('PUT', `/v1/teams/${teamId}`, {
    name,
    slug,
    visibility: text(data, 'visibility') || 'Private',
    description: text(data, 'description') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function addTeamMemberForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  const userId = text(data, 'userId');
  if (!teamId || !userId) return;
  await request('POST', `/v1/teams/${teamId}/members`, {
    userId,
    authority: text(data, 'authority') || 'Member',
    professionalTitle: text(data, 'professionalTitle') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function changeTeamMemberForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  const userId = text(data, 'userId');
  if (!teamId || !userId) return;
  await request('PUT', `/v1/teams/${teamId}/members/${userId}`, {
    authority: text(data, 'authority') || 'Member',
    professionalTitle: text(data, 'professionalTitle') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function removeTeamMemberForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  const userId = text(data, 'userId');
  if (!teamId || !userId) return;
  await request('DELETE', `/v1/teams/${teamId}/members/${userId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function createTeamInvitationForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  if (!teamId) return;
  await request('POST', `/v1/teams/${teamId}/invitations`, {
    userId: text(data, 'userId') || null,
    email: text(data, 'email') || null,
    authority: text(data, 'authority') || 'Member',
    expiresAt: isoDate(data, 'expiresAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function revokeTeamInvitationForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  const invitationId = text(data, 'invitationId');
  if (!teamId || !invitationId) return;
  await request('DELETE', `/v1/teams/${teamId}/invitations/${invitationId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function acceptTeamInvitationForm(data: FormData): Promise<void> {
  const invitationId = text(data, 'invitationId');
  if (!invitationId) return;
  await request('POST', `/v1/teams/invitations/${invitationId}:accept`);
  revalidatePath('/invitations');
  revalidatePath('/workspace/projects');
}

export async function updateProjectForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  if (!projectId) return;
  await request('PUT', `/v1/projects/${projectId}`, {
    title: text(data, 'title') || null,
    description: text(data, 'description') || null,
    shortDescription: text(data, 'shortDescription') || null,
    visibility: text(data, 'visibility') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function updateProjectDeliverableUrlForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const downloadUrl = text(data, 'downloadUrl');
  if (!projectId) return;
  if (downloadUrl) {
    const parsed = new URL(downloadUrl);
    if (!['http:', 'https:'].includes(parsed.protocol)) throw new Error('Project deliverable URL must use HTTP or HTTPS.');
  }
  await request('PUT', `/v1/projects/${projectId}`, { downloadUrl });
  revalidatePath(text(data, 'returnPath'));
}

export async function addProjectCollaboratorForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const userId = text(data, 'userId');
  if (!projectId || !userId) return;
  await request('POST', `/v1/projects/${projectId}/collaborators`, {
    userId,
    role: text(data, 'role') || 'Viewer',
    permissions: text(data, 'permissions') || 'Read',
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function removeProjectCollaboratorForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const collaboratorId = text(data, 'collaboratorId');
  if (!projectId || !collaboratorId) return;
  await request('DELETE', `/v1/projects/${projectId}/collaborators/${collaboratorId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function createProjectTaskForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const columnId = text(data, 'columnId');
  const title = text(data, 'title');
  if (!projectId || !columnId || !title) return;
  await request('POST', `/v1/projects/${projectId}/work/tasks`, {
    columnId,
    title,
    description: text(data, 'description') || null,
    priority: projectTaskPriority(data),
    assigneeUserId: text(data, 'assigneeUserId') || null,
    milestoneId: null,
    dueAt: isoDate(data, 'dueAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function moveProjectTaskForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const columnId = text(data, 'columnId');
  if (!projectId || !taskId || !columnId) return;
  await request('PUT', `/v1/projects/${projectId}/work/tasks/${taskId}/move`, {
    columnId,
    position: Number(text(data, 'position') || 0),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function createProjectMilestoneForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const name = text(data, 'name');
  if (!projectId || !name) return;
  await request('POST', `/v1/projects/${projectId}/work/milestones`, {
    name,
    description: text(data, 'description') || null,
    dueAt: isoDate(data, 'dueAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function createProjectLabelForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const name = text(data, 'name');
  if (!projectId || !name) return;
  await request('POST', `/v1/projects/${projectId}/work/labels`, {
    name,
    color: text(data, 'color') || '#64748b',
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function addProjectTaskDependencyForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const dependsOnTaskId = text(data, 'dependsOnTaskId');
  if (!projectId || !taskId || !dependsOnTaskId) return;
  await request('POST', `/v1/projects/${projectId}/work/tasks/${taskId}/dependencies`, { dependsOnTaskId });
  revalidatePath(text(data, 'returnPath'));
}

export async function addProjectTaskCommentForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const body = text(data, 'body');
  if (!projectId || !taskId || !body) return;
  await request('POST', `/v1/projects/${projectId}/work/tasks/${taskId}/comments`, { body });
  revalidatePath(text(data, 'returnPath'));
}

export async function addProjectTaskChecklistForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const itemText = text(data, 'itemText');
  if (!projectId || !taskId || !itemText) return;
  await request('POST', `/v1/projects/${projectId}/work/tasks/${taskId}/checklist`, { text: itemText });
  revalidatePath(text(data, 'returnPath'));
}

export async function setProjectTaskChecklistForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const itemId = text(data, 'itemId');
  if (!projectId || !taskId || !itemId) return;
  await request('PUT', `/v1/projects/${projectId}/work/tasks/${taskId}/checklist/${itemId}`, {
    isCompleted: text(data, 'isCompleted') === 'true',
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function assignProjectTaskLabelForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const taskId = text(data, 'taskId');
  const labelId = text(data, 'labelId');
  if (!projectId || !taskId || !labelId) return;
  await request('POST', `/v1/projects/${projectId}/work/tasks/${taskId}/labels/${labelId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function createWorkspaceFolderForm(data: FormData): Promise<void> {
  const resourceType = text(data, 'resourceType');
  const resourceId = text(data, 'resourceId');
  const name = text(data, 'name');
  if (!resourceType || !resourceId || !name) return;
  await request('POST', `/v1/asset-libraries/${resourceType}/${resourceId}/folders`, {
    name,
    parentFolderId: text(data, 'parentFolderId') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function addProjectTeamForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const teamId = text(data, 'teamId');
  if (!projectId || !teamId) return;
  await request('POST', `/v1/projects/${projectId}/ownership/teams`, {
    teamId,
    role: text(data, 'role') || 'Contributor',
    participationMode: text(data, 'participationMode') || 'SelectedMembers',
    permissions: ['Read'],
    notes: text(data, 'notes') || null,
    contributionPercentage: Number(text(data, 'contributionPercentage') || 0),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function createProjectAllocationForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const projectTeamId = text(data, 'projectTeamId');
  const userId = text(data, 'userId');
  if (!projectId || !projectTeamId || !userId) return;
  await request('POST', `/v1/projects/${projectId}/ownership/allocations`, {
    projectTeamId,
    userId,
    function: text(data, 'function'),
    capacityPercentage: Number(text(data, 'capacityPercentage') || 100),
    startsAt: isoDate(data, 'startsAt'),
    endsAt: isoDate(data, 'endsAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function createProjectAgreementForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  if (!projectId) return;
  await request('POST', `/v1/projects/${projectId}/ownership/agreements`, {
    proposingTeamId: text(data, 'proposingTeamId'),
    receivingTeamId: text(data, 'receivingTeamId'),
    scope: text(data, 'scope'),
    deliverables: text(data, 'deliverables'),
    startsAt: isoDate(data, 'startsAt'),
    endsAt: isoDate(data, 'endsAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function changeProjectAgreementForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const agreementId = text(data, 'agreementId');
  const action = text(data, 'agreementAction');
  if (!projectId || !agreementId || !['accept', 'cancel', 'complete'].includes(action)) return;
  await request('POST', `/v1/projects/${projectId}/ownership/agreements/${agreementId}/${action}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function counterProjectAgreementForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const agreementId = text(data, 'agreementId');
  if (!projectId || !agreementId) return;
  await request('POST', `/v1/projects/${projectId}/ownership/agreements/${agreementId}/counter`, {
    scope: text(data, 'scope'),
    deliverables: text(data, 'deliverables'),
    startsAt: isoDate(data, 'startsAt'),
    endsAt: isoDate(data, 'endsAt'),
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function transferProjectOwnerTeamForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const teamId = text(data, 'teamId');
  if (!projectId || !teamId) return;
  await request('POST', `/v1/projects/${projectId}/ownership/owner-team`, { teamId });
  revalidatePath(text(data, 'returnPath'));
}

export async function removeProjectTeamForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const projectTeamId = text(data, 'projectTeamId');
  if (!projectId || !projectTeamId) return;
  await request('DELETE', `/v1/projects/${projectId}/ownership/teams/${projectTeamId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function removeProjectAllocationForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const allocationId = text(data, 'allocationId');
  if (!projectId || !allocationId) return;
  await request('DELETE', `/v1/projects/${projectId}/ownership/allocations/${allocationId}`);
  revalidatePath(text(data, 'returnPath'));
}

export async function transitionProjectForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  const action = text(data, 'projectAction');
  if (!projectId || !['publish', 'unpublish', 'archive', 'restore'].includes(action)) return;
  await request('POST', `/v1/projects/${projectId}:${action}`);
  revalidatePath(text(data, 'returnPath'));
  revalidatePath('/workspace/projects');
}

export async function deleteProjectForm(data: FormData): Promise<void> {
  const projectId = text(data, 'projectId');
  if (!projectId) return;
  await request('DELETE', `/v1/projects/${projectId}?softDelete=true&reason=${encodeURIComponent(text(data, 'reason') || 'Deleted from Project settings')}`);
  const returnPath = text(data, 'returnPath') || '/workspace/projects';
  revalidatePath(returnPath);
  nextRedirect(returnPath.startsWith('/console/community/') ? '/console/community/projects' : '/workspace/projects');
}

export async function archiveTeamForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  if (!teamId) return;
  await request('DELETE', `/v1/teams/${teamId}`);
  const returnPath = text(data, 'returnPath') || '/workspace/projects';
  revalidatePath(returnPath);
  nextRedirect(returnPath.startsWith('/console/community/') ? '/console/community/teams' : '/workspace/teams');
}

export async function restoreTeamForm(data: FormData): Promise<void> {
  const teamId = text(data, 'teamId');
  if (!teamId) return;
  await request('POST', `/v1/teams/${teamId}:restore`);
  revalidatePath(text(data, 'returnPath'));
}

export async function restrictWorkspaceFolderForm(data: FormData): Promise<void> {
  const folderId = text(data, 'folderId');
  if (!folderId) return;
  const teamIds = text(data, 'teamIds').split(',').map((value) => value.trim()).filter(Boolean);
  const authorities = text(data, 'authorities').split(',').map((value) => value.trim()).filter(Boolean);
  await request('PUT', `/v1/asset-libraries/folders/${folderId}/restriction`, {
    mode: text(data, 'mode') || 'None',
    teamIds,
    authorities,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function copyWorkspaceAssetForm(data: FormData): Promise<void> {
  const referenceId = text(data, 'referenceId');
  if (!referenceId) return;
  await request('POST', `/v1/asset-libraries/assets/${referenceId}/copy`, {
    displayName: text(data, 'displayName') || null,
    folderId: text(data, 'folderId') || null,
  });
  revalidatePath(text(data, 'returnPath'));
}

export async function restoreWorkspaceAssetRevisionForm(data: FormData): Promise<void> {
  const referenceId = text(data, 'referenceId');
  const revisionId = text(data, 'revisionId');
  if (!referenceId || !revisionId) return;
  await request('POST', `/v1/asset-libraries/assets/${referenceId}/revisions/${revisionId}/restore`);
  revalidatePath(text(data, 'returnPath'));
}

export async function uploadWorkspaceAssetForm(data: FormData): Promise<void> {
  const resourceType = text(data, 'resourceType');
  const resourceId = text(data, 'resourceId');
  const file = data.get('file');
  if (!resourceType || !resourceId || !(file instanceof File) || file.size === 0) return;

  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const session = await auth().catch(() => null);
  const token = await getToken();
  const query = new URLSearchParams({
    accessPolicy: 'Inherited',
    parentResourceType: resourceType,
    parentResourceId: resourceId,
  });
  const folderId = text(data, 'folderId');
  if (folderId) query.set('folderId', folderId);
  const body = new FormData();
  body.set('file', file, file.name);
  const response = await fetch(assertSafeServiceUrl(`${apiUrl}/v1/assets?${query}`, apiUrl), {
    redirect: 'error',
    method: 'POST',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(session?.tenantId ? { 'X-Tenant-Id': session.tenantId } : {}),
    },
    body,
  });
  if (!response.ok) throw new Error(`Asset upload failed (${response.status}): ${await response.text()}`);
  revalidatePath(text(data, 'returnPath'));
}
