#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../disposable-postgres.sh
source "$script_dir/../disposable-postgres.sh"

first="$(new_disposable_postgres_password)"
second="$(new_disposable_postgres_password)"
[[ "$first" =~ ^[a-f0-9]{64}$ && "$second" =~ ^[a-f0-9]{64}$ && "$first" != "$second" ]]
register_disposable_postgres_password "$first"
register_disposable_postgres_password "$second"
printf 'PASS disposable database credentials use distinct 256-bit random values\n'

if register_disposable_postgres_password invalid 2>/dev/null; then
  printf 'FAIL invalid credential accepted\n' >&2
  exit 1
fi
printf 'PASS malformed generated credentials fail before infrastructure creation\n'

rendered="$(print_redacted_ci_command dotnet --connection "Password=$first;" --env "PGPASSWORD=$second")"
[[ "$rendered" != *"$first"* && "$rendered" != *"$second"* && "$rendered" == *REDACTED* ]]
output="$(printf 'ordinary output\nPassword=%s; second=%s\ntrailing=%s' "$first" "$second" "$first" \
  | redact_disposable_postgres_output)"
[[ "$output" == $'ordinary output\nPassword=[REDACTED]; second=[REDACTED]\ntrailing=[REDACTED]' ]]
printf 'PASS command, stream, and unterminated output redact every active disposable credential\n'

split="$(node -e '
  const value = process.argv[1];
  process.stdout.write(value.slice(0, 17));
  setImmediate(() => process.stdout.write(value.slice(17)));
' "$first" | redact_disposable_postgres_output)"
[[ "$split" == '[REDACTED]' ]]
printf 'PASS redaction preserves security across split stream chunks\n'

# Both callers use pipefail so a failing command stays failed after redaction.
set +e
bash -c 'printf "diagnostic\n"; exit 42' | redact_disposable_postgres_output >/dev/null
status=$?
set -e
[[ "$status" == 42 ]]
printf 'PASS output redaction retains the original command failure\n'
