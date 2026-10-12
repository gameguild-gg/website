/**
 * Core contracts for the GameGuild Web3 wallet integration.
 *
 * The wallet layer is framework-agnostic: every wallet (MetaMask, Torus, ...)
 * is reached through the EIP-1193 Ethereum Provider JavaScript API and exposed
 * to React through a single {@link WalletAdapter} contract. Nothing in this
 * file imports React or a specific wallet SDK.
 */

// ─── EIP-1193: Ethereum Provider JavaScript API ──────────────────

/** Request arguments accepted by an EIP-1193 provider's `request()`. */
export interface Eip1193RequestArguments {
  /** RPC method, e.g. `eth_requestAccounts`. */
  readonly method: string;
  /** Method parameters. */
  readonly params?: readonly unknown[] | object;
}

/** Error shape emitted by EIP-1193 providers (code + message). */
export interface Eip1193ProviderError extends Error {
  /** Standard provider error code (4001 = user rejected, 4902 = chain not added, ...). */
  readonly code?: number;
  /** Optional structured `data` payload. */
  readonly data?: unknown;
}

/** Event listeners an EIP-1193 provider must support for wallet lifecycle. */
export type Eip1193EventMap = {
  accountsChanged: (accounts: string[]) => void;
  chainChanged: (chainId: string) => void;
  connect: (info: { chainId: string }) => void;
  disconnect: (error: Eip1193ProviderError) => void;
};

/** Minimal EIP-1193 provider surface used by the wallet adapters. */
export interface Eip1193Provider {
  request(args: Eip1193RequestArguments): Promise<unknown>;
  on<K extends keyof Eip1193EventMap>(event: K, listener: Eip1193EventMap[K]): void;
  removeListener<K extends keyof Eip1193EventMap>(
    event: K,
    listener: Eip1193EventMap[K],
  ): void;
}

/** EIP-1193 standard error code: the user rejected the request. */
export const USER_REJECTED_REQUEST_CODE = 4001;
/** EIP-1193 / MetaMask error code: the requested chain has not been added. */
export const UNRECOGNIZED_CHAIN_CODE = 4902;
/** EIP-1474 server error code sometimes wrapping `wallet_switchEthereumChain` failures. */
export const INTERNAL_ERROR_CODE = -32603;

// ─── Chains ───────────────────────────────────────────────────────

/** A chain the application can ask a wallet to switch to. */
export interface ChainInfo {
  /** Decimal chain id as a string, e.g. `"1"` for Ethereum mainnet. */
  readonly id: string;
  /** Human-readable chain name shown in wallet prompts. */
  readonly name: string;
  /** RPC endpoints used when the chain must be added to the wallet. */
  readonly rpcUrls: readonly string[];
  /** Native currency descriptor used when the chain must be added. */
  readonly nativeCurrency?: {
    readonly name: string;
    readonly symbol: string;
    readonly decimals: number;
  };
  /** Optional block explorer URLs. */
  readonly blockExplorerUrls?: readonly string[];
}

/** Well-known chains used as `wallet_addEthereumChain` fallbacks. */
export const COMMON_CHAINS: Record<string, ChainInfo> = {
  '1': {
    id: '1',
    name: 'Ethereum Mainnet',
    rpcUrls: ['https://eth.llamarpc.com'],
    nativeCurrency: { name: 'Ether', symbol: 'ETH', decimals: 18 },
    blockExplorerUrls: ['https://etherscan.io'],
  },
  '11155111': {
    id: '11155111',
    name: 'Sepolia Testnet',
    rpcUrls: ['https://rpc.sepolia.org'],
    nativeCurrency: { name: 'Sepolia Ether', symbol: 'ETH', decimals: 18 },
    blockExplorerUrls: ['https://sepolia.etherscan.io'],
  },
};

// ─── Wallet adapters (common contract, issue #38) ─────────────────

/** Identifier of a wallet adapter, e.g. `metamask` or `torus`. */
export type WalletId = string;

/** Account + network snapshot returned by a successful connection. */
export interface WalletConnection {
  /** 20-byte address, `0x`-prefixed. */
  readonly address: string;
  /** Decimal chain id string, e.g. `"1"`. */
  readonly chainId: string;
}

/** Wallet lifecycle events forwarded by an adapter. */
export interface WalletEventHandlers {
  /** Accounts changed; an empty array means every account was disconnected. */
  onAccountsChanged(accounts: readonly string[]): void;
  /** Active chain changed; `chainId` is a decimal string. */
  onChainChanged(chainId: string): void;
  /** The wallet dropped the connection (extension removed, Torus logged out, ...). */
  onDisconnected(error?: Eip1193ProviderError): void;
}

/**
 * The common wallet contract implemented by every provider integration
 * (MetaMask — issue #39, Torus — issue #16). Framework-agnostic: the React
 * `Web3Provider` consumes adapters, adapters never import React.
 */
export interface WalletAdapter {
  /** Stable adapter id, e.g. `metamask`. */
  readonly id: WalletId;
  /** Human-readable wallet name for buttons and labels. */
  readonly name: string;
  /** True when the wallet is detectable/initializable in this environment. */
  isAvailable(): Promise<boolean>;
  /**
   * Request connection (may prompt the user). Resolves with the primary
   * account and active chain, or rejects with an EIP-1193-style error.
   */
  connect(): Promise<WalletConnection>;
  /**
   * Best-effort wallet-side disconnect; always releases the adapter's
   * provider reference and event subscriptions.
   */
  disconnect(): Promise<void>;
  /**
   * Sign an arbitrary UTF-8 message via EIP-191 `personal_sign` and return
   * the `0x`-prefixed 65-byte signature.
   */
  signMessage(address: string, message: string): Promise<string>;
  /**
   * Ask the wallet to switch chains, adding the chain first when the wallet
   * does not know it yet (`wallet_switchEthereumChain` + `wallet_addEthereumChain`).
   */
  switchChain(chain: ChainInfo): Promise<void>;
  /**
   * Subscribe to wallet events while connected. Returns an unsubscribe
   * function; adapters must tolerate subscription before/after connect.
   */
  onEvent(handlers: WalletEventHandlers): () => void;
}

/** Serializable adapter descriptor exposed to UI (no provider references). */
export interface WalletDescriptor {
  readonly id: WalletId;
  readonly name: string;
  readonly available: boolean;
}

// ─── State machine (issue #38) ────────────────────────────────────

/**
 * Connection lifecycle. `signing` covers any user-prompted signing during
 * the SIWE journey; `authenticated` is reached once the backend verified
 * the signature and issued a session.
 */
export type WalletStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'signing'
  | 'authenticated'
  | 'error';

/** Stable error taxonomy surfaced through the context. */
export type Web3ErrorCode =
  | 'wallet_not_found'
  | 'user_rejected'
  | 'unauthorized'
  | 'connection_failed'
  | 'chain_switch_failed'
  | 'sign_failed'
  | 'challenge_failed'
  | 'verification_failed'
  | 'disconnected'
  | 'unknown';

/** Structured error carried in `Web3State`. */
export interface Web3Error {
  readonly code: Web3ErrorCode;
  readonly message: string;
}

/** Complete wallet state exposed by the React context. */
export interface Web3State {
  /** Current lifecycle status. */
  readonly status: WalletStatus;
  /** Adapter id of the active wallet, once a connect attempt started. */
  readonly walletId: WalletId | undefined;
  /** Display name of the active wallet. */
  readonly walletName: string | undefined;
  /** Active account address (`0x...`). */
  readonly account: string | undefined;
  /** Active chain id (decimal string, e.g. `"1"`). */
  readonly chainId: string | undefined;
  /** Last error; cleared by `clearError()` and on successful transitions. */
  readonly error: Web3Error | undefined;
}

// ─── SIWE (Sign-In with Ethereum, issues #16/#39) ─────────────────

/** Challenge request payload accepted by `POST /api/v1/auth/web3/challenge`. */
export interface SiweChallengeRequest {
  readonly walletAddress: string;
  readonly chainId: string;
}

/** Challenge payload returned by the backend. */
export interface SiweChallenge {
  /** Full EIP-4361 message the wallet must sign. */
  readonly challenge: string;
  /** Nonce bound to the challenge. */
  readonly nonce: string;
  /** ISO timestamp after which the challenge expires. */
  readonly expiresAt?: string;
}

/** Verification payload for `POST /api/v1/auth/web3:verify`. */
export interface SiweVerifyRequest {
  readonly walletAddress: string;
  readonly chainId: string;
  readonly challenge: string;
  readonly nonce: string;
  readonly signature: string;
}

/** Everything needed to prove the completed SIWE journey. */
export interface SiweAuthentication {
  readonly walletAddress: string;
  readonly chainId: string;
  readonly challenge: string;
  readonly nonce: string;
  readonly signature: string;
}

/**
 * Transport used by the shared SIWE flow. The web3 package stays
 * framework-agnostic: the application supplies how the challenge is fetched
 * and how the signature is verified against the GameGuild backend.
 */
export interface SiweTransport {
  /** Fetch a challenge for a wallet on a chain. Throws on failure. */
  getChallenge(request: SiweChallengeRequest): Promise<SiweChallenge>;
  /**
   * Submit the signed challenge for verification. Must throw when the
   * backend rejects the signature — resolving means a session was issued.
   */
  verify(request: SiweVerifyRequest): Promise<void>;
}

/** Convert an unknown thrown value into a normalized provider-style error. */
export function toProviderError(error: unknown): Eip1193ProviderError {
  if (error instanceof Error) {
    return error as Eip1193ProviderError;
  }
  if (typeof error === 'object' && error !== null && 'message' in error) {
    const shaped = error as { message?: unknown; code?: unknown };
    const wrapped = new Error(
      typeof shaped.message === 'string' ? shaped.message : 'Unknown wallet error',
    ) as Eip1193ProviderError;
    if (typeof shaped.code === 'number') wrapped.code = shaped.code;
    return wrapped;
  }
  return new Error(typeof error === 'string' ? error : 'Unknown wallet error');
}

/** True when the error represents a user-initiated rejection (EIP-1193 4001). */
export function isUserRejectedError(error: unknown): boolean {
  return toProviderError(error).code === USER_REJECTED_REQUEST_CODE;
}

/** Map a thrown value to the public error taxonomy. */
export function toWeb3Error(
  error: unknown,
  fallbackCode: Web3ErrorCode = 'unknown',
): Web3Error {
  const providerError = toProviderError(error);
  let code: Web3ErrorCode = fallbackCode;
  if (providerError.code === USER_REJECTED_REQUEST_CODE) code = 'user_rejected';
  else if (providerError.code === 4100) code = 'unauthorized';
  return { code, message: providerError.message };
}
