import { randomBytes, randomUUID } from 'node:crypto';
import { once } from 'node:events';
import { createServer } from 'node:http';
import { describe, expect, it } from 'vitest';
import { createClient } from '../../src/client.js';
import { AuthModule } from '../../src/generated/modules/auth.gen.js';
import type { IdentityAuthenticationCredentialType, IdentityAuthenticationPolymorphicSignInInput } from '../../src/generated/types.gen.js';

describe('Generated polymorphic password sign-in contract', () => {
  it.each<IdentityAuthenticationCredentialType>(['Email', 'Username', 'Phone'])('forwards %s credentials anonymously and parses the common response', async (credentialType) => {
    const body: IdentityAuthenticationPolymorphicSignInInput = {
      credentialType,
      credential: credentialType === 'Email' ? 'synthetic@example.test' : credentialType === 'Phone' ? '+15551234567' : 'synthetic-user',
      password: randomBytes(24).toString('hex'),
      tenantId: randomUUID(),
      deviceFingerprint: randomUUID(),
    };
    const response = { success: true, userId: randomUUID(), email: 'canonical@example.test', tenantId: body.tenantId, sessionId: randomUUID(), accessToken: 'synthetic-api-access', refreshToken: 'synthetic-api-refresh' };
    const observed: Array<{ method?: string; path?: string; authorization?: string; body: unknown }> = [];
    const server = createServer(async (request, outgoing) => {
      const chunks: Buffer[] = [];
      for await (const chunk of request) chunks.push(Buffer.from(chunk));
      observed.push({ method: request.method, path: request.url, authorization: request.headers.authorization, body: JSON.parse(Buffer.concat(chunks).toString()) });
      outgoing.writeHead(200, { 'Content-Type': 'application/json' });
      outgoing.end(JSON.stringify(response));
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing local test port');
      const auth = new AuthModule(createClient({ baseUrl: `http://127.0.0.1:${address.port}`, devtools: { enabled: false } }));
      const result = await auth.postAuthPolymorphic(body);
      expect(observed).toEqual([{ method: 'POST', path: '/v1/auth/polymorphic', authorization: undefined, body }]);
      expect(result).toEqual({ ok: true, data: response });
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });

  it('returns the structured generic authentication failure without a success payload', async () => {
    const server = createServer((_, response) => {
      response.writeHead(401, { 'Content-Type': 'application/problem+json' });
      response.end(JSON.stringify({ status: 401, title: 'Unauthorized', detail: 'Synthetic generic authentication failure' }));
    });
    const listening = once(server, 'listening');
    server.listen(0, '127.0.0.1');
    await listening;
    try {
      const address = server.address();
      if (!address || typeof address === 'string') throw new Error('Missing local test port');
      const auth = new AuthModule(createClient({ baseUrl: `http://127.0.0.1:${address.port}`, devtools: { enabled: false } }));
      const result = await auth.postAuthPolymorphic({ credential: 'missing-user', password: randomBytes(24).toString('hex') });
      expect(result.ok).toBe(false);
      expect(result).not.toHaveProperty('data');
      if (!result.ok) expect(result.error).toBeDefined();
    } finally {
      await new Promise<void>((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
    }
  });
});
