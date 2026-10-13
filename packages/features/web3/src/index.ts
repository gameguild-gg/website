/**
 * @game-guild/web3 — wallet connection context and adapters for GameGuild.
 *
 * React entry point: `Web3Provider` + `useWallet`/`useWalletAddress`/`useChainId`.
 * Adapters: MetaMask (injected/EIP-6963), Torus (lazy-loaded embed).
 * SIWE: `authenticateWithSiwe` / `Web3Provider.authenticate` run the shared
 * challenge → personal_sign → verify journey against the GameGuild backend.
 */
export { Web3Provider, type Web3ProviderProps } from './components/web3-provider';
export { useWeb3, useWallet, useWalletAddress, useChainId, type WalletSnapshot } from './hooks/use-web3';
export { Web3Context, defaultWeb3ContextValue, type Web3ContextValue } from './lib/web3-context';
export {
  Web3ActionType,
  defaultWeb3State,
  web3Reducer,
  type Web3Action,
  type Web3Reducer,
} from './lib/web3-reducer';
export { authenticateWithSiwe } from './lib/siwe';
export {
  formatChainId,
  parseChainId,
  utf8ToHex,
  shortenAddress,
  isEthereumAddress,
} from './lib/utils';
export {
  createMetaMaskAdapter,
  discoverMetaMask,
  selectMetaMaskFromProviders,
  METAMASK_RDNS,
  type MetaMaskAdapterOptions,
  type MetaMaskDiscovery,
  type MetaMaskDiscoveryFn,
  type Eip6963ProviderDetail,
  type Eip6963ProviderInfo,
  type InjectedEthereum,
} from './adapters/metamask';
export {
  createTorusAdapter,
  type TorusAdapterOptions,
  type TorusEmbedLike,
  type TorusNetworkOptions,
} from './adapters/torus';
export { requestChainSwitch, isUnrecognizedChainError } from './adapters/chain-switch';
export type {
  ChainInfo,
  Eip1193EventMap,
  Eip1193Provider,
  Eip1193ProviderError,
  Eip1193RequestArguments,
  SiweAuthentication,
  SiweChallenge,
  SiweChallengeRequest,
  SiweTransport,
  SiweVerifyRequest,
  WalletAdapter,
  WalletConnection,
  WalletDescriptor,
  WalletEventHandlers,
  WalletId,
  WalletStatus,
  Web3Error,
  Web3ErrorCode,
  Web3State,
} from './types';
export {
  COMMON_CHAINS,
  INTERNAL_ERROR_CODE,
  UNRECOGNIZED_CHAIN_CODE,
  USER_REJECTED_REQUEST_CODE,
  isUserRejectedError,
  toProviderError,
  toWeb3Error,
} from './types';
