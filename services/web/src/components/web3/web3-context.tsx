'use client';

import React from 'react';

type Action =
  | { type: 'FETCH_INITIAL' }
  | { type: 'FETCH_SUCCESS', payload: {} }
  | { type: 'FETCH_FAILURE', payload: { error: string } };

type Context = { state: State; dispatch: Dispatch } | undefined;

type Dispatch = (action: Action) => void;

type Props = { children: React.ReactNode };

type State = {};

const Web3Context = React.createContext<Context>(undefined);

const InitialState: State = {};

function web3Reducer(state: State, action: Action) {
  switch (action.type) {
    case 'FETCH_INITIAL': {
      return {
        ...state,
      };
    }
    case 'FETCH_SUCCESS': {
      return {
        ...state,
      };
    }
    case 'FETCH_FAILURE': {
      return {
        ...state,
        error: action.payload.error,
      };
    }
    default: {
      // throw new Error(`Unhandled action type: ${action.type}`);
      return state;
    }
  }
}

function Web3Provider({ children }: Readonly<Props>) {
  const [state, dispatch] = React.useReducer(web3Reducer, InitialState);

  React.useEffect(() => {
  }, []);

  const value = { state, dispatch };

  return (
    <Web3Context.Provider value={value}>
      {children}
    </Web3Context.Provider>
  );
}

function useWeb3() {
  const context = React.useContext(Web3Context);

  if (context === undefined) {
    throw new Error('useWeb3 must be used within a Web3Provider');
  }

  return context;
}

export { Web3Context, useWeb3 };
