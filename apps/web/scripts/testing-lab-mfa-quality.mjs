import { responseFailure } from './testing-lab-browser-quality.mjs';

const resourceDenial = 'Failed to load resource: the server responded with a status of 403 (Forbidden)';
const credentialFields = ['accessToken', 'refreshToken', 'sessionId', 'user', 'userId', 'url'];

/** Recognizes only the first-factor challenge of a subsequently verified native sign-in. */
export function createTestingLabMfaQualityMonitor(webBaseUrl) {
  const origin = new URL(webBaseUrl).origin;
  const responses = [];
  const consoleErrors = [];
  let activeSignIn;

  return {
    beginSignIn() {
      if (activeSignIn) throw new Error('A Testing Lab browser sign-in is already pending.');
      activeSignIn = {};
    },
    response(response) {
      const failure = responseFailure(response, webBaseUrl);
      if (failure) responses.push({ response, failure, signIn: activeSignIn });
    },
    consoleError(message, pageUrl) {
      const location = message.location();
      const text = message.text();
      const source = location.url
        ? ` (${location.url}${location.lineNumber ? `:${location.lineNumber}` : ''})`
        : '';
      consoleErrors.push({ text, location, signIn: activeSignIn, label: `${text}${source} (page: ${pageUrl})` });
    },
    completeSignIn(response, firstFactor, email, session) {
      if (!activeSignIn) throw new Error('No Testing Lab browser sign-in is pending.');
      if (response.ok()) {
        activeSignIn = undefined;
        return;
      }

      const record = responses.find(candidate => candidate.response === response && candidate.signIn === activeSignIn);
      let valid = false;
      try {
        const url = new URL(response.url());
        const request = response.request();
        const body = request.postDataJSON();
        valid = record !== undefined &&
          response.status() === 403 && url.origin === origin &&
          url.pathname === '/api/auth/signin/credentials' && !url.search &&
          request.method() === 'POST' && !Object.hasOwn(body, 'mfaToken') &&
          body.email === email && typeof body.password === 'string' && body.password.length > 0 &&
          firstFactor.error === 'MfaRequired' && firstFactor.status === 403 &&
          typeof firstFactor.mfaToken === 'string' && /^[A-Za-z0-9_-]{43}$/.test(firstFactor.mfaToken) &&
          Array.isArray(firstFactor.availableMethods) && firstFactor.availableMethods.includes('TOTP') &&
          firstFactor.availableMethods.every(method => ['TOTP', 'BackupCode'].includes(method)) &&
          credentialFields.every(field => !Object.hasOwn(firstFactor, field)) &&
          typeof session?.user?.id === 'string' && session.user.id.length > 0 &&
          typeof email === 'string' && email.trim().length > 0 && typeof session.user.email === 'string' &&
          session.user.email.trim().toLowerCase() === email.trim().toLowerCase();
      } catch {
        // Malformed request/response evidence cannot qualify a denied request.
      }
      if (!valid) throw new Error('The native MFA challenge did not satisfy the verified sign-in contract.');
      record.accepted = true;
      activeSignIn = undefined;
    },
    flush(quality) {
      const resourceBudgets = new Map();
      for (const record of responses.splice(0)) {
        if (!record.accepted) {
          quality.failedResponses.push(record.failure);
          continue;
        }
        const urls = resourceBudgets.get(record.signIn) ?? new Map();
        const url = record.response.url();
        urls.set(url, (urls.get(url) ?? 0) + 1);
        resourceBudgets.set(record.signIn, urls);
      }
      for (const record of consoleErrors.splice(0)) {
        const urls = resourceBudgets.get(record.signIn);
        const budget = urls?.get(record.location.url) ?? 0;
        if (record.text === resourceDenial && !record.location.lineNumber && budget > 0) {
          urls.set(record.location.url, budget - 1);
        } else {
          quality.browserErrors.push(record.label);
        }
      }
    },
  };
}
