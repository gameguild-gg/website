import { getPrivacyPreference } from '@/lib/user-settings/queries';
import { GoogleAnalytics } from './google-analytics';
import { GoogleTagManager } from './google-tag-manager';

/**
 * Server-side mount for the opt-in analytics embeds: Google Tag Manager
 * (#44) and Google Analytics 4 (#45).
 *
 * Gating rules, in order:
 *
 * 1. `NEXT_PUBLIC_GTM_CONTAINER_ID` / `NEXT_PUBLIC_GA_MEASUREMENT_ID` unset
 *    or blank → complete no-op. The privacy preference is not even read, so
 *    dev and undeployed environments carry zero GTM/GA footprint. No real
 *    container or measurement id is committed to the repo.
 * 2. Ids configured but the user's analytics-cookie consent
 *    (`privacyPreferences.AnalyticsCookies`, surfaced by the settings
 *    `#privacy-analytics-cookies` switch) is off → the loaders never mount.
 * 3. Preference read fails (API unavailable, transport error) → fails closed:
 *    consent is treated as denied.
 *
 * Anonymous visitors follow the product default for the privacy preferences
 * (`DEFAULT_PRIVACY`), the same shape the settings page renders.
 */
export async function AnalyticsConsent() {
  const gtmContainerId = process.env.NEXT_PUBLIC_GTM_CONTAINER_ID?.trim() || undefined;
  const gaMeasurementId = process.env.NEXT_PUBLIC_GA_MEASUREMENT_ID?.trim() || undefined;
  if (!gtmContainerId && !gaMeasurementId) return null;

  let analyticsConsent = false;
  try {
    analyticsConsent = (await getPrivacyPreference()).analyticsCookies;
  } catch {
    analyticsConsent = false;
  }

  return (
    <>
      {gtmContainerId ? (
        <GoogleTagManager containerId={gtmContainerId} consent={analyticsConsent} />
      ) : null}
      {gaMeasurementId ? (
        <GoogleAnalytics measurementId={gaMeasurementId} consent={analyticsConsent} />
      ) : null}
    </>
  );
}
