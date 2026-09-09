import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getRequestAuthContext: vi.fn(),
  createServerClient: vi.fn(() => ({})),
  revalidatePath: vi.fn(),
  postTestingSubmitSimple: vi.fn(),
  deleteTestingRequests: vi.fn(),
  postTestingRequestsRestore: vi.fn(),
  postTestingSessions: vi.fn(),
  postTestingLocations: vi.fn(),
  postTestingSessionsRegister: vi.fn(),
  postTestingSessionsWaitlist: vi.fn(),
  patchApiTestingLabSettings: vi.fn(),
  postApiTestingLabPermissionsRoleTemplates: vi.fn(),
  postApiTestingLabPermissionsUsersRoles: vi.fn(),
  getApiTestingLabPermissionsUsers: vi.fn(),
  postApiTestingLabPermissionsUsersResources: vi.fn(),
  deleteApiTestingLabPermissionsUsersResources: vi.fn(),
}));

vi.mock('@/auth', () => ({
  getRequestAuthContext: mocks.getRequestAuthContext,
}));

vi.mock('next/cache', () => ({
  revalidatePath: mocks.revalidatePath,
}));

vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    TestingLabTestingRequestsModule: vi.fn(function TestingLabTestingRequestsModule() {
      return {
        postTestingSubmitSimple: mocks.postTestingSubmitSimple,
        deleteTestingRequests: mocks.deleteTestingRequests,
        postTestingRequestsRestore: mocks.postTestingRequestsRestore,
      };
    }),
    TestingLabTestingSessionsModule: vi.fn(function TestingLabTestingSessionsModule() {
      return { postTestingSessions: mocks.postTestingSessions };
    }),
    TestingLabTestingLocationsModule: vi.fn(function TestingLabTestingLocationsModule() {
      return { postTestingLocations: mocks.postTestingLocations };
    }),
    TestingLabTestingParticipantsModule: vi.fn(function TestingLabTestingParticipantsModule() {
      return {
        postTestingSessionsRegister: mocks.postTestingSessionsRegister,
        postTestingSessionsWaitlist: mocks.postTestingSessionsWaitlist,
      };
    }),
    TestingLabTestingFeedbackModule: vi.fn(function TestingLabTestingFeedbackModule() {
      return {};
    }),
    TestingLabSettingsModule: vi.fn(function TestingLabSettingsModule() {
      return { patchApiTestingLabSettings: mocks.patchApiTestingLabSettings };
    }),
    TestingLabPermissionModule: vi.fn(function TestingLabPermissionModule() {
      return {
        postApiTestingLabPermissionsRoleTemplates: mocks.postApiTestingLabPermissionsRoleTemplates,
        postApiTestingLabPermissionsUsersRoles: mocks.postApiTestingLabPermissionsUsersRoles,
        getApiTestingLabPermissionsUsers: mocks.getApiTestingLabPermissionsUsers,
        postApiTestingLabPermissionsUsersResources: mocks.postApiTestingLabPermissionsUsersResources,
        deleteApiTestingLabPermissionsUsersResources: mocks.deleteApiTestingLabPermissionsUsersResources,
      };
    }),
  },
}));

import {
  assignTestingLabRole,
  createTestingLabLocation,
  createTestingLabRole,
  createTestingSession,
  deleteTestingRequest,
  restoreTestingRequest,
  registerForTestingSession,
  joinTestingSessionWaitlist,
  inspectTestingLabUserAccess,
  grantTestingLabResourcePermission,
  revokeTestingLabResourcePermission,
  submitTestingBuild,
  updateTestingLabSettings,
} from './actions';

function form(values: Record<string, string>) {
  const data = new FormData();
  Object.entries(values).forEach(([key, value]) => data.set(key, value));
  return data;
}

describe('Testing Lab server actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getRequestAuthContext.mockResolvedValue({
      token: 'token',
      tenantId: 'tenant-1',
      session: { user: { id: 'manager-1' }, tenantId: 'tenant-1' },
    });
  });

  it('uses one request authentication context for both token and tenant', async () => {
    mocks.postApiTestingLabPermissionsRoleTemplates.mockResolvedValue({ ok: true, data: { id: 'role-1' } });

    await createTestingLabRole(form({ name: 'Facilitator' }));

    const clientOptions = mocks.createServerClient.mock.calls[0]?.[0];
    await expect(clientOptions.auth.getAccessToken()).resolves.toBe('token');
    await expect(clientOptions.tenant.getTenantId()).resolves.toBe('tenant-1');
    expect(mocks.getRequestAuthContext).toHaveBeenCalledTimes(1);
  });

  it('submits a build through the generated requests client', async () => {
    mocks.postTestingSubmitSimple.mockResolvedValue({ ok: true, data: { id: 'request-1' } });

    const result = await submitTestingBuild(
      form({
        title: 'Vertical slice',
        projectId: 'project-1',
        versionNumber: '0.3.0',
        maxTesters: '12',
        instructionsType: 'Text',
      }),
    );

    expect(result).toEqual({ success: true, data: { id: 'request-1' }, message: 'Testing request created.' });
    expect(mocks.postTestingSubmitSimple).toHaveBeenCalledWith(
      expect.objectContaining({
        title: 'Vertical slice',
        projectId: 'project-1',
        versionNumber: '0.3.0',
        maxTesters: 12,
        instructionsType: 'Text',
      }),
    );
    expect(mocks.postTestingSubmitSimple.mock.calls[0]?.[0]).not.toHaveProperty('teamIdentifier');
    expect(mocks.revalidatePath).toHaveBeenCalledWith('/workspace/testing-lab');
  });

  it('returns validation errors without calling the API', async () => {
    const result = await submitTestingBuild(form({ title: '', projectId: '', versionNumber: '' }));

    expect(result).toEqual({ success: false, error: 'Title, project, and version are required.' });
    expect(mocks.postTestingSubmitSimple).not.toHaveBeenCalled();
  });

  it('surfaces field-level API validation details instead of the generic validation title', async () => {
    mocks.postTestingSubmitSimple.mockResolvedValue({
      ok: false,
      error: {
        message: 'One or more validation errors occurred.',
        fieldErrors: {
          TeamIdentifier: ['The TeamIdentifier field is required.'],
          StartDate: ['Start date must be in the future.'],
        },
      },
    });

    const result = await submitTestingBuild(
      form({
        title: 'Vertical slice',
        projectId: 'project-1',
        versionNumber: '0.3.0',
        instructionsType: 'Text',
      }),
    );

    expect(result).toEqual({
      success: false,
      error: 'TeamIdentifier: The TeamIdentifier field is required. StartDate: Start date must be in the future.',
    });
  });

  it('deletes and restores requests through generated client operations', async () => {
    mocks.deleteTestingRequests.mockResolvedValue({ ok: true, data: undefined });
    mocks.postTestingRequestsRestore.mockResolvedValue({ ok: true, data: undefined });

    await expect(deleteTestingRequest(form({ requestId: 'request-1' }))).resolves.toEqual({
      success: true,
      data: null,
      message: 'Testing request archived.',
    });
    await expect(restoreTestingRequest(form({ requestId: 'request-1' }))).resolves.toEqual({
      success: true,
      data: null,
      message: 'Testing request restored.',
    });
  });

  it('creates sessions and locations through their generated modules', async () => {
    mocks.postTestingSessions.mockResolvedValue({ ok: true, data: { id: 'session-1' } });
    mocks.postTestingLocations.mockResolvedValue({ ok: true, data: { id: 'location-1' } });

    const session = await createTestingSession(
      form({
        testingRequestId: 'request-1',
        locationId: 'location-1',
        sessionName: 'Friday playtest',
        sessionDate: '2026-08-01',
        startTime: '2026-08-01T18:00:00.000Z',
        endTime: '2026-08-01T20:00:00.000Z',
        maxTesters: '16',
        maxProjects: '4',
        status: 'Scheduled',
      }),
    );
    const location = await createTestingLabLocation(
      form({
        name: 'Remote Lab',
        isVirtual: 'true',
        virtualUrl: 'https://meet.gameguild.gg/lab',
        contactEmail: 'facilitator@gameguild.gg',
        contactPhone: '+55 11 5555-0100',
        status: 'Active',
        maxTestersCapacity: '30',
        maxProjectsCapacity: '8',
      }),
    );

    expect(session.success).toBe(true);
    expect(mocks.postTestingSessions).toHaveBeenCalledWith(expect.objectContaining({ managerUserId: 'manager-1' }));
    expect(location.success).toBe(true);
    expect(mocks.postTestingLocations).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Remote Lab',
        isVirtual: true,
        virtualUrl: 'https://meet.gameguild.gg/lab',
        contactEmail: 'facilitator@gameguild.gg',
        contactPhone: '+55 11 5555-0100',
      }),
    );
  });

  it('registers members or joins the waitlist through the generated participant module', async () => {
    mocks.postTestingSessionsRegister.mockResolvedValue({ ok: true, data: { id: 'registration-1' } });
    mocks.postTestingSessionsWaitlist.mockResolvedValue({ ok: true, data: { id: 'waitlist-1' } });

    await expect(registerForTestingSession(form({ sessionId: 'session-1', registrationType: 'Tester' }))).resolves.toEqual({
      success: true,
      data: { id: 'registration-1' },
      message: 'Registered for testing session.',
    });
    await expect(joinTestingSessionWaitlist(form({ sessionId: 'session-1', registrationType: 'Tester' }))).resolves.toEqual({
      success: true,
      data: { id: 'waitlist-1' },
      message: 'Added to session waitlist.',
    });
  });

  it('updates settings and manages roles through generated modules', async () => {
    mocks.patchApiTestingLabSettings.mockResolvedValue({ ok: true, data: { labName: 'GameGuild Testing Lab' } });
    mocks.postApiTestingLabPermissionsRoleTemplates.mockResolvedValue({ ok: true, data: { id: 'role-1' } });
    mocks.postApiTestingLabPermissionsUsersRoles.mockResolvedValue({ ok: true, data: undefined });

    const settings = await updateTestingLabSettings(
      form({
        labName: 'GameGuild Testing Lab',
        timezone: 'America/Sao_Paulo',
        maxSimultaneousSessions: '6',
        defaultSessionDuration: '120',
        allowPublicSignups: 'on',
      }),
    );
    const role = await createTestingLabRole(
      form({
        name: 'Facilitator',
        description: 'Runs testing sessions',
        canViewSessions: 'on',
        canCreateSessions: 'on',
      }),
    );
    const assignment = await assignTestingLabRole(
      form({
        userId: 'user-1',
        roleName: 'Facilitator',
        tenantId: 'tenant-1',
      }),
    );

    expect(settings.success).toBe(true);
    expect(role.success).toBe(true);
    expect(mocks.postApiTestingLabPermissionsRoleTemplates).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Facilitator',
        permissions: expect.objectContaining({ canViewSessions: true, canCreateSessions: true }),
      }),
    );
    expect(assignment.success).toBe(true);
  });
  it('inspects effective access and grants or revokes resource permissions', async () => {
    mocks.getApiTestingLabPermissionsUsers.mockResolvedValue({
      ok: true,
      data: { userId: 'user-1', assignedRoles: ['Facilitator'], permissions: { canViewSessions: true } },
    });
    mocks.postApiTestingLabPermissionsUsersResources.mockResolvedValue({ ok: true, data: undefined });
    mocks.deleteApiTestingLabPermissionsUsersResources.mockResolvedValue({ ok: true, data: undefined });

    await expect(inspectTestingLabUserAccess(form({ userId: 'user-1', tenantId: 'tenant-1' }))).resolves.toEqual({
      success: true,
      data: { userId: 'user-1', assignedRoles: ['Facilitator'], permissions: { canViewSessions: true } },
      message: 'Effective Testing Lab access loaded.',
    });
    await expect(
      grantTestingLabResourcePermission(
        form({
          userId: 'user-1',
          resourceType: 'TestingSession',
          resourceId: 'session-1',
          action: 'edit',
          expiresAt: '2026-08-15T18:00',
        }),
      ),
    ).resolves.toEqual({ success: true, data: null, message: 'Resource permission granted.' });
    await expect(
      revokeTestingLabResourcePermission(form({ userId: 'user-1', resourceType: 'TestingSession', resourceId: 'session-1', action: 'edit' })),
    ).resolves.toEqual({ success: true, data: null, message: 'Resource permission revoked.' });

    expect(mocks.getApiTestingLabPermissionsUsers).toHaveBeenCalledWith('user-1', { tenantId: 'tenant-1' });
    expect(mocks.postApiTestingLabPermissionsUsersResources).toHaveBeenCalledWith(
      'user-1',
      'TestingSession',
      'session-1',
      expect.objectContaining({ action: 'edit', expiresAt: expect.stringMatching(/^2026-08-15T/) }),
    );
    expect(mocks.deleteApiTestingLabPermissionsUsersResources).toHaveBeenCalledWith('user-1', 'TestingSession', 'session-1', {
      action: 'edit',
      tenantId: undefined,
    });
  });
});
