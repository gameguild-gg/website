/**
 * WalletConnect adapter (issue #18).
 *
 * Speaks the WalletConnect v2 protocol through the official
 * `@walletconnect/ethereum-provider` SDK, which exposes exactly the EIP-1193
 * surface the shared `WalletAdapter` contract needs:
 * - `connect()` runs the pairing flow (QR modal by default; `display_uri`
 *   carries the pairing URI for hosts that render their own QR);
 * - `personal_sign` session requests sign the SIWE challenge in the paired
 *   wallet and the signature is verified by the existing backend endpoint;
 * - `accountsChanged` / `chainChanged` / `disconnect` plus the session-level
 *   `session_delete` / `session_expire` events cover account, network and
 *   session-expiration handling.
 *
 * The SDK is loaded behind a lazy dynamic `import()` and the adapter is
 * config-gated fail-closed: without `enabled: true` AND a `projectId` the
 * wallet reports unavailable and every operation rejects (`wallet_not_found`).
 * Current SDK releases ship under the proprietary WalletConnect/Reown
 * Community License, so this repository does not vendor the package — a
 * deployment that has accepted the license installs it, and the adapter
 * resolves it from the host at runtime (mirroring the Torus fail-closed
 * pattern, see `src/types/walletconnect-ethereum-provider.d.ts`).
 */
import { toProviderError, toWeb3Error } from '../types';
import type {
  ChainInfo,
  Eip1193ProviderError,
  Eip1193RequestArguments,
  WalletAdapter,
  WalletConnection,
  WalletEventHandlers,
} from '../types';
import { parseChainId, utf8ToHex } from '../lib/utils';
import { requestChainSwitch } from './chain-switch';

/**
 * The subset of the WalletConnect v2 Ethereum provider the adapter relies
 * on. `loadProvider` injects this shape, so tests (and deployments that
 * prefer a different SDK build) never need the real package.
 */
export interface WalletConnectEthereumProviderLike {
  request(args: Eip1193RequestArguments): Promise<unknown>;
  on(event: string, listener: (...args: never[]) => void): void;
  removeListener(event: string, listener: (...args: never[]) => void): void;
  /** Runs the pairing flow (shows the QR modal unless suppressed). */
  connect(options?: { pairingTopic?: string }): Promise<unknown>;
  /** Deletes the relay session and notifies the paired wallet. */
  disconnect(): Promise<void>;
}

/** dapp metadata shown to the user in the pairing wallet. */
export interface WalletConnectMetadata {
  readonly name: string;
  readonly description?: string;
  readonly url?: string;
  readonly icons?: readonly string[];
}

/** Chain/RPC/session options forwarded to `EthereumProvider.init`. */
export interface WalletConnectNetworkOptions {
  /** Required EVM chains as decimal chain id strings (default: `["1"]`). */
  readonly chains?: readonly string[];
  /** Additional chains the session may switch to. */
  readonly optionalChains?: readonly string[];
  /** RPC urls per decimal chain id for wallets without their own rpc. */
  readonly rpcMap?: Readonly<Record<string, string>>;
  readonly metadata?: WalletConnectMetadata;
  /** Render the built-in QR pairing modal (default: `true`). */
  readonly showQrModal?: boolean;
  /** Overrides for the built-in pairing modal. */
  readonly qrModalOptions?: Record<string, unknown>;
}

export interface WalletConnectAdapterOptions extends WalletConnectNetworkOptions {
  /**
   * Deployment gate (default `false`). WalletConnect only becomes connectable
   * when the app opts in — same fail-closed pattern as the Torus adapter.
   */
  readonly enabled?: boolean;
  /**
   * WalletConnect Cloud project id (a public identifier, not a secret).
   * Required when enabled; absent means unavailable.
   */
  readonly projectId?: string;
  /**
   * Loader for the SDK-backed provider. Defaults to the lazy dynamic import
   * of `@walletconnect/ethereum-provider` + `EthereumProvider.init(...)`;
   * rejects (fail closed) when the package is not installed or the relay is
   * unreachable.
   */
  readonly loadProvider?: () => Promise<WalletConnectEthereumProviderLike>;
}

/** Adapter returned by {@link createWalletConnectAdapter}. */
export type WalletConnectAdapter = WalletAdapter & {
  /**
   * The current pairing URI (`wc:` URI v2), captured from the provider's
   * `display_uri` event / `uri` property while a pairing is pending.
   * Hosts that render their own QR code read it here.
   */
  getPairingUri(): string | undefined;
};

const toNumberChains = (chains: readonly string[]): number[] =>
  chains.map((chainId) => Number.parseInt(chainId, 10));

const isPairingRejection = (error: unknown): boolean => {
  const providerError = toProviderError(error);
  if (providerError.code === 4001) return true;
  return /user reject|user cancel|connection request expiry|session request expired/i.test(
    providerError.message,
  );
};

/**
 * Create the WalletConnect adapter (issue #18).
 *
 * `isAvailable()` is intentionally cheap: it checks the config gate
 * (`enabled` + `projectId`) without touching the network or opening a
 * pairing. The SDK is only imported inside `connect()`.
 */
export const createWalletConnectAdapter = (
  options: WalletConnectAdapterOptions = {},
): WalletConnectAdapter => {
  const enabled = options.enabled ?? false;
  const projectId = options.projectId;
  const chains = options.chains ?? ['1'];
  const showQrModal = options.showQrModal ?? true;

  let providerPromise: Promise<WalletConnectEthereumProviderLike> | undefined;
  let paired = false;
  let pairingUri: string | undefined;

  const loadProvider =
    options.loadProvider ??
    (async (): Promise<WalletConnectEthereumProviderLike> => {
      const module = await import('@walletconnect/ethereum-provider');
      return module.default.init({
        projectId: projectId as string,
        chains: toNumberChains(chains),
        ...(options.optionalChains
          ? { optionalChains: toNumberChains(options.optionalChains) }
          : {}),
        ...(options.rpcMap ? { rpcMap: { ...options.rpcMap } } : {}),
        ...(options.metadata
          ? {
              metadata: {
                name: options.metadata.name,
                ...(options.metadata.description
                  ? { description: options.metadata.description }
                  : {}),
                ...(options.metadata.url ? { url: options.metadata.url } : {}),
                ...(options.metadata.icons ? { icons: [...options.metadata.icons] } : {}),
              },
            }
          : {}),
        showQrModal,
        ...(options.qrModalOptions ? { qrModalOptions: { ...options.qrModalOptions } } : {}),
      });
    });

  const resetSession = (): void => {
    providerPromise = undefined;
    paired = false;
    pairingUri = undefined;
  };

  const ensureProvider = async (): Promise<WalletConnectEthereumProviderLike> => {
    if (!enabled || !projectId || projectId.length === 0) {
      throw toWeb3Error(
        new Error(
          'WalletConnect sign-in is not enabled for this deployment (missing projectId or disabled).',
        ),
        'wallet_not_found',
      );
    }
    if (!providerPromise) {
      providerPromise = loadProvider().catch((error: unknown) => {
        resetSession();
        throw toWeb3Error(
          new Error(
            `The WalletConnect SDK could not be loaded.${
              error instanceof Error ? ` ${error.message}` : ''
            }`,
          ),
          'wallet_not_found',
        );
      });
    }
    return providerPromise;
  };

  const readPairingUri = (provider: WalletConnectEthereumProviderLike): void => {
    const uri = (provider as { uri?: unknown }).uri;
    if (typeof uri === 'string' && uri.length > 0) pairingUri = uri;
  };

  const forwardSessionTermination = (
    provider: WalletConnectEthereumProviderLike,
    terminate: (error?: Eip1193ProviderError) => void,
  ): (() => void) => {
    const onSessionDelete = (...args: never[]) => {
      const error = args[0] as Eip1193ProviderError | undefined;
      resetSession();
      terminate(error);
    };
    // session_delete: the wallet (or user) killed the session.
    provider.on('session_delete', onSessionDelete);
    // session_expire: the pairing/session TTL elapsed without extension.
    provider.on('session_expire', onSessionDelete);
    return () => {
      provider.removeListener('session_delete', onSessionDelete);
      provider.removeListener('session_expire', onSessionDelete);
    };
  };

  const adapter: WalletConnectAdapter = {
    id: 'walletconnect',
    name: 'WalletConnect',

    isAvailable: async () => {
      if (!enabled || !projectId || projectId.length === 0) return false;
      // The SDK is loaded lazily on first connect; availability here means
      // "the deployment allows WalletConnect and a loader is configured".
      return typeof loadProvider === 'function';
    },

    connect: async (): Promise<WalletConnection> => {
      const provider = await ensureProvider();
      // Surface the pairing URI to hosts rendering their own QR code.
      const onDisplayUri = (...args: never[]) => {
        const uri = args[0] as string | undefined;
        if (typeof uri === 'string') pairingUri = uri;
      };
      provider.on('display_uri', onDisplayUri);
      try {
        if (!paired) {
          try {
            await provider.connect();
            paired = true;
          } catch (error) {
            resetSession();
            throw toWeb3Error(
              isPairingRejection(error)
                ? toProviderError(error)
                : new Error(
                    `WalletConnect pairing failed. ${
                      toProviderError(error).message
                    }`,
                  ),
              isPairingRejection(error) ? 'user_rejected' : 'connection_failed',
            );
          }
          readPairingUri(provider);
        }
        const accounts = (await provider.request({ method: 'eth_accounts' })) as
          | string[]
          | undefined;
        const address = Array.isArray(accounts) ? accounts[0] : undefined;
        if (typeof address !== 'string' || address.length === 0) {
          throw toWeb3Error(
            new Error('WalletConnect did not return an account.'),
            'connection_failed',
          );
        }
        const chainIdHex = (await provider.request({ method: 'eth_chainId' })) as string;
        return { address, chainId: parseChainId(chainIdHex) };
      } finally {
        provider.removeListener('display_uri', onDisplayUri);
      }
    },

    disconnect: async () => {
      if (!providerPromise) {
        resetSession();
        return;
      }
      try {
        const provider = await providerPromise;
        if (paired) {
          await provider.disconnect();
        }
      } catch {
        // Best-effort relay session delete; local state resets regardless.
      } finally {
        resetSession();
      }
    },

    signMessage: async (address: string, message: string): Promise<string> => {
      const provider = await ensureProvider();
      const signature = (await provider.request({
        method: 'personal_sign',
        params: [utf8ToHex(message), address],
      })) as string;
      if (typeof signature !== 'string' || signature.length === 0) {
        throw toWeb3Error(
          new Error('WalletConnect did not return a signature.'),
          'sign_failed',
        );
      }
      return signature;
    },

    switchChain: async (chain: ChainInfo): Promise<void> => {
      const provider = await ensureProvider();
      await requestChainSwitch(provider, chain);
    },

    onEvent: (handlers: WalletEventHandlers): (() => void) => {
      if (!providerPromise) return () => undefined;
      let unsubscribe: (() => void) | undefined;
      let cancelled = false;
      void providerPromise
        .then((provider) => {
          if (cancelled) return;
          const accountsChanged = (...args: never[]) => {
            const accounts = args[0] as string[] | undefined;
            handlers.onAccountsChanged(accounts ?? []);
          };
          const chainChanged = (...args: never[]) => {
            const chainId = args[0] as string | undefined;
            handlers.onChainChanged(parseChainId(chainId ?? '0x1'));
          };
          const disconnected = (...args: never[]) => {
            resetSession();
            handlers.onDisconnected(args[0] as Eip1193ProviderError | undefined);
          };
          provider.on('accountsChanged', accountsChanged);
          provider.on('chainChanged', chainChanged);
          provider.on('disconnect', disconnected);
          const unsubscribeSession = forwardSessionTermination(provider, (error) =>
            handlers.onDisconnected(error),
          );
          unsubscribe = () => {
            provider.removeListener('accountsChanged', accountsChanged);
            provider.removeListener('chainChanged', chainChanged);
            provider.removeListener('disconnect', disconnected);
            unsubscribeSession();
          };
        })
        .catch(() => undefined);
      return () => {
        cancelled = true;
        unsubscribe?.();
      };
    },

    getPairingUri: () => pairingUri,
  };

  return adapter;
};
