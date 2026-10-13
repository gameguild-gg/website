import assert from 'node:assert/strict';
import test from 'node:test';
import { createTestingLabMfaQualityMonitor } from './testing-lab-mfa-quality.mjs';
import { throwForBrowserQualityFailures } from './testing-lab-browser-quality.mjs';

const baseUrl = 'http://example.test';
const email = 'admin@example.test';
const resourceDenial = 'Failed to load resource: the server responded with a status of 403 (Forbidden)';
const challenge = { error: 'MfaRequired', status: 403, mfaToken: 'x'.repeat(43), availableMethods: ['TOTP', 'BackupCode'] };
const session = { user: { id: 'verified-user', email } };

function response({ status = 403, url = `${baseUrl}/api/auth/signin/credentials`, method = 'POST', body = { email, password: 'disposable-password' } } = {}) {
  return { status: () => status, ok: () => status < 400, url: () => url,
    request: () => ({ method: () => method, postDataJSON: () => body }) };
}

function consoleError({ text = resourceDenial, url = `${baseUrl}/api/auth/signin/credentials`, lineNumber = 0 } = {}) {
  return { text: () => text, location: () => ({ url, lineNumber }) };
}

function fixture() {
  const monitor = createTestingLabMfaQualityMonitor(baseUrl);
  const quality = { failedResponses: [], browserErrors: [] };
  monitor.beginSignIn();
  const submitted = response();
  monitor.response(submitted);
  monitor.consoleError(consoleError(), `${baseUrl}/sign-in?redirectTo=%2Fworkspace`);
  return { monitor, quality, submitted };
}

test('a pending MFA challenge remains a failure until sign-in completes', () => {
  const { monitor, quality } = fixture();
  monitor.flush(quality);
  assert.deepEqual(quality.failedResponses, ['403 /api/auth/signin/credentials']);
  assert.equal(quality.browserErrors.length, 1);
  assert.throws(() => throwForBrowserQualityFailures(quality), /HTTP failures.*403.*Browser errors/s);
});

test('recognizes exactly the native first-factor challenge after the same account has a verified session', () => {
  const { monitor, quality, submitted } = fixture();
  monitor.completeSignIn(submitted, challenge, email, session);
  monitor.flush(quality);
  assert.deepEqual(quality, { failedResponses: [], browserErrors: [] });
  assert.doesNotThrow(() => throwForBrowserQualityFailures(quality));
});

for (const verifiedEmail of ['ADMIN@EXAMPLE.TEST', ' admin@example.test ']) {
  test(`recognizes the same normalized account email ${JSON.stringify(verifiedEmail)}`, () => {
    const { monitor, quality, submitted } = fixture();
    monitor.completeSignIn(submitted, challenge, email, { user: { ...session.user, email: verifiedEmail } });
    monitor.flush(quality);
    assert.doesNotThrow(() => throwForBrowserQualityFailures(quality));
    assert.deepEqual(quality, { failedResponses: [], browserErrors: [] });
  });
}

for (const [name, mutate] of [
  ['ordinary credential denial', () => ({ firstFactor: { ...challenge, error: 'CredentialsSignin' } })],
  ['account lock', () => ({ firstFactor: { ...challenge, error: 'AccountLocked' } })],
  ['invalid limited token', () => ({ firstFactor: { ...challenge, mfaToken: 'invalid' } })],
  ['incorrect body status', () => ({ firstFactor: { ...challenge, status: 200 } })],
  ['missing native methods', () => ({ firstFactor: { ...challenge, availableMethods: [] } })],
  ['unknown native method', () => ({ firstFactor: { ...challenge, availableMethods: ['TOTP', 'Unknown'] } })],
  ['missing verified session', () => ({ verifiedSession: null })],
  ['missing verified actor', () => ({ verifiedSession: { user: { id: '', email } } })],
  ['missing verified account email', () => ({ verifiedSession: { user: { id: 'verified-user' } } })],
  ['another account session', () => ({ verifiedSession: { user: { id: 'other', email: 'other@example.test' } } })],
  ...['accessToken', 'refreshToken', 'sessionId', 'user', 'userId', 'url'].map(field =>
    [`unexpected ${field} in first factor`, () => ({ firstFactor: { ...challenge, [field]: 'unexpected' } })]),
]) {
  test(`does not qualify ${name}`, () => {
    const { monitor, quality, submitted } = fixture();
    const changes = mutate();
    assert.throws(() => monitor.completeSignIn(submitted, changes.firstFactor ?? challenge, email,
      Object.hasOwn(changes, 'verifiedSession') ? changes.verifiedSession : session), /verified sign-in contract/);
    monitor.flush(quality);
    assert.equal(quality.failedResponses.length, 1);
    assert.equal(quality.browserErrors.length, 1);
    assert.throws(() => throwForBrowserQualityFailures(quality), /403/);
  });
}

for (const [name, options] of [
  ['denied MFA completion', { body: { mfaToken: 'x'.repeat(43), method: 'Totp', code: '123456' } }],
  ['another account request', { body: { email: 'other@example.test', password: 'disposable-password' } }],
  ['missing password', { body: { email } }],
  ['non-POST request', { method: 'GET' }],
  ['another origin', { url: 'http://other.test/api/auth/signin/credentials' }],
  ['another endpoint', { url: `${baseUrl}/api/auth/mfa/enrollment` }],
  ['query variant', { url: `${baseUrl}/api/auth/signin/credentials?other=1` }],
  ['unexpected 401', { status: 401 }],
  ['server failure', { status: 503 }],
  ['malformed body', { body: null }],
]) {
  test(`does not qualify ${name}`, () => {
    const monitor = createTestingLabMfaQualityMonitor(baseUrl);
    monitor.beginSignIn();
    const submitted = response(options);
    monitor.response(submitted);
    assert.throws(() => monitor.completeSignIn(submitted, challenge, email, session), /verified sign-in contract/);
  });
}

test('does not recognize a challenge observed outside the current sign-in', () => {
  const monitor = createTestingLabMfaQualityMonitor(baseUrl);
  const submitted = response();
  monitor.response(submitted);
  monitor.beginSignIn();
  assert.throws(() => monitor.completeSignIn(submitted, challenge, email, session), /verified sign-in contract/);
});

test('another page cannot qualify the observed response', () => {
  const { submitted } = fixture();
  const other = createTestingLabMfaQualityMonitor(baseUrl);
  other.beginSignIn();
  assert.throws(() => other.completeSignIn(submitted, challenge, email, session), /verified sign-in contract/);
});

test('retains a second denial and its browser error on the same URL during MFA', () => {
  const { monitor, quality, submitted } = fixture();
  monitor.response(response({ body: { mfaToken: challenge.mfaToken, code: 'denied' } }));
  monitor.consoleError(consoleError(), `${baseUrl}/sign-in`);
  monitor.completeSignIn(submitted, challenge, email, session);
  monitor.flush(quality);
  assert.equal(quality.failedResponses.length, 1);
  assert.equal(quality.browserErrors.length, 1);
  assert.throws(() => throwForBrowserQualityFailures(quality), /403/);
});

for (const [name, options] of [
  ['runtime console error', { text: 'Hydration failed' }],
  ['error in source code', { lineNumber: 12 }],
  ['resource error on another URL', { url: `${baseUrl}/api/other` }],
]) {
  test(`retains ${name} during an otherwise completed MFA sign-in`, () => {
    const { monitor, quality, submitted } = fixture();
    monitor.consoleError(consoleError(options), `${baseUrl}/sign-in`);
    monitor.completeSignIn(submitted, challenge, email, session);
    monitor.flush(quality);
    assert.equal(quality.browserErrors.length, 1);
    assert.throws(() => throwForBrowserQualityFailures(quality), /Browser errors/);
  });
}

test('later responses on the same URL are not covered by a completed MFA sign-in', () => {
  const { monitor, quality, submitted } = fixture();
  monitor.completeSignIn(submitted, challenge, email, session);
  monitor.response(response());
  monitor.consoleError(consoleError(), `${baseUrl}/workspace`);
  monitor.flush(quality);
  assert.equal(quality.failedResponses.length, 1);
  assert.equal(quality.browserErrors.length, 1);
});

test('ordinary successful sign-in cannot clear unrelated failures', () => {
  const { monitor, quality } = fixture();
  monitor.completeSignIn(response({ status: 200 }), { url: '/workspace' }, email, session);
  monitor.flush(quality);
  assert.equal(quality.failedResponses.length, 1);
  assert.equal(quality.browserErrors.length, 1);
});

test('flush appends to existing findings without duplicating drained records', () => {
  const { monitor, quality } = fixture();
  quality.browserErrors.push('Existing page error');
  monitor.flush(quality);
  monitor.flush(quality);
  assert.equal(quality.failedResponses.length, 1);
  assert.equal(quality.browserErrors.length, 2);
});

test('rejects overlapping sign-ins and unsolicited completion', () => {
  const monitor = createTestingLabMfaQualityMonitor(baseUrl);
  assert.throws(() => monitor.completeSignIn(response(), challenge, email, session), /No.*pending/);
  monitor.beginSignIn();
  assert.throws(() => monitor.beginSignIn(), /already pending/);
});
