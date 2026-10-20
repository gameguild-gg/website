import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getAuthSessions: vi.fn(),
  getAuthSessionsAnalyzeSecurity: vi.fn(),
  getAuthTrustedDevices: vi.fn(),
  revalidatePath: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: vi.fn().mockResolvedValue('token') }));

vi.mock('next/cache', () => ({ revalidatePath: mocks.revalidatePath }));

vi.mock('@game-guild/client', () => ({
  createServerClient: vi.fn(() => ({})),
  GeneratedApi: {
    AuthSessionsModule: class {
      getAuthSessions = mocks.getAuthSessions;
      getAuthSessionsAnalyzeSecurity = mocks.getAuthSessionsAnalyzeSecurity;
    },
    AuthTrustedDevicesModule: class {
      getAuthTrustedDevices = mocks.getAuthTrustedDevices;
    },
  },
}));

const { loadSessionsData } = await import('./sessions-actions');

const SESSIONS = [{ id: 's1', isCurrent: true }] as never;
const ANALYSIS = { activeSessionCount: 1 } as never;
const TRUSTED_DEVICES = [{ id: 'd1' }] as never;

describe('loadSessionsData (issue #864 contract-drift defense)', () => {
  beforeEach(() => {
    mocks.getAuthSessions.mockReset();
    mocks.getAuthSessionsAnalyzeSecurity.mockReset();
    mocks.getAuthTrustedDevices.mockReset();
  });

  it('returns data when all calls succeed', async () => {
    mocks.getAuthSessions.mockResolvedValue({ ok: true, data: SESSIONS });
    mocks.getAuthSessionsAnalyzeSecurity.mockResolvedValue({ ok: true, data: ANALYSIS });
    mocks.getAuthTrustedDevices.mockResolvedValue({ ok: true, data: TRUSTED_DEVICES });

    await expect(loadSessionsData()).resolves.toEqual({
      sessions: SESSIONS,
      analysis: ANALYSIS,
      trustedDevices: TRUSTED_DEVICES,
    });
  });

  it('degrades to empty lists and null analysis when safeParse throws on contract mismatch instead of crashing the RSC render', async () => {
    const contractDrift = new Error('[GET /v1/auth/sessions] Response validation failed');
    mocks.getAuthSessions.mockRejectedValue(contractDrift);
    mocks.getAuthSessionsAnalyzeSecurity.mockRejectedValue(contractDrift);
    mocks.getAuthTrustedDevices.mockRejectedValue(contractDrift);

    await expect(loadSessionsData()).resolves.toEqual({
      sessions: [],
      analysis: null,
      trustedDevices: [],
    });
  });

  it('degrades individual failures while keeping successful sections', async () => {
    mocks.getAuthSessions.mockRejectedValue(new Error('drift'));
    mocks.getAuthSessionsAnalyzeSecurity.mockResolvedValue({ ok: true, data: ANALYSIS });
    mocks.getAuthTrustedDevices.mockResolvedValue({ ok: false, error: { status: 500 } });

    await expect(loadSessionsData()).resolves.toEqual({
      sessions: [],
      analysis: ANALYSIS,
      trustedDevices: [],
    });
  });

  it('degrades non-ok results to empty shapes', async () => {
    mocks.getAuthSessions.mockResolvedValue({ ok: false, error: { status: 401 } });
    mocks.getAuthSessionsAnalyzeSecurity.mockResolvedValue({ ok: false, error: { status: 500 } });
    mocks.getAuthTrustedDevices.mockResolvedValue({ ok: false, error: { status: 404 } });

    await expect(loadSessionsData()).resolves.toEqual({
      sessions: [],
      analysis: null,
      trustedDevices: [],
    });
  });
});
