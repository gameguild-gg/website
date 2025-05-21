'use client';

import React, { createContext, useCallback, useContext, useEffect, useReducer } from 'react';
import { BrowserProvider } from 'ethers';

export const Web3ActionTypes = {
  CHECK_PROVIDER: 'CHECK_PROVIDER',
  PROVIDER_AVAILABLE: 'PROVIDER_AVAILABLE',  
  PROVIDER_UNAVAILABLE: 'PROVIDER_UNAVAILABLE',
  CONNECT_START: 'CONNECT_START',
  CONNECT_SUCCESS: 'CONNECT_SUCCESS',
  CONNECT_FAILURE: 'CONNECT_FAILURE',
  ACCOUNT_CHANGED: 'ACCOUNT_CHANGED',
  DISCONNECT: 'DISCONNECT',
} as const;

type Web3Action =
  | { type: typeof Web3ActionTypes.CHECK_PROVIDER }
  | { type: typeof Web3ActionTypes.PROVIDER_AVAILABLE }
  | { type: typeof Web3ActionTypes.PROVIDER_UNAVAILABLE; payload: { error: string } }
  | { type: typeof Web3ActionTypes.CONNECT_START }
  | { type: typeof Web3ActionTypes.CONNECT_SUCCESS; payload: { provider: BrowserProvider; accountAddress: string } }
  | { type: typeof Web3ActionTypes.CONNECT_FAILURE; payload: { error: string } }
  | { type: typeof Web3ActionTypes.ACCOUNT_CHANGED; payload: { accountAddress: string } }
  | { type: typeof Web3ActionTypes.DISCONNECT };

type Web3State = {
  provider: BrowserProvider | undefined;
  accountAddress: string | undefined;
  isConnecting: boolean;
  isProviderAvailable: boolean;
  isProviderChecked: boolean;
  error: string | undefined;
};

type Web3ContextType = {
  state: Web3State;
  connect: () => Promise<void>;
  disconnect: () => void;
};

type Web3ProviderProps = {
  children: React.ReactNode;
};

const initialState: Web3State = {
  provider: undefined,
  accountAddress: undefined,
  isConnecting: false,
  isProviderAvailable: false,
  isProviderChecked: false,
  error: undefined,
};

function web3Reducer(state: Web3State, action: Web3Action): Web3State {
  switch (action.type) {
    case Web3ActionTypes.CHECK_PROVIDER:
      return {
        ...state,
        isProviderChecked: false,
      };
    case Web3ActionTypes.PROVIDER_AVAILABLE:
      return {
        ...state,
        isProviderAvailable: true,
        isProviderChecked: true,
        error: undefined,
      };
    case Web3ActionTypes.PROVIDER_UNAVAILABLE:
      return {
        ...state,
        isProviderAvailable: false,
        isProviderChecked: true,
        error: action.payload.error,
      };
    case Web3ActionTypes.CONNECT_START:
      return {
        ...state,
        isConnecting: true,
        error: undefined,
      };
    case Web3ActionTypes.CONNECT_SUCCESS:
      return {
        ...state,
        isConnecting: false,
        provider: action.payload.provider,
        accountAddress: action.payload.accountAddress,
        error: undefined,
      };
    case Web3ActionTypes.CONNECT_FAILURE:
      return {
        ...state,
        isConnecting: false,
        error: action.payload.error,
      };
    case Web3ActionTypes.ACCOUNT_CHANGED:
      return {
        ...state,
        accountAddress: action.payload.accountAddress,
      };
    case Web3ActionTypes.DISCONNECT:
      return {
        ...state,
        provider: undefined,
        accountAddress: undefined,
        isConnecting: false,
        error: undefined,
      };
    default:
      return state;
  }
}

export const Web3Context = createContext<Web3ContextType | undefined>(undefined);

export function Web3Provider({ children }: Web3ProviderProps) {
  const [state, dispatch] = useReducer(web3Reducer, initialState);

  // Check if provider is available without connecting
  useEffect(() => {
    dispatch({ type: Web3ActionTypes.CHECK_PROVIDER });
    
    if (window.ethereum) {
      dispatch({ type: Web3ActionTypes.PROVIDER_AVAILABLE });
      
      // Set up account change listener
      const handleAccountsChanged = (accounts: string[]) => {
        if (accounts.length === 0) {
          // User disconnected
          dispatch({ type: Web3ActionTypes.DISCONNECT });
        } else if (state.accountAddress && accounts[0] !== state.accountAddress) {
          // Only trigger if already connected and account changed
          dispatch({
            type: Web3ActionTypes.ACCOUNT_CHANGED,
            payload: { accountAddress: accounts[0] },
          });
        }
      };
      
      window.ethereum.on('accountsChanged', handleAccountsChanged);
      
      return () => {
        window.ethereum.removeListener('accountsChanged', handleAccountsChanged);
      };
    } else {
      dispatch({ 
        type: Web3ActionTypes.PROVIDER_UNAVAILABLE,
        payload: { error: 'No Ethereum provider found. Please install MetaMask.' }
      });
    }
  }, []);

  const connect = useCallback(async () => {
    if (!window.ethereum) {
      dispatch({
        type: Web3ActionTypes.CONNECT_FAILURE,
        payload: { error: 'No Ethereum provider found. Please install MetaMask.' },
      });
      return;
    }

    try {
      dispatch({ type: Web3ActionTypes.CONNECT_START });

      const accounts = await window.ethereum.request({ method: 'eth_requestAccounts' });
      
      const provider = new BrowserProvider(window.ethereum);

      dispatch({
        type: Web3ActionTypes.CONNECT_SUCCESS,
        payload: {
          provider,
          accountAddress: accounts[0],
        },
      });
    } catch (error) {
      dispatch({
        type: Web3ActionTypes.CONNECT_FAILURE,
        payload: { error: error instanceof Error ? error.message : 'Failed to connect' },
      });
    }
  }, []);

  const disconnect = useCallback(() => {
    dispatch({ type: Web3ActionTypes.DISCONNECT });
  }, []);

  const contextValue: Web3ContextType = {
    state,
    connect,
    disconnect,
  };

  return <Web3Context.Provider value={contextValue}>{children}</Web3Context.Provider>;
}

// Add TypeScript declaration for window.ethereum
declare global {
  interface Window {
    ethereum?: {
      request: (args: { method: string; params?: any[] }) => Promise<any>;
      on: (event: string, listener: any) => void;
      removeListener: (event: string, listener: any) => void;
    };
  }
}
