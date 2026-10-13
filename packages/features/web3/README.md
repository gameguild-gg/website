# @game-guild/web3

Wallet connection context, EIP-1193 adapters and the shared Sign-In with
Ethereum (SIWE) journey for GameGuild web clients.

Issues: #38 (React context), #39 (MetaMask), #16 (Torus), under the umbrella
of #291 (Web3 Wallet Authentication System). The backend challenge/verify
endpoints are owned by #292 (`POST /v1/auth/web3/challenge`,
`POST /v1/auth/web3:verify`).

## Supported provider scope

| Wallet   | Transport                                                   | Availability                                                            |
| -------- | ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| MetaMask | Injected EIP-1193 provider (EIP-6963 discovery, legacy `window.ethereum` fallback incl. multi-wallet `providers[]`) | Button rendered whenever the extension is detected. No SDK dependency.  |
| Torus    | `@toruslabs/torus-embed` loaded through a lazy dynamic `import()` | Config-gated (`enabled: true`) and fails closed if the SDK is missing. Never part of the main bundle. |

Both adapters speak plain EIP-1193 (`eth_requestAccounts`, `eth_accounts`,
`eth_chainId`, `personal_sign`, `wallet_switchEthereumChain`,
`wallet_addEthereumChain`) — no ethers/web3.js dependency. Any future
EIP-1193 wallet (#17 Trust, #18 WalletConnect) plugs in by implementing the
same `WalletAdapter` contract and registering it on the provider.

Torus runtime behavior and limits:

- The embed SDK is an **optional** runtime dependency of the consuming app.
  When it is not installed (or the chunk fails to load) the adapter reports
  unavailable and the Torus button is hidden — it never breaks the bundle.
- The SDK is LGPL-3.0; embedding it in the (proprietary) GameGuild web app is
  acceptable for unmodified dynamic use, but any fork/patch of the SDK would
  need license review. Bundle impact is isolated to a lazy chunk
  (~1 MB minified across the embed + its iframe assets).
- Signature rejection (`login()`/`personal_sign` dismissed), disconnection
  (`disconnect` event or `accountsChanged: []`) and account/network changes
  (`accountsChanged`/`chainChanged`) are forwarded through the shared state
  machine for every wallet.

## Usage

```tsx
import {
  Web3Provider,
  createMetaMaskAdapter,
  createTorusAdapter,
} from '@game-guild/web3';

<Web3Provider
  adapters={[
    createMetaMaskAdapter(),
    createTorusAdapter({
      enabled: process.env.NEXT_PUBLIC_WALLET_TORUS_ENABLED === 'true',
    }),
  ]}
>
  <SignInButtons />
</Web3Provider>
```

The SIWE journey runs `challenge → personal_sign → verify` through an
injected `SiweTransport`, so the package stays framework-agnostic:

```ts
const { state, connect, authenticate } = useWeb3();
await connect('metamask');
await authenticate({
  getChallenge: async (request) => (await fetch('/api/auth/web3/challenge', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  })).json(),
  verify: async (request) => { /* POST /v1/auth/web3:verify */ },
});
```

State machine (`Web3State.status`): `disconnected → connecting → connected →
signing → authenticated`, with `error` reachable from any user-facing step
and full cleanup on disconnect (wallet logout, listener teardown, state
reset). `accountsChanged` downgrades `authenticated` back to `connected`
because the session identity changed.
