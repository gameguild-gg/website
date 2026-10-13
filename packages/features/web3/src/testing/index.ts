/**
 * Testing utilities: a configurable EIP-1193 provider mock used by this
 * package's suite and by application tests of the wallet UI.
 */
import type { Eip1193EventMap, Eip1193Provider, Eip1193RequestArguments } from '../types';

export interface MockEip1193Provider extends Eip1193Provider {
  /** Programmatically emit a provider event to all listeners. */
  emit<K extends keyof Eip1193EventMap>(event: K, ...args: Parameters<Eip1193EventMap[K]>): void;
  /** All `request()` calls, in order. */
  readonly requests: ReadonlyArray<Eip1193RequestArguments>;
  /** Replace the request handler. */
  onRequest(handler: (args: Eip1193RequestArguments) => unknown): void;
  /** Fail the next `request()` with a provider-style error, once. */
  failNextRequest(error: Error & { code?: number }): void;
}

export interface MockProviderOptions {
  /** Accounts returned by account methods (default: one deterministic address). */
  readonly accounts?: readonly string[];
  /** Chain id returned by `eth_chainId` (default hex mainnet `0x1`). */
  readonly chainId?: string;
  /** Signatures returned by `personal_sign`. */
  readonly signatures?: readonly string[];
}

export { COMMON_CHAINS } from '../types';

export const TEST_ADDRESS = '0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb0';
export const TEST_CHAIN_ID_HEX = '0x1';
export const TEST_SIGNATURE =
  '0x5d99b659a52d3f5a7c95b1c6047ff8c22e5f4e8c9d3a7b6f2e1d0c9b8a7f6e5d4c3b2a1908f7e6d5c4b3a29180f0e7d6c5b4a3928170f6e5d4c3b2a1908070605040302011b';

/**
 * Create an EIP-1193 provider mock with default happy-path behavior:
 * `eth_requestAccounts`/`eth_accounts` → accounts, `eth_chainId` → chainId,
 * `personal_sign` → signatures (rotating), everything else → `null`.
 */
export const createMockEip1193Provider = (
  options: MockProviderOptions = {},
): MockEip1193Provider => {
  const accounts = options.accounts ?? [TEST_ADDRESS];
  const chainId = options.chainId ?? TEST_CHAIN_ID_HEX;
  const signatures = options.signatures ?? [TEST_SIGNATURE];

  const requests: Eip1193RequestArguments[] = [];
  const listeners = new Map<keyof Eip1193EventMap, Set<Eip1193EventMap[keyof Eip1193EventMap]>>();
  let handler: (args: Eip1193RequestArguments) => unknown = (args) => {
    if (args.method === 'eth_requestAccounts' || args.method === 'eth_accounts') {
      return [...accounts];
    }
    if (args.method === 'eth_chainId') return chainId;
    if (args.method === 'personal_sign') {
      return signatures[Math.min(requests.length, signatures.length - 1)] ?? TEST_SIGNATURE;
    }
    return null;
  };
  let nextError: (Error & { code?: number }) | undefined;

  const provider: MockEip1193Provider = {
    request: async (args) => {
      requests.push(args);
      if (nextError) {
        const error = nextError;
        nextError = undefined;
        throw error;
      }
      return handler(args);
    },
    on: (event, listener) => {
      const set = listeners.get(event) ?? new Set();
      set.add(listener);
      listeners.set(event, set);
    },
    removeListener: (event, listener) => {
      listeners.get(event)?.delete(listener);
    },
    emit: (event, ...args) => {
      for (const listener of listeners.get(event) ?? []) {
        (listener as (...a: unknown[]) => void)(...args);
      }
    },
    get requests() {
      return requests;
    },
    onRequest: (replacement) => {
      handler = replacement;
    },
    failNextRequest: (error) => {
      nextError = error;
    },
  };

  return provider;
};
