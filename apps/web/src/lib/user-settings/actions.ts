'use server';

/**
 * User settings server actions.
 *
 * All mutations go through the `@game-guild/client` ApiClient. The profile
 * and preference endpoints use the client's own `request` pipeline because
 * their generated schemas do not match the deployed API contracts. Using
 * `client.request` keeps the same auth, tenant, and error pipeline without
 * the mismatched generated validation.
 */

import { getAuthenticatedUserId, getUserSettingsApiClient } from './api-client';
import {
  type AccessibilityPreferenceData,
  type EditorGlobalPreferencesBlob,
  type LocalizationPreferenceData,
  type PreferencePatch,
  type PrivacyPreferenceData,
  type ThemePreference,
  buildAccessibilityPayload,
  buildLocalizationPayload,
  buildPrivacyPayload,
} from './preferences-mappers';
import { getAccessibilityPreference, getGeneralPreferences } from './queries';

export type ActionResult<T = void> =
  | { readonly success: true; readonly data: T }
  | { readonly success: false; readonly error: string };

const PROFILE_FIELD_LIMITS = {
  handle: 80,
  displayName: 100,
  bio: 1000,
  location: 100,
  website: 255,
  jobTitle: 100,
  company: 100,
  avatarUrl: 500,
  bannerUrl: 500,
} as const;

export interface ProfileFormInput {
  readonly handle: string;
  readonly displayName: string;
  readonly bio: string;
  readonly location: string;
  readonly website: string;
  readonly jobTitle: string;
  readonly company: string;
  readonly avatarUrl: string;
  readonly bannerUrl: string;
}

const PROFILE_FIELDS = [
  'handle',
  'displayName',
  'bio',
  'location',
  'website',
  'jobTitle',
  'company',
  'avatarUrl',
  'bannerUrl',
] as const;

const PROFILE_URL_FIELDS = ['website', 'avatarUrl', 'bannerUrl'] as const;

type AccountProfileBody = Pick<ProfileFormInput, 'displayName' | 'bio' | 'location' | 'website' | 'jobTitle' | 'company'>;

function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function text(value: unknown): string {
  return typeof value === 'string' ? value : '';
}

async function patchPreferences(
  userId: string,
  body: PreferencePatch,
): Promise<ActionResult> {
  const client = getUserSettingsApiClient();
  const result = await client.request<void>({
    method: 'PATCH',
    path: `/v1/users/${userId}/preferences`,
    body,
    requiresAuth: true,
  });

  if (!result.ok) {
    return { success: false, error: result.error.message || 'Failed to save preferences.' };
  }

  return { success: true, data: undefined };
}

async function patchCategoryPreferences(
  category: 'localization' | 'privacy' | 'accessibility',
  body: PreferencePatch,
): Promise<ActionResult> {
  const userId = await getAuthenticatedUserId();
  if (!userId) return { success: false, error: 'Not authenticated.' };

  const client = getUserSettingsApiClient();
  const result = await client.request<void>({
    method: 'PATCH',
    path: `/v1/users/${userId}/preferences/${category}`,
    body,
    requiresAuth: true,
  });

  if (!result.ok) {
    return { success: false, error: result.error.message || 'Failed to save preferences.' };
  }

  return { success: true, data: undefined };
}

// ---------------------------------------------------------------------------
// Profile
// ----------------------------------------------------------------------------

/**
 * Saves both profile records (account profile + community/social profile) as
 * one logical operation:
 *
 * 1. Validates fields and pre-checks community handle availability so the
 *    most common conflict surfaces before any write happens.
 * 2. Snapshots the previous account profile so the first write can be rolled
 *    back when the second (community) write fails — no partial saves.
 * 3. Verifies the persisted handle/displayName from the write responses
 *    (readback) so silent persistence failures are reported.
 */
export async function updateProfileAction(input: ProfileFormInput): Promise<ActionResult> {
  const userId = await getAuthenticatedUserId();
  if (!userId) return { success: false, error: 'Not authenticated.' };

  const trimmed: ProfileFormInput = {
    handle: input.handle.trim().replace(/^@/, '').toLowerCase(),
    displayName: input.displayName.trim(),
    bio: input.bio.trim(),
    location: input.location.trim(),
    website: input.website.trim(),
    jobTitle: input.jobTitle.trim(),
    company: input.company.trim(),
    avatarUrl: input.avatarUrl.trim(),
    bannerUrl: input.bannerUrl.trim(),
  };

  for (const field of PROFILE_FIELDS) {
    const limit = PROFILE_FIELD_LIMITS[field];
    if (trimmed[field].length > limit) {
      return { success: false, error: `Field "${field}" must be at most ${limit} characters.` };
    }
  }
  if (!/^[a-z0-9_-]{3,80}$/.test(trimmed.handle)) {
    return { success: false, error: 'Community handle must use 3–80 letters, numbers, underscores, or hyphens.' };
  }
  if (trimmed.displayName.length === 0) {
    return { success: false, error: 'Display name is required.' };
  }
  for (const field of PROFILE_URL_FIELDS) {
    if (trimmed[field] && !isHttpUrl(trimmed[field])) {
      return { success: false, error: `Field "${field}" must be a valid http(s) URL.` };
    }
  }

  const client = getUserSettingsApiClient();

  // Pre-check handle availability. A miss (404) means the handle is free;
  // transport errors fall through — the upsert below stays authoritative.
  const handleLookup = await client.request<{ readonly userId?: unknown }>({
    method: 'GET',
    path: `/api/social/profiles/@${encodeURIComponent(trimmed.handle)}`,
    requiresAuth: true,
  });
  if (handleLookup.ok) {
    const owner = text(handleLookup.data?.userId).toLowerCase();
    if (owner && owner !== userId.toLowerCase()) {
      return { success: false, error: 'That community handle is already taken. Choose another one.' };
    }
  }

  // Snapshot the previous account profile for rollback.
  const previousAccount = await client.request<Record<string, unknown>>({
    method: 'GET',
    path: `/v1/users/${userId}/profile`,
    requiresAuth: true,
  });
  const rollbackBody: AccountProfileBody = previousAccount.ok
    ? {
        displayName: text(previousAccount.data?.displayName),
        bio: text(previousAccount.data?.bio),
        location: text(previousAccount.data?.location),
        website: text(previousAccount.data?.website),
        jobTitle: text(previousAccount.data?.jobTitle),
        company: text(previousAccount.data?.company),
      }
    : { displayName: '', bio: '', location: '', website: '', jobTitle: '', company: '' };

  const accountBody: AccountProfileBody = {
    displayName: trimmed.displayName,
    bio: trimmed.bio,
    location: trimmed.location,
    website: trimmed.website,
    jobTitle: trimmed.jobTitle,
    company: trimmed.company,
  };

  const accountResult = await client.request<{ readonly displayName?: unknown }>({
    method: 'PATCH',
    path: `/v1/users/${userId}/profile`,
    body: accountBody,
    requiresAuth: true,
  });

  if (!accountResult.ok) {
    return { success: false, error: accountResult.error.message || 'Failed to save profile.' };
  }

  const socialBody = {
    handle: trimmed.handle,
    displayName: trimmed.displayName,
    bio: trimmed.bio || null,
    avatarUrl: trimmed.avatarUrl || null,
    bannerUrl: trimmed.bannerUrl || null,
    headline: [trimmed.jobTitle, trimmed.company].filter(Boolean).join(' at ') || null,
    location: trimmed.location || null,
    websiteUrl: trimmed.website || null,
    socialLinksJson: '{}',
  };

  const socialResult = await client.request<{ readonly handle?: unknown; readonly displayName?: unknown }>({
    method: 'PUT',
    path: `/api/social/profiles/users/${userId}`,
    body: socialBody,
    requiresAuth: true,
  });

  if (!socialResult.ok) {
    // Second write failed: roll the account write back so no partial save
    // survives. If the rollback itself fails, say so explicitly.
    const rollback = await client.request<void>({
      method: 'PATCH',
      path: `/v1/users/${userId}/profile`,
      body: rollbackBody,
      requiresAuth: true,
    });
    const reason = socialResult.error.message || 'Failed to save community profile.';
    if (!rollback.ok) {
      return {
        success: false,
        error: `${reason} Your account profile was saved but could not be rolled back — save again to retry.`,
      };
    }
    return { success: false, error: reason };
  }

  // Readback verification from the write responses.
  if (
    typeof socialResult.data === 'object' &&
    socialResult.data !== null &&
    (text(socialResult.data.handle).toLowerCase() !== trimmed.handle ||
      text(socialResult.data.displayName) !== trimmed.displayName)
  ) {
    return {
      success: false,
      error: 'The profile was saved but did not persist as submitted. Reload the page and try again.',
    };
  }
  if (
    typeof accountResult.data === 'object' &&
    accountResult.data !== null &&
    text(accountResult.data.displayName) !== trimmed.displayName
  ) {
    return {
      success: false,
      error: 'The profile was saved but did not persist as submitted. Reload the page and try again.',
    };
  }

  return { success: true, data: undefined };
}

// ---------------------------------------------------------------------------
// Profile visibility (community profile privacy)
// ----------------------------------------------------------------------------

export type ProfileVisibilitySetting = 'public' | 'members' | 'private';

const SOCIAL_VISIBILITY_BY_SETTING = {
  public: 'Public',
  members: 'Connections',
  private: 'Private',
} as const satisfies Record<ProfileVisibilitySetting, string>;

function booleanOr(value: unknown, fallback: boolean): boolean {
  return typeof value === 'boolean' ? value : fallback;
}

/**
 * Updates who can see the user's public community profile. Persists through
 * the social profile privacy endpoint; the other privacy flags are round-
 * tripped from the current record so they are never clobbered.
 */
export async function updateProfileVisibilityAction(
  visibility: ProfileVisibilitySetting,
): Promise<ActionResult> {
  const userId = await getAuthenticatedUserId();
  if (!userId) return { success: false, error: 'Not authenticated.' };

  const client = getUserSettingsApiClient();
  const current = await client.request<Record<string, unknown>>({
    method: 'GET',
    path: `/api/social/profiles/users/${userId}`,
    requiresAuth: true,
  });

  if (!current.ok) {
    if (current.error.status === 404) {
      return { success: false, error: 'Save your profile once before changing its visibility.' };
    }
    return { success: false, error: current.error.message || 'Failed to load community profile.' };
  }

  const result = await client.request<void>({
    method: 'PUT',
    path: `/api/social/profiles/users/${userId}/privacy`,
    body: {
      visibility: SOCIAL_VISIBILITY_BY_SETTING[visibility],
      showActivity: booleanOr(current.data?.showActivity, true),
      showPortfolio: booleanOr(current.data?.showPortfolio, true),
      showSkills: booleanOr(current.data?.showSkills, true),
    },
    requiresAuth: true,
  });

  if (!result.ok) {
    return { success: false, error: result.error.message || 'Failed to save profile visibility.' };
  }

  return { success: true, data: undefined };
}

// ---------------------------------------------------------------------------
// Theme (general preferences)
// ---------------------------------------------------------------------------

export async function updateThemePreferenceAction(theme: ThemePreference): Promise<ActionResult> {
  const userId = await getAuthenticatedUserId();
  if (!userId) return { success: false, error: 'Not authenticated.' };

  return patchPreferences(userId, {
    generalPreferences: { theme },
  });
}

export async function getThemePreferenceAction(): Promise<ActionResult<ThemePreference | null>> {
  try {
    const preferences = await getGeneralPreferences();
    return { success: true, data: preferences.theme };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Failed to load preferences.' };
  }
}

// ---------------------------------------------------------------------------
// Localization
// ---------------------------------------------------------------------------

export async function updateLocalizationPreferenceAction(
  data: LocalizationPreferenceData,
): Promise<ActionResult> {
  return patchCategoryPreferences('localization', buildLocalizationPayload(data));
}

// ---------------------------------------------------------------------------
// Privacy
// ---------------------------------------------------------------------------

export async function updatePrivacyPreferenceAction(
  data: PrivacyPreferenceData,
): Promise<ActionResult> {
  return patchCategoryPreferences('privacy', buildPrivacyPayload(data));
}

// ---------------------------------------------------------------------------
// Accessibility
// ---------------------------------------------------------------------------

export async function updateAccessibilityPreferenceAction(
  data: AccessibilityPreferenceData,
): Promise<ActionResult> {
  return patchCategoryPreferences('accessibility', buildAccessibilityPayload(data));
}

export async function getAccessibilityPreferenceAction(): Promise<
  ActionResult<AccessibilityPreferenceData>
> {
  try {
    return { success: true, data: await getAccessibilityPreference() };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Failed to load preferences.' };
  }
}

// ---------------------------------------------------------------------------
// Editor global preferences mirror
// ---------------------------------------------------------------------------

export async function updateEditorGlobalPreferencesAction(
  global: EditorGlobalPreferencesBlob,
): Promise<ActionResult> {
  const userId = await getAuthenticatedUserId();
  if (!userId) return { success: false, error: 'Not authenticated.' };

  return patchPreferences(userId, {
    generalPreferences: { EditorPreferences: global },
  });
}

export async function getEditorGlobalPreferencesAction(): Promise<
  ActionResult<EditorGlobalPreferencesBlob | null>
> {
  try {
    const preferences = await getGeneralPreferences();
    return { success: true, data: preferences.editorPreferences };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : 'Failed to load preferences.' };
  }
}
