# #224: explicit EVM host and journal startup composition

Refs #224. Full issue acceptance remains pending.

The actual API product composition now selects the EVM pipeline only through the explicit
`BlockchainCertificates:Evm:Enabled` setting. An absent or false switch preserves the existing
none/local default and adds no EVM database/key/RPC/worker services. The enabled host validates and
captures one immutable public network/profile, canonical issuer, signer environment reference,
integer-wei budget and bounded attempt/lease/replacement policy before registration. Invalid options
and binder/provider errors are redacted without retaining their inner exception. Simultaneous local
and EVM selection is rejected. Registration performs no key read, database connection or RPC call.

The host composes the existing transactional certificate producer, owner/tenant-scoped consumer,
dedicated PostgreSQL journal, constrained RPC clients, software signer and bounded hosted publisher.
No schema, HTTP contract or legacy transaction-hash field changes are introduced by this increment.
Configuration reload does not retarget the captured profile. Original-operation mismatches retain
the existing visible failure behavior rather than rebinding a digest or nonce.

The existing database initializer invokes a journal migration prerequisite using its own migration
connection before its normal migrations and runtime grants. The publication worker retains only the
runtime connection. A lifecycle readiness check performs read-only history/table checks during
`StartingAsync`, before any hosted `StartAsync`. Disabled startup initialization requires prior
operator-applied migration/grants; enabled publication does not bypass a missing journal. Existing
production role-validation requirements remain in force.

Tests are authored for disabled/default composition, invalid settings and redaction, immutable
policy, actual product registration, idempotent dedicated migrations, missing/deleted schema,
hosted-service startup ordering and a restricted PostgreSQL runtime role that can persist intents
but cannot alter the schema. These definitions require matching-source hosted execution. No local
SDK/build/restore/test/database/container/server/EVM execution occurred. The isolated database and
controlled composition tests do not establish authenticated HTTP or external-chain acceptance.

An inactive Sepolia template is documented. Its illustrative fee caps do not estimate current gas
prices, prove funding, deploy a contract or create a key. Full #224 acceptance still requires the
authenticated asynchronous status and fresh-proof surface, original-profile/quarantined-nonce
recovery, discovery after journal loss, and actual funded public-testnet deployment, anchor,
revocation and finalized-block evidence. Test definitions and private Anvil regression do not close
the issue.
