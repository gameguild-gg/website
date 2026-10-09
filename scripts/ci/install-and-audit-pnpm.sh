#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"

# shellcheck source=economy-gate.sh
source "$script_dir/economy-gate.sh"

audit_root="$repository_root/artifacts/test-results/pnpm-audit"
audit_lock="$audit_root/pnpm-lock.yaml"
audit_report="$audit_root/audit.json"
audit_stderr_report="$audit_root/audit.stderr.log"
root_lock="$repository_root/pnpm-lock.yaml"
virtual_store_lock="$repository_root/node_modules/.pnpm/lock.yaml"

[[ -f "$root_lock" ]] || economy_gate_error "The repository pnpm lockfile is required: $root_lock"
lock_hash_before="$(sha256sum "$root_lock" | awk '{print $1}')"

mkdir -p "$audit_root"
rm -f "$audit_lock" "$audit_report" "$audit_stderr_report"

cd "$repository_root"
export CI=true

# Discard the derived dependency-status cache so the frozen install checks
# the current lockfile and security patches, including after a pnpm upgrade.
rm -f "$repository_root/node_modules/.pnpm-workspace-state.json"
printf '> pnpm install --frozen-lockfile --ignore-scripts\n'
pnpm install --frozen-lockfile --ignore-scripts
# Patch application uses pnpm's dependency build stage. Only these two
# reviewed packages are approved for that stage in the root manifest.
printf '> pnpm rebuild braces sprintf-js\n'
pnpm rebuild braces sprintf-js
lock_hash_after_install="$(sha256sum "$root_lock" | awk '{print $1}')"
[[ "$lock_hash_before" == "$lock_hash_after_install" ]] || economy_gate_error 'pnpm install unexpectedly changed the repository lockfile'
[[ -f "$virtual_store_lock" ]] || economy_gate_error "pnpm install did not produce its virtual-store resolution: $virtual_store_lock"

cp "$virtual_store_lock" "$audit_lock"

export npm_config_lockfile_dir="$audit_root"
printf '> pnpm audit --json\n'
set +e
pnpm audit --json >"$audit_report" 2>"$audit_stderr_report"
audit_exit_code=$?
set -e

lock_hash_after_audit="$(sha256sum "$root_lock" | awk '{print $1}')"
[[ "$lock_hash_before" == "$lock_hash_after_audit" ]] || economy_gate_error 'pnpm audit unexpectedly changed the repository lockfile'

printf '> node scripts/ci/validate-pnpm-audit.mjs\n'
node "$script_dir/validate-pnpm-audit.mjs" "$audit_report" "$audit_exit_code"
node --test "$script_dir/tests/dependency-security.test.mjs" "$script_dir/tests/next-dev-origin-security.test.mjs"
