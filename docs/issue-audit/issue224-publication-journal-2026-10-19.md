# #224 — durable EVM publication journal

## Requirement and current scope

The owner retained real blockchain anchoring and selected configurable EVM networks,
starting with a testnet. This increment implements the durable publication journal
needed by that integration. It does not close #224.

The accepted contract/read-proof increment is PR #903. Its isolated Anvil evidence
does not establish an external Sepolia deployment or transaction. Proposed Sepolia
chain ID is 11155111; no public deployment or funded signer is claimed here.

## Implemented publication foundation

- Capture public network identity, finality policy, document, authenticated owner,
  tenant, digest and operation kind in an immutable intent. Do not persist an RPC URL
  or private key. A signer reference is a public configuration key.
- Deduplicate by network/contract/digest/operation and by producer/document. Reject
  conflicting owner, tenant, profile or digest rather than rewriting the intent.
- Acquire an expiring worker lease atomically. A new lease advances the operation
  revision; stale workers fail optimistic concurrency or the fencing-token check.
- Allocate each wallet nonce globally per chain, across contracts and workers.
  Commit the counter, operation and event together. Never change a reserved nonce
  because an RPC pending-nonce estimate or fee configuration later changes.
- Validate a bounded canonical EIP-1559 envelope before decoding it: exact chain,
  contract, calldata, digest, zero value, nonce, gas limit, low-S signature and signer.
  Use integer wei and enforce the captured fee and total transaction caps.
- Persist the signed payload and calculated transaction hash before recording any
  broadcast. A repeated payload is idempotent. Fee replacements preserve the intent,
  nonce and earlier attempts; an earlier replacement can still be the mined winner.
- Retain operation events and attempts through retries, restarts and reorgs.
  Bind observations to the original network and document intent. A worker may adopt
  independently verified original chain evidence when recovering an existing anchor.
- Delay revocation work until its matching anchor has verified state. This journal
  is a trusted worker boundary, not an HTTP command or a public proof verifier.

## Persistence and migration

`EvmCertificateJournalDbContext` is opt-in and has four dedicated PostgreSQL tables
under `gameguild.authentication`. Its history table must be
`__EF_EvmCertificateJournal` in that schema. The constructor rejects the application
history table. The initial migration leaves legacy anchor tables untouched and its
down operation drops only its own four tables. The schema version and migration
snapshot are frozen together; future schema changes require a new version.

The native suite uses `MigrateAsync`, checks for pending model changes and applied
migrations, and exercises independent database contexts. It never uses EnsureCreated
as evidence that a migration works.

## Verification status

New unit cases cover configuration capture, exact wei budgets, malformed encodings
and cryptographic binding of signed transactions. New PostgreSQL cases cover actual
migration, conflicting intents, competing claims and nonce reservations, restart,
replacement history, cancellation after a database command, retries, observation
binding, reorg history and revocation ordering.

These are authored test definitions until the current source is executed by hosted
CI. No local .NET, database, container, EVM or test process is used for this increment.
The contract workflow retains the real Anvil/HTTPS tests and adds the PostgreSQL
journal suite to its existing native project. Constructed observations in journal
tests prove storage/binding behavior; they do not prove a transaction on a chain.

## Still required before full #224 acceptance

1. Implement the configured signer and bounded publication RPC port, with network
   validation, key privacy, known-hash recovery after ambiguous broadcast, fee
   replacement policy and reorg reconciliation.
2. Integrate the worker and trusted host producer. Preserve the original digest and
   network for revocation, and close the crash gap after certificate issuance.
   Keep the existing certificate lifecycle auxiliary/nonfatal behavior.
3. Add actual journal-to-signer-to-EVM publication/recovery tests and host tests.
4. Deploy the pinned registry to the configured external testnet using an approved
   funded signer, exercise issuance and revocation, and inspect real finality.
5. Record current-source hosted results, the external deployment/transaction evidence
   and the owner acceptance before closing the issue.

Framework API shapes were checked against the pinned
[EF Core 10.0.9 relational options source](https://github.com/dotnet/efcore/blob/v10.0.9/src/EFCore.Relational/Infrastructure/RelationalOptionsExtension.cs)
and [command interceptor source](https://github.com/dotnet/efcore/blob/v10.0.9/src/EFCore.Relational/Diagnostics/DbCommandInterceptor.cs).
