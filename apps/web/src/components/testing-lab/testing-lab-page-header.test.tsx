import { renderToString } from 'react-dom/server';
import { NextIntlClientProvider } from 'next-intl';
import { describe, expect, it } from 'vitest';

import { TestingLabOperationsNavigation } from './testing-lab-page-header';

describe('TestingLabOperationsNavigation', () => {
  it('renders through the server without falling back to client rendering', () => {
    expect(() =>
      renderToString(
        <NextIntlClientProvider locale="en-US" messages={{}}>
          <TestingLabOperationsNavigation />
        </NextIntlClientProvider>,
      ),
    ).not.toThrow();
  });

  it('names every workspace and marks the current destination', () => {
    const html = renderToString(
      <NextIntlClientProvider locale="en-US" messages={{}}>
        <TestingLabOperationsNavigation activeHref="/console/community/testing-lab" />
      </NextIntlClientProvider>,
    );

    expect(html).toContain('Overview');
    expect(html).toContain('Events');
    expect(html).toContain('Applications');
    expect(html).toContain('Projects');
    expect(html).toContain('Participants');
    expect(html).toContain('Analytics');
    expect(html).toContain('Settings');
    expect(html).toContain('aria-current="page"');
  });
});
