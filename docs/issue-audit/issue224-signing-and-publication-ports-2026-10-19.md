# #224 — signing and publication ports

Full issue acceptance remains pending. The journal increment was accepted in PR #906,
develop commit `95fa8b5fca7901349d92aeb43a9b5f0de313a7ad`.

## Implemented increment

- An explicit signing interface supports hardware/remote implementations without
  requiring exported private keys. The optional software implementation obtains
  a fresh buffer by public key reference, enforces a valid secp256k1 scalar, binds
  the address to the reserved intent, and validates the resulting EIP-1559 envelope.
- Fee and priority values must fit the original captured budget. Invalid fees are
  rejected before resolving a private key. A changed key cannot silently replace
  the signer associated with a reserved nonce.
- Software signing clears the transferred private-key input buffer on success,
  cancellation and failure. Third-party cryptography may make temporary managed
  copies; this is not a promise of erasing every managed-memory copy.
- The optional environment adapter accepts only an explicitly configured map of
  public references to variable names. It does not expose arbitrary environment
  reads or retain key values in the reference map. Neither the adapter nor the
  signer is registered or activated by default.
- A fixed publication RPC port supports pending-nonce lookup and signed broadcast.
  Both validate the captured and configured network identities against a fresh
  proof-verifier result. A broadcast validates the reserved envelope and requires
  the provider's returned hash to equal the locally calculated transaction hash.
- Read and publication ports reuse the existing bounded JSON-RPC transport. The
  read interface retains its six-method allowlist; there is no public arbitrary
  method-name passthrough. Deadlines, response bounds, response correlation and
  safe error categories are preserved.
- The named client removes default HTTP loggers and disables its HTTP diagnostic
  handler through a null activity propagator. This affects this client's handler,
  not the application's other clients. The native suite includes a positive
  instrumentation control before checking the protected client.

The diagnostic-handler condition and URI exposure were checked in the pinned
[.NET 10.0.9 SocketsHttpHandler source](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/SocketsHttpHandler.cs)
and [DiagnosticsHandler source](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs).
Actual behavior still requires the current hosted native test result.

## Verification scope

New unit cases cover key-reference isolation, buffer clearing, cancellation,
sanitized failures, key rotation, anchor/revoke signatures, budgets, canonical
nonces, exact network binding, returned-hash correlation and explicit DI composition.

New hosted native cases connect the real software signer, PostgreSQL journal,
bounded publication RPC, deployed Anvil registry and read-proof verifier. They
require the signed attempt to be durable before actual submission, verify actual
anchor/revoke state and exercise recovery of a known transaction hash using a fresh
journal instance and simulated lease expiry. That recovery case deliberately omits
the acknowledgement after a real accepted node submission; it is not an actual
process kill or production-worker acceptance.

The native harness transfers its already generated, masked, ephemeral fixture key
to the test process through an environment variable and unsets it during cleanup.
No private key is written to an artifact or repository file. TLS trust overrides
are limited to the isolated loopback fixture and its expected certificate hash.
Production trust settings are unchanged.

Successful native publications retain explicit public evidence projections in
`publication-*.json`: operation, digest, nonce, transaction hash and verified network,
block and registry state. The projection excludes private keys, endpoints and raw
environment values. Diagnostic observation is scoped to a unique parent span to
avoid collecting unrelated parallel tests.

Tests are authored until executed by hosted CI. No local SDK, database, container,
EVM, server, application or test process is used for this increment.

## Full requirement still pending

1. Production orchestration/worker with persisted-attempt recovery, bounded retry,
   replacement policy, safe nonce-gap handling and reorg reconciliation.
2. Durable host issuance intent/recovery and original digest/profile retention for
   revocation, preserving the auxiliary/nonfatal certificate lifecycle contract.
3. Actual host/provider integration acceptance and failure/recovery cases.
4. An approved funded signer, deployed registry and real anchor/revoke finality on
   the configured external testnet, followed by owner acceptance.

Existing none/local provider selection is preserved. The isolated Anvil chain12345
is not external Sepolia acceptance. No external deployment or funded signer is claimed.
