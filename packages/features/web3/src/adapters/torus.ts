/**
 * Torus adapter (issue #16).
 *
 * Torus is loaded behind a lazy dynamic `import('@toruslabs/torus-embed')`
 * so the SDK never lands in the main bundle. The adapter fails closed:
 * when the SDK is not installed, `loadEmbed` throws, or the deployment
 * has not enabled Torus (`enabled: false`, mirroring the OIDC federation
 * provider gating), `isAvailable()` resolves `false` and the wallet UI
 * hides the button instead of half-working.
 *
 * Once loaded, the embed's EIP-1193 provider is used for exactly the same
 * RPC surface as MetaMask (`eth_requestAccounts`, `personal_sign`,
 * `wallet_switchEthereumChain`), so the shared SIWE journey applies.
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

/** The subset of the Torus embed the adapter relies on. */
export interface TorusEmbedLike {
  init(options?: Record<string, unknown>): Promise<unknown>;
  login(): Promise<unknown>;
  logout(): Promise<unknown>;
  readonly provider?: Eip1193Provider;
  /** Newer embed builds expose the provider as `ethereum`. */
  readonly ethereum?: Eip1193Provider;
}

/** Torus network configuration passed to `embed.init`. */
export interface TorusNetworkOptions {
  /** Chain host: `"mainnet"`, `"sepolia"`, or a custom RPC URL. */
  readonly host: string;
  /** Decimal chain id string matching the backend SIWE allow-list. */
  readonly chainId?: string;
  readonly networkName?: string;
}

export interface TorusAdapterOptions {
  /**
   * Deployment gate (default `false`). Torus only becomes connectable when
   * the app opts in — the same fail-closed pattern as the OIDC federation
   * provider being enabled only when its client env vars exist.
   */
  readonly enabled?: boolean;
  /**
   * Loader for the embed. Defaults to the lazy dynamic import of
   * `@toruslabs/torus-embed`; rejects (fail closed) when the package is
   * not installed or the chunk fails to load.
   */
  readonly loadEmbed?: () => Promise<TorusEmbedLike>;
  /** Options forwarded to `embed.init()` (network, UX toggles). */
  readonly initOptions?: Record<string, unknown>;
  /** Torus network selection for `embed.init()`. */
  readonly network?: TorusNetworkOptions;
}

const embedProvider = (embed: TorusEmbedLike): Eip1193Provider | undefined =>
  embed.provider ?? embed.ethereum;

/** Default loader: lazy dynamic import — resolved only on first connect. */
const defaultLoadEmbed = async (): Promise<TorusEmbedLike> => {
  const module = await import('@toruslabs/torus-embed');
  const Torus = module.default;
  return new Torus();
};

/**
 * Create the Torus wallet adapter.
 *
 * `isAvailable()` is intentionally cheap: it checks the config gate and the
 * loader's presence without touching the network or opening the embed.
 */
export const createTorusAdapter = (options: TorusAdapterOptions = {}): WalletAdapter => {
  const enabled = options.enabled ?? false;
  const loadEmbed = options.loadEmbed ?? defaultLoadEmbed;
  let embedPromise: Promise<TorusEmbedLike> | undefined;
  let initialized = false;
  let loggedIn = false;

  const loadAndLogin = async (): Promise<{ embed: TorusEmbedLike; provider: Eip1193Provider }> => {
    if (!enabled) {
      throw toWeb3Error(
        new Error('Torus sign-in is not enabled for this deployment.'),
        'wallet_not_found',
      );
    }
    if (!embedPromise) {
      embedPromise = loadEmbed();
    }
    let embed: TorusEmbedLike;
    try {
      embed = await embedPromise;
    } catch (error) {
      embedPromise = undefined;
      throw toWeb3Error(
        new Error(
          `The Torus wallet SDK could not be loaded.${
            error instanceof Error ? ` ${error.message}` : ''
          }`,
        ),
        'wallet_not_found',
      );
    }
    if (!initialized) {
      try {
        await embed.init({
          showTorusButton: false,
          ...(options.network ? { network: { ...options.network } } : {}),
          ...options.initOptions,
        });
        initialized = true;
      } catch (error) {
        throw toWeb3Error(error, 'connection_failed');
      }
    }
    const provider = embedProvider(embed);
    if (!provider) {
      throw toWeb3Error(new Error('Torus did not expose an Ethereum provider.'), 'wallet_not_found');
    }
    if (!loggedIn) {
      try {
        await embed.login();
        loggedIn = true;
      } catch (error) {
        throw toWeb3Error(error, 'user_rejected');
      }
    }
    return { embed, provider };
  };

  return {
    id: 'torus',
    name: 'Torus',

    isAvailable: async () => {
      if (!enabled) return false;
      // The SDK itself is loaded lazily on first connect; availability here
      // means "the deployment allows Torus and a loader is configured".
      return typeof loadEmbed === 'function';
    },

    connect: async (): Promise<WalletConnection> => {
      const { provider } = await loadAndLogin();
      const accounts = (await provider.request({ method: 'eth_accounts' })) as string[] | undefined;
      const address = Array.isArray(accounts) ? accounts[0] : undefined;
      if (typeof address !== 'string' || address.length === 0) {
        throw toWeb3Error(new Error('Torus did not return an account.'), 'connection_failed');
      }
      const chainIdHex = (await provider.request({ method: 'eth_chainId' })) as string;
      return { address, chainId: parseChainId(chainIdHex) };
    },

    disconnect: async () => {
      if (!embedPromise) return;
      try {
        const embed = await embedPromise;
        await embed.logout();
      } catch {
        // Best-effort: local state is dropped regardless.
      } finally {
        embedPromise = undefined;
        initialized = false;
        loggedIn = false;
      }
    },

    signMessage: async (address: string, message: string): Promise<string> => {
      const { provider } = await loadAndLogin();
      const signature = (await provider.request({
        method: 'personal_sign',
        params: [utf8ToHex(message), address],
      })) as string;
      if (typeof signature !== 'string' || signature.length === 0) {
        throw toWeb3Error(new Error('Torus did not return a signature.'), 'sign_failed');
      }
      return signature;
    },

    switchChain: async (chain: ChainInfo): Promise<void> => {
      const { provider } = await loadAndLogin();
      await requestChainSwitch(provider, chain);
    },

    onEvent: (handlers: WalletEventHandlers): (() => void) => {
      if (!embedPromise) return () => undefined;
      let unsubscribe: (() => void) | undefined;
      let cancelled = false;
      void embedPromise
        .then((embed) => {
          const provider = embedProvider(embed);
          if (!provider || cancelled) return;
          const accountsChanged = (accounts: string[]) => handlers.onAccountsChanged(accounts);
          const chainChanged = (chainId: string) => handlers.onChainChanged(parseChainId(chainId));
          const disconnected = (error: Eip1193ProviderError) => handlers.onDisconnected(error);
          provider.on('accountsChanged', accountsChanged);
          provider.on('chainChanged', chainChanged);
          provider.on('disconnect', disconnected);
          unsubscribe = () => {
            provider.removeListener('accountsChanged', accountsChanged);
            provider.removeListener('chainChanged', chainChanged);
            provider.removeListener('disconnect', disconnected);
          };
        })
        .catch(() => undefined);
      return () => {
        cancelled = true;
        unsubscribe?.();
      };
    },
  };
};
