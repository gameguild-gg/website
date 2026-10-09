'use client';

import Script from 'next/script';
import { useEffect } from 'react';

/**
 * Window shape expected by the standard Google Tag Manager container snippet.
 */
declare global {
  interface Window {
    dataLayer?: Record<string, unknown>[];
  }
}

export interface GoogleTagManagerProps {
  /**
   * GTM container id (for example `GTM-XXXXXXX`). When missing or blank the
   * component is a complete no-op: it renders nothing, never touches
   * `window.dataLayer`, and injects no scripts. This keeps development and
   * undeployed environments free of any GTM/GA signature.
   */
  readonly containerId?: string;
  /**
   * Analytics-cookie consent read from the user's privacy settings (the
   * `#privacy-analytics-cookies` switch). The loader stays dormant until
   * consent is granted.
   */
  readonly consent?: boolean;
}

/**
 * Consent-gated Google Tag Manager container loader.
 *
 * Renders the standard GTM pair — a `gtm.js` script loader plus the
 * `<noscript>` iframe fallback — and initializes `window.dataLayer` with the
 * `gtm.start` event, matching Google's documented embed. The component only
 * mounts when both a container id is configured and the user granted
 * analytics-cookie consent; consent is evaluated per server render, so
 * turning the switch off removes the container from the next page load.
 */
export function GoogleTagManager({ containerId: rawContainerId, consent = false }: GoogleTagManagerProps) {
  const containerId = rawContainerId?.trim() || undefined;
  const enabled = Boolean(containerId) && consent;

  useEffect(() => {
    if (!enabled) return;

    window.dataLayer = window.dataLayer || [];
    window.dataLayer.push({ 'gtm.start': new Date().getTime(), event: 'gtm.js' });
  }, [enabled]);

  if (!enabled) return null;

  return (
    <>
      <Script
        id="gg-gtm-loader"
        src={`https://www.googletagmanager.com/gtm.js?id=${containerId}`}
        strategy="afterInteractive"
      />
      <noscript>
        <iframe
          src={`https://www.googletagmanager.com/ns.html?id=${containerId}`}
          height="0"
          width="0"
          style={{ display: 'none', visibility: 'hidden' }}
        />
      </noscript>
    </>
  );
}
