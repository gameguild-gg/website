/**
 * Wallet adapter registry (issues #17/#18 under umbrella #291).
 *
 * Single composition point where the supported wallets — MetaMask (#39),
 * Trust Wallet (#17), WalletConnect (#18) and Torus (#16) — plug in side by
 * side. Every entry implements the same framework-agnostic `WalletAdapter`
 * contract, so the `Web3Provider` registry (its adapter map) and the shared
 * SIWE journey apply to all of them uniformly.
 *
 * Default posture:
 * - MetaMask and Trust Wallet are detection-driven: register them always,
 *   the button only renders when the adapter reports available.
 * - WalletConnect and Torus are opt-in: pass their options (with the
 *   deployment gate enabled) to register them; they fail closed otherwise.
 */
import type { WalletAdapter } from '../types';
import { createMetaMaskAdapter } from './metamask';
import { createTrustWalletAdapter, TrustWalletAdapterOptions } from './trust-wallet';
import {
  createWalletConnectAdapter,
  WalletConnectAdapterOptions,
} from './walletconnect';
import { createTorusAdapter, TorusAdapterOptions } from './torus';

export interface WalletRegistryConfig {
  /** Register the MetaMask adapter (default `true`). */
  readonly metamask?: boolean;
  /**
   * Register the Trust Wallet adapter. `true` uses the defaults (detection
   * with the mobile deep-link fallback enabled); pass options to customize.
   * Default: registered.
   */
  readonly trustWallet?: boolean | TrustWalletAdapterOptions;
  /**
   * Register the WalletConnect adapter. Pass its options including
   * `enabled: true` and the deployment's `projectId` — the adapter fails
   * closed (unavailable) without them. Default: not registered.
   */
  readonly walletConnect?: WalletConnectAdapterOptions;
  /**
   * Register the Torus adapter. Pass its options including `enabled: true`
   * (the OIDC-style deployment gate) — the adapter fails closed otherwise.
   * Default: not registered.
   */
  readonly torus?: TorusAdapterOptions;
}

/**
 * Drop adapters whose id was already registered: the first registration
 * wins, so a wallet can never silently shadow another when multiple
 * sources compose the same registry.
 */
export const dedupeAdapters = (
  adapters: readonly WalletAdapter[],
): WalletAdapter[] => {
  const seen = new Set<string>();
  return adapters.filter((adapter) => {
    if (seen.has(adapter.id)) return false;
    seen.add(adapter.id);
    return true;
  });
};

/**
 * Compose the wallet adapter registry from configuration. Adapters are
 * returned in stable button order (injected wallets first, then protocol
 * wallets); duplicate adapter ids are skipped so a wallet can never
 * silently shadow another registration.
 */
export const createWalletAdapters = (config: WalletRegistryConfig = {}): WalletAdapter[] => {
  const adapters: WalletAdapter[] = [];

  if (config.metamask !== false) {
    adapters.push(createMetaMaskAdapter());
  }

  const trustWallet = config.trustWallet ?? true;
  if (trustWallet !== false) {
    adapters.push(
      typeof trustWallet === 'object'
        ? createTrustWalletAdapter(trustWallet)
        : createTrustWalletAdapter(),
    );
  }

  if (config.walletConnect) {
    adapters.push(createWalletConnectAdapter(config.walletConnect));
  }

  if (config.torus) {
    adapters.push(createTorusAdapter(config.torus));
  }

  return dedupeAdapters(adapters);
};
