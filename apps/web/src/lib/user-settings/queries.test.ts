import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getAuthenticatedUserId: vi.fn(),
  getUserSettingsApiClient: vi.fn(),
  request: vi.fn(),
}));

vi.mock('./api-client', () => ({
  getAuthenticatedUserId: mocks.getAuthenticatedUserId,
  getUserSettingsApiClient: mocks.getUserSettingsApiClient,
}));

import { getCommunityProfile, getGeneralPreferences, getLocalizationPreference } from './queries';

describe('user settings queries', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAuthenticatedUserId.mockResolvedValue('user-1');
    mocks.getUserSettingsApiClient.mockReturnValue({ request: mocks.request });
  });

  it('uses documented defaults only when the preferences row does not exist', async () => {
    mocks.request.mockResolvedValue({ ok: false, error: { status: 404, message: 'Not found' } });

    await expect(getLocalizationPreference()).resolves.toEqual({
      language: 'en-US',
      timezone: 'UTC',
      dateFormat: 'MM/dd/yyyy',
      timeFormat: '12h',
      currency: 'USD',
    });
    expect(mocks.request).toHaveBeenCalledWith({
      method: 'GET',
      path: '/v1/users/user-1/preferences/localization',
      requiresAuth: true,
    });
  });

  it('surfaces authentication and transport failures instead of treating them as defaults', async () => {
    mocks.request.mockResolvedValue({ ok: false, error: { status: 401, message: 'Unauthorized' } });
    await expect(getLocalizationPreference()).rejects.toThrow('Unable to load localization preferences (401): Unauthorized');

    mocks.request.mockResolvedValue({ ok: false, error: { status: 500, message: 'Unavailable' } });
    await expect(getGeneralPreferences()).rejects.toThrow('Unable to load general preferences (500): Unavailable');
  });

  it('normalizes the community profile with its visibility setting', async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: {
        id: 'sp-1',
        userId: 'user-1',
        handle: 'ari-games',
        displayName: 'Ari',
        avatarUrl: 'https://cdn.example.com/a.png',
        bannerUrl: null,
        visibility: 'Connections',
        showActivity: true,
        showPortfolio: false,
        showSkills: true,
      },
    });

    await expect(getCommunityProfile()).resolves.toEqual({
      handle: 'ari-games',
      displayName: 'Ari',
      avatarUrl: 'https://cdn.example.com/a.png',
      bannerUrl: null,
      visibility: 'members',
    });
    expect(mocks.request).toHaveBeenCalledWith({
      method: 'GET',
      path: '/api/social/profiles/users/user-1',
      requiresAuth: true,
    });
  });

  it('treats a missing community profile as null and unknown visibility as public', async () => {
    mocks.request.mockResolvedValue({ ok: false, error: { status: 404, message: 'Not found' } });
    await expect(getCommunityProfile()).resolves.toEqual(null);

    mocks.request.mockResolvedValue({ ok: true, data: { handle: 'ari', visibility: 'SomethingElse' } });
    await expect(getCommunityProfile()).resolves.toEqual({
      handle: 'ari',
      displayName: null,
      avatarUrl: null,
      bannerUrl: null,
      visibility: 'public',
    });
  });
});
