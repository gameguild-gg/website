import assert from 'node:assert/strict';
import test from 'node:test';

import {
  buildTestingLabFixtureUsername,
  collectAccessibilityFailures,
  cleanupTestingLabFixture,
  createTestingLabFixtureUsername,
  createTestingLabFixtureIdentities,
  requireDisposableDatabaseMode,
  responseFailure,
  throwForBrowserQualityFailures,
} from './testing-lab-browser-quality.mjs';

for (const kind of ['owner', 'reviewer', 'tester']) {
  test(`builds a valid bounded ${kind} fixture username with the complete unique tag`, () => {
    const tag = '1791223200000-ab12cd34';
    const username = buildTestingLabFixtureUsername(kind, tag);
    assert.match(username, /^[a-z0-9_]{3,50}$/i);
    assert.equal(username, `tl_browser_${kind}_1791223200000_ab12cd34`);
  });
}

test('keeps fixture usernames distinct across roles and runs', () => {
  const usernames = ['owner', 'reviewer', 'tester'].flatMap((kind) =>
    ['1791223200000-ab12cd34', '1791223200000-cd34ef56'].map((tag) =>
      buildTestingLabFixtureUsername(kind, tag),
    ),
  );
  assert.equal(new Set(usernames).size, 6);
});

test('rejects overlong fixture usernames before sending a sign-up request', () => {
  assert.throws(() => buildTestingLabFixtureUsername('reviewer', 'a'.repeat(51)), RangeError);
});

test('creates all fixture identities in order without concurrent source-IP sign-ins', async () => {
  const calls = [];
  let active = false;
  const identities = await createTestingLabFixtureIdentities(async (kind) => {
    assert.equal(active, false, 'the previous identity must finish before the next sign-in');
    active = true;
    calls.push(kind);
    await new Promise((resolve) => setImmediate(resolve));
    active = false;
    return { kind, authenticated: true };
  });
  assert.deepEqual(calls, ['owner', 'reviewer', 'tester']);
  assert.deepEqual(identities, calls.map((kind) => ({ kind, authenticated: true })));
});

test('stops fixture bootstrap on authentication failure without creating later identities', async () => {
  const calls = [];
  const denial = new Error('fixture sign-in denied');
  await assert.rejects(createTestingLabFixtureIdentities(async (kind) => {
    calls.push(kind);
    if (kind === 'reviewer') throw denial;
    return { kind };
  }), (error) => error === denial);
  assert.deepEqual(calls, ['owner', 'reviewer']);
});

test('ignores controls hidden from the accessibility tree', async () => {
  const heading = {
    id: '',
    tagName: 'H1',
    getAttribute: () => null,
    getBoundingClientRect: () => ({ width: 100, height: 20 }),
  };
  const hiddenSelect = {
    id: '',
    tagName: 'SELECT',
    textContent: '',
    getAttribute: (name) => (name === 'aria-hidden' ? 'true' : null),
    getBoundingClientRect: () => ({ width: 120, height: 36 }),
    closest: () => null,
  };
  const querySelectorAll = (selector) => {
    if (selector === 'h1') return [heading];
    if (selector === 'button, input, select, textarea') return [hiddenSelect];
    return [];
  };
  const originalDocument = globalThis.document;
  const originalWindow = globalThis.window;
  const originalCss = globalThis.CSS;
  globalThis.document = {
    querySelectorAll,
    querySelector: () => null,
    getElementById: () => null,
  };
  globalThis.window = {
    getComputedStyle: () => ({ display: 'block', visibility: 'visible' }),
  };
  globalThis.CSS = { escape: (value) => value };

  try {
    const page = { evaluate: async (callback) => callback() };
    assert.deepEqual(await collectAccessibilityFailures(page, 'Hidden control'), []);
  } finally {
    globalThis.document = originalDocument;
    globalThis.window = originalWindow;
    globalThis.CSS = originalCss;
  }
});
test('recognizes controls named by a wrapping label', async () => {
  const heading = {
    id: '',
    tagName: 'H1',
    getAttribute: () => null,
    getBoundingClientRect: () => ({ width: 100, height: 20 }),
  };
  const input = {
    id: '',
    tagName: 'INPUT',
    textContent: '',
    getAttribute: () => null,
    getBoundingClientRect: () => ({ width: 120, height: 36 }),
    closest: (selector) => selector === 'label' ? { textContent: 'Search events' } : null,
  };
  const querySelectorAll = (selector) => {
    if (selector === 'h1') return [heading];
    if (selector === 'button, input, select, textarea') return [input];
    return [];
  };
  const originalDocument = globalThis.document;
  const originalWindow = globalThis.window;
  const originalCss = globalThis.CSS;
  globalThis.document = {
    querySelectorAll,
    querySelector: () => null,
    getElementById: () => null,
  };
  globalThis.window = {
    getComputedStyle: () => ({ display: 'block', visibility: 'visible' }),
  };
  globalThis.CSS = { escape: (value) => value };

  try {
    const page = { evaluate: async (callback) => callback() };
    assert.deepEqual(await collectAccessibilityFailures(page, 'Wrapped label'), []);
  } finally {
    globalThis.document = originalDocument;
    globalThis.window = originalWindow;
    globalThis.CSS = originalCss;
  }
});
test('captures same-origin RSC failures instead of hiding them', () => {
  assert.equal(
    responseFailure(
      {
        status: 404,
        url: 'http://localhost:3011/dashboard/testing-lab/events?_rsc=abc123',
      },
      'http://localhost:3011',
    ),
    '404 /dashboard/testing-lab/events?_rsc=abc123',
  );
});

test('ignores successful and external responses', () => {
  assert.equal(
    responseFailure(
      { status: 200, url: 'http://localhost:3011/testing-lab' },
      'http://localhost:3011',
    ),
    null,
  );
  assert.equal(
    responseFailure(
      { status: 503, url: 'https://static.example.test/beacon.js' },
      'http://localhost:3011',
    ),
    null,
  );
});

test('preserves Playwright response method binding', () => {
  const response = {
    currentStatus: 404,
    currentUrl: 'http://localhost:3011/missing?_rsc=bound',
    status() {
      return this.currentStatus;
    },
    url() {
      return this.currentUrl;
    },
  };

  assert.equal(
    responseFailure(response, 'http://localhost:3011'),
    '404 /missing?_rsc=bound',
  );
});

test('reports HTTP, browser, accessibility, and viewport failures together', () => {
  assert.throws(
    () =>
      throwForBrowserQualityFailures({
        failedResponses: ['404 /missing?_rsc=1'],
        browserErrors: ['hydration failed'],
        accessibilityFailures: ['event detail: button without an accessible name'],
        viewportFailures: ['mobile settings: 430px content in a 390px viewport'],
      }),
    /404 \/missing.*hydration failed.*button without an accessible name.*430px content/s,
  );
});

test('refuses to run destructive browser fixtures against a shared database', () => {
  assert.throws(() => requireDisposableDatabaseMode('shared'), /disposable database/i);
  assert.doesNotThrow(() => requireDisposableDatabaseMode('disposable'));
});

test('leaves the disposable fixture in a valid cancelled state before database disposal', async () => {
  const calls = [];
  const request = async (pathname, init) => calls.push([pathname, init]);

  const failures = await cleanupTestingLabFixture({ eventId: 'event-id' }, request);

  assert.deepEqual(failures, []);
  assert.deepEqual(calls, [
    ['/v1/testing/events/event-id:cancel', { method: 'POST', body: JSON.stringify({ reason: 'Browser E2E fixture completed.' }) }],
  ]);
});

test('returns every cleanup failure instead of masking teardown errors', async () => {
  const failures = await cleanupTestingLabFixture(
    { eventId: 'event-id' },
    async (pathname) => {
      throw new Error(`cannot clean ${pathname}`);
    },
  );

  assert.equal(failures.length, 1);
  assert.match(failures[0], /cannot clean \/v1\/testing\/events/);
});

test('all browser fixture usernames satisfy the API handle length and keep their unique suffix', () => {
  const tag = '1791252567123-abcdef12';
  const usernames = ['owner', 'reviewer', 'tester'].map((kind) => createTestingLabFixtureUsername(kind, tag));
  assert.equal(new Set(usernames).size, 3);
  for (const username of usernames) {
    assert.match(username, /^[a-z0-9_]{3,50}$/);
    assert.ok(username.endsWith('1791252567123_abcdef12'));
  }
  assert.notEqual(createTestingLabFixtureUsername('reviewer', tag),
    createTestingLabFixtureUsername('reviewer', '1791252567123-abcdef13'));
});
