'use server';

import { getToken } from '@/auth';
import { createServerClient } from '@game-guild/client';

/**
 * Server-side data source for the GitHub star prompt (issue #90).
 *
 * Detects whether the signed-in account is linked to GitHub via the metadata-only
 * HEAD /v1/auth/external-logins endpoint (X-Linked-Providers header). This covers
 * both "signed in with GitHub" and "linked GitHub afterwards".
 */
export async function isGitHubLinkedAction(): Promise<boolean> {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    'http://localhost:8080';
  const client = createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });

  try {
    const result = await client.requestRaw<void>({
      method: 'HEAD',
      path: '/v1/auth/external-logins',
      requiresAuth: true,
    });
    if (!result.ok) {
      return false;
    }

    const header = result.data.headers.get('x-linked-providers') ?? '';
    return header
      .split(',')
      .map((pair) => pair.split('=')[0]?.trim().toLowerCase())
      .includes('github');
  } catch {
    // Fail closed: an unreachable API must not nag the user.
    return false;
  }
}
