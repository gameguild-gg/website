#!/usr/bin/env bash
set -euo pipefail
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../../.." && pwd)"
fixture_root="$(mktemp -d "${TMPDIR:-/tmp}/affected-api-tests.XXXXXX")"
trap 'rm -rf "$fixture_root"' EXIT
mkdir -p "$fixture_root/bin"

cat > "$fixture_root/bin/docker" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
printf 'docker %s\n' "$*" >> "$MOCK_LOG"
case "$1" in
  run) printf 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\n' ;;
  port) printf '127.0.0.1:54329\n' ;;
  exec)
    if [[ "$*" == *'psql --host 127.0.0.1'* ]]; then
      [[ "${MOCK_FAILURE:-}" != readiness ]] || exit 1
      if [[ "${MOCK_FAILURE:-}" == socket_race ]]; then
        attempt="$(cat "$MOCK_PROBE_COUNT" 2>/dev/null || printf 0)"
        attempt=$((attempt + 1))
        printf '%s\n' "$attempt" > "$MOCK_PROBE_COUNT"
        ((attempt >= 3)) || exit 1
      fi
    elif [[ "$*" == *'createdb '* && "${MOCK_FAILURE:-}" == socket_race ]]; then
      # The image's temporary server accepts socket probes, then shuts down.
      [[ "$(cat "$MOCK_PROBE_COUNT" 2>/dev/null || printf 0)" -ge 3 ]] || exit 43
    fi
    ;;
esac
SH
cat > "$fixture_root/bin/sleep" <<'SH'
#!/usr/bin/env bash
exit 0
SH
cat > "$fixture_root/bin/dotnet" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
printf 'dotnet %s | template=%s\n' "$*" "${ECONOMY_POSTGRES_TEMPLATE_DATABASE:-}" >> "$MOCK_LOG"
printf 'fixture connection: %s\n' "${ECONOMY_POSTGRES_CONNECTION:-}"
if [[ "$1" == ef && "${MOCK_FAILURE:-}" == migration ]]; then exit 41; fi
if [[ "$1" == test ]]; then
  [[ "${MOCK_FAILURE:-}" != test ]] || exit 42
  test_name="$(basename "${2%.csproj}")"
  previous=''
  for argument in "$@"; do
    if [[ "$previous" == '--results-directory' ]]; then
      mkdir -p "$argument"
      printf '<TestRun><ResultSummary><Counters total="1" passed="1" failed="0"/></ResultSummary></TestRun>\n' \
        > "$argument/$test_name.trx"
    fi
    previous="$argument"
  done
fi
SH
chmod +x "$fixture_root/bin/docker" "$fixture_root/bin/dotnet" "$fixture_root/bin/sleep"
export PATH="$fixture_root/bin:$PATH"
export MOCK_LOG="$fixture_root/commands.log"
export MOCK_PROBE_COUNT="$fixture_root/probe-count"
export AFFECTED_DOTNET_TEST_ARTIFACTS="$fixture_root/results"
unset ECONOMY_POSTGRES_CONNECTION ECONOMY_POSTGRES_TEMPLATE_DATABASE

run_case() {
  local name="$1" expected_status="$2" failure="$3"
  shift 3
  : > "$MOCK_LOG"
  rm -f "$MOCK_PROBE_COUNT"
  printf '%s\n' "$@" > "$fixture_root/changed-files.txt"
  export MOCK_FAILURE="$failure"
  set +e
  bash "$repository_root/scripts/ci/run-affected-dotnet-tests.sh" \
    --files-from "$fixture_root/changed-files.txt" > "$fixture_root/$name.log" 2>&1
  local status=$?
  set -e
  [[ "$status" == "$expected_status" ]] || {
    cat "$fixture_root/$name.log" >&2
    printf '%s: expected status %s, got %s\n' "$name" "$expected_status" "$status" >&2
    exit 1
  }
}

economy_source='apps/api/Source/Modules/GameGuild.Finance.Economy/Ledger/Journal.cs'
api_source='apps/api/Source/GameGuild.API/Program.cs'
run_case migrated 0 '' "$economy_source"
fixture_password="$(sed -n 's/^docker run .*POSTGRES_PASSWORD=\([a-f0-9]\{64\}\) .*/\1/p' "$MOCK_LOG")"
[[ "$fixture_password" =~ ^[a-f0-9]{64}$ ]]
grep -Fq -- '--env POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256' "$MOCK_LOG"
grep -Fq -- "PGPASSWORD=$fixture_password" "$MOCK_LOG"
grep -Fq -- "Password=$fixture_password;" "$MOCK_LOG"
! grep -Fq -- "$fixture_password" "$fixture_root/migrated.log"
grep -Fq -- 'Password=[REDACTED];' "$fixture_root/migrated.log"
printf 'PASS generated credential is shared by authenticated readiness and template migration, and redacted from output\n'
grep -Fq -- 'public.ecr.aws/docker/library/postgres:17-alpine -c max_locks_per_transaction=512' "$MOCK_LOG"
grep -Fq -- '--context ApplicationDbContext --configuration Release --no-build' "$MOCK_LOG"
grep -Eq '^dotnet test .*Finance.Economy.UnitTests.* \| template=economy_tests_template$' "$MOCK_LOG"
[[ "$(grep -c '^docker rm --force aaaa' "$MOCK_LOG")" == 1 ]]
printf 'PASS affected Economy tests use the complete migrated template and owned cleanup\n'

run_case socket_race 0 socket_race "$economy_source"
[[ "$(<"$MOCK_PROBE_COUNT")" == 3 ]]
! grep -q 'pg_isready' "$MOCK_LOG"
socket_password="$(sed -n 's/^docker run .*POSTGRES_PASSWORD=\([a-f0-9]\{64\}\) .*/\1/p' "$MOCK_LOG")"
[[ "$socket_password" =~ ^[a-f0-9]{64}$ ]]
grep -Fq -- "PGPASSWORD=$socket_password" "$MOCK_LOG"
grep -Fq -- 'psql --host 127.0.0.1 --username postgres --dbname economy_tests' "$MOCK_LOG"
grep -Fq -- '--no-password --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --command SELECT 1;' "$MOCK_LOG"
printf 'PASS temporary socket readiness does not permit template creation before authenticated TCP readiness\n'

run_case readiness_failure 1 readiness "$economy_source"
! grep -q 'createdb\|^dotnet test ' "$MOCK_LOG"
grep -Fq 'docker logs aaaa' "$MOCK_LOG"
grep -Fq 'docker inspect --format {{json .State}} aaaa' "$MOCK_LOG"
[[ "$(grep -c '^docker rm --force aaaa' "$MOCK_LOG")" == 1 ]]
printf 'PASS failed TCP readiness retains server evidence and cleans only its owned container\n'

run_case mixed 0 '' "$api_source" "$economy_source"
grep -Eq '^dotnet test .*GameGuild.API.UnitTests.* \| template=$' "$MOCK_LOG"
grep -Eq '^dotnet test .*SharedKernel.UnitTests' "$MOCK_LOG"
[[ "$(grep -c '^docker run ' "$MOCK_LOG")" == 1 ]]
[[ "$(grep -c '^dotnet test ' "$MOCK_LOG")" == 3 ]]
printf 'PASS migration-sensitive API tests retain empty databases and every selected suite runs\n'

run_case core 0 '' "$api_source"
! grep -q '^docker ' "$MOCK_LOG"
[[ "$(grep -c '^dotnet test ' "$MOCK_LOG")" == 2 ]]
printf 'PASS ordinary core selection does not start Economy infrastructure\n'

run_case migration_failure 41 migration "$economy_source"
! grep -q '^dotnet test ' "$MOCK_LOG"
[[ "$(grep -c '^docker rm --force aaaa' "$MOCK_LOG")" == 1 ]]
printf 'PASS migration failure fails closed and cleans its owned container\n'

run_case test_failure 42 test "$economy_source"
[[ "$(grep -c '^docker rm --force aaaa' "$MOCK_LOG")" == 1 ]]
printf 'PASS test failure propagates and cleans its owned container\n'

run_case fallback 0 '' 'unused.txt'
! grep -q '^docker ' "$MOCK_LOG"
[[ "$(grep -c '^dotnet test ' "$MOCK_LOG")" == 1 ]]
printf 'PASS empty selection retains the existing API test fallback\n'
# The real selector rejects an invalid option before creating any infrastructure.
set +e
: > "$MOCK_LOG"
bash "$repository_root/scripts/ci/run-affected-dotnet-tests.sh" --invalid-option \
  > "$fixture_root/invalid-option.log" 2>&1
selector_status=$?
set -e
[[ "$selector_status" != 0 ]]
[[ ! -s "$MOCK_LOG" ]]
printf 'PASS selector errors fail before infrastructure creation\n'
