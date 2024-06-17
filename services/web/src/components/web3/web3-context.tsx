"use client";

import React from "react";
import {BrowserProvider} from "ethers";


type Action =
  | { type: "CONNECT_TO_PROVIDER_INITIAL" }
  | { type: "CONNECT_TO_PROVIDER_SUCCESS", payload: { provider: BrowserProvider, accountAddress: string } }
  | { type: "CONNECT_TO_PROVIDER_FAILURE", payload: { error: string } }
  | { type: "FETCH_INITIAL" }
  | { type: "FETCH_SUCCESS", payload: {} }
  | { type: "FETCH_FAILURE", payload: { error: string } };

type Web3ContextData = { state: State; dispatch: Dispatch } | undefined;

type Dispatch = (action: Action) => void;

type Props = { children: React.ReactNode };

type State = {
  provider: BrowserProvider | undefined;
  accountAddress: string | undefined;
};

function web3Reducer(state: State, action: Action) {
  switch (action.type) {
    case "CONNECT_TO_PROVIDER_INITIAL": {
      return {
        ...state
      };
    }
    case "CONNECT_TO_PROVIDER_SUCCESS": {
      return {
        ...state,
        provider: action.payload.provider,
        accountAddress: action.payload.accountAddress
      };
    }
    case "CONNECT_TO_PROVIDER_FAILURE": {
      return {
        ...state,
        error: action.payload.error
      };
    }
    default: {
      // throw new Error(`Unhandled action type: ${action.type}`);
      return state;
    }
  }
}

export const Web3Context = React.createContext<Web3ContextData>(undefined);

const InitialState: State = {
  provider: undefined,
  accountAddress: undefined
};

export function Web3Provider({children}: Readonly<Props>) {
  const [state, dispatch] = React.useReducer(web3Reducer, InitialState);

  React.useEffect(() => {
  }, []);

  const value = {state, dispatch};

  return (
    <Web3Context.Provider value={value}>
      {children}
    </Web3Context.Provider>
  );
}
