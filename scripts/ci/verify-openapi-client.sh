#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
openapi_artifact="${1:-${OPENAPI_ARTIFACT:-}}"

if [[ -z "$openapi_artifact" || ! -f "$openapi_artifact" ]]; then
  printf 'Usage: %s OPENAPI_ARTIFACT\n' "${0##*/}" >&2
  printf 'Set OPENAPI_ARTIFACT or provide a captured Swagger JSON file as the first argument.\n' >&2
  exit 2
fi

if [[ "$openapi_artifact" != /* ]]; then
  openapi_artifact="$(cd "$(dirname "$openapi_artifact")" && pwd)/$(basename "$openapi_artifact")"
fi

if command -v cygpath >/dev/null 2>&1; then
  openapi_artifact="$(cygpath --windows "$openapi_artifact")"
fi

cd "$repository_root"

pnpm --filter @game-guild/client generate -- --openapi "$openapi_artifact" --force

# Retain the exact generated source alongside the captured contract even when
# drift fails the gate. This supports source review/reconciliation without
# rebuilding or running the API on a developer's machine.
client_evidence="${OPENAPI_ARTIFACT_ROOT:-$repository_root/artifacts/test-results/openapi}/client-source"
mkdir -p "$client_evidence"
cp -R "$repository_root/packages/infrastructure/client/src/generated/." "$client_evidence/"

pnpm --filter @game-guild/client generate:diff
