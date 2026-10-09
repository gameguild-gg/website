/**
 * SSRF guard for outbound fetch targets (Codacy `rule-node-ssrf` remediation).
 *
 * The client package is dependency-free and can be consumed standalone, so
 * this mirrors `apps/web/src/lib/security/safe-remote-url.ts` instead of
 * importing it. Same-origin relative paths pass through untouched. Absolute
 * URLs must:
 * - use `https:`,
 * - carry no embedded credentials,
 * - resolve to a host on the configured allowlist — env
 *   `REMOTE_ASSET_ALLOWED_HOSTS` (comma/space separated; defaults to the
 *   hosts this codebase already talks to),
 * - not use a private, loopback, link-local or unspecified address literal.
 *
 * `ALLOW_UNSAFE_REMOTE_URL=true` bypasses every check (local development only,
 * e.g. for a `http://localhost:8080` API base). Test runs (`NODE_ENV=test`)
 * bypass as well so fixtures using local HTTP bases keep working.
 */

const DEFAULT_ALLOWED_HOSTS = 'cdn.gameguild.gg,gameguild.gg,api.gameguild.gg';

export class UnsafeRemoteUrlError extends Error {
  constructor(
    reason: string,
    readonly url: string,
  ) {
    super(`Unsafe remote URL rejected (${reason}): ${url}`);
    this.name = 'UnsafeRemoteUrlError';
  }
}

function envValue(name: string): string | undefined {
  return (globalThis as { process?: { env?: Record<string, string | undefined> } }).process?.env?.[name];
}

function allowedHosts(): Set<string> {
  const raw = envValue('REMOTE_ASSET_ALLOWED_HOSTS') ?? DEFAULT_ALLOWED_HOSTS;
  return new Set(
    raw
      .split(/[,\s]+/)
      .map((host) => host.trim().toLowerCase())
      .filter(Boolean),
  );
}

function isPrivateIpv4(host: string): boolean {
  const octets = host.split('.');
  if (octets.length !== 4 || !octets.every((part) => /^\d{1,3}$/.test(part))) return false;
  if (octets.some((part) => Number(part) > 255)) return false;
  const [a, b] = octets.map(Number) as [number, number, number, number];
  return (
    a === 0 || // 0.0.0.0/8 this network / unspecified
    a === 127 || // 127.0.0.0/8 loopback
    a === 10 || // 10.0.0.0/8 private
    (a === 172 && b >= 16 && b <= 31) || // 172.16.0.0/12 private
    (a === 192 && b === 168) || // 192.168.0.0/16 private
    (a === 169 && b === 254) // 169.254.0.0/16 link-local / cloud metadata
  );
}

function isPrivateIpv6(host: string): boolean {
  if (host === '::' || host === '::1') return true;
  // fc00::/7 unique-local, fe80::/10 link-local, fec0::/10 legacy site-local.
  if (/^(?:f[cd][0-9a-f]{2}|fe[89a-f][0-9a-f]):/.test(host)) return true;
  // WHATWG URL serialization expresses an IPv4-mapped tail as two hex words.
  const mapped = /^::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/.exec(host);
  if (!mapped) return false;
  const upper = Number.parseInt(mapped[1], 16);
  const lower = Number.parseInt(mapped[2], 16);
  return isPrivateIpv4([upper >>> 8, upper & 255, lower >>> 8, lower & 255].join('.'));
}

function isPrivateHost(hostname: string): boolean {
  // A DNS root dot must not hide localhost or its subdomains from this check.
  const host = hostname.toLowerCase().replace(/^\[|\]$/g, '');
  let hostEnd = host.length;
  while (hostEnd > 0 && host[hostEnd - 1] === '.') hostEnd--;
  const localHost = host.slice(0, hostEnd);
  if (localHost === 'localhost' || localHost.endsWith('.localhost')) return true;
  return isPrivateIpv6(localHost) || isPrivateIpv4(localHost);
}

function bypassEnabled(): boolean {
  const environment = envValue('NODE_ENV');
  return environment === 'test' || (environment === 'development' && envValue('ALLOW_UNSAFE_REMOTE_URL') === 'true');
}

/**
 * Assert that `input` is a safe outbound request target and return it (the
 * parsed URL for absolute targets, the input verbatim otherwise). Relative
 * paths are same-origin by construction and always allowed.
 *
 * @throws {UnsafeRemoteUrlError} when an absolute URL violates the policy.
 */
export function assertSafeRemoteUrl(input: string | URL): string | URL {
  const raw = input.toString();
  if (bypassEnabled()) return input;
  // Protocol-relative URLs are cross-origin by construction.
  if (raw.startsWith('//')) throw new UnsafeRemoteUrlError('protocol-relative URL', raw);
  const hasScheme = /^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(raw);
  if (!hasScheme) {
    // Parse against a sentinel solely to detect disguised absolute/network paths.
    // Preserve relative fetch targets so the caller's real origin is unchanged.
    const relativeBase = 'https://relative.invalid';
    if (new URL(raw, relativeBase).origin !== relativeBase) {
      throw new UnsafeRemoteUrlError('disguised cross-origin URL', raw);
    }
    return input;
  }
  const url = new URL(raw);
  if (url.protocol !== 'https:') throw new UnsafeRemoteUrlError('only https is allowed', raw);
  if (url.username || url.password) throw new UnsafeRemoteUrlError('embedded credentials', raw);
  if (isPrivateHost(url.hostname)) throw new UnsafeRemoteUrlError('private or loopback address', raw);
  if (!allowedHosts().has(url.hostname.toLowerCase())) {
    throw new UnsafeRemoteUrlError('host not on the allowlist (REMOTE_ASSET_ALLOWED_HOSTS)', raw);
  }
  return url;
}
/**
 * Pin a service request to the origin supplied by trusted application configuration.
 * HTTP and private hosts are valid for internal APIs; request data cannot select
 * another origin. This guard is enforced in every environment.
 */
export function assertSafeServiceUrl(input: string | URL, configuredBaseUrl: string): string | URL {
  const relativeOrigin = 'https://relative.invalid';
  const hasScheme = (value: string) => /^[a-zA-Z][a-zA-Z0-9+.-]*:/.test(value);
  const parse = (value: string, base: string) => {
    try {
      return hasScheme(value) ? new URL(value) : new URL(value, base);
    } catch {
      throw new UnsafeRemoteUrlError('invalid service URL', value);
    }
  };
  const assertRelative = (value: string) => {
    const parsed = parse(value, relativeOrigin);
    if (value.startsWith('//') || parsed.origin !== relativeOrigin || parsed.protocol !== 'https:') {
      throw new UnsafeRemoteUrlError('disguised cross-origin URL', value);
    }
  };

  const absoluteBase = hasScheme(configuredBaseUrl);
  if (!absoluteBase) assertRelative(configuredBaseUrl);
  const base = parse(configuredBaseUrl || '/', relativeOrigin);
  if (!['http:', 'https:'].includes(base.protocol)) {
    throw new UnsafeRemoteUrlError('service must use HTTP or HTTPS', configuredBaseUrl);
  }
  if (base.username || base.password) {
    throw new UnsafeRemoteUrlError('embedded credentials', configuredBaseUrl);
  }

  const raw = input.toString();
  const absoluteTarget = hasScheme(raw);
  if (absoluteTarget && !absoluteBase) {
    throw new UnsafeRemoteUrlError('relative service requires a relative target', raw);
  }
  if (!absoluteTarget) assertRelative(raw);
  const target = parse(raw, base.href);
  if (!['http:', 'https:'].includes(target.protocol)) {
    throw new UnsafeRemoteUrlError('service must use HTTP or HTTPS', raw);
  }
  if (target.username || target.password) {
    throw new UnsafeRemoteUrlError('embedded credentials', raw);
  }
  if (target.origin !== base.origin) {
    throw new UnsafeRemoteUrlError('target differs from configured service origin', raw);
  }
  // A relative configured base belongs to the caller's actual browser origin.
  if (!absoluteBase) return input;
  return input instanceof URL ? input : target.href;
}
