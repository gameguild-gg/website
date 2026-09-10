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
} as const;

export interface ProfileFormInput {
  readonly handle: string;
  readonly displayName: string;
  readonly bio: string;
  readonly location: string;
  readonly website: string;
  readonly jobTitle: string;
  readonly company: string;
}

const PROFILE_FIELDS = [
  'handle',
  'displayName',
  'bio',
  'location',
  'website',
  'jobTitle',
  'company',
] as const;

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
// ---------------------------------------------------------------------------

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

  const client = getUserSettingsApiClient();
  const result = await client.request<void>({
    method: 'PATCH',
    path: `/v1/users/${userId}/profile`,
    body: {
      displayName: trimmed.displayName,
      bio: trimmed.bio,
      location: trimmed.location,
      website: trimmed.website,
      jobTitle: trimmed.jobTitle,
      company: trimmed.company,
    },
    requiresAuth: true,
  });

  if (!result.ok) {
    return { success: false, error: result.error.message || 'Failed to save profile.' };
  }

  const socialProfile = await client.request<void>({
    method: 'PUT',
    path: `/api/social/profiles/users/${userId}`,
    body: {
      handle: trimmed.handle,
      displayName: trimmed.displayName,
      bio: trimmed.bio || null,
      headline: [trimmed.jobTitle, trimmed.company].filter(Boolean).join(' at ') || null,
      location: trimmed.location || null,
      websiteUrl: trimmed.website || null,
      socialLinksJson: '{}',
    },
    requiresAuth: true,
  });

  if (!socialProfile.ok) {
    return { success: false, error: socialProfile.error.message || 'Failed to save community profile.' };
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
