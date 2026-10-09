import '@testing-library/jest-dom/vitest';
import { render } from '@testing-library/react';
import { renderToString } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const scriptRenders: Array<Record<string, unknown>> = [];

vi.mock('next/script', () => ({
  default: (props: Record<string, unknown>) => {
    scriptRenders.push(props);
    return null;
  },
}));

import { GoogleTagManager } from './google-tag-manager';

type DataLayerWindow = Window & { dataLayer?: Record<string, unknown>[] };

function dataLayer(): Record<string, unknown>[] | undefined {
  return (window as DataLayerWindow).dataLayer;
}

describe('GoogleTagManager', () => {
  beforeEach(() => {
    scriptRenders.length = 0;
    delete (window as DataLayerWindow).dataLayer;
  });

  it('is a no-op without a container id', () => {
    const { container } = render(<GoogleTagManager consent />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(dataLayer()).toBeUndefined();
  });

  it('treats a blank container id as unset', () => {
    const { container } = render(<GoogleTagManager containerId="   " consent />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(dataLayer()).toBeUndefined();
  });

  it('stays dormant while analytics-cookie consent is off', () => {
    const { container } = render(<GoogleTagManager containerId="GTM-TESTID" />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(dataLayer()).toBeUndefined();
  });

  it('stays dormant when consent is explicitly off', () => {
    const { container } = render(<GoogleTagManager containerId="GTM-TESTID" consent={false} />);

    expect(container).toBeEmptyDOMElement();
    expect(scriptRenders).toHaveLength(0);
    expect(dataLayer()).toBeUndefined();
  });

  it('loads the container script and initializes dataLayer once consent is granted', () => {
    const { container } = render(<GoogleTagManager containerId="GTM-TESTID" consent />);

    expect(scriptRenders).toHaveLength(1);
    expect(scriptRenders[0]).toMatchObject({
      id: 'gg-gtm-loader',
      strategy: 'afterInteractive',
      src: 'https://www.googletagmanager.com/gtm.js?id=GTM-TESTID',
    });

    const layer = dataLayer();
    expect(layer).toHaveLength(1);
    expect(layer![0]).toMatchObject({ event: 'gtm.js' });
    expect(layer![0]!['gtm.start']).toEqual(expect.any(Number));

    // jsdom's client renderer skips element children inside <noscript>, so the
    // no-JS fallback is asserted on the server-rendered markup browsers get.
    expect(container.querySelector('noscript')).not.toBeNull();
    const html = renderToString(<GoogleTagManager containerId="GTM-TESTID" consent />);
    expect(html).toContain('<noscript>');
    expect(html).toContain('https://www.googletagmanager.com/ns.html?id=GTM-TESTID');
  });

  it('keeps an existing dataLayer and appends to it', () => {
    (window as DataLayerWindow).dataLayer = [{ event: 'existing' }];

    render(<GoogleTagManager containerId="GTM-TESTID" consent />);

    const layer = dataLayer();
    expect(layer).toHaveLength(2);
    expect(layer![0]).toMatchObject({ event: 'existing' });
    expect(layer![1]).toMatchObject({ event: 'gtm.js' });
  });
});
