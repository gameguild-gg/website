#!/usr/bin/env bash
set -Eeuo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd "$script_dir/../.." && pwd)"
artifact_root="$repository_root/artifacts/test-results"
manifest_path="$script_dir/economy-projects.json"

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
    source_file="$(find "$project_directory" -type f -name '*.cs' \
      ! -path '*/bin/*' ! -path '*/obj/*' -print -quit)"
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

skip_whole_solution=false
skip_provider_contracts=false
skip_openapi=false
skip_frontend=false
skip_browser=false

while (($#)); do
  case "$1" in
    --skip-whole-solution) skip_whole_solution=true ;;
    --skip-provider-contracts) skip_provider_contracts=true ;;
    --skip-openapi) skip_openapi=true ;;
    --skip-frontend) skip_frontend=true ;;
    --skip-browser) skip_browser=true ;;
    *) printf 'Unknown argument: %s\n' "$1" >&2; exit 2 ;;
  esac
  shift
done

api_pid=''
web_pid=''
postgres_container=''

cleanup() {
  local status=$?
  trap - EXIT INT TERM
  set +e
  stop_process_tree "$web_pid"
  stop_process_tree "$api_pid"
  if [[ -n "$postgres_container" ]]; then
    docker rm --force "$postgres_container" >/dev/null 2>&1 || true
  fi
  exit "$status"
}
trap cleanup EXIT INT TERM

run() {
  printf '> '
  printf '%q ' "$@"
  printf '\n'
  "$@"
}

run_logged() {
  local output_path="$1"
  shift
  printf '> '
  printf '%q ' "$@"
  printf '\n'
  set +e
  "$@" 2>&1 | tee "$output_path"
  local command_status=${PIPESTATUS[0]}
  set -e
  return "$command_status"
}

native_path() {
  if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
    cygpath -w "$1"
  else
    printf '%s\n' "$1"
  fi
}

cd "$repository_root"
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

run bash "$script_dir/tests/verify-economy.sh"
run pnpm test:smoke
assert_economy_manifest "$repository_root" "$manifest_path"

declare -a economy_production=()
declare -a economy_tests=()
declare -a economy_coverage_records=()
declare -a provider_contracts=()
while IFS=$'\t' read -r record_type first second third; do
  record_type="$(normalize_shell_record_field "$record_type")"
  first="$(normalize_shell_record_field "$first")"
  second="$(normalize_shell_record_field "$second")"
  third="$(normalize_shell_record_field "$third")"
  case "$record_type" in
    production) economy_production+=("$first") ;;
    test) economy_tests+=("$first") ;;
    coverage) economy_coverage_records+=("$first"$'\t'"$second"$'\t'"$third") ;;
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
    prefixes = ",".join(value.replace("\\", "/") for value in entry.get("coveragePathPrefixes", []))
    for test in entry.get("testProjects", []):
        normalized = test.replace("\\", "/")
        print("test", normalized, sep="\t")
        print("coverage", normalized, assemblies, prefixes, sep="\t")
for contract in manifest.get("providerContractProjects", []):
    print("provider", contract["project"].replace("\\", "/"), contract["filter"], sep="\t")
PY
)

postgres_container="gameguild-economy-ci-$$-$RANDOM"
run docker run --detach --rm --name "$postgres_container" \
  --env POSTGRES_DB=economy_ci \
  --env POSTGRES_USER=postgres \
  --env POSTGRES_PASSWORD=postgres \
  --publish 127.0.0.1::5432 \
  postgres:17-alpine >/dev/null

postgres_probe() {
  docker exec "$postgres_container" psql --username postgres --dbname economy_ci --tuples-only --command 'SELECT 1;' >/dev/null 2>&1
}
wait_for_consecutive_successes postgres_probe 2 90 1

postgres_mapping="$(docker port "$postgres_container" '5432/tcp')"
[[ "$postgres_mapping" =~ :([0-9]+)$ ]] || economy_gate_error "Could not resolve disposable PostgreSQL port from '$postgres_mapping'"
postgres_port="${BASH_REMATCH[1]}"
connection_string="Host=127.0.0.1;Port=$postgres_port;Database=economy_ci;Username=postgres;Password=postgres;Include Error Detail=true"
export ECONOMY_POSTGRES_CONNECTION="$connection_string"
export ConnectionStrings__DefaultConnection="$connection_string"
export ConnectionStrings__AuthenticationDb="$connection_string"
export ConnectionStrings__MigrationConnection="$connection_string"
export Database__FailStartupOnMigrationFailure=true
export SeedData__ImportSnapshotCourses=false

probe_sql='SELECT 1;'
[[ "${ECONOMY_CI_PROBE_POSTGRES_FAILURE:-0}" != '1' ]] || probe_sql='SELECT 1 / 0;'
run docker exec "$postgres_container" psql --username postgres --dbname economy_ci --set ON_ERROR_STOP=1 --command "$probe_sql"
printf 'database=economy_ci\nport=%s\n' "$postgres_port" > "$artifact_root/postgres/connection.txt"

run dotnet restore apps/api/GameGuild.sln --nologo
run dotnet build apps/api/GameGuild.sln -c Release --no-restore --nologo --verbosity minimal

base_sha="${ECONOMY_BASE_SHA:-}"
if [[ -z "$base_sha" ]]; then
  base_sha="$(git merge-base HEAD develop 2>/dev/null || git rev-parse HEAD~1)"
fi
[[ -n "$base_sha" ]] || economy_gate_error 'Unable to determine warning-scope base SHA'

mapfile -t changed_paths < <(git diff --name-only "$base_sha...HEAD")
mapfile -t touched_commerce < <(get_touched_commerce_projects "$repository_root" "${changed_paths[@]}")
warning_projects=("${economy_production[@]}" "${economy_tests[@]}" "${touched_commerce[@]}")

if [[ "${ECONOMY_CI_PROBE_WARNING_FAILURE:-0}" == '1' ]]; then
  warning_fixture="$artifact_root/warning-fixture"
  mkdir -p "$warning_fixture"
  printf '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n' > "$warning_fixture/WarningFixture.csproj"
  printf 'namespace EconomyGateProbe; public static class WarningFixture { public static void Emit() { int intentionallyUnused = 1; } }\n' > "$warning_fixture/WarningFixture.cs"
  run dotnet restore "$warning_fixture/WarningFixture.csproj" --nologo
  warning_projects+=("$warning_fixture/WarningFixture.csproj")
fi

if ((${#warning_projects[@]} > 0)); then
  mapfile -t warning_projects < <(printf '%s\n' "${warning_projects[@]}" | awk 'NF' | LC_ALL=C sort -u)
  for project in "${warning_projects[@]}"; do
    run dotnet build "$project" -c Release --no-restore --nologo \
      -p:BuildProjectReferences=false \
      -p:TreatWarningsAsErrors=true \
      -p:EnableNETAnalyzers=true \
      -p:EnforceCodeStyleInBuild=true
  done
fi

for record in "${economy_coverage_records[@]}"; do
  IFS=$'\t' read -r test_project assemblies_csv path_prefixes_csv <<< "$record"
  test_name="$(basename "${test_project%.csproj}")"
  results="$artifact_root/trx/economy/$test_name"
  mkdir -p "$results"
  IFS=',' read -ra coverage_assemblies <<< "$assemblies_csv"
  include=''
  for assembly in "${coverage_assemblies[@]}"; do
    [[ -z "$include" ]] || include+=','
    include+="[$assembly]*"
  done
  run dotnet test "$test_project" -c Release --no-build --nologo \
    --logger "trx;LogFileName=$test_name.trx" \
    --results-directory "$results" \
    --collect 'XPlat Code Coverage' -- \
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura' \
    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=$include"
  assert_trx_evidence "$results/$test_name.trx" >/dev/null
  coverage_report="$(find "$results" -type f -name 'coverage.cobertura.xml' -print -quit)"
  [[ -n "$coverage_report" ]] || economy_gate_error "Coverage report was not produced for $test_project"
  coverage_destination="$artifact_root/coverage/$test_name.cobertura.xml"
  cp "$coverage_report" "$coverage_destination"
  for assembly in "${coverage_assemblies[@]}"; do
    assert_cobertura_coverage "$coverage_destination" "$assembly" "$path_prefixes_csv" >> "$artifact_root/coverage/summary.jsonl"
  done
done

if [[ "${ECONOMY_CI_PROBE_COVERAGE_FAILURE:-0}" == '1' ]]; then
  lowered="$artifact_root/coverage/lowered.cobertura.xml"
  printf '<coverage><packages><package name="GameGuild.Economy.Probe" line-rate="0.99" branch-rate="1"><classes><class name="Probe"><methods><method name="Covered"><lines><line number="1" hits="1" /></lines></method></methods></class></classes></package></packages></coverage>\n' > "$lowered"
  assert_cobertura_coverage "$lowered" 'GameGuild.Economy.Probe' >/dev/null
fi

if [[ "$skip_whole_solution" == false ]]; then
  whole_solution_results="$artifact_root/trx/whole-solution"
  whole_solution_log="$whole_solution_results/dotnet-test.log"
  run_logged "$whole_solution_log" dotnet test apps/api/GameGuild.sln -c Release --no-build --nologo --verbosity minimal -m:1 \
    --logger 'trx;LogFilePrefix=whole-solution' \
    --results-directory "$whole_solution_results"
  shopt -s nullglob
  whole_solution_trx=("$whole_solution_results"/*.trx)
  shopt -u nullglob
  assert_whole_solution_evidence "$repository_root" "$whole_solution_log" "${whole_solution_trx[@]}" >/dev/null
fi

if [[ "$skip_provider_contracts" == false ]]; then
  for record in "${provider_contracts[@]}"; do
    IFS=$'\t' read -r project filter <<< "$record"
    name="$(basename "${project%.csproj}")"
    results="$artifact_root/trx/provider/$name"
    mkdir -p "$results"
    run dotnet test "$project" -c Release --no-restore --nologo \
      --filter "$filter" \
      --logger "trx;LogFileName=$name.trx" \
      --results-directory "$results"
    assert_trx_evidence "$results/$name.trx" >/dev/null
  done
fi

if [[ "$skip_openapi" == false ]]; then
  publish_directory="$artifact_root/publish/api"
  run dotnet publish apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release --no-restore --nologo --output "$publish_directory"
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
fi

if [[ "$skip_frontend" == false ]]; then
  client_evidence="$artifact_root/vitest/client.json"
  run pnpm --filter @game-guild/client exec vitest run --reporter=json "--outputFile=$client_evidence"
  assert_vitest_evidence "$client_evidence" >/dev/null
  run pnpm --filter @game-guild/client build

  web_evidence="$artifact_root/vitest/web.json"
  run env -u API_URL pnpm --filter @game-guild/web exec vitest run --reporter=json "--outputFile=$web_evidence"
  assert_vitest_evidence "$web_evidence" >/dev/null
  GAMEGUILD_DISABLE_WEBPACK_CACHE=1 run pnpm --filter @game-guild/web build
fi

if [[ "$skip_browser" == false ]]; then
  web_port="$(get_ephemeral_port)"
  playwright_evidence="$artifact_root/playwright/public-smoke.json"
  export PORT="$web_port"
  export HOSTNAME=127.0.0.1
  export PUBLIC_E2E_BASE_URL="http://127.0.0.1:$web_port"
  export PLAYWRIGHT_JSON_OUTPUT_NAME="$(native_path "$playwright_evidence")"
  export AUTH_SECRET='economy-ci-browser-secret-not-for-production-use-2026'
  node "$repository_root/apps/web/node_modules/next/dist/bin/next" start "$repository_root/apps/web" \
    --hostname 127.0.0.1 --port "$web_port" \
    >"$artifact_root/playwright/web.stdout.log" 2>"$artifact_root/playwright/web.stderr.log" &
  web_pid=$!
  wait_http_ready "http://127.0.0.1:$web_port/" "$web_pid" 90
  run pnpm --filter @game-guild/web test:browser:public
  assert_playwright_evidence "$playwright_evidence" >/dev/null
fi

evidence_count="$(find "$artifact_root" -type f | wc -l | tr -d ' ')"
((evidence_count > 0)) || economy_gate_error "No verification evidence was written under $artifact_root"
printf 'Economy verification passed with %s evidence files under %s\n' "$evidence_count" "$artifact_root"
