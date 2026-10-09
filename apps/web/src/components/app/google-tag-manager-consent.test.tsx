import '@testing-library/jest-dom/vitest';
import { render } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { PrivacyPreferenceData } from '@/lib/user-settings/preferences-mappers';

const mocks = vi.hoisted(() => ({
  getPrivacyPreference: vi.fn(),
  gtmProps: [] as Array<Record<string, unknown>>,
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

import { GoogleTagManagerConsent } from './google-tag-manager-consent';

const ENV_KEY = 'NEXT_PUBLIC_GTM_CONTAINER_ID';

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

describe('GoogleTagManagerConsent', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.gtmProps.length = 0;
    delete process.env[ENV_KEY];
  });

  afterEach(() => {
    delete process.env[ENV_KEY];
  });

  it('is a no-op without a configured container id', async () => {
    const { container } = render(await GoogleTagManagerConsent());

    expect(container).toBeEmptyDOMElement();
    expect(mocks.getPrivacyPreference).not.toHaveBeenCalled();
  });

  it('treats a blank container id as unset', async () => {
    process.env[ENV_KEY] = '   ';

    const { container } = render(await GoogleTagManagerConsent());

    expect(container).toBeEmptyDOMElement();
    expect(mocks.getPrivacyPreference).not.toHaveBeenCalled();
  });

  it('mounts the loader with consent for analytics cookies on', async () => {
    process.env[ENV_KEY] = 'GTM-TESTID';
    mocks.getPrivacyPreference.mockResolvedValue(privacy({ analyticsCookies: true }));

    const { container } = render(await GoogleTagManagerConsent());

    expect(mocks.getPrivacyPreference).toHaveBeenCalledTimes(1);
    expect(container.querySelector('[data-testid="gtm-mount"]')).not.toBeNull();
    expect(mocks.gtmProps.at(-1)).toEqual({
      containerId: 'GTM-TESTID',
      consent: true,
    });
  });

  it('keeps the loader dormant when the analytics-cookie switch is off', async () => {
    process.env[ENV_KEY] = 'GTM-TESTID';
    mocks.getPrivacyPreference.mockResolvedValue(privacy({ analyticsCookies: false }));

    const { container } = render(await GoogleTagManagerConsent());

    expect(mocks.getPrivacyPreference).toHaveBeenCalledTimes(1);
    expect(container.querySelector('[data-testid="gtm-mount"]')).not.toBeNull();
    expect(mocks.gtmProps.at(-1)).toEqual({
      containerId: 'GTM-TESTID',
      consent: false,
    });
  });

  it('fails closed when the preference read fails', async () => {
    process.env[ENV_KEY] = 'GTM-TESTID';
    mocks.getPrivacyPreference.mockRejectedValue(new Error('preferences API unavailable'));

    const { container } = render(await GoogleTagManagerConsent());

    expect(container.querySelector('[data-testid="gtm-mount"]')).not.toBeNull();
    expect(mocks.gtmProps.at(-1)).toEqual({
      containerId: 'GTM-TESTID',
      consent: false,
    });
  });
});
