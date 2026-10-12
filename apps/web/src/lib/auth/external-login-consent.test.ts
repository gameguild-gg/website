import { describe, expect, it } from 'vitest';

import { parseGrantedScopesHeader } from '@/lib/auth/external-login-consent';

describe('parseGrantedScopesHeader (issue #250 consent header)', () => {
  it('parses a single consent entry with scopes', () => {
    const [consent] = parseGrantedScopesHeader(
      'discord=2026-10-09T18:44:14.0000000Z|1|identify%20email',
    );

    expect(consent).toEqual({
      provider: 'discord',
      consentedAt: '2026-10-09T18:44:14.0000000Z',
      consentVersion: 1,
      grantedScopes: ['identify', 'email'],
    });
  });

  it('parses multiple comma-separated entries and keeps provider order', () => {
    const consents = parseGrantedScopesHeader(
      'google=2026-10-01T00:00:00.0000000Z|1|openid%20email%20profile,discord=2026-10-09T18:44:14.0000000Z|1|identify%20email',
    );

    expect(consents.map((c) => c.provider)).toEqual(['google', 'discord']);
    expect(consents[0].grantedScopes).toEqual(['openid', 'email', 'profile']);
  });

  it('reports an empty scope list for a consent recorded with no granted scopes', () => {
    const [consent] = parseGrantedScopesHeader('discord=2026-10-09T18:44:14.0000000Z|1|');

    expect(consent?.grantedScopes).toEqual([]);
  });

  it('skips malformed entries instead of throwing', () => {
    const consents = parseGrantedScopesHeader('no-equals-sign,=2026-10-09T18:44:14.0000000Z|1|x,google=|1|email');

    // "=timestamp..." has an empty provider, "google=" has no timestamp → both dropped.
    expect(consents).toEqual([]);
  });

  it('treats a non-numeric version as legacy 0', () => {
    const [numeric, notANumber] = parseGrantedScopesHeader(
      'a=2026-10-09T18:44:14.0000000Z|1|email,b=2026-10-09T18:44:14.0000000Z|v2|email',
    );

    expect(numeric?.consentVersion).toBe(1);
    expect(notANumber?.consentVersion).toBe(0);
  });

  it('returns [] for null/undefined/empty headers', () => {
    expect(parseGrantedScopesHeader(null)).toEqual([]);
    expect(parseGrantedScopesHeader(undefined)).toEqual([]);
    expect(parseGrantedScopesHeader('')).toEqual([]);
  });

  it('decodes percent-encoded pipe characters inside scope tokens', () => {
    const [consent] = parseGrantedScopesHeader('acme=2026-10-09T18:44:14.0000000Z|1|a%7Cb%20c');

    expect(consent?.grantedScopes).toEqual(['a|b', 'c']);
  });
});
