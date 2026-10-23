import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  postAuthTrustedDevices: vi.fn(),
  revalidatePath: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: vi.fn().mockResolvedValue('synthetic-bearer') }));
vi.mock('next/cache', () => ({ revalidatePath: mocks.revalidatePath }));
vi.mock('@game-guild/client', () => ({
  createServerClient: vi.fn(() => ({})),
  GeneratedApi: {
    AuthTrustedDevicesModule: class {
      postAuthTrustedDevices = mocks.postAuthTrustedDevices;
    },
  },
}));

const { trustCurrentDeviceAction } = await import('./sessions-actions');

describe('trustCurrentDeviceAction (issue278)', () => {
  beforeEach(() => {
    mocks.postAuthTrustedDevices.mockReset();
    mocks.revalidatePath.mockReset();
  });

  it('sends the existing non-null display-name contract when the UI supplies no name', async () => {
    mocks.postAuthTrustedDevices.mockResolvedValue({ ok: true, data: {} });
    await expect(trustCurrentDeviceAction()).resolves.toEqual({ success: true, status: 'success' });
    expect(mocks.postAuthTrustedDevices).toHaveBeenCalledTimes(1);
    expect(mocks.postAuthTrustedDevices).toHaveBeenCalledWith({ deviceName: '' });
    expect(mocks.revalidatePath).toHaveBeenCalledTimes(1);
    expect(mocks.revalidatePath).toHaveBeenCalledWith('/workspace/settings/security');
  });

  it('preserves the requested display name without sending device or owner identity', async () => {
    mocks.postAuthTrustedDevices.mockResolvedValue({ ok: true, data: {} });
    await trustCurrentDeviceAction('Personal browser');
    expect(mocks.postAuthTrustedDevices).toHaveBeenCalledTimes(1);
    expect(mocks.postAuthTrustedDevices).toHaveBeenCalledWith({ deviceName: 'Personal browser' });
  });

  it('propagates API authentication rejection as an unsuccessful action', async () => {
    mocks.postAuthTrustedDevices.mockResolvedValue({ ok: false, error: { status: 401 } });
    await expect(trustCurrentDeviceAction()).resolves.toEqual({ success: false, status: 'unauthorized' });
  });
});
