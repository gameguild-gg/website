'use client';

import { useReportWebVitals } from 'next/web-vitals';
import { sendGAEvent } from '@next/third-parties/google';

export const WebVitals = (): null => {
  useReportWebVitals((metric) => {
    const payload = JSON.stringify(metric);

    // Try to send the metric to an analytics endpoint
    // using `navigator.sendBeacon` if available.
    // TODO: Uncomment this when the endpoint is ready.
    // if (navigator.sendBeacon) {
    //   navigator.sendBeacon('/api/web‑vitals', payload);
    // } else {
    //   void fetch('/api/web‑vitals', {
    //     method: 'POST',
    //     headers: { 'Content-Type': 'application/json' },
    //     body: payload,
    //     keepalive: true,
    //   });
    // }

    sendGAEvent('event', metric.name, {
      value: Math.round(metric.name === 'CLS' ? metric.value * 1000 : metric.value),
      metric_id: metric.id,
      metric_category: 'Web Vitals',
      metric_value: metric.value,
      metric_delta: metric.delta,
      metric_rating: metric.rating,
      metric_navigation_type: metric.navigationType,
      page_path: window.location.pathname,
      non_interaction: true,
    });

    // TODO: Remove this console log in production
    console.log('Web Vitals metric:', metric);
  });

  return null;
};
