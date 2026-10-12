/**
 * MetaMask adapter (issue #39).
 *
 * Detection follows EIP-6963 (Multi Injected Provider Discovery) when the
 * wallet supports it, falling back to the legacy `window.ethereum.isMetaMask`
 * probe (including the multi-wallet `providers[]` array). Every operation is
 * plain EIP-1193 RPC — no MetaMask-specific SDK, no ethers dependency.
 */
import { toWeb3Error } from '../types';
import type {
  ChainInfo,
  Eip1193Provider,
  Eip1193ProviderError,
  WalletAdapter,
  WalletConnection,
  WalletEventHandlers,
} from '../types';
import { parseChainId, utf8ToHex } from '../lib/utils';
import { requestChainSwitch } from './chain-switch';

/** EIP-6963 provider info announced by injected wallets. */
export interface Eip6963ProviderInfo {
  readonly uuid: string;
  readonly name: string;
  readonly icon: string;
  readonly rdns: string;
}

/** EIP-6963 announced provider detail. */
export interface Eip6963ProviderDetail {
  readonly info: Eip6963ProviderInfo;
  readonly provider: Eip1193Provider;
}

/** MetaMask's EIP-6963 reverse-DNS identifier. */
export const METAMASK_RDNS = 'io.metamask';

/**
 * Minimal shape of the legacy `window.ethereum` object. Multi-wallet
 * browsers (Brave, Phantom, ...) expose a `providers` array.
 */
export interface InjectedEthereum {
  readonly isMetaMask?: boolean;
  readonly providers?: readonly InjectedEthereum[];
  request(args: { method: string; params?: readonly unknown[] | object }): Promise<unknown>;
  on?(event: string, listener: (...args: never[]) => void): void;
  removeListener?(event: string, listener: (...args: never[]) => void): void;
}

/**
 * Provider discovery result: where the MetaMask EIP-1193 provider came from.
 * Exposed for tests and diagnostics.
 */
export interface MetaMaskDiscovery {
  readonly provider: Eip1193Provider;
  readonly source: 'eip6963' | 'injected';
}

/** Overridable discovery seam — tests inject a fixed environment. */
export type MetaMaskDiscoveryFn = () => Promise<MetaMaskDiscovery | undefined>;

export interface MetaMaskAdapterOptions {
  /** Custom discovery (tests). Defaults to EIP-6963 + `window.ethereum`. */
  readonly discover?: MetaMaskDiscoveryFn;
}

const isProviderLike = (candidate: unknown): candidate is InjectedEthereum =>
  typeof candidate === 'object' &&
  candidate !== null &&
  typeof (candidate as InjectedEthereum).request === 'function';

/** Pick the MetaMask entry out of a legacy `providers[]` array. */
export const selectMetaMaskFromProviders = (
  providers: readonly unknown[],
): InjectedEthereum | undefined => {
  const candidates = providers.filter(isProviderLike);
  // Prefer an entry explicitly flagged `isMetaMask` that is not another
  // wallet's shim (e.g. Phantom's combined provider also sets isMetaMask;
  // ordering keeps the first dedicated entry).
  return (
    candidates.find((entry) => entry.isMetaMask === true && !('isPhantom' in entry)) ??
    candidates.find((entry) => entry.isMetaMask === true) ??
    candidates.find((entry) => entry.providers === undefined && entry.isMetaMask === undefined)
  );
};

const asEip1193 = (injected: InjectedEthereum): Eip1193Provider => ({
  request: (args) => injected.request(args),
  on: (event, listener) => injected.on?.(event, listener as (...args: never[]) => void),
  removeListener: (event, listener) =>
    injected.removeListener?.(event, listener as (...args: never[]) => void),
});

/**
 * Default discovery: EIP-6963 announcement first (fresh event dispatch), then
 * the legacy injected probe. Safe on servers (`typeof window` guarded).
 */
export const discoverMetaMask = async (): Promise<MetaMaskDiscovery | undefined> => {
  if (typeof window === 'undefined') return undefined;

  const eip6963 = await discoverViaEip6963(window);
  if (eip6963) return eip6963;

  const injected = (window as { ethereum?: unknown }).ethereum;
  if (!injected) return undefined;

  let candidate: InjectedEthereum | undefined;
  if (isProviderLike(injected)) {
    candidate =
      Array.isArray(injected.providers) && injected.providers.length > 0
        ? selectMetaMaskFromProviders(injected.providers)
        : injected.isMetaMask === true || injected.providers !== undefined
          ? injected
          : undefined;
  } else if (Array.isArray(injected)) {
    candidate = selectMetaMaskFromProviders(injected);
  }
  if (!candidate) return undefined;
  return { provider: asEip1193(candidate), source: 'injected' };
};

const discoverViaEip6963 = (target: {
  addEventListener(type: string, listener: (event: Event) => void): void;
  removeEventListener(type: string, listener: (event: Event) => void): void;
  dispatchEvent(event: Event): boolean;
}): Promise<MetaMaskDiscovery | undefined> =>
  new Promise((resolve) => {
    let settled = false;
    const finish = (detail: MetaMaskDiscovery | undefined) => {
      if (settled) return;
      settled = true;
      target.removeEventListener('eip6963:announceProvider', onAnnounce);
      resolve(detail);
    };
    const onAnnounce = (event: Event) => {
      const detail = (event as CustomEvent<Eip6963ProviderDetail>).detail;
      if (detail?.info?.rdns === METAMASK_RDNS) {
        finish({ provider: detail.provider, source: 'eip6963' });
      }
    };
    target.addEventListener('eip6963:announceProvider', onAnnounce);
    target.dispatchEvent(new Event('eip6963:requestProvider'));
    // EIP-6963 wallets answer synchronously after the request event; a short
    // macrotask hop is enough before falling back to the legacy probe.
    setTimeout(() => finish(undefined), 0);
  });

/**
 * Create the MetaMask wallet adapter.
 *
 * The adapter is inert until `connect()`; discovery runs on demand for
 * `isAvailable()` so mounting never prompts the user.
 */
export const createMetaMaskAdapter = (options: MetaMaskAdapterOptions = {}): WalletAdapter => {
  const discover = options.discover ?? discoverMetaMask;
  let activeProvider: Eip1193Provider | undefined;

  const requireProvider = async (): Promise<Eip1193Provider> => {
    if (activeProvider) return activeProvider;
    const discovery = await discover();
    if (!discovery) {
      throw toWeb3Error(new Error('MetaMask was not detected in this browser.'), 'wallet_not_found');
    }
    activeProvider = discovery.provider;
    return activeProvider;
  };

  return {
    id: 'metamask',
    name: 'MetaMask',

    isAvailable: async () => {
      try {
        const discovery = await discover();
        return discovery !== undefined;
      } catch {
        return false;
      }
    },

    connect: async (): Promise<WalletConnection> => {
      const provider = await requireProvider();
      const accounts = (await provider.request({
        method: 'eth_requestAccounts',
      })) as string[] | undefined;
      const address = Array.isArray(accounts) ? accounts[0] : undefined;
      if (typeof address !== 'string' || address.length === 0) {
        throw toWeb3Error(new Error('MetaMask did not return an account.'), 'connection_failed');
      }
      const chainIdHex = (await provider.request({ method: 'eth_chainId' })) as string;
      return { address, chainId: parseChainId(chainIdHex) };
    },

    disconnect: async () => {
      // Injected wallets have no reliable logout RPC; dropping the provider
      // reference is the EIP-1193-sanctioned disconnect for dapps.
      activeProvider = undefined;
    },

    signMessage: async (address: string, message: string): Promise<string> => {
      const provider = await requireProvider();
      const signature = (await provider.request({
        method: 'personal_sign',
        params: [utf8ToHex(message), address],
      })) as string;
      if (typeof signature !== 'string' || signature.length === 0) {
        throw toWeb3Error(new Error('MetaMask did not return a signature.'), 'sign_failed');
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
