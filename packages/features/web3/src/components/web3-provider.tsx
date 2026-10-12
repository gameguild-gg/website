'use client';
/**
 * Web3Provider — React context wiring for the wallet connection state
 * machine (issue #38).
 *
 * The provider owns:
 * - adapter registration + availability probing (`wallets`),
 * - the connect/disconnect lifecycle with full state cleanup,
 * - EIP-1193 event forwarding (`accountsChanged`/`chainChanged`/`disconnect`),
 * - the shared SIWE journey (challenge → personal_sign → verify, #16/#39).
 *
 * The provider is UI-free: applications render their own buttons from
 * `wallets` and `state` via the `useWallet`/`useWalletAddress`/`useChainId`
 * hooks. It must run in the browser ( wallets inject into `window`);
 * server rendering renders children with the default disconnected state.
 */
import React, { PropsWithChildren, useCallback, useEffect, useMemo, useReducer, useRef } from 'react';
import { Web3Context, Web3ContextValue } from '../lib/web3-context';
import {
  Web3ActionType,
  defaultWeb3State,
  web3Reducer,
} from '../lib/web3-reducer';
import { authenticateWithSiwe } from '../lib/siwe';
import { toWeb3Error } from '../types';
import type {
  ChainInfo,
  SiweAuthentication,
  SiweTransport,
  WalletAdapter,
  WalletDescriptor,
  WalletId,
  Web3Error,
} from '../types';

export interface Web3ProviderProps {
  /** Wallet adapters to register (MetaMask, Torus, ...). */
  readonly adapters: readonly WalletAdapter[];
}

const notConnectedError = (): Web3Error => ({
  code: 'unauthorized',
  message: 'Connect a wallet before performing this operation.',
});

export const Web3Provider = ({
  adapters,
  children,
}: Readonly<PropsWithChildren<Web3ProviderProps>>): React.JSX.Element => {
  const [state, dispatch] = useReducer(web3Reducer, defaultWeb3State);

  const adaptersRef = useRef(new Map<WalletId, WalletAdapter>());
  adaptersRef.current = new Map(adapters.map((adapter) => [adapter.id, adapter] as const));

  const activeAdapterRef = useRef<WalletAdapter | undefined>(undefined);
  const unsubscribeRef = useRef<(() => void) | undefined>(undefined);
  /**
   * Latest known account/chain, mirrored outside React state so back-to-back
   * async calls (`await connect(); await authenticate()`) never read a
   * pre-render snapshot.
   */
  const connectionRef = useRef<{ account: string; chainId: string } | undefined>(undefined);

  // Probe availability for every registered wallet once in the browser.
  const [wallets, setWallets] = React.useState<readonly WalletDescriptor[]>(() =>
    adapters.map((adapter) => ({ id: adapter.id, name: adapter.name, available: false })),
  );
  useEffect(() => {
    let cancelled = false;
    void (async () => {
      const descriptors = await Promise.all(
        Array.from(adaptersRef.current.values()).map(
          async (adapter): Promise<WalletDescriptor> => {
            let available = false;
            try {
              available = await adapter.isAvailable();
            } catch {
              available = false;
            }
            return { id: adapter.id, name: adapter.name, available };
          }),
      );
      if (!cancelled) setWallets(descriptors);
    })();
    return () => {
      cancelled = true;
    };
  }, [adapters]);

  const releaseActiveWallet = useCallback(() => {
    unsubscribeRef.current?.();
    unsubscribeRef.current = undefined;
    activeAdapterRef.current = undefined;
    connectionRef.current = undefined;
  }, []);

  const connect = useCallback(async (walletId: WalletId): Promise<void> => {
    // A previous wallet may be connected — release it before switching.
    releaseActiveWallet();
    const adapter = adaptersRef.current.get(walletId);
    if (!adapter) {
      const error = toWeb3Error(new Error(`No wallet registered as "${walletId}".`), 'wallet_not_found');
      dispatch({ type: Web3ActionType.CONNECT_FAILURE, error });
      throw error;
    }
    dispatch({ type: Web3ActionType.CONNECT_START, walletId: adapter.id, walletName: adapter.name });
    try {
      const connection = await adapter.connect();
      activeAdapterRef.current = adapter;
      connectionRef.current = { account: connection.address, chainId: connection.chainId };
      unsubscribeRef.current = adapter.onEvent({
        onAccountsChanged: (accounts) => {
          if (accounts.length === 0) {
            releaseActiveWallet();
            dispatch({
              type: Web3ActionType.WALLET_DISCONNECTED,
              error: { code: 'disconnected', message: 'The wallet disconnected all accounts.' },
            });
            return;
          }
          const next = accounts[0] as string;
          if (connectionRef.current) {
            connectionRef.current = { ...connectionRef.current, account: next };
          }
          dispatch({ type: Web3ActionType.ACCOUNTS_CHANGED, account: next });
        },
        onChainChanged: (chainId) => {
          if (connectionRef.current) {
            connectionRef.current = { ...connectionRef.current, chainId };
          }
          dispatch({ type: Web3ActionType.CHAIN_CHANGED, chainId });
        },
        onDisconnected: (providerError) => {
          releaseActiveWallet();
          dispatch({
            type: Web3ActionType.WALLET_DISCONNECTED,
            error: {
              code: 'disconnected',
              message: providerError?.message ?? 'The wallet disconnected.',
            },
          });
        },
      });
      dispatch({
        type: Web3ActionType.CONNECT_SUCCESS,
        account: connection.address,
        chainId: connection.chainId,
      });
    } catch (error) {
      const web3Error = toWeb3Error(error, 'connection_failed');
      dispatch({ type: Web3ActionType.CONNECT_FAILURE, error: web3Error });
      throw web3Error;
    }
  }, [releaseActiveWallet]);

  const disconnect = useCallback(async (): Promise<void> => {
    const adapter = activeAdapterRef.current;
    releaseActiveWallet();
    try {
      await adapter?.disconnect();
    } catch {
      // Best-effort wallet-side logout; local state resets regardless.
    }
    dispatch({ type: Web3ActionType.WALLET_DISCONNECTED });
  }, [releaseActiveWallet]);

  const requireActiveWallet = useCallback((): { adapter: WalletAdapter; account: string; chainId: string } => {
    const adapter = activeAdapterRef.current;
    const connection = connectionRef.current;
    if (!adapter || !connection) {
      throw notConnectedError();
    }
    return { adapter, account: connection.account, chainId: connection.chainId };
  }, []);

  const authenticate = useCallback(
    async (transport: SiweTransport): Promise<SiweAuthentication> => {
      try {
        const { adapter, account, chainId } = requireActiveWallet();
        dispatch({ type: Web3ActionType.AUTH_SIGNING });
        const authentication = await authenticateWithSiwe(
          adapter,
          { address: account, chainId },
          transport,
        );
        dispatch({ type: Web3ActionType.AUTH_SUCCESS });
        return authentication;
      } catch (error) {
        // Not-connected attempts surface the unauthorized taxonomy; every later
        // failure is a verification failure of the SIWE round trip.
        const web3Error =
          error !== null && typeof error === 'object' && (error as Web3Error).code === 'unauthorized'
            ? (error as Web3Error)
            : toWeb3Error(error, 'verification_failed');
        dispatch({ type: Web3ActionType.AUTH_FAILURE, error: web3Error });
        throw web3Error;
      }
    },
    [requireActiveWallet],
  );

  const signMessage = useCallback(
    async (message: string): Promise<string> => {
      const { adapter, account } = requireActiveWallet();
      try {
        return await adapter.signMessage(account, message);
      } catch (error) {
        throw toWeb3Error(error, 'sign_failed');
      }
    },
    [requireActiveWallet],
  );

  const switchChain = useCallback(
    async (chain: ChainInfo): Promise<void> => {
      const { adapter } = requireActiveWallet();
      try {
        await adapter.switchChain(chain);
        dispatch({ type: Web3ActionType.CHAIN_CHANGED, chainId: chain.id });
      } catch (error) {
        throw toWeb3Error(error, 'chain_switch_failed');
      }
    },
    [requireActiveWallet],
  );

  const clearError = useCallback(() => {
    dispatch({ type: Web3ActionType.CLEAR_ERROR });
  }, []);

  // Release event subscriptions on unmount (wallets stay connected; the
  // provider component itself is being discarded).
  useEffect(() => releaseActiveWallet, [releaseActiveWallet]);

  const value = useMemo<Web3ContextValue>(
    () => ({
      state,
      wallets,
      connect,
      disconnect,
      authenticate,
      signMessage,
      switchChain,
      clearError,
    }),
    [state, wallets, connect, disconnect, authenticate, signMessage, switchChain, clearError],
  );

  return <Web3Context.Provider value={value}>{children}</Web3Context.Provider>;
};
