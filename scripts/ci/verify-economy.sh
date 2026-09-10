#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
artifact_root="$repository_root/artifacts/test-results/economy"
manifest_path="$script_dir/economy-projects.json"
summary_path="$artifact_root/preflight-summary.txt"
timings_path="$artifact_root/timings.jsonl"
gate_stage='initializing'
run_sequence=0
gate_started_epoch="$(date +%s)"
gate_profile="${ECONOMY_GATE_PROFILE:-full}"
test_hang_timeout="${ECONOMY_TEST_HANG_TIMEOUT:-5m}"
api_test_timeout="${ECONOMY_API_TEST_TIMEOUT:-12m}"
whole_solution_jobs="${ECONOMY_WHOLE_SOLUTION_JOBS:-}"

# shellcheck source=economy-gate.sh
source "$script_dir/economy-gate.sh"

whole_solution_scaffold_project() {
  case "$1" in
    GameGuild.Localization.IntegrationTests.dll)
      printf '%s\n' 'apps/api/tests/GameGuild.Localization.IntegrationTests/GameGuild.Localization.IntegrationTests.csproj'
      ;;
    GameGuild.Contents.IntegrationTests.dll)
      printf '%s\n' 'apps/api/tests/GameGuild.Contents.IntegrationTests/GameGuild.Contents.IntegrationTests.csproj'
      ;;
    GameGuild.UserProfiles.IntegrationTests.dll)
      printf '%s\n' 'apps/api/tests/GameGuild.UserProfiles.IntegrationTests/GameGuild.UserProfiles.IntegrationTests.csproj'
      ;;
    *) return 1 ;;
  esac
}

whole_solution_test_project() {
  local project_path="$1"

  ! grep -Eq '<IsTestProject>[[:space:]]*false[[:space:]]*</IsTestProject>' "$project_path"
}

economy_test_project() {
  local project_path="$1" economy_project

  for economy_project in "${economy_tests[@]}"; do
    [[ "$project_path" == "$economy_project" ]] && return 0
  done
  return 1
}

test_assembly_from_trx_metadata() {
  "$PYTHON_BIN" - "$1" <<'PY'
import re
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
candidates = set()

for element in root.iter():
    tag = element.tag.rsplit("}", 1)[-1]
    if tag not in {"StdOut", "Text"}:
        continue

    text = "".join(element.itertext())
    for match in re.finditer(
        r"(?:Discovering|Discovered):\s+([A-Za-z0-9_.-]+)",
        text,
    ):
        assembly = match.group(1)
        candidates.add(
            assembly if assembly.endswith(".dll") else f"{assembly}.dll"
        )

    for match in re.finditer(
        r"No test is available in\s+([^\r\n]+?\.dll)",
        text,
    ):
        candidates.add(re.split(r"[\\/]", match.group(1))[-1])

if len(candidates) != 1:
    raise SystemExit(
        "TRX metadata must identify exactly one test assembly; "
        f"found {sorted(candidates)}"
    )

print(next(iter(candidates)))
PY
}

test_assembly_for_trx() {
  local output_log="$1" trx_path="$2"
  local target_name="${trx_path//\\//}"
  local line current_assembly='' result_path result_name matched_assembly=''
  target_name="${target_name##*/}"

  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    case "$line" in
      'Test run for '*)
        current_assembly="${line#Test run for }"
        current_assembly="${current_assembly%% (*}"
        ;;
      'Results File: '*)
        result_path="${line#Results File: }"
        result_path="${result_path//\\//}"
        result_name="${result_path##*/}"
        if [[ "$result_name" == "$target_name" ]]; then
          [[ -z "$matched_assembly" ]] || economy_gate_error "TRX evidence is reported more than once in dotnet output: $trx_path"
          matched_assembly="$current_assembly"
        fi
        ;;
    esac
  done < "$output_log"

  if [[ -z "$matched_assembly" ]]; then
    if ! matched_assembly="$(test_assembly_from_trx_metadata "$trx_path")"; then
      economy_gate_error "Could not identify the test assembly for TRX evidence: $trx_path"
      return 1
    fi
  fi
  printf '%s\n' "$matched_assembly"
}

assert_whole_solution_evidence() {
  local root="$1" output_log="$2"
  shift 2
  local trx output assembly_path assembly_name project_path project_directory source_file

  (($# > 0)) || economy_gate_error 'Whole-solution tests produced no TRX evidence'
  [[ -f "$output_log" ]] || economy_gate_error "Whole-solution test output is missing: $output_log"

  for trx in "$@"; do
    if output="$(assert_trx_evidence "$trx" 2>&1)"; then
      continue
    fi
    [[ "$output" == *'TRX suite contains zero tests:'* ]] || economy_gate_error "$output"

    assembly_path="$(test_assembly_for_trx "$output_log" "$trx")"
    assembly_path="${assembly_path//\\//}"
    assembly_name="${assembly_path##*/}"
    if ! project_path="$(whole_solution_scaffold_project "$assembly_name")"; then
      printf '%s\n' "$output" >&2
      economy_gate_error "Zero-test TRX came from a non-scaffold test assembly: $assembly_name"
    fi

    [[ -f "$root/$project_path" ]] || economy_gate_error "Known whole-solution scaffold is missing: $project_path"
    project_directory="$(dirname "$root/$project_path")"
    source_file="$(find "$project_directory" \
      \( -type d \( -name bin -o -name obj \) -prune \) -o \
      \( -type f -name '*.cs' -print -quit \))"
    if [[ -n "$source_file" ]] || grep -qE '<Compile([[:space:]>])' "$root/$project_path"; then
      economy_gate_error "Known whole-solution scaffold contains C# source but produced zero tests: $project_path"
    fi
    printf 'Accepted zero-test evidence from source-empty scaffold: %s\n' "$project_path"
  done
}

if [[ "${1:-}" == '--validate-whole-solution-evidence' ]]; then
  shift
  (($# >= 3)) || economy_gate_error 'Usage: --validate-whole-solution-evidence REPOSITORY_ROOT OUTPUT_LOG TRX...'
  validation_root="$1"
  validation_log="$2"
  shift 2
  assert_whole_solution_evidence "$validation_root" "$validation_log" "$@"
  exit
fi

if (($#)); then
  printf 'Unknown argument: %s\n' "$1" >&2
  exit 2
fi

case "$gate_profile" in
  pr|full) ;;
  *)
    printf 'Unknown Economy gate profile: %s\n' "$gate_profile" >&2
    exit 2
    ;;
esac

if [[ -z "$whole_solution_jobs" ]]; then
  whole_solution_jobs=2
fi
[[ "$whole_solution_jobs" =~ ^[1-9][0-9]*$ ]] || {
  printf 'ECONOMY_WHOLE_SOLUTION_JOBS must be a positive integer: %s\n' "$whole_solution_jobs" >&2
  exit 2
}

api_pid=''
web_pid=''
postgres_container=''
economy_postgres_container=''
whole_solution_postgres_container=''
whole_solution_connection_string=''
garage_container=''
testcontainers_baseline=''
testcontainers_reaper_disabled=false
declare -a whole_solution_worker_pids=()

cleanup() {
  local status=$?
  trap - EXIT INT TERM
  set +e
  mkdir -p "$(dirname "$summary_path")"
  if ((status == 0)); then
    gate_result='passed'
  else
    gate_result='failed'
  fi
  {
    printf 'gate=economy\n'
    printf 'profile=%s\n' "$gate_profile"
    printf 'result=%s\n' "$gate_result"
    printf 'status=%s\n' "$status"
    printf 'stage=%s\n' "$gate_stage"
    printf 'duration_seconds=%s\n' "$(( $(date +%s) - gate_started_epoch ))"
  } > "$summary_path"
  for worker_pid in "${whole_solution_worker_pids[@]}"; do
    stop_process_tree "$worker_pid"
  done
  stop_process_tree "$web_pid"
  stop_process_tree "$api_pid"
  if [[ "$testcontainers_reaper_disabled" == true ]]; then
    while IFS= read -r testcontainer_id; do
      [[ -z "$testcontainer_id" ]] && continue
      if ! grep -Fxq -- "$testcontainer_id" <<< "$testcontainers_baseline"; then
        docker rm --force "$testcontainer_id" >/dev/null 2>&1 || true
      fi
    done < <(docker ps -aq --filter label=org.testcontainers=true)
  fi
  if [[ -n "$postgres_container" ]]; then
    docker rm --force "$postgres_container" >/dev/null 2>&1 || true
  fi
  if [[ -n "$economy_postgres_container" ]]; then
    docker rm --force "$economy_postgres_container" >/dev/null 2>&1 || true
  fi
  if [[ -n "$whole_solution_postgres_container" ]]; then
    docker rm --force "$whole_solution_postgres_container" >/dev/null 2>&1 || true
  fi
  if [[ -n "$garage_container" ]]; then
    docker rm --force "$garage_container" >/dev/null 2>&1 || true
  fi
  exit "$status"
}
trap cleanup EXIT INT TERM

record_timing() {
  local sequence="$1" stage="$2" started_epoch="$3" status="$4"
  printf '{"sequence":%s,"stage":"%s","durationSeconds":%s,"status":%s}\n' \
    "$sequence" "$stage" "$(( $(date +%s) - started_epoch ))" "$status" >> "$timings_path"
}

run() {
  run_sequence=$((run_sequence + 1))
  local output_path="$artifact_root/logs/${run_sequence}-${gate_stage}.log"
  local started_epoch="$(date +%s)"
  mkdir -p "$(dirname "$output_path")"
  printf '> '
  printf '%q ' "$@"
  printf '\n'
  set +e
  "$@" 2>&1 | tee "$output_path"
  local command_status=${PIPESTATUS[0]}
  set -e
  record_timing "$run_sequence" "$gate_stage" "$started_epoch" "$command_status"
  return "$command_status"
}

run_logged() {
  local output_path="$1"
  shift
  run_sequence=$((run_sequence + 1))
  local started_epoch="$(date +%s)"
  printf '> '
  printf '%q ' "$@"
  printf '\n'
  set +e
  "$@" 2>&1 | tee "$output_path"
  local command_status=${PIPESTATUS[0]}
  set -e
  record_timing "$run_sequence" "$gate_stage" "$started_epoch" "$command_status"
  return "$command_status"
}

run_logged_append() {
  local output_path="$1"
  shift
  run_sequence=$((run_sequence + 1))
  local started_epoch="$(date +%s)"
  printf '> '
  printf '%q ' "$@"
  printf '\n'
  set +e
  "$@" 2>&1 | tee -a "$output_path"
  local command_status=${PIPESTATUS[0]}
  set -e
  record_timing "$run_sequence" "$gate_stage" "$started_epoch" "$command_status"
  return "$command_status"
}

run_test_with_timeout() {
  # VSTest's blame timeout starts only after a test begins. Bound the complete
  # process group as well, so a testhost that stalls before reporting its first
  # test cannot hold the gate indefinitely. --blame remains enabled below to
  # collect a dump whenever VSTest has enough state to produce one.
  run timeout --kill-after=30s "$test_hang_timeout" "$@"
}
native_path() {
  if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
    cygpath -w "$1"
  else
    printf '%s\n' "$1"
  fi
}

cd "$repository_root"
declare -a pnpm_command=(pnpm)
declare -a dotnet_build_isolation=()
if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
  # The shared MSBuild server and reusable worker nodes can retain stale compiler
  # state on Windows hosts. These are deliberately host-only gate settings; they
  # are not container settings.
  export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1
  export MSBUILDDISABLENODEREUSE=1
  export TESTCONTAINERS_RYUK_DISABLED=true
  dotnet_build_isolation=(-m:4 -p:UseSharedCompilation=false)
  testcontainers_reaper_disabled=true
  testcontainers_baseline="$(docker ps -aq --filter label=org.testcontainers=true)"
fi
rm -rf "$artifact_root"
mkdir -p \
  "$artifact_root/trx/whole-solution" \
  "$artifact_root/trx/economy" \
  "$artifact_root/trx/provider" \
  "$artifact_root/coverage" \
  "$artifact_root/openapi" \
  "$artifact_root/api" \
  "$artifact_root/vitest" \
  "$artifact_root/playwright" \
  "$artifact_root/postgres" \
  "$artifact_root/publish"

gate_stage='preflight-smoke'
run node --test scripts/devops/smoke-check.test.mjs
gate_stage='preflight-manifest'
assert_economy_manifest "$repository_root" "$manifest_path"

gate_stage='preflight-postgres-isolation'
mapfile -t nested_postgres_builders < <(
  grep -RIl --include='*.cs' --exclude-dir=bin --exclude-dir=obj 'new PostgreSqlBuilder' apps/api/tests \
    | grep -v '^apps/api/tests/GameGuild.TestSupport.Finance.Economy/' \
    || true
)
if ((${#nested_postgres_builders[@]})); then
  economy_gate_error "PostgreSQL tests must use the gate-owned ECONOMY_POSTGRES_CONNECTION; nested Testcontainers builders found: ${nested_postgres_builders[*]}"
fi

declare -a economy_production=()
declare -a economy_tests=()
declare -a economy_coverage_records=()
declare -a provider_contracts=()
while IFS=$'\t' read -r record_type first second third fourth; do
  record_type="$(normalize_shell_record_field "$record_type")"
  first="$(normalize_shell_record_field "$first")"
  second="$(normalize_shell_record_field "$second")"
  third="$(normalize_shell_record_field "$third")"
  fourth="$(normalize_shell_record_field "$fourth")"
  case "$record_type" in
    production) economy_production+=("$first") ;;
    test) economy_tests+=("$first") ;;
    coverage)
      economy_coverage_records+=("$first"$'\t'"$second"$'\t'"$third"$'\t'"$fourth")
      ;;
    provider) provider_contracts+=("$first"$'\t'"$second") ;;
  esac
done < <("$PYTHON_BIN" - "$manifest_path" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    manifest = json.load(handle)
for entry in manifest.get("projects", []):
    production = entry["productionProject"].replace("\\", "/")
    print("production", production, sep="\t")
    assemblies = ",".join(entry.get("coverageAssemblies", []))
    prefixes = ",".join(value.replace("\\", "/") for value in entry.get("coveragePathPrefixes", [])) or "__all__"
    minimum_branch_rate = str(entry.get("minimumBranchRate", 1))
    for test in entry.get("testProjects", []):
        normalized = test.replace("\\", "/")
        print("test", normalized, sep="\t")
        print("coverage", normalized, assemblies, prefixes, minimum_branch_rate, sep="\t")
for contract in manifest.get("providerContractProjects", []):
    print("provider", contract["project"].replace("\\", "/"), contract["filter"], sep="\t")
PY
)

postgres_container="gameguild-economy-ci-app-$$-$RANDOM"
economy_postgres_container="gameguild-economy-ci-tests-$$-$RANDOM"
gate_stage='postgres-app'
run docker run --detach --rm --name "$postgres_container" \
  --env POSTGRES_DB=economy_ci \
  --env POSTGRES_USER=postgres \
  --env POSTGRES_PASSWORD=postgres \
  --publish 127.0.0.1::5432 \
  postgres:17-alpine >/dev/null

app_postgres_probe() {
  docker exec "$postgres_container" psql --username postgres --dbname economy_ci --tuples-only --command 'SELECT 1;' >/dev/null 2>&1
}
wait_for_consecutive_successes app_postgres_probe 2 90 1

postgres_mapping="$(docker port "$postgres_container" '5432/tcp')"
[[ "$postgres_mapping" =~ :([0-9]+)$ ]] || economy_gate_error "Could not resolve disposable PostgreSQL port from '$postgres_mapping'"
postgres_port="${BASH_REMATCH[1]}"
connection_string="Host=127.0.0.1;Port=$postgres_port;Database=economy_ci;Username=postgres;Password=postgres;Include Error Detail=true"

gate_stage='postgres-economy-tests'
run docker run --detach --rm --name "$economy_postgres_container" \
  --env POSTGRES_DB=economy_tests \
  --env POSTGRES_USER=postgres \
  --env POSTGRES_PASSWORD=postgres \
  --tmpfs /var/lib/postgresql/data:rw \
  --publish 127.0.0.1::5432 \
  postgres:17-alpine >/dev/null

economy_postgres_probe() {
  docker exec "$economy_postgres_container" psql --username postgres --dbname economy_tests --tuples-only --command 'SELECT 1;' >/dev/null 2>&1
}
wait_for_consecutive_successes economy_postgres_probe 2 90 1

economy_postgres_mapping="$(docker port "$economy_postgres_container" '5432/tcp')"
[[ "$economy_postgres_mapping" =~ :([0-9]+)$ ]] || economy_gate_error "Could not resolve Economy test PostgreSQL port from '$economy_postgres_mapping'"
economy_postgres_port="${BASH_REMATCH[1]}"
economy_connection_string="Host=127.0.0.1;Port=$economy_postgres_port;Database=economy_tests;Username=postgres;Password=postgres;Include Error Detail=true"
export ECONOMY_POSTGRES_CONNECTION="$economy_connection_string"
export ConnectionStrings__DefaultConnection="$connection_string"
export ConnectionStrings__AuthenticationDb="$connection_string"
export ConnectionStrings__MigrationConnection="$connection_string"
export Database__FailStartupOnMigrationFailure=true
export SeedData__ImportSnapshotCourses=false

if [[ "$gate_profile" == full ]]; then
  gate_stage='postgres-whole-solution-migrations'
  whole_solution_postgres_container="gameguild-economy-ci-whole-solution-$$-$RANDOM"
  run docker run --detach --rm --name "$whole_solution_postgres_container" \
    --env POSTGRES_DB=whole_solution_tests \
    --env POSTGRES_USER=postgres \
    --env POSTGRES_PASSWORD=postgres \
    --tmpfs /var/lib/postgresql/data:rw \
    --publish 127.0.0.1::5432 \
    postgres:17-alpine >/dev/null

  whole_solution_postgres_probe() {
    docker exec "$whole_solution_postgres_container" psql --username postgres --dbname whole_solution_tests \
      --tuples-only --command 'SELECT 1;' >/dev/null 2>&1
  }
  wait_for_consecutive_successes whole_solution_postgres_probe 2 90 1

  whole_solution_postgres_mapping="$(docker port "$whole_solution_postgres_container" '5432/tcp')"
  [[ "$whole_solution_postgres_mapping" =~ :([0-9]+)$ ]] || \
    economy_gate_error "Could not resolve whole-solution PostgreSQL port from '$whole_solution_postgres_mapping'"
  whole_solution_postgres_port="${BASH_REMATCH[1]}"
  whole_solution_connection_string="Host=127.0.0.1;Port=$whole_solution_postgres_port;Database=whole_solution_tests;Username=postgres;Password=postgres;Include Error Detail=true"
fi

probe_sql='SELECT 1;'
[[ "${ECONOMY_CI_PROBE_POSTGRES_FAILURE:-0}" != '1' ]] || probe_sql='SELECT 1 / 0;'
run docker exec "$postgres_container" psql --username postgres --dbname economy_ci --set ON_ERROR_STOP=1 --command "$probe_sql"
run docker exec "$economy_postgres_container" psql --username postgres --dbname economy_tests --set ON_ERROR_STOP=1 --command "$probe_sql"
printf 'app_database=economy_ci\napp_port=%s\neconomy_test_database=economy_tests\neconomy_test_port=%s\n' \
  "$postgres_port" "$economy_postgres_port" > "$artifact_root/postgres/connection.txt"
if [[ -n "$whole_solution_connection_string" ]]; then
  printf 'whole_solution_test_database=whole_solution_tests\nwhole_solution_test_port=%s\n' \
    "$whole_solution_postgres_port" >> "$artifact_root/postgres/connection.txt"
fi

{
  garage_container='gameguild-economy-ci-garage-'$$-$RANDOM
  garage_config="$(native_path "$repository_root/scripts/garage/garage.toml")"
  run env MSYS_NO_PATHCONV=1 docker run --detach --rm --name "$garage_container" \
    --env GARAGE_CONFIG_FILE=/etc/garage/garage.toml \
    --volume "$garage_config:/etc/garage/garage.toml:ro" \
    --publish 127.0.0.1::3900 \
    --publish 127.0.0.1::3903 \
    dxflrs/garage:v2.3.0 >/dev/null

  garage_s3_mapping="$(docker port "$garage_container" '3900/tcp')"
  [[ "$garage_s3_mapping" =~ :([0-9]+)$ ]] || economy_gate_error "Could not resolve disposable Garage S3 port from '$garage_s3_mapping'"
  garage_s3_port="${BASH_REMATCH[1]}"
  garage_admin_mapping="$(docker port "$garage_container" '3903/tcp')"
  [[ "$garage_admin_mapping" =~ :([0-9]+)$ ]] || economy_gate_error "Could not resolve disposable Garage admin port from '$garage_admin_mapping'"
  garage_admin_port="${BASH_REMATCH[1]}"

  run env GARAGE_HOST=127.0.0.1 \
    GARAGE_ADMIN_PORT="$garage_admin_port" \
    GARAGE_ADMIN_TOKEN=development-garage-admin-token \
    GARAGE_S3_BUCKET=assets \
    GARAGE_KEY_ID=GK333333333333333333333333 \
    GARAGE_KEY_SECRET=2222222222222222222222222222222222222222222222222222222222222222 \
    sh scripts/garage/init.sh

  export S3_SERVICE_URL="http://127.0.0.1:$garage_s3_port"
  export GARAGE_ADMIN_URL="http://127.0.0.1:$garage_admin_port"
  export GARAGE_ADMIN_TOKEN=development-garage-admin-token
  export S3_BUCKET=assets
  export S3_ACCESS_KEY=GK333333333333333333333333
  export S3_SECRET_KEY=2222222222222222222222222222222222222222222222222222222222222222
  export S3_REGION=garage
}

gate_stage='build'
run dotnet restore apps/api/GameGuild.sln --nologo
run dotnet tool restore
if [[ "$gate_profile" == full ]]; then
  run dotnet build apps/api/GameGuild.sln -c Release --no-restore --nologo --verbosity minimal \
    "${dotnet_build_isolation[@]}" \
    -p:TreatWarningsAsErrors=true
else
  for record in "${economy_coverage_records[@]}"; do
    IFS=$'\t' read -r test_project _ _ _ <<< "$record"
    run dotnet build "$test_project" -c Release --no-restore --nologo --verbosity minimal \
      "${dotnet_build_isolation[@]}" \
      -p:TreatWarningsAsErrors=true
  done
fi

gate_stage='postgres-economy-template'
economy_template_database='economy_tests_template'
economy_template_connection="Host=127.0.0.1;Port=$economy_postgres_port;Database=$economy_template_database;Username=postgres;Password=postgres;Include Error Detail=true"
run docker exec "$economy_postgres_container" createdb --username postgres "$economy_template_database"
run dotnet ef database update \
  --project apps/api/Source/GameGuild.API/GameGuild.API.csproj \
  --startup-project apps/api/Source/GameGuild.API/GameGuild.API.csproj \
  --context ApplicationDbContext \
  --configuration Release \
  --no-build \
  --connection "$economy_template_connection"
run docker exec "$economy_postgres_container" psql --username postgres --dbname postgres \
  --set ON_ERROR_STOP=1 --command "ALTER DATABASE \"$economy_template_database\" IS_TEMPLATE true;"
export ECONOMY_POSTGRES_TEMPLATE_DATABASE="$economy_template_database"
printf 'economy_template_database=%s\n' "$economy_template_database" >> "$artifact_root/postgres/connection.txt"

test_hang_arguments=(
  --blame-hang-timeout "$test_hang_timeout"
  --blame-hang-dump-type mini
)

for record in "${economy_coverage_records[@]}"; do
  gate_stage='economy-tests'
  IFS=$'\t' read -r test_project assemblies_csv path_prefixes_csv minimum_branch_rate <<< "$record"
  [[ "$path_prefixes_csv" == '__all__' ]] && path_prefixes_csv=''
  test_name="$(basename "${test_project%.csproj}")"
  results="$artifact_root/trx/economy/$test_name"
  mkdir -p "$results"
  IFS=',' read -ra coverage_assemblies <<< "$assemblies_csv"
  include=''
  for assembly in "${coverage_assemblies[@]}"; do
    [[ -z "$include" ]] || include+=','
    include+="[$assembly]*"
  done
  run_test_with_timeout dotnet test "$test_project" -c Release --no-build --nologo "${test_hang_arguments[@]}" \
    --logger "trx;LogFileName=$test_name.trx" \
    --results-directory "$results" \
    --collect 'XPlat Code Coverage' -- \
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura' \
    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=$include" \
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute=CompilerGenerated,GeneratedCodeAttribute'
  assert_trx_evidence "$results/$test_name.trx" >/dev/null
  coverage_report="$(find "$results" -type f -name 'coverage.cobertura.xml' -print -quit)"
  [[ -n "$coverage_report" ]] || economy_gate_error "Coverage report was not produced for $test_project"
  coverage_destination="$artifact_root/coverage/$test_name.cobertura.xml"
  cp "$coverage_report" "$coverage_destination"
  for assembly in "${coverage_assemblies[@]}"; do
    assert_cobertura_coverage "$coverage_destination" "$assembly" "$path_prefixes_csv" "$minimum_branch_rate" >> "$artifact_root/coverage/summary.jsonl"
  done
done

if [[ "${ECONOMY_CI_PROBE_COVERAGE_FAILURE:-0}" == '1' ]]; then
  lowered="$artifact_root/coverage/lowered.cobertura.xml"
  printf '<coverage><packages><package name="GameGuild.Finance.Economy.Probe" line-rate="0.99" branch-rate="1"><classes><class name="Probe"><methods><method name="Covered"><lines><line number="1" hits="1" /></lines></method></methods></class></classes></package></packages></coverage>\n' > "$lowered"
  assert_cobertura_coverage "$lowered" 'GameGuild.Finance.Economy.Probe' >/dev/null
fi

run_whole_solution_test_project() {
  local test_project="$1" whole_solution_results="$2"
  local test_name results project_log project_timeout="$test_hang_timeout"
  local -a test_environment=()

  test_name="$(basename "${test_project%.csproj}")"
  results="$whole_solution_results/$test_name"
  project_log="$results/dotnet-test.log"
  mkdir -p "$results"
  if [[ "$test_name" == 'GameGuild.API.UnitTests' ]]; then
    [[ -n "$whole_solution_connection_string" ]] || \
      economy_gate_error 'The API migration tests require their isolated whole-solution PostgreSQL server'
    test_environment=(
      env
      ECONOMY_POSTGRES_CONNECTION="$whole_solution_connection_string"
      ECONOMY_POSTGRES_TEMPLATE_DATABASE=
    )
    project_timeout="$api_test_timeout"
  fi
  run_logged "$project_log" timeout --kill-after=30s "$project_timeout" \
    "${test_environment[@]}" \
    dotnet test "$test_project" -c Release --no-build --nologo --verbosity minimal -m:1 "${test_hang_arguments[@]}" \
    --logger "trx;LogFileName=$test_name.trx" \
    --results-directory "$results"
  [[ -f "$results/$test_name.trx" ]] || economy_gate_error "Whole-solution test project produced no TRX: $test_project"
}

wait_for_whole_solution_batch() {
  local worker_pid worker_status=0

  for worker_pid in "${whole_solution_worker_pids[@]}"; do
    if ! wait "$worker_pid"; then
      worker_status=1
    fi
  done
  whole_solution_worker_pids=()
  return "$worker_status"
}

if [[ "$gate_profile" == full ]]; then
  gate_stage='whole-solution-tests'
  whole_solution_results="$artifact_root/trx/whole-solution"
  whole_solution_log="$whole_solution_results/dotnet-test.log"
  : > "$whole_solution_log"
  mapfile -t whole_solution_candidates < <(
    dotnet sln apps/api/GameGuild.sln list |
      awk 'NR > 2 && /Tests\.csproj$/ { print "apps/api/" $0 }' |
      tr '\134' '/'
  )
  whole_solution_projects=()
  for test_project in "${whole_solution_candidates[@]}"; do
    whole_solution_test_project "$test_project" || continue
    economy_test_project "$test_project" && continue
    whole_solution_projects+=("$test_project")
  done
  ((${#whole_solution_projects[@]} > 0)) || economy_gate_error 'The solution does not contain test projects'

  # Run isolated VSTest hosts in bounded batches. This releases state per assembly
  # without serializing the entire solution on Linux CI.
  for test_project in "${whole_solution_projects[@]}"; do
    run_whole_solution_test_project "$test_project" "$whole_solution_results" &
    whole_solution_worker_pids+=("$!")
    if ((${#whole_solution_worker_pids[@]} >= whole_solution_jobs)); then
      wait_for_whole_solution_batch
    fi
  done
  wait_for_whole_solution_batch
  while IFS= read -r project_log; do
    cat "$project_log" >> "$whole_solution_log"
  done < <(find "$whole_solution_results" -mindepth 2 -type f -name 'dotnet-test.log' -print | LC_ALL=C sort)
  mapfile -t whole_solution_trx < <(find "$whole_solution_results" -type f -name '*.trx' -print | LC_ALL=C sort)
  assert_whole_solution_evidence "$repository_root" "$whole_solution_log" "${whole_solution_trx[@]}" >/dev/null
fi

{
  gate_stage='provider-contract-tests'
  for record in "${provider_contracts[@]}"; do
    IFS=$'\t' read -r project filter <<< "$record"
    name="$(basename "${project%.csproj}")"
    results="$artifact_root/trx/provider/$name"
    mkdir -p "$results"
    provider_build_arguments=(--no-restore)
    [[ "$gate_profile" != full ]] || provider_build_arguments=(--no-build --no-restore)
    run_test_with_timeout dotnet test "$project" -c Release "${provider_build_arguments[@]}" --nologo "${test_hang_arguments[@]}" \
      --filter "$filter" \
      --logger "trx;LogFileName=$name.trx" \
      --results-directory "$results"
    assert_trx_evidence "$results/$name.trx" >/dev/null
  done
}

if [[ "$gate_profile" == full ]]; then
{
  gate_stage='openapi-client'
  publish_directory="$artifact_root/publish/api"
  # Some API project references are intentionally not solution members. Build
  # during publish so their output is always materialized before packaging.
  run dotnet publish apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release --no-restore --nologo --output "$publish_directory" \
    "${dotnet_build_isolation[@]}" \
    -p:TreatWarningsAsErrors=true
  api_port="$(get_ephemeral_port)"
  export ASPNETCORE_ENVIRONMENT=Development
  export ASPNETCORE_URLS="http://127.0.0.1:$api_port"
  export API_URL="http://127.0.0.1:$api_port"
  export PaymentGateways__Stripe__IsEnabled=true
  export PaymentGateways__Stripe__UseSimulation=true
  dotnet "$publish_directory/GameGuild.API.dll" --contentRoot "$publish_directory" \
    >"$artifact_root/api/stdout.log" 2>"$artifact_root/api/stderr.log" &
  api_pid=$!
  wait_http_ready "http://127.0.0.1:$api_port/live" "$api_pid" 90
  run curl --fail --silent --show-error "http://127.0.0.1:$api_port/ready" --output "$artifact_root/api/ready.json"

  raw_openapi="$artifact_root/openapi/openapi.raw.json"
  captured_openapi="$artifact_root/openapi/openapi.json"
  run curl --fail --silent --show-error "http://127.0.0.1:$api_port/swagger/v1/swagger.json" --output "$raw_openapi"
  canonicalize_json "$raw_openapi" "$captured_openapi"
  run bash "$script_dir/verify-openapi-client.sh" "$captured_openapi"
}

{
  gate_stage='frontend'
  client_evidence="$artifact_root/vitest/client.json"
  run "${pnpm_command[@]}" --filter @game-guild/client exec vitest run --reporter=json "--outputFile=$client_evidence"
  assert_vitest_evidence "$client_evidence" >/dev/null
  run "${pnpm_command[@]}" --filter @game-guild/client build

  web_evidence="$artifact_root/vitest/web.json"
  run env -u API_URL "${pnpm_command[@]}" --filter @game-guild/web exec vitest run --maxWorkers=4 --reporter=json "--outputFile=$web_evidence"
  assert_vitest_evidence "$web_evidence" >/dev/null
  economy_web_evidence="$artifact_root/vitest/economy-coverage.json"
  ECONOMY_WEB_COVERAGE_DIR="$artifact_root/coverage/web" run env -u API_URL \
    "${pnpm_command[@]}" --filter @game-guild/web run test:economy:coverage \
    --reporter=json "--outputFile=$economy_web_evidence"
  assert_vitest_evidence "$economy_web_evidence" >/dev/null
  run "${pnpm_command[@]}" --filter @game-guild/web run build:emception-runtime-dependencies
  run "${pnpm_command[@]}" --filter @game-guild/web run sync:emception
  GAMEGUILD_DISABLE_WEBPACK_CACHE=1 run "${pnpm_command[@]}" --filter @game-guild/web exec next build --webpack
}

{
  gate_stage='browser'
  web_port="$(get_ephemeral_port)"
  playwright_evidence="$artifact_root/playwright/public-smoke.json"
  standalone_web_root="$repository_root/apps/web/.next/standalone/apps/web"
  mkdir -p "$standalone_web_root/.next"
  cp -R "$repository_root/apps/web/public" "$standalone_web_root/public"
  cp -R "$repository_root/apps/web/.next/static" "$standalone_web_root/.next/static"
  export PORT="$web_port"
  export HOSTNAME=0.0.0.0
  export PUBLIC_E2E_BASE_URL="http://127.0.0.1:$web_port"
  export PLAYWRIGHT_JSON_OUTPUT_NAME="$(native_path "$playwright_evidence")"
  export AUTH_SECRET='economy-ci-browser-secret-not-for-production-use-2026'
  node "$standalone_web_root/server.js" \
    >"$artifact_root/playwright/web.stdout.log" 2>"$artifact_root/playwright/web.stderr.log" &
  web_pid=$!
  wait_http_ready "http://127.0.0.1:$web_port/" "$web_pid" 90
  run "${pnpm_command[@]}" --filter @game-guild/web test:browser:public
  assert_playwright_evidence "$playwright_evidence" >/dev/null
  economy_playwright_evidence="$artifact_root/playwright/economy-browser.json"
  export PLAYWRIGHT_JSON_OUTPUT_NAME="$(native_path "$economy_playwright_evidence")"
  run "${pnpm_command[@]}" --filter @game-guild/web test:browser:economy
  assert_playwright_evidence "$economy_playwright_evidence" >/dev/null
}
fi

evidence_count="$(find "$artifact_root" -type f | wc -l | tr -d ' ')"
((evidence_count > 0)) || economy_gate_error "No verification evidence was written under $artifact_root"
gate_stage='completed'
printf 'Economy verification passed with %s evidence files under %s\n' "$evidence_count" "$artifact_root"
