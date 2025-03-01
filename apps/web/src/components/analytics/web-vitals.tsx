'use client';

import { useReportWebVitals } from 'next/web-vitals';

export function WebVitals() {
  useReportWebVitals((metric) => {
    // Try to send the metric to an analytics endpoint
    // using `navigator.sendBeacon` if available.
    // if (navigator.sendBeacon){
    //
    // } else {
    // TODO: Fallback to `fetch` if `navigator.sendBeacon` is not available.
    // }

    // TODO: https://github.com/GoogleChrome/web-vitals#send-the-results-to-google-analytics

    console.log('Web Vital Metrics: %O', metric);
  });

  return null;
}
