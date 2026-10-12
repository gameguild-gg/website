import '@testing-library/jest-dom/vitest';
import { render } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const scriptRenders: Array<Record<string, unknown>> = [];

vi.mock('next/script', () => ({
  default: (props: Record<string, unknown>) => {
    scriptRenders.push(props);
    return null;
  },
}));

import { GoogleAnalytics } from './google-analytics';

type AnalyticsWindow = Window & {
  dataLayer?: unknown[];
  gtag?: (...args: unknown[]) => void;
};

function analyticsWindow(): AnalyticsWindow {
  return window as AnalyticsWindow;
}

describe('GoogleAnalytics', () => {
  beforeEach(() => {
    scriptRenders.length = 0;
    delete analyticsWindow().dataLayer;
    delete analyticsWindow().gtag;
  });

  it('is a no-op without a measurement id', () => {
    const { container } = render(<GoogleAnalytics consent />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(analyticsWindow().dataLayer).toBeUndefined();
    expect(analyticsWindow().gtag).toBeUndefined();
  });

  it('treats a blank measurement id as unset', () => {
    const { container } = render(<GoogleAnalytics measurementId="   " consent />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(analyticsWindow().dataLayer).toBeUndefined();
  });

  it('stays dormant while analytics-cookie consent is off', () => {
    const { container } = render(<GoogleAnalytics measurementId="G-TESTID" />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(analyticsWindow().dataLayer).toBeUndefined();
    expect(analyticsWindow().gtag).toBeUndefined();
  });

  it('loads gtag.js and issues the standard js/config calls once consent is granted', () => {
    render(<GoogleAnalytics measurementId="G-TESTID" consent />);

    expect(scriptRenders).toHaveLength(1);
    expect(scriptRenders[0]).toMatchObject({
      id: 'gg-ga4-loader',
      strategy: 'afterInteractive',
      src: 'https://www.googletagmanager.com/gtag/js?id=G-TESTID',
    });

    const w = analyticsWindow();
    expect(typeof w.gtag).toBe('function');

    const layer = w.dataLayer;
    expect(layer).toHaveLength(2);
    expect(layer![0]).toEqual(['js', expect.any(Date)]);
    expect(layer![1]).toEqual(['config', 'G-TESTID']);
  });

  it('keeps an existing dataLayer and preserves an existing gtag shim', () => {
    const w = analyticsWindow();
    w.dataLayer = [{ event: 'existing' }];
    const existingGtag = vi.fn();
    w.gtag = existingGtag;

    render(<GoogleAnalytics measurementId="G-TESTID" consent />);

    expect(w.gtag).toBe(existingGtag);
    expect(existingGtag).toHaveBeenCalledWith('js', expect.any(Date));
    expect(existingGtag).toHaveBeenCalledWith('config', 'G-TESTID');
    expect(w.dataLayer).toHaveLength(1);
  });
});
