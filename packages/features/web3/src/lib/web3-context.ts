/**
 * React context for the Web3 wallet state (issue #38).
 */
import { createContext } from 'react';
import { defaultWeb3State } from './web3-reducer';
import type {
  ChainInfo,
  SiweAuthentication,
  SiweTransport,
  WalletDescriptor,
  WalletId,
  Web3State,
} from '../types';

/** Everything the Web3Provider exposes to consumers. */
export interface Web3ContextValue {
  /** Connection lifecycle state (status, account, chain, error). */
  readonly state: Web3State;
  /** Registered wallets with their current availability. */
  readonly wallets: readonly WalletDescriptor[];
  /**
   * Connect a wallet by adapter id. Resolves once the account is available,
   * or throws when the wallet is missing/unavailable or the user rejects.
   */
  connect(walletId: WalletId): Promise<void>;
  /**
   * Disconnect the active wallet: adapter-side logout (when supported),
   * event unsubscribe and full state cleanup.
   */
  disconnect(): Promise<void>;
  /**
   * Run the full SIWE journey over the active wallet connection:
   * challenge (backend) → `personal_sign` (wallet) → verify (backend).
   * Resolves with the signed challenge; rejects on any step.
   */
  authenticate(transport: SiweTransport): Promise<SiweAuthentication>;
  /** Sign an arbitrary UTF-8 message with the active account (EIP-191). */
  signMessage(message: string): Promise<string>;
  /** Ask the active wallet to switch chains (with add-chain fallback). */
  switchChain(chain: ChainInfo): Promise<void>;
  /** Clear the surfaced error without touching account/chain state. */
  clearError(): void;
}

/** Sentinel default so hooks can detect usage outside of a provider. */
export const defaultWeb3ContextValue: Web3ContextValue = {
  state: defaultWeb3State,
  wallets: [],
  connect: () => Promise.reject(new Error('Web3Context used outside of a Web3Provider')),
  disconnect: () => Promise.reject(new Error('Web3Context used outside of a Web3Provider')),
  authenticate: () => Promise.reject(new Error('Web3Context used outside of a Web3Provider')),
  signMessage: () => Promise.reject(new Error('Web3Context used outside of a Web3Provider')),
  switchChain: () => Promise.reject(new Error('Web3Context used outside of a Web3Provider')),
  clearError: () => console.error('Web3Context used outside of a Web3Provider'),
};

export const Web3Context = createContext<Web3ContextValue>(defaultWeb3ContextValue);
