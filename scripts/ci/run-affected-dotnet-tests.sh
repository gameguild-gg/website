#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
# shellcheck source=disposable-postgres.sh
source "$script_dir/disposable-postgres.sh"
cd "$repository_root"

# Capture selection before starting infrastructure; a failed selector must fail
# the job, rather than silently falling back through a process substitution.
project_list="$(node "$script_dir/select-affected-dotnet-tests.mjs" "$@")"
if [[ -z "$project_list" ]]; then
  project_list='apps/api/tests/GameGuild.API.UnitTests/GameGuild.API.UnitTests.csproj'
fi
mapfile -t projects <<< "$project_list"
artifact_root="${AFFECTED_DOTNET_TEST_ARTIFACTS:-artifacts/test-results/affected-api}"
mkdir -p "$artifact_root"
printf '%s\n' "${projects[@]}" > "$artifact_root/projects.txt"

postgres_id=''
template_database='economy_tests_template'
cleanup() {
  local status=$?
  trap - EXIT INT TERM
  if [[ -n "$postgres_id" ]]; then
    docker logs "$postgres_id" > "$artifact_root/postgres-server.log" 2>&1 || true
    docker inspect --format '{{json .State}}' "$postgres_id" \
      > "$artifact_root/postgres-state.json" 2>&1 || true
    # This exact ID is returned by this runner's own successful docker run.
    if ! docker rm --force "$postgres_id" > "$artifact_root/postgres-cleanup.log" 2>&1; then
      printf 'Could not remove the affected-test PostgreSQL container\n' >&2
      ((status != 0)) || status=1
    fi
  fi
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

prepare_economy_template() {
  local container_name="gameguild-affected-api-$$-$RANDOM"
  local mapping postgres_port template_connection candidate_id attempt password ready=false
  password="$(new_disposable_postgres_password)"
  register_disposable_postgres_password "$password"
  candidate_id="$(docker run --detach --rm --name "$container_name" \
    --label gameguild.ci=affected-api \
    --env POSTGRES_DB=economy_tests \
    --env POSTGRES_USER=postgres --env "POSTGRES_PASSWORD=$password" \
    --env POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256 \
    --publish 127.0.0.1::5432 \
    postgres:17-alpine -c max_locks_per_transaction=512)"
  [[ "$candidate_id" =~ ^[a-f0-9]{12,64}$ ]] || {
    printf 'Docker did not return a valid owned container ID\n' >&2
    return 1
  }
  postgres_id="$candidate_id"
  for ((attempt=0; attempt<90; attempt++)); do
    # The image starts a socket-only initialization server and then stops it.
    # Wait for the final server to accept an authenticated TCP query.
    if docker exec --env "PGPASSWORD=$password" "$postgres_id" \
      psql --host 127.0.0.1 --username postgres --dbname economy_tests \
      --no-password --no-psqlrc --set ON_ERROR_STOP=1 --tuples-only --command 'SELECT 1;' \
      > "$artifact_root/postgres-readiness.log" 2>&1; then
      ready=true
      break
    fi
    sleep 1
  done
  [[ "$ready" == true ]] || { printf 'Affected-test PostgreSQL did not become ready\n' >&2; return 1; }
  mapping="$(docker port "$postgres_id" '5432/tcp')"
  [[ "$mapping" =~ :([0-9]+)$ ]] || { printf 'Missing PostgreSQL loopback port\n' >&2; return 1; }
  postgres_port="${BASH_REMATCH[1]}"
  export ECONOMY_POSTGRES_CONNECTION="Host=127.0.0.1;Port=$postgres_port;Database=economy_tests;Username=postgres;Password=$password;Include Error Detail=true"
  template_connection="Host=127.0.0.1;Port=$postgres_port;Database=$template_database;Username=postgres;Password=$password;Include Error Detail=true"
  docker exec "$postgres_id" createdb --username postgres "$template_database"
  dotnet tool restore > "$artifact_root/tool-restore.log" 2>&1
  # Build API is a preceding required workflow step. Use the same complete
  # ApplicationDbContext migrations and template strategy as verify-economy.sh.
  dotnet ef database update \
    --project apps/api/Source/GameGuild.API/GameGuild.API.csproj \
    --startup-project apps/api/Source/GameGuild.API/GameGuild.API.csproj \
    --context ApplicationDbContext --configuration Release --no-build \
    --connection "$template_connection" 2>&1 \
    | redact_disposable_postgres_output > "$artifact_root/template-migration.log"
  docker exec "$postgres_id" psql --username postgres --dbname postgres \
    --set ON_ERROR_STOP=1 --command "ALTER DATABASE \"$template_database\" IS_TEMPLATE true;"
  export ECONOMY_POSTGRES_TEMPLATE_DATABASE="$template_database"
}

for project in "${projects[@]}"; do
  [[ "$project" =~ ^apps/api/tests/GameGuild\.[A-Za-z0-9.]+/GameGuild\.[A-Za-z0-9.]+\.csproj$ ]] \
    && [[ -f "$project" ]] || { printf 'Invalid selected test project: %s\n' "$project" >&2; exit 1; }
  test_name="$(basename "${project%.csproj}")"
  results="$artifact_root/$test_name"
  mkdir -p "$results"
  test_environment=(env)
  if [[ "$test_name" == 'GameGuild.API.UnitTests' ]]; then
    # These tests explicitly verify migrations on an empty database.
    test_environment+=(ECONOMY_POSTGRES_TEMPLATE_DATABASE=)
  elif grep -Fq 'GameGuild.TestSupport.Finance.Economy.csproj' "$project"; then
    if [[ -z "$postgres_id" ]]; then
      prepare_economy_template
    fi
  fi
  "${test_environment[@]}" dotnet test "$project" -c Release --nologo \
    --blame-hang-timeout 5m \
    --logger "trx;LogFileName=$test_name.trx" --results-directory "$results" \
    2>&1 | redact_disposable_postgres_output
  [[ -f "$results/$test_name.trx" ]] || { printf 'Missing test evidence: %s\n' "$project" >&2; exit 1; }
done
