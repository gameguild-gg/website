'use client';
/**
 * Typed hooks over the Web3 context (issue #38).
 */
import { useContext } from 'react';
import { defaultWeb3ContextValue, Web3Context, Web3ContextValue } from '../lib/web3-context';
import type { WalletStatus, Web3Error } from '../types';

/** Full context access. Throws outside of a `Web3Provider`. */
export function useWeb3(): Web3ContextValue {
  const context = useContext(Web3Context);

  if (context === defaultWeb3ContextValue) throw new Error('useWeb3 must be used within a Web3Provider');

  return context;
}

/** Convenience snapshot for wallet buttons and connection UI. */
export interface WalletSnapshot {
  /** Current lifecycle status. */
  readonly status: WalletStatus;
  /** Active wallet adapter id (undefined while never connected). */
  readonly walletId: string | undefined;
  /** Active wallet display name. */
  readonly walletName: string | undefined;
  /** Last surfaced error, if any. */
  readonly error: Web3Error | undefined;
  /** True while `connecting` or `signing`. */
  readonly isPending: boolean;
  /** True once the SIWE journey completed (backend verified the signature). */
  readonly isAuthenticated: boolean;
  /** Registered wallets with availability, for rendering buttons. */
  readonly wallets: ReadonlyArray<{ id: string; name: string; available: boolean }>;
  /** Connect a wallet by id. */
  connect(walletId: string): Promise<void>;
  /** Disconnect the active wallet. */
  disconnect(): Promise<void>;
  /** Clear the surfaced error. */
  clearError(): void;
}

/**
 * Connection-focused snapshot of the wallet context. Throws outside of a
 * `Web3Provider`.
 */
export function useWallet(): WalletSnapshot {
  const { state, wallets, connect, disconnect, clearError } = useWeb3();
  return {
    status: state.status,
    walletId: state.walletId,
    walletName: state.walletName,
    error: state.error,
    isPending: state.status === 'connecting' || state.status === 'signing',
    isAuthenticated: state.status === 'authenticated',
    wallets,
    connect,
    disconnect,
    clearError,
  };
}

/** Active account address, or `undefined` while disconnected. */
export function useWalletAddress(): string | undefined {
  return useWeb3().state.account;
}

/** Active chain id as a decimal string, or `undefined` while disconnected. */
export function useChainId(): string | undefined {
  return useWeb3().state.chainId;
}
