/**
 * Token Refresh Manager
 *
 * Handles automatic token refresh with mutex pattern to prevent concurrent refreshes.
 */

import type { TokenProvider, TokenPair } from './types.js';
import { TokenRefreshError } from './errors.js';

export interface TokenRefreshConfig {
  /**
   * Refresh token endpoint URL (relative to base URL)
   *
   * @example '/api/auth/refresh'
   * @example '/v1/token/refresh'
   */
  refreshUrl: string;
  /** Refresh token before this many ms until expiry (default: 30000) */
  refreshThreshold: number;
  /** Maximum number of retry attempts (default: 3) */
  maxRetries: number;
  /** Base delay for exponential backoff in ms (default: 1000) */
  backoffBase: number;
}

/**
 * Default configuration for token refresh.
 *
 * IMPORTANT: Always provide your own `refreshUrl` to match your API's auth endpoint.
 * The default is a placeholder that may not match your backend.
 */
const DEFAULT_CONFIG: TokenRefreshConfig = {
  refreshUrl: '/api/auth/refresh', // Common default - override for your API
  refreshThreshold: 30_000, // 30 seconds before expiry
  maxRetries: 3,
  backoffBase: 1000,
};

/**
 * Token refresh manager with mutex pattern
 */
export class TokenRefreshManager {
  private refreshPromise: Promise<TokenPair | null> | null = null;
  private tokenExpiry: number | null = null;
  private config: TokenRefreshConfig;

  constructor(
    private provider: TokenProvider,
    private baseUrl: string,
    config: Partial<TokenRefreshConfig> = {},
  ) {
    this.config = { ...DEFAULT_CONFIG, ...config };
  }

  /**
   * Set the token expiry time
   */
  setExpiry(expiresIn: number): void {
    // Convert seconds to timestamp
    this.tokenExpiry = Date.now() + expiresIn * 1000;
  }

  /**
   * Check if token needs refresh
   */
  shouldRefresh(): boolean {
    if (!this.tokenExpiry) return false;

    const timeUntilExpiry = this.tokenExpiry - Date.now();
    return timeUntilExpiry < this.config.refreshThreshold;
  }

  /**
   * Refresh the token if needed
   *
   * Uses mutex pattern to prevent concurrent refresh calls.
   */
  async refreshIfNeeded(): Promise<TokenPair | null> {
    if (!this.shouldRefresh()) {
      return null;
    }

    return this.refresh();
  }

  /**
   * Force refresh the token
   */
  async refresh(): Promise<TokenPair | null> {
    // Use existing refresh promise if one is in progress (mutex pattern)
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshPromise = this.doRefresh();

    try {
      return await this.refreshPromise;
    } finally {
      this.refreshPromise = null;
    }
  }

  /**
   * Perform the actual refresh
   */
  private async doRefresh(): Promise<TokenPair | null> {
    const refreshToken = await this.provider.getRefreshToken?.();

    if (!refreshToken) {
      // No refresh token available, trigger auth required
      await this.provider.onAuthenticationRequired?.();
      return null;
    }

    let lastError: Error | null = null;

    for (let attempt = 0; attempt < this.config.maxRetries; attempt++) {
      try {
        const tokens = await this.executeRefresh(refreshToken);

        // Update expiry
        if (tokens.expiresIn) {
          this.setExpiry(tokens.expiresIn);
        }

        // Notify provider of new tokens
        await this.provider.onTokenRefresh?.(tokens);

        return tokens;
      } catch (error) {
        lastError = error instanceof Error ? error : new Error(String(error));
        if (error instanceof TokenRefreshError && (error.responseStatus === 401 || error.responseStatus === 403)) {
          this.tokenExpiry = null;
          await this.provider.onAuthenticationRequired?.();
          return null;
        }

        // Exponential backoff
        if (attempt < this.config.maxRetries - 1) {
          const delay = this.config.backoffBase * Math.pow(2, attempt);
          await sleep(delay);
        }
      }
    }

    // All retries failed
    console.error('Token refresh failed after', this.config.maxRetries, 'attempts:', lastError);
    await this.provider.onAuthenticationRequired?.();
    return null;
  }

  /**
   * Execute the refresh API call
   */
  private async executeRefresh(refreshToken: string): Promise<TokenPair> {
    const response = await fetch(`${this.baseUrl}${this.config.refreshUrl}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) {
      throw new TokenRefreshError(`Refresh failed: ${response.status} ${response.statusText}`, undefined, response.status);
    }

    const data = await response.json();

    return {
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      expiresIn: data.expiresIn,
      tokenType: 'Bearer',
      scope: data.scope,
    };
  }
}

/**
 * Sleep utility
 */
function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}
