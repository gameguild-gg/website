'use client';

import Script from 'next/script';
import { useEffect } from 'react';

export interface GoogleAnalyticsProps {
  /**
   * GA4 measurement id (for example `G-XXXXXXX`). When missing or blank the
   * component is a complete no-op: it renders nothing, never touches
   * `window.dataLayer`/`window.gtag`, and injects no scripts. This keeps
   * development and undeployed environments free of any GA signature.
   */
  readonly measurementId?: string;
  /**
   * Analytics-cookie consent read from the user's privacy settings (the
   * `#privacy-analytics-cookies` switch). The loader stays dormant until
   * consent is granted.
   */
  readonly consent?: boolean;
}

type AnalyticsWindow = Window & {
  dataLayer?: unknown[];
  gtag?: (...args: unknown[]) => void;
};

/**
 * Consent-gated Google Analytics 4 (gtag.js) loader.
 *
 * Implements the documented GA4 embed: loads `gtag.js` for the measurement
 * id, ensures `window.dataLayer` exists, defines the `window.gtag` shim that
 * queues commands onto the data layer, and issues the standard `js` +
 * `config` calls. GA4 shares `window.dataLayer` with the Google Tag Manager
 * embed (both are googletagmanager.com loaders), so the two can be mounted
 * side by side. Consent is evaluated per server render, so turning the
 * analytics-cookie switch off removes the loader from the next page load.
 */
export function GoogleAnalytics({ measurementId: rawMeasurementId, consent = false }: GoogleAnalyticsProps) {
  const measurementId = rawMeasurementId?.trim() || undefined;
  const enabled = Boolean(measurementId) && consent;

  useEffect(() => {
    if (!enabled) return;

    const w = window as AnalyticsWindow;
    w.dataLayer = w.dataLayer || [];
    w.gtag ??= (...args: unknown[]) => {
      w.dataLayer!.push(args);
    };
    w.gtag('js', new Date());
    w.gtag('config', measurementId);
  }, [enabled, measurementId]);

  if (!enabled) return null;

  return <Script id="gg-ga4-loader" src={`https://www.googletagmanager.com/gtag/js?id=${measurementId}`} strategy="afterInteractive" />;
}
