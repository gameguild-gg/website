function valueOf(candidate, owner) {
  return typeof candidate === 'function' ? candidate.call(owner) : candidate;
}

function unique(values) {
  return [...new Set(values.filter(Boolean))];
}

export function buildTestingLabFixtureUsername(kind, tag) {
  const username = `tl_browser_${kind}_${String(tag).replace(/[^a-z0-9]/gi, '_')}`;
  if (!/^[a-z0-9_]{3,50}$/i.test(username)) {
    throw new RangeError('Testing Lab fixture usernames must contain 3 to 50 letters, numbers, or underscores.');
  }
  return username;
}

export async function createTestingLabFixtureIdentities(createIdentity) {
  const identities = [];
  // Local sign-in serializes the shared source-IP budget. Bootstrap these users
  // in order so the fixture respects the same advisory lock as real clients.
  for (const kind of ['owner', 'reviewer', 'tester']) {
    identities.push(await createIdentity(kind));
  }
  return identities;
}

export function requireDisposableDatabaseMode(mode) {
  if (String(mode).toLowerCase() !== 'disposable') {
    throw new Error(
      'Testing Lab browser E2E requires a disposable database. Use the isolated E2E runner instead of a shared development or production database.',
    );
  }
}

export function responseFailure(response, webBaseUrl) {
  const status = Number(valueOf(response.status, response));
  if (status < 400) return null;

  const url = new URL(String(valueOf(response.url, response)));
  if (url.origin !== new URL(webBaseUrl).origin) return null;
  return `${status} ${url.pathname}${url.search}`;
}

export function throwForBrowserQualityFailures({
  failedResponses = [],
  browserErrors = [],
  accessibilityFailures = [],
  viewportFailures = [],
}) {
  const sections = [
    ['HTTP failures', unique(failedResponses)],
    ['Browser errors', unique(browserErrors)],
    ['Accessibility failures', unique(accessibilityFailures)],
    ['Viewport failures', unique(viewportFailures)],
  ].filter(([, failures]) => failures.length > 0);

  if (sections.length === 0) return;
  throw new Error(
    sections
      .map(([title, failures]) => `${title}:\n${failures.join('\n')}`)
      .join('\n\n'),
  );
}

export async function cleanupTestingLabFixture(fixture, request) {
  if (!fixture?.eventId) return [];

  const failures = [];
  try {
    await request(`/v1/testing/events/${fixture.eventId}:cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: 'Browser E2E fixture completed.' }),
    });
  } catch (error) {
    failures.push(error instanceof Error ? error.message : String(error));
  }
  return failures;
}

export async function collectAccessibilityFailures(page, label) {
  const issues = await page.evaluate(() => {
    const visible = (element) => {
      const style = window.getComputedStyle(element);
      const bounds = element.getBoundingClientRect();
      return (
        style.display !== 'none' &&
        style.visibility !== 'hidden' &&
        bounds.width > 0 &&
        bounds.height > 0
      );
    };
    const accessibleName = (element) => {
      const labelledBy = element.getAttribute('aria-labelledby');
      const labelledText = labelledBy
        ?.split(/\s+/)
        .map((id) => document.getElementById(id)?.textContent?.trim())
        .filter(Boolean)
        .join(' ');
      const label = element.id
        ? document.querySelector(`label[for="${CSS.escape(element.id)}"]`)?.textContent?.trim()
        : '';
      const wrappingLabel = element.closest('label')?.textContent?.trim();
      return (
        element.getAttribute('aria-label')?.trim() ||
        labelledText ||
        label ||
        wrappingLabel ||
        element.getAttribute('title')?.trim() ||
        element.textContent?.trim() ||
        ''
      );
    };
    const findings = [];
    const ids = new Map();

    document.querySelectorAll('[id]').forEach((element) => {
      ids.set(element.id, (ids.get(element.id) ?? 0) + 1);
    });
    ids.forEach((count, id) => {
      if (count > 1) findings.push(`duplicate id "${id}" appears ${count} times`);
    });

    if (document.querySelector('main main')) findings.push('nested main landmarks');
    const headings = [...document.querySelectorAll('h1')].filter(visible);
    if (headings.length !== 1) findings.push(`expected one visible h1, found ${headings.length}`);

    document.querySelectorAll('button, input, select, textarea').forEach((element) => {
      if (!visible(element) || element.getAttribute('type') === 'hidden') return;
      if (
        element.getAttribute('aria-hidden') === 'true' ||
        element.closest('[aria-hidden="true"], [inert]')
      ) return;
      if (!accessibleName(element)) findings.push(`${element.tagName.toLowerCase()} without an accessible name`);
    });
    document.querySelectorAll('img').forEach((image) => {
      if (visible(image) && !image.hasAttribute('alt')) findings.push('visible image without alt text');
    });
    document.querySelectorAll('a').forEach((link) => {
      if (visible(link) && !link.getAttribute('href')) findings.push('visible link without href');
    });

    return findings;
  });
  return issues.map((issue) => `${label}: ${issue}`);
}

export async function collectViewportFailures(page, label) {
  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  return dimensions.scrollWidth > dimensions.clientWidth + 2
    ? [`${label}: ${dimensions.scrollWidth}px content in a ${dimensions.clientWidth}px viewport`]
    : [];
}

// Compatibility for existing callers; retain the complete bounded unique tag.
export const createTestingLabFixtureUsername = buildTestingLabFixtureUsername;
