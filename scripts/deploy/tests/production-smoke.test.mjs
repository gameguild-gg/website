import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { createServer } from 'node:http';
import { fileURLToPath } from 'node:url';
import test from 'node:test';

const smokeScript = fileURLToPath(new URL('../production-smoke.mjs', import.meta.url));

test('production smoke checks Testing Lab, social feed, stories, and authenticated project access without cache', async (t) => {
  const requests = [];
  const server = createServer((request, response) => {
    requests.push({ url: request.url, headers: request.headers });
    response.setHeader('Content-Type', 'application/json');
    if (request.url === '/v1/auth/sign-in') {
      response.end(JSON.stringify({ accessToken: 'token', tenantId: 'tenant-id' }));
      return;
    }
    response.end(JSON.stringify(request.url?.includes('/v1/testing/events') ? [] : { status: 'ok' }));
  });
  server.listen(0, '127.0.0.1');
  await once(server, 'listening');
  t.after(() => server.close());
  const address = server.address();
  const origin = `http://127.0.0.1:${address.port}`;

  const child = spawn(process.execPath, [smokeScript], {
    env: {
      ...process.env,
      GAMEGUILD_API_URL: origin,
      GAMEGUILD_WEB_URL: origin,
      GAMEGUILD_SMOKE_ADMIN_EMAIL: 'admin@example.com',
      GAMEGUILD_SMOKE_ADMIN_PASSWORD: 'secret',
      GAMEGUILD_SMOKE_PROJECT_ID: 'project-id',
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  });
  let stderr = '';
  child.stderr.setEncoding('utf8');
  child.stderr.on('data', (chunk) => { stderr += chunk; });
  const [exitCode] = await once(child, 'exit');

  assert.equal(exitCode, 0, stderr);
  assert.ok(requests.some((entry) => entry.url === '/testing-lab/events'));
  assert.ok(requests.some((entry) => entry.url === '/v1/testing/events?skip=0&take=1'));
  const feed = requests.find((entry) => entry.url === '/api/social/feed?scope=for-you&take=1');
  const stories = requests.find((entry) => entry.url === '/api/social/stories');
  assert.equal(feed.headers.authorization, 'Bearer token');
  assert.equal(stories.headers.authorization, 'Bearer token');
  const project = requests.find((entry) => entry.url === '/v1/projects/project-id');
  assert.equal(project.headers.authorization, 'Bearer token');
  assert.equal(project.headers['x-tenant-id'], 'tenant-id');
  assert.equal(project.headers['cache-control'], 'no-cache, no-store, max-age=0');
});
