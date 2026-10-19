/**
 * Tests for OAuth Provider Type-Safe Helpers
 */

import { describe, it, expect, vi } from 'vitest';
import {
  getOAuthExchangeToken,
  getOAuthAuthorizeUrl,
  getOAuthHandleCallback,
  type OAuthProviderWithMethods,
} from '../../src/integrations/next/oauth-helpers.js';

function makeBaseProvider(overrides?: Partial<OAuthProviderWithMethods>): OAuthProviderWithMethods {
  return {
    id: 'test-provider',
    name: 'Test Provider',
    type: 'oauth',
    clientId: 'fixture-client-id',
    clientSecret: '',
    ...overrides,
  };
}

describe('getOAuthExchangeToken', () => {
  it('should return the exchangeToken function when present', () => {
    const exchangeFn = vi.fn();
    const provider = makeBaseProvider({ exchangeToken: exchangeFn });

    const result = getOAuthExchangeToken(provider);

    expect(result).toBe(exchangeFn);
  });

  it('should return undefined when exchangeToken is not present', () => {
    const provider = makeBaseProvider();

    const result = getOAuthExchangeToken(provider);

    expect(result).toBeUndefined();
  });

  it('should return undefined when exchangeToken is not a function', () => {
    const provider = makeBaseProvider();
    Reflect.set(provider, 'exchangeToken', 'not-a-function');

    const result = getOAuthExchangeToken(provider);

    expect(result).toBeUndefined();
  });
});

describe('getOAuthAuthorizeUrl', () => {
  it('should return the getAuthorizeUrl function when present', () => {
    const authorizeFn = vi.fn();
    const provider = makeBaseProvider({ getAuthorizeUrl: authorizeFn });

    const result = getOAuthAuthorizeUrl(provider);

    expect(result).toBe(authorizeFn);
  });

  it('should return undefined when getAuthorizeUrl is not present', () => {
    const provider = makeBaseProvider();

    const result = getOAuthAuthorizeUrl(provider);

    expect(result).toBeUndefined();
  });

  it('should return undefined when getAuthorizeUrl is not a function', () => {
    const provider = makeBaseProvider();
    Reflect.set(provider, 'getAuthorizeUrl', 42);

    const result = getOAuthAuthorizeUrl(provider);

    expect(result).toBeUndefined();
  });
});

describe('getOAuthHandleCallback', () => {
  it('should return the handleCallback function when present', () => {
    const callbackFn = vi.fn();
    const provider = makeBaseProvider({ handleCallback: callbackFn });

    const result = getOAuthHandleCallback(provider);

    expect(result).toBe(callbackFn);
  });

  it('should return undefined when handleCallback is not present', () => {
    const provider = makeBaseProvider();

    const result = getOAuthHandleCallback(provider);

    expect(result).toBeUndefined();
  });

  it('should return undefined when handleCallback is not a function', () => {
    const provider = makeBaseProvider();
    Reflect.set(provider, 'handleCallback', null);

    const result = getOAuthHandleCallback(provider);

    expect(result).toBeUndefined();
  });
});

describe('OAuthProviderWithMethods integration', () => {
  it('should work with a provider that has all methods', () => {
    const provider = makeBaseProvider({
      exchangeToken: vi.fn(async () => ({
        tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
        user: { id: '1', email: 'a@b.com', name: null, image: null },
      })),
      getAuthorizeUrl: vi.fn(async () => 'https://auth.example.com'),
      handleCallback: vi.fn(async () => ({
        tokens: { accessToken: 'at', refreshToken: 'rt', tokenType: 'Bearer' as const },
        user: { id: '1', email: 'a@b.com', name: null, image: null },
      })),
    });

    expect(getOAuthExchangeToken(provider)).toBeDefined();
    expect(getOAuthAuthorizeUrl(provider)).toBeDefined();
    expect(getOAuthHandleCallback(provider)).toBeDefined();
  });

  it('should work with a provider that has no methods', () => {
    const provider = makeBaseProvider();

    expect(getOAuthExchangeToken(provider)).toBeUndefined();
    expect(getOAuthAuthorizeUrl(provider)).toBeUndefined();
    expect(getOAuthHandleCallback(provider)).toBeUndefined();
  });
});
