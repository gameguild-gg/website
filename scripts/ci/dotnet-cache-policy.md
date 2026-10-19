# NuGet cache ownership in CI

Pull requests restore `~/.nuget/packages` through `actions/cache/restore`.
They use the existing pinned action revision, project-input hash, and restore
prefix, but do not register the cache action's post-job save. Non-PR callers,
including Nightly Full on the default branch, keep the existing restore/save
action. Normal NuGet restore still runs when a package is missing.

GitHub scopes a cache saved by a PR to that PR's merge ref. Other PRs cannot
restore it. They can restore eligible default/base-branch caches instead. See
the [GitHub cache scope documentation](https://docs.github.com/en/actions/reference/workflows-and-actions/dependency-caching#restrictions-for-accessing-a-cache)
and the [pinned restore action contract](https://github.com/actions/cache/blob/55cc8345863c7cc4c66a329aec7e433d2d1c52a9/restore/README.md).

The observed repository snapshot on 2026-10-18 contained five `nuget-*`
merge-ref caches totaling 2,566,647,978 bytes, in addition to automatic NuGet
dependency-submission caches and other caches. Avoiding new per-PR package
archives limits this action's redundant storage growth. Existing entries are
not removed by this change. The separate automatic dependency-submission
cache policy is not changed.

This does not prove that cache eviction caused a particular missing Toolchain
cache, a PostgreSQL timeout, or a runner shutdown. No lower workflow latency or
complete hosted acceptance is claimed before the new source is verified.

## Verification

The hosted classifier runs `scripts/ci/tests/dotnet-cache-policy.test.mjs` to
check the PR/non-PR branches, identical cache identity, and the existing nightly
consumer. Cache misses continue through the normal package restore path.
Package versions, SDK version, certificate preparation, builds, test selection,
assertions, time limits, and Toolchain receipt validation are unchanged.

The tradeoff is that packages unique to a PR may need downloading again in a
later run until a shared branch cache contains them. A missing cache must not
be treated as a successful test result or as a reason to skip verification.
