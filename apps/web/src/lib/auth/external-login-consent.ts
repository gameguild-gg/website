/**
 * Parsing of the X-Granted-Scopes consent header (issue #250).
 *
 * The API's HEAD /v1/auth/external-logins conveys one comma-separated entry
 * per link with a recorded consent:
 *
 *   provider=iso8601-consent-timestamp|consent-version|url-encoded-space-separated-scopes
 *
 * e.g. `discord=2026-10-09T18:44:14.0000000Z|1|identify%20email`.
 * Legacy rows without a consent record contribute no entry. Scope tokens are
 * percent-encoded, so a literal `|` inside a token arrives as `%7C` and the
 * three-way split on `|` stays unambiguous.
 */

export interface ProviderConsent {
  provider: string;
  /** ISO-8601 UTC timestamp of the recorded consent. */
  consentedAt: string;
  /** Version of the consent terms agreed to (0 = legacy row). */
  consentVersion: number;
  /** Scope tokens currently granted on the link. */
  grantedScopes: string[];
}

export function parseGrantedScopesHeader(header: string | null | undefined): ProviderConsent[] {
  if (!header) {
    return [];
  }

  const consents: ProviderConsent[] = [];
  for (const entry of header.split(',')) {
    const trimmed = entry.trim();
    if (!trimmed) {
      continue;
    }

    const eq = trimmed.indexOf('=');
    if (eq <= 0) {
      continue;
    }

    const provider = trimmed.slice(0, eq);
    const rest = trimmed.slice(eq + 1);
    const [consentedAt, version, encodedScopes] = rest.split('|');
    if (!consentedAt) {
      continue;
    }

    const consentVersion = Number.parseInt(version ?? '', 10);
    const grantedScopes = decodeURIComponent(encodedScopes ?? '')
      .split(' ')
      .filter((scope) => scope !== '');

    consents.push({
      provider,
      consentedAt,
      consentVersion: Number.isNaN(consentVersion) ? 0 : consentVersion,
      grantedScopes,
    });
  }

  return consents;
}
