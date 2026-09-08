'use server';

import { getRequestAuthContext } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type ApiError,
  type Result,
  type TestingLabAttendanceStatus,
  type TestingLabFeedbackQuality,
  type TestingLabInstructionType,
  type TestingLabLocationStatus,
  type TestingLabRegistrationType,
  type TestingLabSessionStatus,
  type TestingLabTestingLabPermissions,
  type TestingLabTestingRequestStatus,
  type TestingLabUserTestingLabPermissions,
  type ProjectsVersionSubmissionPolicy,
} from '@game-guild/client';
import { revalidatePath } from 'next/cache';

const DASHBOARD_PATH = '/workspace/testing-lab';

type TestingLabActionData<T> = [T] extends [void] ? null : T | null;

export type TestingLabActionResult<T = null> = { success: true; data: TestingLabActionData<T>; message: string } | { success: false; error: string };

function createModules(requestAuth = getRequestAuthContext()) {
  const client = createServerClient({
    baseUrl: process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080',
    auth: { getAccessToken: async () => (await requestAuth).token },
    tenant: { getTenantId: async () => (await requestAuth).tenantId },
  });

  return {
    requests: new GeneratedApi.TestingLabTestingRequestsModule(client),
    sessions: new GeneratedApi.TestingLabTestingSessionsModule(client),
    locations: new GeneratedApi.TestingLabTestingLocationsModule(client),
    participants: new GeneratedApi.TestingLabTestingParticipantsModule(client),
    feedback: new GeneratedApi.TestingLabTestingFeedbackModule(client),
    settings: new GeneratedApi.TestingLabSettingsModule(client),
    permissions: new GeneratedApi.TestingLabPermissionModule(client),
  };
}

function text(formData: FormData, key: string): string {
  const value = formData.get(key);
  return typeof value === 'string' ? value.trim() : '';
}

function optionalText(formData: FormData, key: string): string | null {
  return text(formData, key) || null;
}

function numberValue(formData: FormData, key: string, fallback = 0): number {
  const value = Number(text(formData, key));
  return Number.isFinite(value) ? value : fallback;
}

function optionalNumber(formData: FormData, key: string): number | null {
  const raw = text(formData, key);
  if (!raw) return null;
  const value = Number(raw);
  return Number.isFinite(value) ? value : null;
}

function checked(formData: FormData, key: string): boolean {
  return formData.get(key) === 'on' || formData.get(key) === 'true';
}

function isoDate(formData: FormData, key: string): string | null {
  const raw = text(formData, key);
  if (!raw) return null;
  const date = new Date(raw);
  return Number.isNaN(date.valueOf()) ? null : date.toISOString();
}

function revalidateTestingLab(...paths: string[]) {
  revalidatePath(DASHBOARD_PATH);
  revalidatePath('/testing-lab');
  paths.forEach((path) => revalidatePath(path));
}

async function complete<T>(operation: Promise<Result<T, ApiError>>, message: string, ...paths: string[]): Promise<TestingLabActionResult<T>> {
  try {
    const result = await operation;
    if (!result.ok) {
      const fieldErrors = 'fieldErrors' in result.error
        ? (result.error.fieldErrors as Record<string, string[]>)
        : undefined;
      const validationDetails = fieldErrors
        ? Object.entries(fieldErrors)
            .flatMap(([field, messages]) => messages.map((fieldMessage) => `${field}: ${fieldMessage}`))
            .join(' ')
        : '';
      return { success: false, error: validationDetails || result.error.message };
    }
    revalidateTestingLab(...paths);
    return { success: true, data: (result.data ?? null) as TestingLabActionData<T>, message };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'The Testing Lab operation failed.',
    };
  }
}

function required(formData: FormData, keys: string[], message: string): TestingLabActionResult | null {
  return keys.every((key) => text(formData, key)) ? null : { success: false, error: message };
}

export async function submitTestingBuild(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const invalid = required(formData, ['title', 'projectId', 'versionNumber'], 'Title, project, and version are required.');
  if (invalid) return invalid;

  const api = createModules();
  return complete(
    api.requests.postTestingSubmitSimple({
      title: text(formData, 'title'),
      projectId: text(formData, 'projectId'),
      versionNumber: text(formData, 'versionNumber'),
      description: optionalText(formData, 'description'),
      downloadUrl: optionalText(formData, 'downloadUrl'),
      instructionsType: (text(formData, 'instructionsType') || 'Text') as TestingLabInstructionType,
      instructionsContent: optionalText(formData, 'instructionsContent'),
      instructionsUrl: optionalText(formData, 'instructionsUrl'),
      feedbackFormContent: optionalText(formData, 'feedbackFormContent'),
      maxTesters: optionalNumber(formData, 'maxTesters'),
      startDate: isoDate(formData, 'startDate'),
      endDate: isoDate(formData, 'endDate'),
    }),
    'Testing request created.',
    `${DASHBOARD_PATH}/requests`,
  );
}

export async function updateTestingRequest(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const invalid = required(formData, ['requestId', 'title'], 'Request and title are required.');
  if (invalid) return invalid;

  const requestId = text(formData, 'requestId');
  const api = createModules();
  const current = await api.requests.getTestingRequestsForGetTestingRequestsById(requestId);
  if (!current.ok) return { success: false, error: current.error.message };

  const startDate = isoDate(formData, 'startDate') ?? current.data.startDate;
  const endDate = isoDate(formData, 'endDate') ?? current.data.endDate;
  if (!startDate || !endDate) {
    return { success: false, error: 'Testing requests require both a start and end date.' };
  }

  return complete(
    api.requests.putTestingRequests(requestId, {
      title: text(formData, 'title'),
      description: optionalText(formData, 'description'),
      downloadUrl: optionalText(formData, 'downloadUrl'),
      instructionsContent: optionalText(formData, 'instructionsContent'),
      feedbackFormContent: optionalText(formData, 'feedbackFormContent'),
      maxTesters: optionalNumber(formData, 'maxTesters'),
      startDate,
      endDate,
      status: (text(formData, 'status') || current.data.status) as TestingLabTestingRequestStatus,
    }),
    'Testing request updated.',
    `${DASHBOARD_PATH}/requests/${requestId}`,
  );
}

export async function deleteTestingRequest(formData: FormData): Promise<TestingLabActionResult> {
  const requestId = text(formData, 'requestId');
  if (!requestId) return { success: false, error: 'Request is required.' };
  return complete(createModules().requests.deleteTestingRequests(requestId), 'Testing request archived.', `${DASHBOARD_PATH}/requests`);
}

export async function restoreTestingRequest(formData: FormData): Promise<TestingLabActionResult> {
  const requestId = text(formData, 'requestId');
  if (!requestId) return { success: false, error: 'Request is required.' };
  return complete(createModules().requests.postTestingRequestsRestore(requestId), 'Testing request restored.', `${DASHBOARD_PATH}/requests`);
}

export async function bulkUpdateTestingRequests(formData: FormData): Promise<TestingLabActionResult<{ processed: number }>> {
  const requestIds = formData.getAll('requestIds').filter((value): value is string => typeof value === 'string' && value.length > 0);
  const operation = text(formData, 'operation');
  if (requestIds.length === 0) return { success: false, error: 'Select at least one testing request.' };
  if (!['archive', 'restore'].includes(operation)) return { success: false, error: 'Choose a valid bulk action.' };

  const requests = createModules().requests;
  const results = await Promise.all(
    requestIds.map((id) => (operation === 'restore' ? requests.postTestingRequestsRestore(id) : requests.deleteTestingRequests(id))),
  );
  const failure = results.find((result) => !result.ok);
  if (failure && !failure.ok) return { success: false, error: failure.error.message };
  revalidateTestingLab(`${DASHBOARD_PATH}/requests`);
  return { success: true, data: { processed: requestIds.length }, message: `${requestIds.length} requests updated.` };
}

export async function createTestingSession(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const invalid = required(
    formData,
    ['testingRequestId', 'locationId', 'sessionName', 'sessionDate', 'startTime', 'endTime'],
    'Request, location, name, date, start, and end are required.',
  );
  if (invalid) return invalid;

  const requestAuth = getRequestAuthContext();
  const { session } = await requestAuth;
  const managerUserId = text(formData, 'managerUserId') || session?.user?.id?.trim();
  if (!managerUserId) return { success: false, error: 'A session manager is required.' };

  return complete(
    createModules(requestAuth).sessions.postTestingSessions({
      testingRequestId: text(formData, 'testingRequestId'),
      locationId: text(formData, 'locationId'),
      managerUserId,
      sessionName: text(formData, 'sessionName'),
      sessionDate: text(formData, 'sessionDate'),
      startTime: text(formData, 'startTime'),
      endTime: text(formData, 'endTime'),
      maxTesters: numberValue(formData, 'maxTesters'),
      maxProjects: numberValue(formData, 'maxProjects'),
      status: (text(formData, 'status') || 'Scheduled') as TestingLabSessionStatus,
    }),
    'Testing session created.',
    `${DASHBOARD_PATH}/sessions`,
  );
}

export async function updateTestingSession(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  const api = createModules();
  const current = await api.sessions.getTestingSessionsForGetTestingSessionsById(sessionId);
  if (!current.ok) return { success: false, error: current.error.message };

  return complete(
    api.sessions.putTestingSessions(sessionId, {
      ...current.data,
      sessionName: text(formData, 'sessionName') || current.data.sessionName,
      sessionDate: text(formData, 'sessionDate') || current.data.sessionDate,
      startTime: text(formData, 'startTime') || current.data.startTime,
      endTime: text(formData, 'endTime') || current.data.endTime,
      locationId: text(formData, 'locationId') || current.data.locationId,
      maxTesters: optionalNumber(formData, 'maxTesters') ?? current.data.maxTesters,
      maxProjects: optionalNumber(formData, 'maxProjects') ?? current.data.maxProjects,
      status: (text(formData, 'status') || current.data.status) as TestingLabSessionStatus,
    }),
    'Testing session updated.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function deleteTestingSession(formData: FormData): Promise<TestingLabActionResult> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(createModules().sessions.deleteTestingSessions(sessionId), 'Testing session archived.', `${DASHBOARD_PATH}/sessions`);
}

export async function restoreTestingSession(formData: FormData): Promise<TestingLabActionResult> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(createModules().sessions.postTestingSessionsRestore(sessionId), 'Testing session restored.', `${DASHBOARD_PATH}/sessions`);
}

export async function updateTestingAttendance(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['sessionId', 'userId', 'attendanceStatus'], 'Session, user, and attendance status are required.');
  if (invalid) return invalid;
  const sessionId = text(formData, 'sessionId');
  return complete(
    createModules().sessions.postTestingSessionsAttendance(sessionId, {
      userId: text(formData, 'userId'),
      attendanceStatus: text(formData, 'attendanceStatus') as TestingLabAttendanceStatus,
    }),
    'Attendance updated.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function linkTestingSessionProject(formData: FormData): Promise<TestingLabActionResult<{ linkId?: string }>> {
  const invalid = required(formData, ['sessionId', 'projectId'], 'Session and project are required.');
  if (invalid) return invalid;
  const sessionId = text(formData, 'sessionId');
  return complete(
    createModules().sessions.postTestingSessionsProjects(sessionId, {
      projectId: text(formData, 'projectId'),
      projectVersionId: optionalText(formData, 'projectVersionId'),
      notes: optionalText(formData, 'notes'),
    }),
    'Project linked to session.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function unlinkTestingSessionProject(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['sessionId', 'projectId'], 'Session and project are required.');
  if (invalid) return invalid;
  const sessionId = text(formData, 'sessionId');
  return complete(
    createModules().sessions.deleteTestingSessionsProjects(sessionId, text(formData, 'projectId')),
    'Project removed from session.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function addTestingParticipant(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const invalid = required(formData, ['requestId', 'userId'], 'Request and user are required.');
  if (invalid) return invalid;
  const requestId = text(formData, 'requestId');
  return complete(
    createModules().participants.postTestingRequestsParticipants(requestId, text(formData, 'userId')),
    'Participant added.',
    `${DASHBOARD_PATH}/requests/${requestId}`,
  );
}

export async function removeTestingParticipant(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['requestId', 'userId'], 'Request and user are required.');
  if (invalid) return invalid;
  const requestId = text(formData, 'requestId');
  return complete(
    createModules().participants.deleteTestingRequestsParticipants(requestId, text(formData, 'userId')),
    'Participant removed.',
    `${DASHBOARD_PATH}/requests/${requestId}`,
  );
}

export async function registerForTestingSession(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(
    createModules().participants.postTestingSessionsRegister(sessionId, {
      registrationType: (text(formData, 'registrationType') || 'Tester') as TestingLabRegistrationType,
      notes: optionalText(formData, 'notes'),
    }),
    'Registered for testing session.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function unregisterFromTestingSession(formData: FormData): Promise<TestingLabActionResult> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(createModules().participants.deleteTestingSessionsRegister(sessionId), 'Registration cancelled.', `${DASHBOARD_PATH}/sessions/${sessionId}`);
}

export async function joinTestingSessionWaitlist(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(
    createModules().participants.postTestingSessionsWaitlist(sessionId, {
      registrationType: (text(formData, 'registrationType') || 'Tester') as TestingLabRegistrationType,
      notes: optionalText(formData, 'notes'),
    }),
    'Added to session waitlist.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function leaveTestingSessionWaitlist(formData: FormData): Promise<TestingLabActionResult> {
  const sessionId = text(formData, 'sessionId');
  if (!sessionId) return { success: false, error: 'Session is required.' };
  return complete(
    createModules().participants.deleteTestingSessionsWaitlist(sessionId),
    'Removed from session waitlist.',
    `${DASHBOARD_PATH}/sessions/${sessionId}`,
  );
}

export async function submitTestingFeedback(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['testingRequestId', 'feedbackResponses'], 'Request and feedback are required.');
  if (invalid) return invalid;
  return complete(
    createModules().feedback.postTestingFeedback({
      testingRequestId: text(formData, 'testingRequestId'),
      sessionId: optionalText(formData, 'sessionId'),
      feedbackResponses: text(formData, 'feedbackResponses'),
      additionalNotes: optionalText(formData, 'additionalNotes'),
      overallRating: optionalNumber(formData, 'overallRating'),
      wouldRecommend: checked(formData, 'wouldRecommend'),
    }),
    'Feedback submitted.',
    `${DASHBOARD_PATH}/feedback`,
  );
}

export async function rateTestingFeedback(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['feedbackId', 'quality'], 'Feedback and quality are required.');
  if (invalid) return invalid;
  return complete(
    createModules().feedback.postTestingFeedbackQuality(text(formData, 'feedbackId'), {
      quality: text(formData, 'quality') as TestingLabFeedbackQuality,
    }),
    'Feedback quality updated.',
    `${DASHBOARD_PATH}/feedback`,
  );
}

export async function reportTestingFeedback(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['feedbackId', 'reason'], 'Feedback and report reason are required.');
  if (invalid) return invalid;
  return complete(
    createModules().feedback.postTestingFeedbackReport(text(formData, 'feedbackId'), {
      reason: text(formData, 'reason'),
    }),
    'Feedback reported for review.',
    `${DASHBOARD_PATH}/feedback`,
  );
}

export async function createTestingLabLocation(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const name = text(formData, 'name');
  if (!name) return { success: false, error: 'Location name is required.' };
  return complete(
    createModules().locations.postTestingLocations({
      name,
      description: optionalText(formData, 'description'),
      address: optionalText(formData, 'address'),
      city: optionalText(formData, 'city'),
      state: optionalText(formData, 'state'),
      postalCode: optionalText(formData, 'postalCode'),
      country: optionalText(formData, 'country'),
      isVirtual: checked(formData, 'isVirtual'),
      virtualUrl: optionalText(formData, 'virtualUrl'),
      contactEmail: optionalText(formData, 'contactEmail'),
      contactPhone: optionalText(formData, 'contactPhone'),
      equipmentAvailable: optionalText(formData, 'equipmentAvailable'),
      maxTestersCapacity: optionalNumber(formData, 'maxTestersCapacity') ?? 0,
      maxProjectsCapacity: optionalNumber(formData, 'maxProjectsCapacity') ?? 0,
      status: (text(formData, 'status') || 'Active') as TestingLabLocationStatus,
    }),
    'Testing location created.',
    `${DASHBOARD_PATH}/locations`,
  );
}

export async function updateTestingLabLocation(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const locationId = text(formData, 'locationId');
  if (!locationId) return { success: false, error: 'Location is required.' };
  return complete(
    createModules().locations.putTestingLocations(locationId, {
      name: optionalText(formData, 'name'),
      description: optionalText(formData, 'description'),
      address: optionalText(formData, 'address'),
      city: optionalText(formData, 'city'),
      state: optionalText(formData, 'state'),
      postalCode: optionalText(formData, 'postalCode'),
      country: optionalText(formData, 'country'),
      isVirtual: checked(formData, 'isVirtual'),
      virtualUrl: optionalText(formData, 'virtualUrl'),
      contactEmail: optionalText(formData, 'contactEmail'),
      contactPhone: optionalText(formData, 'contactPhone'),
      equipmentAvailable: optionalText(formData, 'equipmentAvailable'),
      maxTestersCapacity: optionalNumber(formData, 'maxTestersCapacity'),
      maxProjectsCapacity: optionalNumber(formData, 'maxProjectsCapacity'),
      status: (text(formData, 'status') || 'Active') as TestingLabLocationStatus,
    }),
    'Testing location updated.',
    `${DASHBOARD_PATH}/locations`,
  );
}

export async function deleteTestingLabLocation(formData: FormData): Promise<TestingLabActionResult> {
  const locationId = text(formData, 'locationId');
  if (!locationId) return { success: false, error: 'Location is required.' };
  return complete(createModules().locations.deleteTestingLocations(locationId), 'Testing location archived.', `${DASHBOARD_PATH}/locations`);
}

export async function restoreTestingLabLocation(formData: FormData): Promise<TestingLabActionResult> {
  const locationId = text(formData, 'locationId');
  if (!locationId) return { success: false, error: 'Location is required.' };
  return complete(createModules().locations.postTestingLocationsRestore(locationId), 'Testing location restored.', `${DASHBOARD_PATH}/locations`);
}

export async function updateTestingLabSettings(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const labName = text(formData, 'labName');
  if (!labName) return { success: false, error: 'Lab name is required.' };
  return complete(
    createModules().settings.patchApiTestingLabSettings({
      labName,
      description: optionalText(formData, 'description'),
      timezone: optionalText(formData, 'timezone'),
      maxSimultaneousSessions: optionalNumber(formData, 'maxSimultaneousSessions'),
      defaultSessionDuration: optionalNumber(formData, 'defaultSessionDuration'),
      allowPublicSignups: checked(formData, 'allowPublicSignups'),
      requireApproval: checked(formData, 'requireApproval'),
      enableNotifications: checked(formData, 'enableNotifications'),
      reminderDaysBefore: optionalText(formData, 'reminderDaysBefore'),
      versionSubmissionPolicy: (text(formData, 'versionSubmissionPolicy') || 'ReadyMutableUntilReview') as ProjectsVersionSubmissionPolicy,
    }),
    'Testing Lab settings updated.',
    `${DASHBOARD_PATH}/settings`,
  );
}

export async function resetTestingLabSettings(): Promise<TestingLabActionResult<{ id?: string }>> {
  return complete(createModules().settings.postApiTestingLabSettingsReset(), 'Testing Lab settings reset.', `${DASHBOARD_PATH}/settings`);
}

const permissionKeys: Array<keyof TestingLabTestingLabPermissions> = [
  'canApproveRequests',
  'canCreateFeedback',
  'canCreateLocations',
  'canCreateRequests',
  'canCreateSessions',
  'canDeleteFeedback',
  'canDeleteLocations',
  'canDeleteRequests',
  'canDeleteSessions',
  'canEditFeedback',
  'canEditLocations',
  'canEditRequests',
  'canEditSessions',
  'canManageParticipants',
  'canModerateFeedback',
  'canViewFeedback',
  'canViewLocations',
  'canViewParticipants',
  'canViewRequests',
  'canViewSessions',
];

function permissionsFromForm(formData: FormData): TestingLabTestingLabPermissions {
  return Object.fromEntries(permissionKeys.map((permission) => [permission, checked(formData, permission)]));
}

export async function createTestingLabRole(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const name = text(formData, 'name');
  if (!name) return { success: false, error: 'Role name is required.' };
  return complete(
    createModules().permissions.postApiTestingLabPermissionsRoleTemplates({
      name,
      description: optionalText(formData, 'description'),
      permissions: permissionsFromForm(formData),
    }),
    'Testing Lab role created.',
    `${DASHBOARD_PATH}/access`,
  );
}

export async function updateTestingLabRole(formData: FormData): Promise<TestingLabActionResult<{ id?: string }>> {
  const idOrName = text(formData, 'idOrName');
  if (!idOrName) return { success: false, error: 'Role is required.' };
  return complete(
    createModules().permissions.putApiTestingLabPermissionsRoleTemplates(idOrName, {
      name: optionalText(formData, 'name'),
      description: optionalText(formData, 'description'),
      permissions: permissionsFromForm(formData),
    }),
    'Testing Lab role updated.',
    `${DASHBOARD_PATH}/access`,
  );
}

export async function deleteTestingLabRole(formData: FormData): Promise<TestingLabActionResult> {
  const idOrName = text(formData, 'idOrName');
  if (!idOrName) return { success: false, error: 'Role is required.' };
  return complete(createModules().permissions.deleteApiTestingLabPermissionsRoleTemplates(idOrName), 'Testing Lab role deleted.', `${DASHBOARD_PATH}/access`);
}

export async function assignTestingLabRole(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['userId', 'roleName'], 'User and role are required.');
  if (invalid) return invalid;
  return complete(
    createModules().permissions.postApiTestingLabPermissionsUsersRoles(text(formData, 'userId'), {
      roleName: text(formData, 'roleName'),
      tenantId: optionalText(formData, 'tenantId'),
      expiresAt: isoDate(formData, 'expiresAt'),
    }),
    'Testing Lab role assigned.',
    `${DASHBOARD_PATH}/access`,
  );
}

export async function revokeTestingLabRole(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['userId', 'roleName'], 'User and role are required.');
  if (invalid) return invalid;
  return complete(
    createModules().permissions.deleteApiTestingLabPermissionsUsersRoles(text(formData, 'userId'), text(formData, 'roleName'), {
      tenantId: optionalText(formData, 'tenantId') ?? undefined,
    }),
    'Testing Lab role revoked.',
    `${DASHBOARD_PATH}/access`,
  );
}

export async function inspectTestingLabUserAccess(formData: FormData): Promise<TestingLabActionResult<TestingLabUserTestingLabPermissions>> {
  const userId = text(formData, 'userId');
  if (!userId) return { success: false, error: 'User is required.' };
  try {
    const result = await createModules().permissions.getApiTestingLabPermissionsUsers(userId, {
      tenantId: optionalText(formData, 'tenantId') ?? undefined,
    });
    return result.ok ? { success: true, data: result.data, message: 'Effective Testing Lab access loaded.' } : { success: false, error: result.error.message };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Could not load Testing Lab access.' };
  }
}
export async function grantTestingLabResourcePermission(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['userId', 'resourceType', 'resourceId', 'action'], 'User, resource, and action are required.');
  if (invalid) return invalid;
  return complete(
    createModules().permissions.postApiTestingLabPermissionsUsersResources(
      text(formData, 'userId'),
      text(formData, 'resourceType'),
      text(formData, 'resourceId'),
      {
        action: text(formData, 'action'),
        tenantId: optionalText(formData, 'tenantId'),
        expiresAt: isoDate(formData, 'expiresAt'),
      },
    ),
    'Resource permission granted.',
    `${DASHBOARD_PATH}/access`,
  );
}

export async function revokeTestingLabResourcePermission(formData: FormData): Promise<TestingLabActionResult> {
  const invalid = required(formData, ['userId', 'resourceType', 'resourceId', 'action'], 'User, resource, and action are required.');
  if (invalid) return invalid;
  return complete(
    createModules().permissions.deleteApiTestingLabPermissionsUsersResources(
      text(formData, 'userId'),
      text(formData, 'resourceType'),
      text(formData, 'resourceId'),
      {
        action: text(formData, 'action'),
        tenantId: optionalText(formData, 'tenantId') ?? undefined,
      },
    ),
    'Resource permission revoked.',
    `${DASHBOARD_PATH}/access`,
  );
}
