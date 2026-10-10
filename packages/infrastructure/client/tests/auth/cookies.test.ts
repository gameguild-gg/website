/**
 * Cookie Management Tests
 */

import { describe, it, expect } from 'vitest';
import { resolveCookieOptions, getCookieName, SessionStore, CsrfStore, CallbackStore } from '../../src/runtime/auth/cookies.js';

describe('Cookie Management', () => {
  describe('resolveCookieOptions', () => {
    it('should return defaults when no config provided', () => {
      const options = resolveCookieOptions();

      expect(options.name).toBe('__me');
      expect(options.secure).toBe(false);
      expect(options.sameSite).toBe('lax');
      expect(options.path).toBe('/');
      expect(options.maxAge).toBe(30 * 24 * 60 * 60); // 30 days
      expect(options.httpOnly).toBe(true);
      expect(options.domain).toBeUndefined();
    });

    it('should override defaults with config values', () => {
      const options = resolveCookieOptions({
        name: 'custom',
        secure: true,
        sameSite: 'strict',
        path: '/app',
        domain: 'example.com',
        maxAge: 86400,
        httpOnly: false,
      });

      expect(options.name).toBe('custom');
      expect(options.secure).toBe(true);
      expect(options.sameSite).toBe('strict');
      expect(options.path).toBe('/app');
      expect(options.domain).toBe('example.com');
      expect(options.maxAge).toBe(86400);
      expect(options.httpOnly).toBe(false);
    });

    it('should use isSecure parameter when config.secure is not set', () => {
      const options = resolveCookieOptions(undefined, true);
      expect(options.secure).toBe(true);
    });

    it('should prefer config.secure over isSecure parameter', () => {
      const options = resolveCookieOptions({ secure: false }, true);
      expect(options.secure).toBe(false);
    });

    it('should default secure to false when nothing specified', () => {
      const options = resolveCookieOptions({});
      expect(options.secure).toBe(false);
    });
  });

  describe('getCookieName', () => {
    it('should return plain name when not secure', () => {
      expect(getCookieName('__me.session-token', false)).toBe('__me.session-token');
    });

    it('should add __Secure- prefix when secure', () => {
      expect(getCookieName('__me.session-token', true)).toBe('__Secure-__me.session-token');
    });

    it('should handle empty base name', () => {
      expect(getCookieName('', true)).toBe('__Secure-');
      expect(getCookieName('', false)).toBe('');
    });
  });

  describe('SessionStore', () => {
    const defaultOptions = resolveCookieOptions();

    it('should generate correct cookie name', () => {
      const store = new SessionStore(defaultOptions);
      expect(store.getCookieName()).toBe('__me.session-token');
    });

    it('should generate secure cookie name', () => {
      const secureOptions = resolveCookieOptions({ secure: true });
      const store = new SessionStore(secureOptions);
      expect(store.getCookieName()).toBe('__Secure-__me.session-token');
    });

    describe('read', () => {
      it('should return null when cookie does not exist', () => {
        const store = new SessionStore(defaultOptions);
        const getCookie = (_name: string) => undefined;

        expect(store.read(getCookie)).toBeNull();
      });

      it('should return value from single cookie', () => {
        const store = new SessionStore(defaultOptions);
        const getCookie = (name: string) => {
          if (name === '__me.session-token') return 'jwt-token-value';
          return undefined;
        };

        expect(store.read(getCookie)).toBe('jwt-token-value');
      });

      it('should reassemble chunked cookies', () => {
        const store = new SessionStore(defaultOptions);
        const cookies: Record<string, string> = {
          '__me.session-token': 'chunk0',
          '__me.session-token.1': 'chunk1',
          '__me.session-token.2': 'chunk2',
        };
        const getCookie = (name: string) => cookies[name];

        expect(store.read(getCookie)).toBe('chunk0chunk1chunk2');
      });

      it('should stop reading chunks at first missing index', () => {
        const store = new SessionStore(defaultOptions);
        const cookies: Record<string, string> = {
          '__me.session-token': 'chunk0',
          '__me.session-token.1': 'chunk1',
          // missing .2
          '__me.session-token.3': 'chunk3',
        };
        const getCookie = (name: string) => cookies[name];

        expect(store.read(getCookie)).toBe('chunk0chunk1');
      });

      it('should return null for empty main cookie', () => {
        const store = new SessionStore(defaultOptions);
        const getCookie = (name: string) => {
          if (name === '__me.session-token') return '';
          return undefined;
        };

        expect(store.read(getCookie)).toBeNull();
      });
    });

    describe('write', () => {
      it('should write small value as single cookie', () => {
        const store = new SessionStore(defaultOptions);
        const written: Array<{ name: string; value: string }> = [];
        const setCookie = (name: string, value: string, _opts: any) => {
          written.push({ name, value });
        };

        store.write('small-token', setCookie);

        // Should write main cookie + cleanup chunks
        const mainCookie = written.find((c) => c.name === '__me.session-token');
        expect(mainCookie).toBeDefined();
        expect(mainCookie!.value).toBe('small-token');
      });

      it('should omit Max-Age for session storage policy (remember me off)', () => {
        const store = new SessionStore(defaultOptions);
        const written: Array<{ name: string; opts: any }> = [];
        const setCookie = (name: string, _value: string, opts: any) => {
          written.push({ name, opts });
        };

        store.write('session-token', setCookie, { storage: 'session' });

        // Chunk cleanup cookies intentionally keep maxAge 0; only the main
        // session cookie carries the storage policy.
        const mainCookie = written.find((c) => c.name === '__me.session-token');
        expect(mainCookie).toBeDefined();
        expect(mainCookie!.opts.maxAge).toBeUndefined();
      });

      it('should keep Max-Age for persistent storage policy (remember me on)', () => {
        const store = new SessionStore(defaultOptions);
        const written: Array<{ name: string; opts: any }> = [];
        const setCookie = (name: string, _value: string, opts: any) => {
          written.push({ name, opts });
        };

        store.write('persistent-token', setCookie, { storage: 'persistent' });

        const mainCookie = written.find((c) => c.name === '__me.session-token');
        expect(mainCookie).toBeDefined();
        expect(mainCookie!.opts.maxAge).toBe(defaultOptions.maxAge);
      });

      it('should default to persistent storage when no policy is provided', () => {
        const store = new SessionStore(defaultOptions);
        const written: Array<{ name: string; opts: any }> = [];
        const setCookie = (name: string, _value: string, opts: any) => {
          written.push({ name, opts });
        };

        store.write('default-token', setCookie);

        const mainCookie = written.find((c) => c.name === '__me.session-token');
        expect(mainCookie).toBeDefined();
        expect(mainCookie!.opts.maxAge).toBe(defaultOptions.maxAge);
      });

      it('should chunk large values', () => {
        const store = new SessionStore(defaultOptions);
        const written: Array<{ name: string; value: string }> = [];
        const setCookie = (name: string, value: string, _opts: any) => {
          written.push({ name, value });
        };

        // Create value larger than 3800 bytes
        const largeValue = 'x'.repeat(8000);
        store.write(largeValue, setCookie);

        // Should have main cookie + at least one chunk
        const mainCookie = written.find((c) => c.name === '__me.session-token');
        const chunk1 = written.find((c) => c.name === '__me.session-token.1');
        const chunk2 = written.find((c) => c.name === '__me.session-token.2');

        expect(mainCookie).toBeDefined();
        expect(chunk1).toBeDefined();
        expect(chunk2).toBeDefined();

        // Reassembled should equal original
        const reassembled = (mainCookie?.value ?? '') + (chunk1?.value ?? '') + (chunk2?.value ?? '');
        expect(reassembled).toBe(largeValue);
      });
    });

    describe('delete', () => {
      it('should delete main cookie and cleanup chunks', () => {
        const store = new SessionStore(defaultOptions);
        const deleted: string[] = [];
        const setCookie = (name: string, value: string, opts: any) => {
          if (opts.maxAge === 0) deleted.push(name);
        };

        store.delete(setCookie);

        expect(deleted).toContain('__me.session-token');
        // Should also try to clear chunks
        expect(deleted.length).toBeGreaterThan(1);
      });
    });
  });

  describe('CsrfStore', () => {
    const options = resolveCookieOptions();

    it('should generate correct cookie name', () => {
      const store = new CsrfStore(options);
      expect(store.getCookieName()).toBe('__me.csrf-token');
    });

    it('should generate secure cookie name', () => {
      const secureOptions = resolveCookieOptions({ secure: true });
      const store = new CsrfStore(secureOptions);
      expect(store.getCookieName()).toBe('__Secure-__me.csrf-token');
    });

    it('should read cookie value', () => {
      const store = new CsrfStore(options);
      const getCookie = (name: string) => {
        if (name === '__me.csrf-token') return 'csrf-value';
        return undefined;
      };

      expect(store.read(getCookie)).toBe('csrf-value');
    });

    it('should return null when cookie missing', () => {
      const store = new CsrfStore(options);
      expect(store.read(() => undefined)).toBeNull();
    });

    it('should write cookie value', () => {
      const store = new CsrfStore(options);
      let writtenName = '';
      let writtenValue = '';
      const setCookie = (name: string, value: string, _opts: any) => {
        writtenName = name;
        writtenValue = value;
      };

      store.write('csrf-token-value', setCookie);

      expect(writtenName).toBe('__me.csrf-token');
      expect(writtenValue).toBe('csrf-token-value');
    });

    it('should delete cookie', () => {
      const store = new CsrfStore(options);
      let deletedOpts: any = {};
      const setCookie = (_name: string, _value: string, opts: any) => {
        deletedOpts = opts;
      };

      store.delete(setCookie);

      expect(deletedOpts.maxAge).toBe(0);
    });
  });

  describe('CallbackStore', () => {
    const options = resolveCookieOptions();

    it('should generate correct cookie name', () => {
      const store = new CallbackStore(options);
      expect(store.getCookieName()).toBe('__me.callback-url');
    });

    it('should generate secure cookie name', () => {
      const secureOptions = resolveCookieOptions({ secure: true });
      const store = new CallbackStore(secureOptions);
      expect(store.getCookieName()).toBe('__Secure-__me.callback-url');
    });

    it('should read and write values', () => {
      const store = new CallbackStore(options);
      const cookies: Record<string, string> = {};
      const setCookie = (name: string, value: string, _opts: any) => {
        cookies[name] = value;
      };
      const getCookie = (name: string) => cookies[name];

      store.write('https://example.com/callback', setCookie);
      expect(store.read(getCookie)).toBe('https://example.com/callback');
    });

    it('should delete cookie', () => {
      const store = new CallbackStore(options);
      let maxAge: number | undefined;
      const setCookie = (_name: string, _value: string, opts: any) => {
        maxAge = opts.maxAge;
      };

      store.delete(setCookie);

      expect(maxAge).toBe(0);
    });
  });
});
