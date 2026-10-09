import { getPrivacyPreference } from '@/lib/user-settings/queries';
import { GoogleTagManager } from './google-tag-manager';

/**
 * Server-side mount for the Google Tag Manager container.
 *
 * Gating rules, in order:
 *
 * 1. `NEXT_PUBLIC_GTM_CONTAINER_ID` unset or blank → complete no-op. The
 *    privacy preference is not even read, so dev and undeployed environments
 *    carry zero GTM/GA footprint. No container id is committed to the repo.
 * 2. Container configured but the user's analytics-cookie consent
 *    (`privacyPreferences.AnalyticsCookies`, surfaced by the settings
 *    `#privacy-analytics-cookies` switch) is off → the loader never mounts.
 * 3. Preference read fails (API unavailable, transport error) → fails closed:
 *    consent is treated as denied.
 *
 * Anonymous visitors follow the product default for the privacy preferences
 * (`DEFAULT_PRIVACY`), the same shape the settings page renders.
 */
export async function GoogleTagManagerConsent() {
  const containerId = process.env.NEXT_PUBLIC_GTM_CONTAINER_ID?.trim();
  if (!containerId) return null;

  let analyticsConsent = false;
  try {
    analyticsConsent = (await getPrivacyPreference()).analyticsCookies;
  } catch {
    analyticsConsent = false;
  }

  return <GoogleTagManager containerId={containerId} consent={analyticsConsent} />;
}
