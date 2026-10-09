import '@testing-library/jest-dom/vitest';
import { render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PrivacyPreferenceData } from '@/lib/user-settings/preferences-mappers';

const mocks = vi.hoisted(() => ({
  getPrivacyPreference: vi.fn(),
  gtmProps: [] as Array<Record<string, unknown>>,
  gaProps: [] as Array<Record<string, unknown>>,
}));

vi.mock('@/lib/user-settings/queries', () => ({
  getPrivacyPreference: mocks.getPrivacyPreference,
}));

vi.mock('./google-tag-manager', () => ({
  GoogleTagManager: (props: Record<string, unknown>) => {
    mocks.gtmProps.push(props);
    return <div data-testid="gtm-mount" />;
  },
}));

vi.mock('./google-analytics', () => ({
  GoogleAnalytics: (props: Record<string, unknown>) => {
    mocks.gaProps.push(props);
    return <div data-testid="ga-mount" />;
  },
}));

import { AnalyticsConsent } from './analytics-consent';

const GTM_ENV_KEY = 'NEXT_PUBLIC_GTM_CONTAINER_ID';
const GA_ENV_KEY = 'NEXT_PUBLIC_GA_MEASUREMENT_ID';

function privacy(overrides: Partial<PrivacyPreferenceData> = {}): PrivacyPreferenceData {
  return {
    profileVisibility: 'public',
    activityTracking: true,
    marketingEmails: true,
    analyticsCookies: true,
    personalizedContent: true,
    ...overrides,
  };
}

describe('AnalyticsConsent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getPrivacyPreference.mockResolvedValue(privacy());
    mocks.gtmProps.length = 0;
    mocks.gaProps.length = 0;
    delete process.env[GTM_ENV_KEY];
    delete process.env[GA_ENV_KEY];
  });

  afterEach(() => {
    delete process.env[GTM_ENV_KEY];
    delete process.env[GA_ENV_KEY];
  });

  it('is a no-op without configured analytics ids', async () => {
    const { container } = render(await AnalyticsConsent());

    expect(container).toBeEmptyDOMElement();
    expect(mocks.getPrivacyPreference).not.toHaveBeenCalled();
    expect(mocks.gtmProps).toHaveLength(0);
    expect(mocks.gaProps).toHaveLength(0);
  });

  it('treats blank analytics ids as unset', async () => {
    process.env[GTM_ENV_KEY] = '   ';
    process.env[GA_ENV_KEY] = '   ';

    const { container } = render(await AnalyticsConsent());

    expect(container).toBeEmptyDOMElement();
    expect(mocks.getPrivacyPreference).not.toHaveBeenCalled();
  });

  it('mounts only the GTM loader when just the container id is set', async () => {
    process.env[GTM_ENV_KEY] = 'GTM-TESTID';

    render(await AnalyticsConsent());

    expect(mocks.getPrivacyPreference).toHaveBeenCalledTimes(1);
    expect(mocks.gtmProps).toEqual([{ containerId: 'GTM-TESTID', consent: true }]);
    expect(mocks.gaProps).toHaveLength(0);
  });

  it('mounts only the GA4 loader when just the measurement id is set', async () => {
    process.env[GA_ENV_KEY] = 'G-TESTID';

    render(await AnalyticsConsent());

    expect(mocks.getPrivacyPreference).toHaveBeenCalledTimes(1);
    expect(mocks.gaProps).toEqual([{ measurementId: 'G-TESTID', consent: true }]);
    expect(mocks.gtmProps).toHaveLength(0);
  });

  it('mounts both loaders with a single consent read when both ids are set', async () => {
    process.env[GTM_ENV_KEY] = 'GTM-TESTID';
    process.env[GA_ENV_KEY] = 'G-TESTID';

    render(await AnalyticsConsent());

    expect(mocks.getPrivacyPreference).toHaveBeenCalledTimes(1);
    expect(mocks.gtmProps).toEqual([{ containerId: 'GTM-TESTID', consent: true }]);
    expect(mocks.gaProps).toEqual([{ measurementId: 'G-TESTID', consent: true }]);
  });

  it('keeps the loaders dormant when the analytics-cookie switch is off', async () => {
    process.env[GTM_ENV_KEY] = 'GTM-TESTID';
    process.env[GA_ENV_KEY] = 'G-TESTID';
    mocks.getPrivacyPreference.mockResolvedValue(privacy({ analyticsCookies: false }));

    render(await AnalyticsConsent());

    expect(mocks.gtmProps).toEqual([{ containerId: 'GTM-TESTID', consent: false }]);
    expect(mocks.gaProps).toEqual([{ measurementId: 'G-TESTID', consent: false }]);
  });

  it('fails closed when the preference read fails', async () => {
    process.env[GTM_ENV_KEY] = 'GTM-TESTID';
    process.env[GA_ENV_KEY] = 'G-TESTID';
    mocks.getPrivacyPreference.mockRejectedValue(new Error('preferences API unavailable'));

    render(await AnalyticsConsent());

    expect(mocks.gtmProps).toEqual([{ containerId: 'GTM-TESTID', consent: false }]);
    expect(mocks.gaProps).toEqual([{ measurementId: 'G-TESTID', consent: false }]);
  });
});
