import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';
import type { TestingLocation } from './testing-types';

function getApiBaseUrl(): string {
  return process.env.NEXT_PUBLIC_API_URL ?? '';
}

export async function getTestingLocations(skip = 0, take = 50): Promise<TestingLocation[]> {
  const apiBaseUrl = getApiBaseUrl();
  const endpoint = apiBaseUrl
    ? `${apiBaseUrl}/v1/testing/locations?skip=${skip}&take=${take}`
    : `/v1/testing/locations?skip=${skip}&take=${take}`;

  try {
    const response = await fetch(assertSafeServiceUrl(endpoint, apiBaseUrl), {
      redirect: 'error',
      headers: {
        Accept: 'application/json',
      },
      credentials: 'include',
      cache: 'no-store',
    });

    if (!response.ok) {
      return [];
    }

    const data = await response.json();
    return Array.isArray(data) ? (data as TestingLocation[]) : [];
  } catch {
    return [];
  }
}
