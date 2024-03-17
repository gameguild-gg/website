"use client";

import React from "react";
import { BrowserProvider } from "ethers";

type Action =
  | { type: "CONNECT_TO_PROVIDER_INITIAL" }
  | { type: "CONNECT_TO_PROVIDER_SUCCESS", payload: { provider: BrowserProvider, accountAddress: string } }
  | { type: "CONNECT_TO_PROVIDER_FAILURE", payload: { error: string } }
  | { type: "FETCH_INITIAL" }
  | { type: "FETCH_SUCCESS", payload: {} }
  | { type: "FETCH_FAILURE", payload: { error: string } };

type Context = { state: State; dispatch: Dispatch } | undefined;

type Dispatch = (action: Action) => void;

type Props = { children: React.ReactNode };

type State = {
  provider: BrowserProvider | undefined;
  accountAddress: string | undefined;
};

const Web3Context = React.createContext<Context>(undefined);

const InitialState: State = {
  provider: undefined,
  accountAddress: undefined
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
    throw new Error("useWeb3 must be used within a Web3Provider");
  }

  return context;
}

async function connectToWallet(dispatch: Dispatch) {
  dispatch({ type: "CONNECT_TO_PROVIDER_INITIAL" });

  try {
    if (window.ethereum) {
      const ethereum = window.ethereum;

      await ethereum.request({ method: "eth_requestAccounts" });

      const provider = new BrowserProvider(window.ethereum);

      const signer = await provider.getSigner();
      const accountAddress = await signer.getAddress();

      dispatch({ type: "CONNECT_TO_PROVIDER_SUCCESS", payload: { provider, accountAddress } });
    } else {
      dispatch({ type: "CONNECT_TO_PROVIDER_FAILURE", payload: { error: "MetaMask is not detected." } });
    }
  } catch (error) {
    dispatch({ type: "CONNECT_TO_PROVIDER_FAILURE", payload: { error: (error as Error).message } });
  }
}

async function requestWeb3SignInChallenge(dispatch: Dispatch) {

}

export { Web3Provider, useWeb3, connectToWallet };
