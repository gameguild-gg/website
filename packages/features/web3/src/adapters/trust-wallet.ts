/**
 * Trust Wallet adapter (issue #17).
 *
 * Trust Wallet is reached through plain EIP-1193 — no SDK dependency:
 * - Browser extension: announces itself via EIP-6963 with the rdns
 *   `com.trustwallet.app`, and legacy builds expose `window.trustwallet`
 *   or flag `window.ethereum` with `isTrust`/`isTrustWallet` (including
 *   the multi-wallet `providers[]` array).
 * - Trust in-app browser (mobile): injects the same provider, so the page
 *   opened inside Trust Wallet is just the extension environment.
 * - Mobile external browser: no provider can be injected, so the adapter
 *   falls back to Trust's universal deep link
 *   (`https://link.trustwallet.com/open_url?coin_id=60&url=…`) which
 *   reopens the current page inside the Trust in-app browser where the
 *   provider exists.
 *
 * Every operation (connect, personal_sign, wallet_switchEthereumChain,
 * events) is the shared EIP-1193 surface, so the SIWE journey and the
 * `Web3Provider` state machine apply unchanged.
 */
import { toWeb3Error } from '../types';
import type {
  ChainInfo,
  Eip1193Provider,
  Eip1193ProviderError,
  WalletAdapter,
  WalletConnection,
  WalletEventHandlers,
  Web3Error,
} from '../types';
import { parseChainId, utf8ToHex } from '../lib/utils';
import { requestChainSwitch } from './chain-switch';
import type { Eip6963ProviderDetail } from './metamask';

/** Trust Wallet's EIP-6963 reverse-DNS identifier. */
export const TRUST_WALLET_RDNS = 'com.trustwallet.app';

/** Trust's dedicated injected object (extension and in-app browser). */
export interface InjectedTrustWallet {
  readonly isTrust?: boolean;
  readonly isTrustWallet?: boolean;
  readonly providers?: readonly InjectedTrustWallet[];
  request(args: { method: string; params?: readonly unknown[] | object }): Promise<unknown>;
  on?(event: string, listener: (...args: never[]) => void): void;
  removeListener?(event: string, listener: (...args: never[]) => void): void;
}

/** Where the Trust Wallet EIP-1193 provider was found. */
export type TrustWalletDiscoverySource =
  | 'eip6963'
  | 'window.trustwallet'
  | 'window.ethereum';

/** Provider discovery result, exposed for tests and diagnostics. */
export interface TrustWalletDiscovery {
  readonly provider: Eip1193Provider;
  readonly source: TrustWalletDiscoverySource;
}

/** Overridable discovery seam — tests inject a fixed environment. */
export type TrustWalletDiscoveryFn = () => Promise<TrustWalletDiscovery | undefined>;

/** Trust universal deep-link configuration. */
export interface TrustWalletDeepLinkOptions {
  /**
   * Whether the mobile fallback may navigate the page into Trust's in-app
   * browser (default `true`). Only ever triggered from an explicit
   * `connect()` attempt on a mobile browser without an injected provider.
   */
  readonly enabled?: boolean;
  /** Returns the page URL to reopen inside Trust (default: `window.location.href`). */
  readonly getCurrentUrl?: () => string;
  /** Navigation effect (default: `window.location.assign`). */
  readonly navigate?: (href: string) => void;
}

export interface TrustWalletAdapterOptions {
  /** Custom discovery (tests). Defaults to EIP-6963 + injected probes. */
  readonly discover?: TrustWalletDiscoveryFn;
  /** Mobile deep-link fallback configuration. */
  readonly deepLink?: TrustWalletDeepLinkOptions;
}

const isProviderLike = (candidate: unknown): candidate is InjectedTrustWallet =>
  typeof candidate === 'object' &&
  candidate !== null &&
  typeof (candidate as InjectedTrustWallet).request === 'function';

const isTrustFlagged = (candidate: InjectedTrustWallet): boolean =>
  candidate.isTrustWallet === true || candidate.isTrust === true;

/**
 * Pick the Trust Wallet entry out of a legacy `providers[]` array (multi-
 * wallet environments such as Phantom or Brave also inject arrays).
 */
export const selectTrustWalletFromProviders = (
  providers: readonly unknown[],
): InjectedTrustWallet | undefined => {
  const candidates = providers.filter(isProviderLike);
  return candidates.find(isTrustFlagged);
};

const asEip1193 = (injected: InjectedTrustWallet): Eip1193Provider => ({
  request: (args) => injected.request(args),
  on: (event, listener) => injected.on?.(event, listener as (...args: never[]) => void),
  removeListener: (event, listener) =>
    injected.removeListener?.(event, listener as (...args: never[]) => void),
});

/**
 * True for phone/tablet user agents — the environments where Trust Wallet
 * lives in an app and the deep-link fallback is meaningful.
 */
export const isMobileBrowser = (userAgent: string): boolean =>
  /android|iphone|ipad|ipod|mobile|silk|kindle/i.test(userAgent);

/**
 * Build Trust Wallet's universal deep link: opens `pageUrl` inside the
 * Trust in-app browser (`coin_id=60` selects the EVM namespace).
 */
export const buildTrustWalletDeepLink = (pageUrl: string, coinId = 60): string =>
  `https://link.trustwallet.com/open_url?coin_id=${coinId}&url=${encodeURIComponent(pageUrl)}`;

const discoverViaEip6963 = (target: {
  addEventListener(type: string, listener: (event: Event) => void): void;
  removeEventListener(type: string, listener: (event: Event) => void): void;
  dispatchEvent(event: Event): boolean;
}): Promise<TrustWalletDiscovery | undefined> =>
  new Promise((resolve) => {
    let settled = false;
    const finish = (detail: TrustWalletDiscovery | undefined) => {
      if (settled) return;
      settled = true;
      target.removeEventListener('eip6963:announceProvider', onAnnounce);
      resolve(detail);
    };
    const onAnnounce = (event: Event) => {
      const detail = (event as CustomEvent<Eip6963ProviderDetail>).detail;
      if (detail?.info?.rdns === TRUST_WALLET_RDNS) {
        finish({ provider: detail.provider, source: 'eip6963' });
      }
    };
    target.addEventListener('eip6963:announceProvider', onAnnounce);
    target.dispatchEvent(new Event('eip6963:requestProvider'));
    setTimeout(() => finish(undefined), 0);
  });

/**
 * Default discovery: EIP-6963 announcement first (fresh event dispatch),
 * then the injected probes (`window.trustwallet`, flagged `window.ethereum`,
 * multi-wallet `providers[]`). Safe on servers (`typeof window` guarded).
 */
export const discoverTrustWallet = async (): Promise<TrustWalletDiscovery | undefined> => {
  if (typeof window === 'undefined') return undefined;

  const eip6963 = await discoverViaEip6963(window);
  if (eip6963) return eip6963;

  const scope = window as {
    trustwallet?: unknown;
    ethereum?: unknown;
  };

  const dedicated = scope.trustwallet;
  if (isProviderLike(dedicated)) {
    return { provider: asEip1193(dedicated), source: 'window.trustwallet' };
  }

  const injected = scope.ethereum;
  if (isProviderLike(injected)) {
    const fromProviders =
      Array.isArray(injected.providers) && injected.providers.length > 0
        ? selectTrustWalletFromProviders(injected.providers)
        : undefined;
    const candidate = fromProviders ?? (isTrustFlagged(injected) ? injected : undefined);
    if (candidate) {
      return { provider: asEip1193(candidate), source: 'window.ethereum' };
    }
  } else if (Array.isArray(injected)) {
    const candidate = selectTrustWalletFromProviders(injected);
    if (candidate) {
      return { provider: asEip1193(candidate), source: 'window.ethereum' };
    }
  }
  return undefined;
};

const isMobileEnvironment = (): boolean => {
  if (typeof navigator === 'undefined') return false;
  return isMobileBrowser(navigator.userAgent);
};

/**
 * Create the Trust Wallet adapter (issue #17).
 *
 * Environments:
 * 1. Browser extension (desktop) — detected via EIP-6963/injected probes;
 * 2. Trust in-app browser (mobile) — same injected provider;
 * 3. Mobile external browser — deep-link fallback reopens the page inside
 *    Trust, where environment 2 applies after the user returns.
 *
 * In environments 1–2 the button behaves exactly like MetaMask's; in
 * environment 3 `connect()` navigates to Trust and rejects with a
 * `connection_failed` error explaining the hand-off.
 */
export const createTrustWalletAdapter = (options: TrustWalletAdapterOptions = {}): WalletAdapter => {
  const discover = options.discover ?? discoverTrustWallet;
  const deepLinkEnabled = options.deepLink?.enabled ?? true;
  const getCurrentUrl = options.deepLink?.getCurrentUrl ?? (() => window.location.href);
  const navigate =
    options.deepLink?.navigate ??
    ((href: string) => {
      window.location.assign(href);
    });

  let activeProvider: Eip1193Provider | undefined;

  const requireProvider = async (): Promise<Eip1193Provider> => {
    if (activeProvider) return activeProvider;
    const discovery = await discover();
    if (!discovery) {
      throw toWeb3Error(
        new Error('Trust Wallet was not detected in this browser.'),
        'wallet_not_found',
      );
    }
    activeProvider = discovery.provider;
    return activeProvider;
  };

  const deepLinkHandOff = (): Web3Error => {
    const href = buildTrustWalletDeepLink(getCurrentUrl());
    navigate(href);
    return toWeb3Error(
      new Error(
        'Opening Trust Wallet — approve the page in the app, then connect again.',
      ),
      'connection_failed',
    );
  };

  return {
    id: 'trustwallet',
    name: 'Trust Wallet',

    isAvailable: async () => {
      try {
        if (await discover()) return true;
      } catch {
        // Fall through to the deep-link availability decision.
      }
      return deepLinkEnabled && isMobileEnvironment();
    },

    connect: async (): Promise<WalletConnection> => {
      let provider: Eip1193Provider;
      try {
        provider = await requireProvider();
      } catch (error) {
        // No injected provider: on mobile, hand the page to the Trust app.
        if (deepLinkEnabled && isMobileEnvironment()) {
          throw deepLinkHandOff();
        }
        throw error;
      }
      const accounts = (await provider.request({
        method: 'eth_requestAccounts',
      })) as string[] | undefined;
      const address = Array.isArray(accounts) ? accounts[0] : undefined;
      if (typeof address !== 'string' || address.length === 0) {
        throw toWeb3Error(
          new Error('Trust Wallet did not return an account.'),
          'connection_failed',
        );
      }
      const chainIdHex = (await provider.request({ method: 'eth_chainId' })) as string;
      return { address, chainId: parseChainId(chainIdHex) };
    },

    disconnect: async () => {
      // Injected providers (extension and in-app browser) have no logout
      // RPC; dropping the provider reference is the sanctioned disconnect.
      activeProvider = undefined;
    },

    signMessage: async (address: string, message: string): Promise<string> => {
      const provider = await requireProvider();
      const signature = (await provider.request({
        method: 'personal_sign',
        params: [utf8ToHex(message), address],
      })) as string;
      if (typeof signature !== 'string' || signature.length === 0) {
        throw toWeb3Error(new Error('Trust Wallet did not return a signature.'), 'sign_failed');
      }
      return signature;
    },

    switchChain: async (chain: ChainInfo): Promise<void> => {
      const provider = await requireProvider();
      await requestChainSwitch(provider, chain);
    },

    onEvent: (handlers: WalletEventHandlers): (() => void) => {
      const providerRef = activeProvider;
      if (!providerRef) return () => undefined;
      const accountsChanged = (accounts: string[]) => handlers.onAccountsChanged(accounts);
      const chainChanged = (chainId: string) => handlers.onChainChanged(parseChainId(chainId));
      const disconnected = (error: Eip1193ProviderError) => handlers.onDisconnected(error);
      providerRef.on('accountsChanged', accountsChanged);
      providerRef.on('chainChanged', chainChanged);
      providerRef.on('disconnect', disconnected);
      return () => {
        providerRef.removeListener('accountsChanged', accountsChanged);
        providerRef.removeListener('chainChanged', chainChanged);
        providerRef.removeListener('disconnect', disconnected);
      };
    },
  };
};
