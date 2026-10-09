import { afterEach, afterAll, beforeAll, describe, expect, it } from 'vitest';

import { assertSafeRemoteUrl, UnsafeRemoteUrlError } from '../../src/runtime/security/safe-remote-url.js';

const originalNodeEnv = process.env.NODE_ENV;

beforeAll(() => {
  // The guard bypasses validation under NODE_ENV=test (existing fixtures use
  // local HTTP bases); these tests exercise the enforced path.
  process.env.NODE_ENV = 'production';
});

afterEach(() => {
  process.env.NODE_ENV = 'production';
  delete process.env.REMOTE_ASSET_ALLOWED_HOSTS;
  delete process.env.ALLOW_UNSAFE_REMOTE_URL;
});

afterAll(() => {
  process.env.NODE_ENV = originalNodeEnv;
});

describe('assertSafeRemoteUrl', () => {
  it('allows allowlisted https URLs', () => {
    expect(assertSafeRemoteUrl('https://api.gameguild.gg/v1/auth/mfa/methods').toString()).toBe('https://api.gameguild.gg/v1/auth/mfa/methods');
  });

  it('allows same-origin relative paths', () => {
    const target = assertSafeRemoteUrl('/api/auth/signin/discord');
    expect(target).toBe('/api/auth/signin/discord');
    expect(new URL(target, 'https://app.gameguild.gg').origin).toBe('https://app.gameguild.gg');
  });

  it.each(['auth/signin/discord', '?page=2', '/api/assets?id=%2F%2Fevil.example.com'])('preserves relative target %s verbatim', (target) => {
    expect(assertSafeRemoteUrl(target)).toBe(target);
  });

  it.each(['  //evil.example.com/x', '\t//evil.example.com/x', '\\\\evil.example.com/x', '/\\evil.example.com/x', '  https://evil.example.com/x'])(
    'rejects disguised cross-origin target %s',
    (target) => {
      expect(() => assertSafeRemoteUrl(target)).toThrow(UnsafeRemoteUrlError);
    },
  );

  it('rejects http', () => {
    expect(() => assertSafeRemoteUrl('http://api.gameguild.gg/v1/auth/mfa/verify')).toThrow(UnsafeRemoteUrlError);
  });

  it('rejects hosts outside the allowlist', () => {
    expect(() => assertSafeRemoteUrl('https://evil.example.com/x')).toThrow(/allowlist/);
  });

  it.each([
    'https://127.0.0.1/x',
    'https://10.0.0.5/x',
    'https://172.16.0.9/x',
    'https://192.168.1.4/x',
    'https://169.254.169.254/latest/meta-data',
    'https://localhost/x',
    'https://[::1]/x',
  ])('rejects private or loopback host %s', (url) => {
    expect(() => assertSafeRemoteUrl(url)).toThrow(UnsafeRemoteUrlError);
  });

  it('rejects embedded credentials', () => {
    expect(() => assertSafeRemoteUrl('https://user:pass@api.gameguild.gg/x')).toThrow(/credentials/);
  });

  it('honours REMOTE_ASSET_ALLOWED_HOSTS additions', () => {
    process.env.REMOTE_ASSET_ALLOWED_HOSTS = 'extra.example.com';
    expect(new URL(assertSafeRemoteUrl('https://extra.example.com/x').toString()).hostname).toBe('extra.example.com');
    expect(() => assertSafeRemoteUrl('https://api.gameguild.gg/x')).toThrow(/allowlist/);
  });

  it('ignores the development bypass in production', () => {
    process.env.ALLOW_UNSAFE_REMOTE_URL = 'true';
    expect(() => assertSafeRemoteUrl('http://localhost:8080/v1/auth/sessions')).toThrow(UnsafeRemoteUrlError);
  });

  it('ALLOW_UNSAFE_REMOTE_URL=true allows localhost http only in development', () => {
    process.env.NODE_ENV = 'development';
    process.env.ALLOW_UNSAFE_REMOTE_URL = 'true';
    expect(assertSafeRemoteUrl('http://localhost:8080/v1/auth/sessions')).toBe('http://localhost:8080/v1/auth/sessions');
  });
});

describe('remote asset literal addresses', () => {
  it.each([
    'https://[::ffff:127.0.0.1]/x',
    'https://[::ffff:127.255.255.255]/x',
    'https://[::ffff:10.0.0.1]/x',
    'https://[::ffff:172.16.0.1]/x',
    'https://[::ffff:172.31.255.255]/x',
    'https://[::ffff:192.168.1.1]/x',
    'https://[::ffff:169.254.169.254]/latest/meta-data',
    'https://[0:0:0:0:0:ffff:7f00:1]/x',
    'https://[::ffff:0:0]/x',
    'https://[::ffff:ff:ffff]/x',
    'https://[::]/x',
    'https://[0:0:0:0:0:0:0:0]/x',
    'https://[fe80::1]/x',
    'https://[febf:ffff:ffff:ffff:ffff:ffff:ffff:ffff]/x',
    'https://[fec0::1]/x',
    'https://[feff:ffff:ffff:ffff:ffff:ffff:ffff:ffff]/x',
    'https://[fc00::1]/x',
    'https://[fdff:ffff:ffff:ffff:ffff:ffff:ffff:ffff]/x',
    'https://[::1]/x',
    'https://0.0.0.0/x',
    'https://0.255.255.255/x',
    'https://127.1/x',
    'https://2130706433/x',
    'https://0x7f000001/x',
    'https://0177.0.0.1/x',
    'https://10.0.0.1/x',
    'https://172.16.0.1/x',
    'https://172.31.255.255/x',
    'https://192.168.1.1/x',
    'https://169.254.169.254/x',
    'https://localhost/x',
    'https://service.localhost/x',
  ])('rejects local literal even when explicitly allowlisted: %s', (raw) => {
    const canonical = new URL(raw);
    process.env.REMOTE_ASSET_ALLOWED_HOSTS = canonical.hostname;
    process.env.ALLOW_UNSAFE_REMOTE_URL = 'true';
    expect(() => assertSafeRemoteUrl(raw)).toThrow(/private or loopback address/);
    expect(() => assertSafeRemoteUrl(canonical)).toThrow(/private or loopback address/);
  });

  it.each([
    'https://8.8.8.8/x',
    'https://[::ffff:8.8.8.8]/x',
    'https://[2001:4860:4860::8888]/x',
    'https://172.15.255.255/x',
    'https://172.32.0.1/x',
    'https://169.253.255.255/x',
    'https://169.255.0.1/x',
    'https://192.167.255.255/x',
    'https://192.169.0.1/x',
    'https://126.255.255.255/x',
    'https://128.0.0.1/x',
    'https://11.0.0.1/x',
    'https://1.0.0.1/x',
  ])('retains allowlisted non-local literal: %s', (raw) => {
    const canonical = new URL(raw);
    process.env.REMOTE_ASSET_ALLOWED_HOSTS = canonical.hostname;
    expect(assertSafeRemoteUrl(raw).toString()).toBe(canonical.href);
    expect(assertSafeRemoteUrl(canonical).toString()).toBe(canonical.href);
  });
});

describe('remote asset localhost DNS spellings', () => {
  it.each([
    'https://localhost./x',
    'https://service.localhost./x',
    'https://LOCALHOST./x',
    'https://LoCaLhOsT./x',
    'https://sub.service.localhost./x',
    'https://localhost../x',
    'https://service.localhost../x',
    'https://localhost.../x',
    'https://service.localhost.../x',
    'https://localhost\u3002/x',
    'https://localhost\uff0e/x',
    'https://localhost\uff61/x',
    'https://service\u3002localhost\u3002/x',
    'https://service\uff0elocalhost\uff61/x',
    'https://%6cocalhost%2e/x',
    'https://service%2elocalhost%2e/x',
    'https://local\thost./x',
    'https://local\nhost./x',
    'https://local\rhost./x',
    'https://localhost.:443/x',
    'https://service.localhost.:8443/x',
    'https://%4c%4f%43%41%4c%48%4f%53%54%2e/x',
    'https://ｌｏｃａｌｈｏｓｔ．/x',
    'https://localhost\u3002\u3002/x',
  ])('rejects allowlisted localhost spelling: %s', (raw) => {
    const canonical = new URL(raw);
    process.env.REMOTE_ASSET_ALLOWED_HOSTS = canonical.hostname;
    process.env.ALLOW_UNSAFE_REMOTE_URL = 'true';
    expect(() => assertSafeRemoteUrl(raw)).toThrow(/private or loopback address/);
    expect(() => assertSafeRemoteUrl(canonical)).toThrow(/private or loopback address/);
  });

  it.each([
    'https://localhost.example./x',
    'https://service.localhost.example./x',
    'https://notlocalhost./x',
    'https://localhosts./x',
    'https://local-host./x',
    'https://example.localhost-safe./x',
    'https://cdn.gameguild.gg./x',
    'https://127.0.0.1.example./x',
    'https://éxample.org./x',
    'https://example.com../x',
  ])('preserves explicitly allowlisted non-local DNS host: %s', (raw) => {
    const canonical = new URL(raw);
    process.env.REMOTE_ASSET_ALLOWED_HOSTS = canonical.hostname;
    expect(assertSafeRemoteUrl(raw).toString()).toBe(canonical.href);
    expect(assertSafeRemoteUrl(canonical).toString()).toBe(canonical.href);
  });

  it.each(['https://cdn.gameguild.gg./x', 'https://api.gameguild.gg./x'])('does not expand the default allowlist for DNS spelling: %s', (raw) => {
    expect(() => assertSafeRemoteUrl(raw)).toThrow(/allowlist/);
    expect(() => assertSafeRemoteUrl(new URL(raw))).toThrow(/allowlist/);
  });

  it.each(['http://localhost.:8080/x', 'http://service.localhost.:8080/x'])('retains the explicit development bypass: %s', (raw) => {
    process.env.NODE_ENV = 'development';
    process.env.ALLOW_UNSAFE_REMOTE_URL = 'true';
    const canonical = new URL(raw);
    expect(assertSafeRemoteUrl(raw)).toBe(raw);
    expect(assertSafeRemoteUrl(canonical)).toBe(canonical);
  });
});
