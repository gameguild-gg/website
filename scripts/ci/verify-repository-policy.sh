#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
artifact_root="$repository_root/artifacts/test-results"
summary_path="$artifact_root/repository-policy/preflight-summary.txt"
gate_stage='initializing'

write_summary() {
  local status="$1" result='passed'
  if ((status != 0)); then
    result='failed'
  fi

  mkdir -p "$(dirname "$summary_path")" || return 0
  {
    printf 'gate=repository-policy\n'
    printf 'result=%s\n' "$result"
    printf 'status=%s\n' "$status"
    printf 'stage=%s\n' "$gate_stage"
  } > "$summary_path"
}

cleanup() {
  local status=$?
  trap - EXIT
  write_summary "$status" || true
  exit "$status"
}
trap cleanup EXIT

gate_stage='dependencies'
bash "$script_dir/install-and-audit-pnpm.sh"

gate_stage='policy-tests'
bash "$script_dir/tests/verify-economy.sh"
node --test "$script_dir/tests/validate-pnpm-audit.test.mjs" "$script_dir/tests/learning-process-security.test.mjs" "$script_dir/tests/dev-stop-process-security.test.mjs" "$script_dir/tests/dependency-advisory-upgrades.test.mjs"

gate_stage='deployment-contracts'
bash -n "$repository_root/scripts/deploy/promote-candidates.sh"
bash -n "$repository_root/scripts/deploy/rollout-devtron.sh"
node --test "$repository_root"/scripts/deploy/tests/*.test.mjs

gate_stage='completed'
