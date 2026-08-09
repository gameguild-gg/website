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
git diff --exit-code -- packages/infrastructure/client/src/generated