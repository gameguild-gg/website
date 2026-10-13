/**
 * Web3 wallet connection state machine (issue #38).
 *
 * Pure reducer: no side effects, no provider access. The React provider
 * dispatches these actions from adapter lifecycle callbacks.
 */
import type { WalletId, Web3Error, Web3State } from '../types';

export const Web3ActionType = {
  CONNECT_START: 'CONNECT_START',
  CONNECT_SUCCESS: 'CONNECT_SUCCESS',
  CONNECT_FAILURE: 'CONNECT_FAILURE',
  AUTH_SIGNING: 'AUTH_SIGNING',
  AUTH_SUCCESS: 'AUTH_SUCCESS',
  AUTH_FAILURE: 'AUTH_FAILURE',
  ACCOUNTS_CHANGED: 'ACCOUNTS_CHANGED',
  CHAIN_CHANGED: 'CHAIN_CHANGED',
  WALLET_DISCONNECTED: 'WALLET_DISCONNECTED',
  CLEAR_ERROR: 'CLEAR_ERROR',
  RESET_STATE: 'RESET_STATE',
} as const;

export type Web3ActionType = (typeof Web3ActionType)[keyof typeof Web3ActionType];

export type Web3Action =
  | { type: typeof Web3ActionType.CONNECT_START; walletId: WalletId; walletName: string }
  | {
      type: typeof Web3ActionType.CONNECT_SUCCESS;
      account: string;
      chainId: string;
    }
  | { type: typeof Web3ActionType.CONNECT_FAILURE; error: Web3Error }
  | { type: typeof Web3ActionType.AUTH_SIGNING }
  | { type: typeof Web3ActionType.AUTH_SUCCESS }
  | { type: typeof Web3ActionType.AUTH_FAILURE; error: Web3Error }
  | { type: typeof Web3ActionType.ACCOUNTS_CHANGED; account: string }
  | { type: typeof Web3ActionType.CHAIN_CHANGED; chainId: string }
  | { type: typeof Web3ActionType.WALLET_DISCONNECTED; error?: Web3Error }
  | { type: typeof Web3ActionType.CLEAR_ERROR }
  | { type: typeof Web3ActionType.RESET_STATE };

export type Web3Reducer = (state: Web3State, action: Web3Action) => Web3State;

export const defaultWeb3State: Web3State = {
  status: 'disconnected',
  walletId: undefined,
  walletName: undefined,
  account: undefined,
  chainId: undefined,
  error: undefined,
};

export const web3Reducer: Web3Reducer = (state: Web3State, action: Web3Action): Web3State => {
  switch (action.type) {
    case Web3ActionType.CONNECT_START: {
      return {
        ...state,
        status: 'connecting',
        walletId: action.walletId,
        walletName: action.walletName,
        error: undefined,
      };
    }

    case Web3ActionType.CONNECT_SUCCESS: {
      return {
        ...state,
        status: 'connected',
        account: action.account,
        chainId: action.chainId,
        error: undefined,
      };
    }

    case Web3ActionType.CONNECT_FAILURE: {
      return {
        ...state,
        status: 'error',
        account: undefined,
        chainId: undefined,
        error: action.error,
      };
    }

    case Web3ActionType.AUTH_SIGNING: {
      // The wallet stays connected while the SIWE message is being signed.
      return { ...state, status: 'signing', error: undefined };
    }

    case Web3ActionType.AUTH_SUCCESS: {
      return { ...state, status: 'authenticated', error: undefined };
    }

    case Web3ActionType.AUTH_FAILURE: {
      // Signing/verification failed, but the wallet connection itself is
      // intact — return to `connected` (or `error` if never fully connected).
      const status = state.account === undefined ? 'error' : 'connected';
      return { ...state, status, error: action.error };
    }

    case Web3ActionType.ACCOUNTS_CHANGED: {
      // A different account invalidates any prior authentication.
      return {
        ...state,
        account: action.account,
        status: 'connected',
        error: undefined,
      };
    }

    case Web3ActionType.CHAIN_CHANGED: {
      return { ...state, chainId: action.chainId };
    }

    case Web3ActionType.WALLET_DISCONNECTED: {
      return {
        ...defaultWeb3State,
        error: action.error,
      };
    }

    case Web3ActionType.CLEAR_ERROR: {
      if (state.error === undefined) return state;
      // Keep the error status honest: clearing an error returns a terminal
      // state (disconnected/connected) instead of erasing the history.
      const status: Web3State['status'] = state.account === undefined ? 'disconnected' : 'connected';
      return { ...state, status, error: undefined };
    }

    case Web3ActionType.RESET_STATE: {
      return { ...defaultWeb3State };
    }

    default: {
      console.warn(`Unhandled Web3 action type: ${(action as { type: string }).type}`);
      return state;
    }
  }
};
