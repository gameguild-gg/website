'use client';
import React, { PropsWithChildren, useCallback, useReducer } from 'react';
import { Web3Context, Web3ContextValue }                     from '../lib/web3-context';
import { defaultWeb3State, web3Reducer }                                       from '../lib/web3-reducer';
import { Web3Config, NetworkInfo }                           from '../types';


interface Web3ProviderProps {
  config?: Partial<Web3Config>;
  // initialState?: Partial<Web3State>;
  // supportedNetworks?: NetworkInfo[];
}

export const Web3Provider = ({
  children,
  // config = {},
  // initialState = defaultWeb3State,
  // supportedNetworks = defaultSupportedNetworks,
}: Readonly<PropsWithChildren<Web3ProviderProps>>): React.JSX.Element => {
  const [state, dispatch] = useReducer(web3Reducer, defaultWeb3State);

  const connect = useCallback(async (): Promise<void> => {
    //
  }, [state.supportedNetworks]);

  const disconnect = useCallback(async (): Promise<void> => {
    //
  }, []);

  const reconnect = useCallback(async (): Promise<void> => {
    //
  }, [connect, state.config.reconnectAttempts, state.config.reconnectDelay]);

  const isNetworkSupported = useCallback( async (chainId: string): Promise<boolean> => {
    //
  },[state.supportedNetworks]);
  
  const getNetwork = useCallback(    async (chainId: string): Promise<NetworkInfo | undefined> => {
    //
  },[state.supportedNetworks]);
  
  const addNetwork = useCallback(async (network: NetworkInfo): Promise<void> => {
    //
  },[]);

  const switchNetwork = useCallback(    async (chainId: string): Promise<void> => {
    //
  },[state.isConnected, state.supportedNetworks]);
  
  const reset = useCallback(async (): Promise<void> => {
    //
  }, []);
  
  const value: Web3ContextValue = {
    //
    state,
    dispatch,
    //
    connect,
    disconnect,
    reconnect,
    //
    isNetworkSupported,
    getNetwork,
    addNetwork,
    switchNetwork,
    //
    reset,
  };

  return (
    <Web3Context.Provider value={value}>
      {/* Todo: Add Web3Provider here */}
      {children}
    </Web3Context.Provider>
  );
};
