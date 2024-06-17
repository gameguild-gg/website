import React from "react";
import {Web3Context} from "@/components/web3/web3-context";


export function useWeb3() {
  const context = React.useContext(Web3Context);

  if (context === undefined) {
    throw new Error("useWeb3 must be used within a Web3Provider");
  }

  return context;
}
