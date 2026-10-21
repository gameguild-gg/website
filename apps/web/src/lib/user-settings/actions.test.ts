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
  updateProfileVisibilityAction,
} from './actions';

const completeInput = {
  handle: 'ari-games',
  displayName: 'Ari',
  bio: 'Designs games.',
  location: 'São Paulo',
  website: 'https://example.com',
  jobTitle: 'Designer',
  company: 'GameGuild',
  avatarUrl: 'https://cdn.example.com/avatar.png',
  bannerUrl: '',
};

/** Queue of `request` results consumed in call order. */
function queueResults(
  ...results: Array<{ ok: true; data: unknown } | { ok: false; error: { message: string; status?: number } }>
) {
  for (const result of results) {
    mocks.request.mockResolvedValueOnce(result);
  }
}

describe('user settings actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getAuthenticatedUserId.mockResolvedValue('user-1');
    mocks.getUserSettingsApiClient.mockReturnValue({ request: mocks.request });
    mocks.request.mockResolvedValue({ ok: true, data: undefined });
  });

  it('saves both profiles in one consistent action with readback verification', async () => {
    queueResults(
      // Handle availability pre-check: handle owned by the actor.
      { ok: true, data: { userId: 'user-1' } },
      // Previous account profile snapshot for rollback.
      { ok: true, data: { displayName: 'Old Name', bio: '', location: '', website: '', jobTitle: '', company: '' } },
      // Account profile write.
      { ok: true, data: { displayName: 'Ari' } },
      // Community profile write with persisted readback payload.
      { ok: true, data: { handle: 'ari-games', displayName: 'Ari' } },
    );

    await expect(updateProfileAction(completeInput)).resolves.toEqual({ success: true, data: undefined });

    expect(mocks.request).toHaveBeenNthCalledWith(1, {
      method: 'GET',
      path: '/api/social/profiles/@ari-games',
      requiresAuth: true,
    });
    expect(mocks.request).toHaveBeenNthCalledWith(2, {
      method: 'GET',
      path: '/v1/users/user-1/profile',
      requiresAuth: true,
    });
    expect(mocks.request).toHaveBeenNthCalledWith(3, {
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
    expect(mocks.request).toHaveBeenNthCalledWith(4, {
      method: 'PUT',
      path: '/api/social/profiles/users/user-1',
      body: {
        handle: 'ari-games',
        displayName: 'Ari',
        bio: 'Designs games.',
        avatarUrl: 'https://cdn.example.com/avatar.png',
        bannerUrl: null,
        headline: 'Designer at GameGuild',
        location: 'São Paulo',
        websiteUrl: 'https://example.com',
        socialLinksJson: '{}',
      },
      requiresAuth: true,
    });
    expect(mocks.request).toHaveBeenCalledTimes(4);
  });

  it('refuses a handle owned by someone else before any write happens', async () => {
    queueResults({ ok: true, data: { userId: 'user-2' } });

    await expect(updateProfileAction(completeInput)).resolves.toEqual({
      success: false,
      error: 'That community handle is already taken. Choose another one.',
    });
    expect(mocks.request).toHaveBeenCalledTimes(1);
  });

  it('rolls the account write back when the community write fails', async () => {
    queueResults(
      { ok: true, data: { userId: 'user-1' } },
      { ok: true, data: { displayName: 'Old Name', bio: 'Old bio', location: '', website: '', jobTitle: '', company: '' } },
      { ok: true, data: { displayName: 'Ari' } },
      { ok: false, error: { message: 'Handle conflict' } },
      // Compensating account PATCH restoring the snapshot.
      { ok: true, data: undefined },
    );

    await expect(updateProfileAction(completeInput)).resolves.toEqual({
      success: false,
      error: 'Handle conflict',
    });

    expect(mocks.request).toHaveBeenNthCalledWith(5, {
      method: 'PATCH',
      path: '/v1/users/user-1/profile',
      body: {
        displayName: 'Old Name',
        bio: 'Old bio',
        location: '',
        website: '',
        jobTitle: '',
        company: '',
      },
      requiresAuth: true,
    });
  });

  it('reports an explicit partial save when the rollback itself fails', async () => {
    queueResults(
      { ok: true, data: { userId: 'user-1' } },
      { ok: true, data: { displayName: 'Old Name', bio: '', location: '', website: '', jobTitle: '', company: '' } },
      { ok: true, data: { displayName: 'Ari' } },
      { ok: false, error: { message: 'Community profile unavailable' } },
      { ok: false, error: { message: 'Rollback failed' } },
    );

    const result = await updateProfileAction(completeInput);
    expect(result.success).toBe(false);
    expect(result.error).toContain('Community profile unavailable');
    expect(result.error).toContain('could not be rolled back');
  });

  it('flags a readback mismatch from the community write response', async () => {
    queueResults(
      { ok: true, data: { userId: 'user-1' } },
      { ok: true, data: { displayName: 'Old Name', bio: '', location: '', website: '', jobTitle: '', company: '' } },
      { ok: true, data: { displayName: 'Ari' } },
      { ok: true, data: { handle: 'someone-else', displayName: 'Ari' } },
    );

    const result = await updateProfileAction(completeInput);
    expect(result.success).toBe(false);
    expect(result.error).toContain('did not persist as submitted');
  });

  it('requires a display name and rejects non-http URLs before any write', async () => {
    await expect(updateProfileAction({ ...completeInput, displayName: '   ' })).resolves.toEqual({
      success: false,
      error: 'Display name is required.',
    });
    await expect(updateProfileAction({ ...completeInput, website: 'ftp://example.com' })).resolves.toEqual({
      success: false,
      error: 'Field "website" must be a valid http(s) URL.',
    });
    await expect(updateProfileAction({ ...completeInput, bannerUrl: 'not a url' })).resolves.toEqual({
      success: false,
      error: 'Field "bannerUrl" must be a valid http(s) URL.',
    });
    expect(mocks.request).not.toHaveBeenCalled();
  });

  it('rejects invalid community handles before writing either profile', async () => {
    await expect(updateProfileAction({
      ...completeInput,
      handle: 'bad handle',
    })).resolves.toEqual({
      success: false,
      error: 'Community handle must use 3–80 letters, numbers, underscores, or hyphens.',
    });
    expect(mocks.request).not.toHaveBeenCalled();
  });

  it('updates community profile visibility without clobbering other privacy flags', async () => {
    queueResults(
      { ok: true, data: { showActivity: true, showPortfolio: false, showSkills: true, visibility: 'Public' } },
      { ok: true, data: undefined },
    );

    await expect(updateProfileVisibilityAction('members')).resolves.toEqual({ success: true, data: undefined });

    expect(mocks.request).toHaveBeenNthCalledWith(2, {
      method: 'PUT',
      path: '/api/social/profiles/users/user-1/privacy',
      body: {
        visibility: 'Connections',
        showActivity: true,
        showPortfolio: false,
        showSkills: true,
      },
      requiresAuth: true,
    });
  });

  it('guides users to save their profile first when no community profile exists', async () => {
    queueResults({ ok: false, error: { message: 'Not found', status: 404 } });

    await expect(updateProfileVisibilityAction('private')).resolves.toEqual({
      success: false,
      error: 'Save your profile once before changing its visibility.',
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

    queueResults({ ok: false, error: { message: 'Not allowed' } });
    await expect(updateProfileVisibilityAction('public')).resolves.toEqual({
      success: false,
      error: 'Not allowed',
    });
  });

  it('returns a controlled failure when a client initializer cannot read server preferences', async () => {
    mocks.getGeneralPreferences.mockRejectedValue(new Error('Unauthorized'));

    await expect(getThemePreferenceAction()).resolves.toEqual({ success: false, error: 'Unauthorized' });
  });
});
