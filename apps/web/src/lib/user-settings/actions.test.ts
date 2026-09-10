import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  getAuthenticatedUserId: vi.fn(),
  getUserSettingsApiClient: vi.fn(),
  getAccessibilityPreference: vi.fn(),
  getGeneralPreferences: vi.fn(),
  request: vi.fn(),
}));

vi.mock('./api-client', () => ({
  getAuthenticatedUserId: mocks.getAuthenticatedUserId,
  getUserSettingsApiClient: mocks.getUserSettingsApiClient,
}));

vi.mock('./queries', () => ({
  getAccessibilityPreference: mocks.getAccessibilityPreference,
  getGeneralPreferences: mocks.getGeneralPreferences,
}));

import {
  getThemePreferenceAction,
  updateLocalizationPreferenceAction,
  updateProfileAction,
} from './actions';

describe('user settings actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAuthenticatedUserId.mockResolvedValue('user-1');
    mocks.getUserSettingsApiClient.mockReturnValue({ request: mocks.request });
    mocks.request.mockResolvedValue({ ok: true, data: undefined });
  });

  it('updates a profile through the configured ApiClient request pipeline', async () => {
    await expect(updateProfileAction({
      handle: 'ari-games',
      displayName: 'Ari',
      bio: 'Designs games.',
      location: 'São Paulo',
      website: 'https://example.com',
      jobTitle: 'Designer',
      company: 'GameGuild',
    })).resolves.toEqual({ success: true, data: undefined });

    expect(mocks.request).toHaveBeenNthCalledWith(1, {
      method: 'PATCH',
      path: '/v1/users/user-1/profile',
      body: {
        displayName: 'Ari',
        bio: 'Designs games.',
        location: 'São Paulo',
        website: 'https://example.com',
        jobTitle: 'Designer',
        company: 'GameGuild',
      },
      requiresAuth: true,
    });
    expect(mocks.request).toHaveBeenNthCalledWith(2, {
      method: 'PUT',
      path: '/api/social/profiles/users/user-1',
      body: {
        handle: 'ari-games',
        displayName: 'Ari',
        bio: 'Designs games.',
        headline: 'Designer at GameGuild',
        location: 'São Paulo',
        websiteUrl: 'https://example.com',
        socialLinksJson: '{}',
      },
      requiresAuth: true,
    });
  });

  it('writes localization values to the category endpoint and returns API failures to the form', async () => {
    await expect(updateLocalizationPreferenceAction({
      language: 'pt-BR',
      timezone: 'America/Sao_Paulo',
      dateFormat: 'dd/MM/yyyy',
      timeFormat: '24h',
      currency: 'BRL',
    })).resolves.toEqual({ success: true, data: undefined });

    expect(mocks.request).toHaveBeenCalledWith({
      method: 'PATCH',
      path: '/v1/users/user-1/preferences/localization',
      body: {
        localizationPreferences: {
          Language: 'pt-BR',
          Timezone: 'America/Sao_Paulo',
          DateFormat: 'dd/MM/yyyy',
          TimeFormat: '24h',
          Currency: 'BRL',
        },
      },
      requiresAuth: true,
    });

    mocks.request.mockResolvedValueOnce({ ok: false, error: { message: 'Not allowed' } });
    await expect(updateProfileAction({
      handle: 'ari',
      displayName: '',
      bio: '',
      location: '',
      website: '',
      jobTitle: '',
      company: '',
    })).resolves.toEqual({ success: false, error: 'Not allowed' });
  });

  it('returns a controlled failure when a client initializer cannot read server preferences', async () => {
    mocks.getGeneralPreferences.mockRejectedValue(new Error('Unauthorized'));

    await expect(getThemePreferenceAction()).resolves.toEqual({ success: false, error: 'Unauthorized' });
  });

  it('rejects invalid community handles before writing either profile', async () => {
    await expect(updateProfileAction({
      handle: 'bad handle',
      displayName: 'Ari',
      bio: '',
      location: '',
      website: '',
      jobTitle: '',
      company: '',
    })).resolves.toEqual({
      success: false,
      error: 'Community handle must use 3–80 letters, numbers, underscores, or hyphens.',
    });
    expect(mocks.request).not.toHaveBeenCalled();
  });
});
