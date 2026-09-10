#!/usr/bin/env node

const requiredEnvironment = [
  'GAMEGUILD_API_URL',
  'GAMEGUILD_WEB_URL',
  'GAMEGUILD_SMOKE_ADMIN_EMAIL',
  'GAMEGUILD_SMOKE_ADMIN_PASSWORD',
  'GAMEGUILD_SMOKE_PROJECT_ID',
];

for (const name of requiredEnvironment) {
  if (!process.env[name]?.trim()) throw new Error(`${name} is required for the production smoke`);
}

const apiUrl = process.env.GAMEGUILD_API_URL;
const webUrl = process.env.GAMEGUILD_WEB_URL;
const timeoutMs = Number.parseInt(process.env.SMOKE_TIMEOUT_MS ?? '15000', 10);

function joinUrl(baseUrl, path) {
  return new URL(path, baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`).toString();
}

async function request(name, baseUrl, path, options = {}) {
  const response = await fetch(joinUrl(baseUrl, path), {
    ...options,
    cache: 'no-store',
    redirect: 'follow',
    signal: AbortSignal.timeout(timeoutMs),
    headers: {
      'Cache-Control': 'no-cache, no-store, max-age=0',
      Pragma: 'no-cache',
      'User-Agent': 'gameguild-production-smoke/1.0',
      ...options.headers,
    },
  });
  const text = await response.text();
  if (!response.ok) throw new Error(`${name} returned ${response.status}: ${text.slice(0, 300)}`);
  process.stdout.write(`PASS ${name} ${response.status}\n`);
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

await request('API live', apiUrl, '/live');
await request('API ready', apiUrl, '/ready');
await request('Web health', webUrl, '/api/health');
await request('Testing Lab public directory', webUrl, '/testing-lab/events');

const session = await request('Production smoke sign-in', apiUrl, '/v1/auth/sign-in', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    email: process.env.GAMEGUILD_SMOKE_ADMIN_EMAIL,
    password: process.env.GAMEGUILD_SMOKE_ADMIN_PASSWORD,
  }),
});

if (typeof session?.accessToken !== 'string' || !session.accessToken) {
  throw new Error('Production smoke sign-in returned no accessToken');
}
if (typeof session?.tenantId !== 'string' || !session.tenantId) {
  throw new Error('Production smoke sign-in returned no tenantId');
}

const authenticatedHeaders = {
  Authorization: `Bearer ${session.accessToken}`,
  'X-Tenant-Id': session.tenantId,
};

await request('Testing Lab authenticated event list', apiUrl, '/v1/testing/events?skip=0&take=1', {
  headers: authenticatedHeaders,
});
await request('Authenticated social feed', apiUrl, '/api/social/feed?scope=for-you&take=1', {
  headers: authenticatedHeaders,
});
await request('Authenticated social stories', apiUrl, '/api/social/stories', {
  headers: authenticatedHeaders,
});
await request(
  'Authenticated project access',
  apiUrl,
  `/v1/projects/${encodeURIComponent(process.env.GAMEGUILD_SMOKE_PROJECT_ID)}`,
  { headers: authenticatedHeaders },
);

process.stdout.write('Production smoke passed.\n');
