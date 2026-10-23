/**
 * Ambient declaration for the lazily-imported `@walletconnect/ethereum-provider`
 * SDK (WalletConnect v2 protocol).
 *
 * The web3 package must type-check WITHOUT the SDK installed: current SDK
 * releases ship under the proprietary WalletConnect/Reown Community License
 * (not OSI-approved), so the repository does not vendor them. A deployment
 * that has accepted that license installs the package and enables the
 * adapter; the adapter loads it through a lazy dynamic `import()` and fails
 * closed when it is absent. Only the surface the adapter touches is
 * declared; see `WalletConnectAdapterOptions.loadProvider` to inject an
 * alternative implementation.
 */
import type { Eip1193RequestArguments } from './index';

declare module '@walletconnect/ethereum-provider' {
  /** Minimal subset of `EthereumProvider.init` options used by the adapter. */
  export interface EthereumProviderInitOptions {
    /** WalletConnect Cloud project id (public identifier, not a secret). */
    projectId: string;
    /** Required EVM chains as numeric chain ids. */
    chains?: number[];
    /** Additional chains the session may switch to. */
    optionalChains?: number[];
    /** RPC urls per chain id for wallets without their own rpc. */
    rpcMap?: Record<string, string>;
    /** dapp metadata shown in the pairing wallet. */
    metadata?: {
      name?: string;
      description?: string;
      url?: string;
      icons?: string[];
    };
    /** Whether the built-in QR pairing modal is rendered (default true). */
    showQrModal?: boolean;
    /** Overrides for the built-in pairing modal. */
    qrModalOptions?: Record<string, unknown>;
  }

  /**
   * WalletConnect v2 Ethereum provider: an EIP-1193 bridge over the relay
   * session. `connect()` runs the pairing flow (QR modal / deep link);
   * `disconnect()` deletes the session; lifecycle arrives as the standard
   * EIP-1193 events plus `display_uri` (pairing URI) and session-level
   * `session_delete`/`session_expire` notifications.
   */
  export default class EthereumProvider {
    static init(options: EthereumProviderInitOptions): Promise<EthereumProvider>;
    request(args: Eip1193RequestArguments): Promise<unknown>;
    on(event: string, listener: (...args: never[]) => void): void;
    removeListener(event: string, listener: (...args: never[]) => void): void;
    connect(options?: { pairingTopic?: string }): Promise<void>;
    disconnect(): Promise<void>;
    readonly accounts: string[];
    readonly chainId: string;
    /** Pairing URI while a pairing is pending (also emitted as display_uri). */
    readonly uri: string | undefined;
  }
}
