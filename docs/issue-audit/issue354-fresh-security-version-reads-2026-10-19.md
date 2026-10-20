# Persisted security version snapshots

Refs #353 and #354. This increment retains all original cache framework and
invalidation acceptance requirements.

`TenantSecurityVersionRepository.GetVersionsAsync` previously enumerated tracked
entities before building its dictionary. EF identity resolution can return an
already tracked entity with its earlier values after another context commits a
security version change. An ACL source-evaluation recheck can then compare two
stale numbers and miss a concurrent invalidation. Pending local edits could also
appear in a snapshot before they were committed.

The snapshot query now explicitly uses no tracking and projects only the stored
tenant identifier and security version. It preserves the existing requested-scope
and soft-deletion predicates. It does not reload, clear, save, or replace the
caller's tracked entities. The tracked lookup used by version writes is unchanged.
There is no HTTP, schema, dependency or generated-client change.

Five PostgreSQL regression cases are defined against the actual host composition:

- Persisted tenant and global version increments become visible within one reader
  scope even when that scope already tracks the old row, including a second read.
- Pending tenant and global edits do not leak into the snapshot or get overwritten.
- Requested scopes and duplicate inputs remain bounded; reads add no tracked rows.

These definitions are unexecuted locally. Exact-source hosted compilation and
native results remain required. They do not establish the complete Redis topology,
cache policy, deployment, benchmark or full framework acceptance.
