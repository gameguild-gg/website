#!/usr/bin/env bash
set -uo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ci_dir="$(cd "$script_dir/.." && pwd)"
repository_root="$(cd "$ci_dir/../.." && pwd)"

if [[ ! -f "$ci_dir/economy-gate.sh" ]]; then
  printf 'FAIL shell gate library is missing: %s\n' "$ci_dir/economy-gate.sh" >&2
  exit 1
fi

# shellcheck source=../economy-gate.sh
source "$ci_dir/economy-gate.sh"

declare -a pnpm_command=(pnpm)

passed=0
failed=0
fixture_root="$(mktemp -d "${TMPDIR:-/tmp}/economy-gate.XXXXXX")"
trap 'rm -rf "$fixture_root"' EXIT

assert_equal() {
  local actual="$1" expected="$2" because="$3"
  [[ "$actual" == "$expected" ]] || {
    printf "Expected '%s' but got '%s': %s\n" "$expected" "$actual" "$because" >&2
    return 1
  }
}

assert_throws() {
  local pattern="$1"
  shift
  local output status
  set +e
  output="$({ "$@"; } 2>&1)"
  status=$?
  set -e
  [[ $status -ne 0 ]] || {
    printf "Expected failure matching '%s', but command succeeded\n" "$pattern" >&2
    return 1
  }
  [[ "$output" =~ $pattern ]] || {
    printf "Expected failure matching '%s', got: %s\n" "$pattern" "$output" >&2
    return 1
  }
}

run_test() {
  local name="$1" function_name="$2"
  if "$function_name"; then
    passed=$((passed + 1))
    printf 'PASS %s\n' "$name"
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n' "$name"
  fi
}

test_shell_only_ci_policy() {
  ! find "$ci_dir" -type f \( -name '*.ps1' -o -name '*.psm1' \) -print -quit | grep -q . || return 1
  ! grep -RqiE 'pwsh|powershell|\.ps1|\.psm1' "$repository_root/.github/workflows" || return 1
  grep -q '"ci:dependencies": "bash scripts/ci/install-and-audit-pnpm.sh"' "$repository_root/package.json" || return 1
  grep -q '"ci:repository-policy": "bash scripts/ci/verify-repository-policy.sh"' "$repository_root/package.json" || return 1
  grep -q '"ci:economy": "bash scripts/ci/verify-economy.sh"' "$repository_root/package.json" || return 1
  grep -Fq 'pnpm install --frozen-lockfile --ignore-scripts' "$ci_dir/install-and-audit-pnpm.sh" || return 1
  grep -Fq 'repository pnpm lockfile is required' "$ci_dir/install-and-audit-pnpm.sh" || return 1
  grep -q 'pnpm audit --json' "$ci_dir/install-and-audit-pnpm.sh" || return 1
  grep -Fq 'pnpm install --frozen-lockfile --ignore-scripts' "$repository_root/.github/workflows/emception.yml" || return 1
  ! grep -Fq 'pnpm-lock.yaml|*/pnpm-lock.yaml' "$repository_root/scripts/repository-hygiene.sh" || return 1
  [[ -f "$repository_root/pnpm-lock.yaml" ]]
}

test_contributors_visualization_uses_native_xvfb() {
  local workflow="$repository_root/.github/workflows/contributors.yml"

  grep -Fq 'sudo apt-get install -y --fix-missing ffmpeg gource xvfb' "$workflow" || return 1
  grep -Fq 'xvfb-run --auto-servernum env OUTPUT_DIR="${RUNNER_TEMP}/gameguild-gource" ./contributors/gource.sh' "$workflow" || return 1
  ! grep -Fq 'coactions/setup-xvfb' "$workflow"
}

test_release_flow_opens_version_pr_to_main() {
  local emception_workflow="$repository_root/.github/workflows/emception.yml"

  [[ ! -e "$repository_root/.github/workflows/release.yml" ]] || return 1
  grep -Fq 'uses: changesets/action@v2' "$emception_workflow" || return 1
  grep -Fq 'version-script: pnpm run version:emception' "$emception_workflow" || return 1
  grep -Fq 'run: pnpm run publish:emception' "$emception_workflow" || return 1
  grep -Fq 'TAG="emception-v${VERSION}"' "$emception_workflow" || return 1
  ! grep -Fq 'auto-changeset.mjs --apply' "$emception_workflow"
}

test_repository_policy_runs_in_a_parallel_required_gate() {
  local workflow="$repository_root/.github/workflows/pr-verify.yml"

  grep -Fq 'repository-policy:' "$workflow" || return 1
  grep -Fq 'name: Repository policy' "$workflow" || return 1
  grep -Fq 'run: bash scripts/ci/verify-repository-policy.sh' "$workflow" || return 1
  grep -Fq 'needs: [classify, repository-policy,' "$workflow"
}

test_workflow_uses_fast_pr_and_full_release_economy_profiles() {
  local pr_workflow="$repository_root/.github/workflows/pr-verify.yml"
  local nightly_workflow="$repository_root/.github/workflows/nightly-full.yml"

  grep -Fq 'name: Economy Release Gate' "$pr_workflow" || return 1
  grep -Fq "needs.classify.outputs.economyCritical == 'true'" "$pr_workflow" || return 1
  grep -Fq 'ECONOMY_GATE_PROFILE: full' "$pr_workflow" || return 1
  grep -Fq 'cron: "17 3 * * *"' "$nightly_workflow" || return 1
  grep -Fq 'ECONOMY_GATE_PROFILE: full' "$nightly_workflow"
}

test_workflow_caches_gate_dependencies() {
  local node_setup="$repository_root/.github/actions/setup-node-workspace/action.yml"
  local dotnet_setup="$repository_root/.github/actions/setup-dotnet/action.yml"
  local playwright_setup="$repository_root/.github/actions/setup-playwright/action.yml"

  grep -Fq 'cache: pnpm' "$node_setup" || return 1
  grep -Fq 'actions/cache@v6' "$dotnet_setup" || return 1
  grep -Fq 'key: nuget-' "$dotnet_setup" || return 1
  grep -Fq 'actions/cache@v6' "$playwright_setup" || return 1
  grep -Fq 'playwright-browsers-' "$playwright_setup"
}

test_economy_preflight_has_no_repository_policy_side_effect() {
  local gate="$ci_dir/verify-economy.sh"

  ! grep -Fq 'tests/verify-economy.sh' "$gate" || return 1
  grep -Fq 'artifact_root="$repository_root/artifacts/test-results/economy"' "$gate" || return 1
  ! grep -Fq 'rm -rf "$repository_root/artifacts/test-results"' "$gate" || return 1
  grep -Fq 'preflight-summary.txt' "$gate"
}

test_economy_preflight_avoids_the_pnpm_install_lock() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'run node --test scripts/devops/smoke-check.test.mjs' "$gate" || return 1
  ! grep -Fq 'run "${pnpm_command[@]}" test:smoke' "$gate"
}

test_economy_gate_rejects_stage_skips() {
  local gate="$ci_dir/verify-economy.sh"

  ! grep -Fq -- '--skip-whole-solution' "$gate" || return 1
  ! grep -Fq -- '--skip-provider-contracts' "$gate" || return 1
  ! grep -Fq -- '--skip-openapi' "$gate" || return 1
  ! grep -Fq -- '--skip-frontend' "$gate" || return 1
  ! grep -Fq -- '--skip-browser' "$gate"
}

test_economy_gate_requires_full_web_coverage() {
  local gate="$ci_dir/verify-economy.sh"
  local package_manifest="$repository_root/apps/web/package.json"
  local coverage_config="$repository_root/apps/web/vitest.economy.config.mts"

  grep -Fq '"test:economy:coverage": "vitest run -c vitest.economy.config.mts --coverage"' "$package_manifest" || return 1
  [[ -f "$coverage_config" ]] || return 1
  grep -Fq 'lines: 100' "$coverage_config" || return 1
  grep -Fq 'branches: 100' "$coverage_config" || return 1
  grep -Fq 'functions: 100' "$coverage_config" || return 1
  grep -Fq 'statements: 100' "$coverage_config" || return 1
  grep -Fq 'ECONOMY_WEB_COVERAGE_DIR' "$gate" || return 1
  grep -Fq 'test:economy:coverage' "$gate"
}

test_economy_gate_runs_economy_browser_surface() {
  local gate="$ci_dir/verify-economy.sh"
  local package_manifest="$repository_root/apps/web/package.json"

  grep -Fq '"test:browser:economy": "node scripts/economy-browser-e2e.mjs"' "$package_manifest" || return 1
  [[ -f "$repository_root/apps/web/scripts/economy-browser-e2e.mjs" ]] || return 1
  grep -Fq 'economy-browser.json' "$gate" || return 1
  grep -Fq 'test:browser:economy' "$gate"
}

test_economy_coverage_ignores_only_compiler_generated_members() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq \
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute=CompilerGenerated,GeneratedCodeAttribute' \
    "$gate" || return 1
  grep -Fq 'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=$include' "$gate" || return 1
  ! grep -Fq 'SkipAutoProps' "$gate"
}

test_economy_gate_bounds_hung_tests_and_records_timings() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq -- '--blame-hang-timeout "$test_hang_timeout"' "$gate" || return 1
  grep -Fq -- '--blame-hang-dump-type mini' "$gate" || return 1
  grep -Fq 'run_test_with_timeout()' "$gate" || return 1
  grep -Fq 'timeout --kill-after=30s "$test_hang_timeout"' "$gate" || return 1
  grep -Fq 'timings.jsonl' "$gate"
}

test_economy_gate_supports_fast_pr_and_full_release_profiles() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'case "$gate_profile" in' "$gate" || return 1
  grep -Fq 'pr|full)' "$gate" || return 1
  grep -Fq '[[ "$gate_profile" == full ]]' "$gate"
}

test_economy_gate_batches_whole_solution_tests() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'ECONOMY_WHOLE_SOLUTION_JOBS' "$gate" || return 1
  grep -Fq 'whole_solution_worker_pids' "$gate"
}

test_economy_unit_tests_bound_parallelism_without_global_serialization() {
  local assembly_info="$repository_root/apps/api/tests/GameGuild.Finance.Economy.UnitTests/AssemblyInfo.cs"
  local database_support="$repository_root/apps/api/tests/GameGuild.TestSupport.Finance.Economy/EconomyPostgreSqlTestDatabase.cs"

  grep -Fq '[assembly: CollectionBehavior(MaxParallelThreads = 3)]' "$assembly_info" || return 1
  ! grep -Fq 'DisableTestParallelization = true' "$assembly_info" || return 1
  ! grep -R -Fq --include='*.cs' '[Collection("Economy PostgreSQL")]' \
    "$repository_root/apps/api/tests/GameGuild.Finance.Economy.UnitTests" || return 1
  grep -Fq 'GateRoleBootstrapLock' "$database_support" || return 1
  grep -Fq 'EnsureGateRolesAsync' "$database_support" || return 1
  grep -Fq 'adminBuilder.CommandTimeout = 120;' "$database_support"
}

test_economy_gate_builds_release_targets_before_packaging() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'dotnet build apps/api/GameGuild.sln -c Release --no-restore --nologo --verbosity minimal' "$gate" || return 1
  ! grep -Fq 'dotnet build apps/api/Source/GameGuild.API/GameGuild.API.csproj' "$gate" || return 1
  grep -Fq 'dotnet publish apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release --no-restore' "$gate" || return 1
  ! grep -Fq 'dotnet publish apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release --no-build' "$gate" || return 1
  grep -A3 -F 'dotnet publish apps/api/Source/GameGuild.API/GameGuild.API.csproj' "$gate" | grep -Fq -- '-p:TreatWarningsAsErrors=true' || return 1
  grep -Fq 'provider_build_arguments=(--no-build --no-restore)' "$gate" || return 1
  grep -Fq 'dotnet_build_isolation=(-m:4 -p:UseSharedCompilation=false)' "$gate" || return 1
  grep -Fq -- '-p:TreatWarningsAsErrors=true' "$gate" || return 1
  ! grep -Fq 'warning_projects=' "$gate"
}

test_openapi_gate_uses_semantic_generated_client_diff() {
  local verifier="$ci_dir/verify-openapi-client.sh"

  grep -Fq 'pnpm --filter @game-guild/client generate:diff' "$verifier" || return 1
  ! grep -Fq 'git diff --exit-code -- packages/infrastructure/client/src/generated' "$verifier"
}

test_economy_gate_rejects_nested_postgres_testcontainers() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq "gate_stage='preflight-postgres-isolation'" "$gate" || return 1
  grep -Fq 'ECONOMY_POSTGRES_CONNECTION' "$gate" || return 1
  grep -Fq "grep -RIl --include='*.cs' --exclude-dir=bin --exclude-dir=obj 'new PostgreSqlBuilder' apps/api/tests" "$gate" || return 1
  grep -Fq 'GameGuild.TestSupport.Finance.Economy/' "$gate"
}

test_economy_gate_isolates_global_economy_roles_from_application_databases() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'economy_postgres_container="gameguild-economy-ci-tests-' "$gate" || return 1
  grep -Fq "gate_stage='postgres-economy-tests'" "$gate" || return 1
  grep -Fq -- '--env POSTGRES_DB=economy_tests' "$gate" || return 1
  grep -Fq 'economy_connection_string=' "$gate" || return 1
  grep -Fq 'export ECONOMY_POSTGRES_CONNECTION="$economy_connection_string"' "$gate" || return 1
  grep -Fq 'export ConnectionStrings__DefaultConnection="$connection_string"' "$gate" || return 1
  grep -Fq 'docker rm --force "$economy_postgres_container"' "$gate"
}

test_full_gate_isolates_api_migration_tests_from_the_economy_template() {
  local gate="$ci_dir/verify-economy.sh"
  local runner
  runner="$(sed -n '/run_whole_solution_test_project()/,/wait_for_whole_solution_batch()/p' "$gate")"

  grep -Fq 'whole_solution_postgres_container="gameguild-economy-ci-whole-solution-' "$gate" || return 1
  grep -Fq "gate_stage='postgres-whole-solution-migrations'" "$gate" || return 1
  grep -Fq -- '--env POSTGRES_DB=whole_solution_tests' "$gate" || return 1
  grep -Fq 'docker rm --force "$whole_solution_postgres_container"' "$gate" || return 1
  grep -Fq "[[ \"\$test_name\" == 'GameGuild.API.UnitTests' ]]" <<< "$runner" || return 1
  grep -Fq 'ECONOMY_POSTGRES_CONNECTION="$whole_solution_connection_string"' <<< "$runner" || return 1
  grep -Fq 'ECONOMY_POSTGRES_TEMPLATE_DATABASE=' <<< "$runner" || return 1
  grep -Fq 'project_timeout="$api_test_timeout"' <<< "$runner" || return 1
  grep -Fq 'timeout --kill-after=30s "$project_timeout"' <<< "$runner" || return 1
  grep -Fq 'api_test_timeout="${ECONOMY_API_TEST_TIMEOUT:-12m}"' "$gate" || return 1
  ! grep -Fq -- '--settings' <<< "$runner" || return 1
  ! grep -Fq 'xunit-postgres-serial.runsettings' "$gate"
}

test_economy_gate_uses_memory_backed_disposable_postgres() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq -- '--tmpfs /var/lib/postgresql/data:rw' "$gate" || return 1
  ! grep -Fq -- '--volume' <<< "$(sed -n '/postgres-economy-tests/,/economy_postgres_probe()/p' "$gate")"
}

test_economy_gate_migrates_one_template_and_clones_isolated_test_databases() {
  local gate="$ci_dir/verify-economy.sh"
  local database_support="$repository_root/apps/api/tests/GameGuild.TestSupport.Finance.Economy/EconomyPostgreSqlTestDatabase.cs"

  grep -Fq "gate_stage='postgres-economy-template'" "$gate" || return 1
  grep -Fq "economy_template_database='economy_tests_template'" "$gate" || return 1
  grep -Fq 'run dotnet tool restore' "$gate" || return 1
  grep -Fq 'dotnet ef database update' "$gate" || return 1
  grep -Fq 'export ECONOMY_POSTGRES_TEMPLATE_DATABASE="$economy_template_database"' "$gate" || return 1
  grep -Fq 'ECONOMY_POSTGRES_TEMPLATE_DATABASE' "$database_support" || return 1
  grep -Fq 'WITH TEMPLATE' "$database_support" || return 1
  grep -Fq 'true,' "$database_support"
}

test_auto_changeset_bumps_entire_lockstep_workspace() {
  local policy="$repository_root/scripts/devops/emception-release-policy.mjs"
  local versioner="$repository_root/scripts/devops/version-emception.mjs"

  grep -Fq 'assertOnlyEmceptionPackageManifests' "$policy" || return 1
  grep -Fq "['exec', 'changeset', 'version']" "$versioner" || return 1
  grep -Eq '"@changesets/cli"' "$repository_root/package.json"
}

test_changesets_config_matches_lockstep_workspace() {
  local config="$repository_root/.changeset/config.json"
  local workspace_packages="$fixture_root/workspace-packages.json"

  (cd "$repository_root" && "${pnpm_command[@]}" list -r --depth -1 --json) > "$workspace_packages" || return 1
  "$PYTHON_BIN" - "$repository_root/package.json" "$config" "$workspace_packages" <<'PY' || return 1
import collections
import json
import sys

root_manifest_path, config_path, workspace_path = sys.argv[1:]
with open(root_manifest_path, encoding="utf-8") as handle:
    root_name = json.load(handle)["name"]
with open(config_path, encoding="utf-8") as handle:
    config = json.load(handle)
with open(workspace_path, encoding="utf-8") as handle:
    workspace = {
        package["name"] for package in json.load(handle)
        if package["name"] != root_name
    }

fixed = [package for group in config.get("fixed", []) for package in group]
linked = [package for group in config.get("linked", []) for package in group]
duplicates = sorted(
    package for package, count in collections.Counter(fixed).items() if count > 1
)
missing = sorted(workspace - set(fixed))
unknown = sorted(set(fixed) - workspace)

errors = []
emception = {
    "emception",
    "@gameguild/emception-toolchain",
    "@gameguild/emception-browser",
    "@gameguild/emception-xterm",
    "@gameguild/emception-react",
    "@gameguild/emception-webcomponent",
    "@gameguild/emception-ide",
}
groups = [set(group) for group in config.get("fixed", [])]
if len(groups) != 2:
    errors.append("the release policy requires the platform and Emception fixed groups")
if emception not in groups:
    errors.append("the seven public Emception packages must have an isolated fixed group")
if linked:
    errors.append(f"linked packages conflict with the lockstep fixed policy: {sorted(linked)}")
if duplicates:
    errors.append(f"duplicate fixed packages: {duplicates}")
if unknown:
    errors.append(f"unknown packages in the fixed group: {unknown}")
if errors:
    raise SystemExit("; ".join(errors))
PY
}

test_emception_emits_a_gate_result_for_every_main_push() {
  local workflow="$repository_root/.github/workflows/emception.yml"
  local trigger
  trigger="$(sed -n '/^on:/,/^permissions:/p' "$workflow")"

  ! grep -Fq 'paths:' <<< "$trigger" || return 1
  grep -Fq 'detect-emception-changes:' "$workflow" || return 1
  grep -Fq 'required: ${{ steps.changes.outputs.required }}' "$workflow" || return 1
  grep -Fq 'needs: detect-emception-changes' "$workflow" || return 1
  grep -Fq "if: needs.detect-emception-changes.outputs.required == 'true'" "$workflow"
}

test_emception_ci_validates_develop_pull_requests() {
  local workflow="$repository_root/.github/workflows/emception.yml"

  grep -A 3 '^  push:' "$workflow" | grep -Fx '      - develop' || return 1
  grep -A 3 '^  pull_request:' "$workflow" | grep -Fx '      - develop' || return 1
  grep -Fq 'apps/web/(Dockerfile|scripts/(sync-emception-cdn|coding-cycle-browser-e2e)\.mjs|src/lib/emception/)' "$workflow"
  grep -Fq 'pnpm --dir apps/web run test:browser:coding-cycle' "$workflow"
}

test_web_vitest_uses_direct_exec_for_json_evidence() {
  grep -Fq 'run env -u API_URL "${pnpm_command[@]}" --filter @game-guild/web exec vitest run --maxWorkers=4 --reporter=json' "$ci_dir/verify-economy.sh" || return 1
  ! grep -q 'pnpm --filter @game-guild/web run test --' "$ci_dir/verify-economy.sh"
}

test_economy_frontend_build_reuses_the_validated_client() {
  local gate="$ci_dir/verify-economy.sh"
  local web_package="$repository_root/apps/web/package.json"

  grep -Fq '"build:emception-runtime-dependencies"' "$web_package" || return 1
  grep -Fq -- '--filter @game-guild/client build' "$gate" || return 1
  grep -Fq -- '--filter @game-guild/web run build:emception-runtime-dependencies' "$gate" || return 1
  grep -Fq -- '--filter @game-guild/web run sync:emception' "$gate" || return 1
  grep -Fq 'GAMEGUILD_DISABLE_WEBPACK_CACHE=1 run "${pnpm_command[@]}" --filter @game-guild/web exec next build --webpack' "$gate" || return 1
  ! grep -Fq -- '--filter @game-guild/web build' "$gate"
}

test_web_server_uses_direct_node_process_for_cleanup() {
  grep -Fq 'node "$standalone_web_root/server.js"' "$ci_dir/verify-economy.sh" || return 1
  ! grep -q 'pnpm --filter @game-guild/web exec next start' "$ci_dir/verify-economy.sh"
}

test_standalone_web_server_uses_origin_safe_bind_address() {
  local gate="$ci_dir/verify-economy.sh"
  local hostname_line launch_line
  hostname_line="$(grep -n 'export HOSTNAME=0.0.0.0' "$gate" | cut -d: -f1)"
  launch_line="$(grep -n 'node "$standalone_web_root/server.js"' "$gate" | cut -d: -f1)"

  [[ -n "$hostname_line" && -n "$launch_line" ]] || return 1
  [[ "$hostname_line" -lt "$launch_line" ]]
}

test_browser_server_uses_published_api() {
  local gate="$ci_dir/verify-economy.sh"
  local api_url_line web_launch_line
  api_url_line="$(grep -n 'export API_URL="http://127.0.0.1:$api_port"' "$gate" | cut -d: -f1)"
  web_launch_line="$(grep -n 'node "$standalone_web_root/server.js"' "$gate" | cut -d: -f1)"

  [[ -n "$api_url_line" && -n "$web_launch_line" ]] || return 1
  [[ "$api_url_line" -lt "$web_launch_line" ]]
}

test_web_vitest_isolated_from_published_api() {
  grep -Fq 'run env -u API_URL "${pnpm_command[@]}" --filter @game-guild/web exec vitest run' "$ci_dir/verify-economy.sh"
}

test_local_api_readiness_enables_simulation_explicitly() {
  local gate="$ci_dir/verify-economy.sh"
  local enabled_line simulation_line launch_line
  enabled_line="$(grep -n 'export PaymentGateways__Stripe__IsEnabled=true' "$gate" | cut -d: -f1)"
  simulation_line="$(grep -n 'export PaymentGateways__Stripe__UseSimulation=true' "$gate" | cut -d: -f1)"
  launch_line="$(grep -n 'dotnet "$publish_directory/GameGuild.API.dll"' "$gate" | cut -d: -f1)"

  [[ -n "$enabled_line" && -n "$simulation_line" && -n "$launch_line" ]] || return 1
  [[ "$enabled_line" -lt "$launch_line" && "$simulation_line" -lt "$launch_line" ]]
}

test_coolify_compose_forwards_stripe_gateway_identity() {
  local compose="$repository_root/compose.coolify.yaml"
  local deployment_docs="$repository_root/docs/deployment-smoke.md"

  grep -Fq -- '- PaymentGateways__Stripe__AccountId=${PaymentGateways__Stripe__AccountId:?set PaymentGateways__Stripe__AccountId}' "$compose" || return 1
  grep -Fq -- '- Billing__Stripe__AccountId=${PaymentGateways__Stripe__AccountId:?set PaymentGateways__Stripe__AccountId}' "$compose" || return 1
  grep -Fq -- '- PaymentGateways__Stripe__ConnectedAccountId=${Billing__Stripe__ConnectedAccountId:-}' "$compose" || return 1
  grep -Fq -- '- PaymentGateways__Stripe__LiveMode=${Billing__Stripe__LiveMode:-false}' "$compose" || return 1
  grep -Fq -- '- Billing__Stripe__LiveMode=${Billing__Stripe__LiveMode:-false}' "$compose" || return 1
  grep -Fq 'PaymentGateways__Stripe__AccountId=acct_' "$deployment_docs" || return 1
  grep -Fq 'Billing__Stripe__LiveMode=false' "$deployment_docs"
}

test_published_api_uses_published_content_root() {
  grep -Fq 'dotnet "$publish_directory/GameGuild.API.dll" --contentRoot "$publish_directory"' \
    "$ci_dir/verify-economy.sh"
}

test_whole_solution_provisions_garage() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq "garage_container='gameguild-economy-ci-garage-" "$gate" || return 1
  grep -Fq 'run env MSYS_NO_PATHCONV=1 docker run --detach --rm --name "$garage_container"' "$gate" || return 1
  grep -Fq 'dxflrs/garage:v2.3.0' "$gate" || return 1
  grep -Fq 'GARAGE_HOST=127.0.0.1' "$gate" || return 1
  grep -Fq 'export S3_SERVICE_URL="http://127.0.0.1:$garage_s3_port"' "$gate" || return 1
  grep -Fq 'export GARAGE_ADMIN_URL="http://127.0.0.1:$garage_admin_port"' "$gate" || return 1
  grep -Fq 'docker rm --force "$garage_container"' "$gate"
}

test_windows_testcontainers_cleanup_is_gate_scoped() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'export TESTCONTAINERS_RYUK_DISABLED=true' "$gate" || return 1
  grep -Fq 'testcontainers_baseline' "$gate" || return 1
  grep -Fq 'label=org.testcontainers=true' "$gate"
}

test_windows_gate_disables_reusable_msbuild_workers() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'export DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER=1' "$gate" || return 1
  grep -Fq 'export MSBUILDDISABLENODEREUSE=1' "$gate"
}

test_manifest_rejects_undeclared_project() {
  local root="$fixture_root/manifest-invalid"
  mkdir -p "$root/apps/api/Source/Modules/GameGuild.Finance.Economy"
  printf '<Project />\n' > "$root/apps/api/Source/Modules/GameGuild.Finance.Economy/GameGuild.Finance.Economy.csproj"
  printf '{"schemaVersion":1,"projects":[]}\n' > "$root/manifest.json"
  assert_throws 'not declared' assert_economy_manifest "$root" "$root/manifest.json"
}

test_manifest_rejects_undeclared_compliance_project() {
  local root="$fixture_root/manifest-compliance-invalid"
  mkdir -p "$root/apps/api/Source/Modules/GameGuild.Compliance.KYC"
  printf '<Project />\n' > "$root/apps/api/Source/Modules/GameGuild.Compliance.KYC/GameGuild.Compliance.KYC.csproj"
  printf '{"schemaVersion":1,"projects":[]}\n' > "$root/manifest.json"
  assert_throws 'not declared' assert_economy_manifest "$root" "$root/manifest.json"
}

test_manifest_accepts_declared_projects() {
  local root="$fixture_root/manifest-valid"
  local production='apps/api/Source/Modules/GameGuild.Finance.Economy/GameGuild.Finance.Economy.csproj'
  local unit='apps/api/tests/GameGuild.Finance.Economy.UnitTests/GameGuild.Finance.Economy.UnitTests.csproj'
  local integration='apps/api/tests/GameGuild.Finance.Economy.IntegrationTests/GameGuild.Finance.Economy.IntegrationTests.csproj'
  mkdir -p "$root/$(dirname "$production")" "$root/$(dirname "$unit")" "$root/$(dirname "$integration")"
  printf '<Project />\n' > "$root/$production"
  printf '<Project />\n' > "$root/$unit"
  printf '<Project />\n' > "$root/$integration"
  printf '{"schemaVersion":1,"projects":[{"productionProject":"%s","testProjects":["%s","%s"],"coverageAssemblies":["GameGuild.Finance.Economy"]}]}\n' \
    "$production" "$unit" "$integration" > "$root/manifest.json"
  assert_economy_manifest "$root" "$root/manifest.json" >/dev/null
}

test_manifest_requires_full_branch_coverage() {
  "$PYTHON_BIN" - "$ci_dir/economy-projects.json" <<'PY'
import json
import sys

with open(sys.argv[1], encoding="utf-8") as handle:
    manifest = json.load(handle)

exceptions = [
    entry["productionProject"]
    for entry in manifest["projects"]
    if entry.get("minimumBranchRate", 1) != 1
]
if exceptions:
    raise SystemExit(f"Economy branch coverage must be 100%: {', '.join(exceptions)}")
PY
}

test_manifest_prunes_build_outputs() {
  local root="$fixture_root/manifest-build-outputs"
  local production='apps/api/Source/Modules/GameGuild.Finance.Economy/GameGuild.Finance.Economy.csproj'
  local unit='apps/api/tests/GameGuild.Finance.Economy.UnitTests/GameGuild.Finance.Economy.UnitTests.csproj'
  local gate_library="$ci_dir/economy-gate.sh"

  mkdir -p "$root/$(dirname "$production")" "$root/$(dirname "$unit")/bin/Release" "$root/$(dirname "$unit")/obj"
  printf '<Project />\n' > "$root/$production"
  printf '<Project />\n' > "$root/$unit"
  printf '<Project />\n' > "$root/$(dirname "$unit")/bin/Release/Generated.csproj"
  printf '<Project />\n' > "$root/$(dirname "$unit")/obj/Generated.csproj"
  printf '{"schemaVersion":1,"projects":[{"productionProject":"%s","testProjects":["%s"],"coverageAssemblies":["GameGuild.Finance.Economy"]}]}\n' \
    "$production" "$unit" > "$root/manifest.json"

  grep -Fq 'for current, directories, filenames in os.walk(base_directory):' "$gate_library" || return 1
  grep -Fq 'directories[:] = [directory for directory in directories if directory not in {"bin", "obj"}]' "$gate_library" || return 1
  assert_economy_manifest "$root" "$root/manifest.json" >/dev/null
}
test_manifest_record_fields_normalize_windows_line_endings() {
  local assembly
  assembly="$(normalize_shell_record_field $'GameGuild.Finance.Economy\r')"
  assert_equal "$assembly" 'GameGuild.Finance.Economy' 'Windows Python output must not alter assembly names'
}

test_coverage_record_fields_preserve_empty_prefixes() {
  local record_type first second third fourth
  local -a records=()
  while IFS=$'\t' read -r record_type first second third fourth; do
    [[ "$record_type" == 'coverage' ]] || continue
    records+=("$first"$'\t'"$second"$'\t'"$third"$'\t'"$fourth")
  done <<< $'coverage\tEconomy.UnitTests.csproj\tGameGuild.Finance.Economy\t__all__\t1'

  IFS=$'\t' read -r first second third fourth <<< "${records[0]}"
  [[ "$third" == '__all__' ]] && third=''
  assert_equal "$first" 'Economy.UnitTests.csproj' 'coverage test project must be preserved' || return 1
  assert_equal "$second" 'GameGuild.Finance.Economy' 'coverage assembly must be preserved' || return 1
  assert_equal "$third" '' 'an empty prefix list must remain empty' || return 1
  assert_equal "$fourth" '1' 'branch threshold must not shift into the prefixes field'
}

test_warning_scope_finds_commerce_projects() {
  local root="$fixture_root/warning-scope"
  local production='apps/api/Source/Modules/GameGuild.Commerce.Payments/GameGuild.Commerce.Payments.csproj'
  local tests='apps/api/tests/GameGuild.Commerce.Payments.UnitTests/GameGuild.Commerce.Payments.UnitTests.csproj'
  mkdir -p "$root/$(dirname "$production")" "$root/$(dirname "$tests")"
  printf '<Project />\n' > "$root/$production"
  printf '<Project />\n' > "$root/$tests"
  local output
  output="$(get_touched_commerce_projects "$root" \
    'apps/api/Source/Modules/GameGuild.Commerce.Payments/Services/PaymentService.cs' \
    'apps/api/tests/GameGuild.Commerce.Payments.UnitTests/PaymentServiceTests.cs' \
    'apps/api/Source/Modules/GameGuild.Assets/Asset.cs')"
  grep -qx "$production" <<< "$output" || return 1
  grep -qx "$tests" <<< "$output" || return 1
  assert_equal "$(wc -l <<< "$output" | tr -d ' ')" '2' 'only owning Commerce projects are returned'
}

test_readiness_requires_consecutive_successes() {
  local attempts_file="$fixture_root/readiness-attempts"
  printf '0\n' > "$attempts_file"
  readiness_probe() {
    local attempt
    attempt="$(<"$attempts_file")"
    attempt=$((attempt + 1))
    printf '%s\n' "$attempt" > "$attempts_file"
    [[ $attempt -eq 1 || $attempt -ge 3 ]]
  }
  wait_for_consecutive_successes readiness_probe 2 4 0
  assert_equal "$(<"$attempts_file")" '4' 'a transient success must not count as stable readiness'
}

test_process_cleanup_stops_background_process() {
  sleep 30 &
  local process_id=$!

  stop_process_tree "$process_id"
  if kill -0 "$process_id" >/dev/null 2>&1; then
    kill -KILL "$process_id" >/dev/null 2>&1 || true
    wait "$process_id" >/dev/null 2>&1 || true
    return 1
  fi
}

test_trx_rejects_skips_and_empty_suites() {
  local skipped="$fixture_root/xunit-skipped.trx"
  cat > "$skipped" <<'XML'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results><UnitTestResult testName="Skipped" outcome="NotExecuted" /></Results><ResultSummary outcome="Completed"><Counters total="2" executed="1" passed="1" failed="0" error="0" timeout="0" aborted="0" inconclusive="0" passedButRunAborted="0" notRunnable="0" notExecuted="0" pending="0" /></ResultSummary></TestRun>
XML
  assert_throws 'skipped or not executed' assert_trx_evidence "$skipped" || return 1

  local empty="$fixture_root/empty.trx"
  printf '<TestRun><ResultSummary><Counters total="0" executed="0" passed="0" failed="0" /></ResultSummary></TestRun>\n' > "$empty"
  assert_throws 'zero tests' assert_trx_evidence "$empty"
}

test_whole_solution_allows_only_source_empty_scaffolds() {
  local root="$fixture_root/whole-solution"
  local project='apps/api/tests/GameGuild.Localization.IntegrationTests/GameGuild.Localization.IntegrationTests.csproj'
  local project_directory="$root/$(dirname "$project")"
  local trx="$root/localization-empty.trx"
  local log="$root/dotnet-test.log"
  mkdir -p "$project_directory"
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$root/$project"
  printf '<TestRun><ResultSummary><Counters total="0" executed="0" passed="0" failed="0" /></ResultSummary></TestRun>\n' > "$trx"
  printf 'Test run for %s/bin/Release/net10.0/GameGuild.Localization.IntegrationTests.dll (.NETCoreApp,Version=v10.0)\nResults File: %s\n' \
    "$project_directory" "$trx" > "$log"

  bash "$ci_dir/verify-economy.sh" --validate-whole-solution-evidence "$root" "$log" "$trx" >/dev/null || return 1

  sed -i 's/GameGuild\.Localization\.IntegrationTests/GameGuild.Unlisted.IntegrationTests/g' "$log"
  assert_throws 'zero tests' bash "$ci_dir/verify-economy.sh" \
    --validate-whole-solution-evidence "$root" "$log" "$trx" || return 1

  sed -i 's/GameGuild\.Unlisted\.IntegrationTests/GameGuild.Localization.IntegrationTests/g' "$log"
  printf 'public sealed class Tests {}\n' > "$project_directory/Tests.cs"
  assert_throws 'contains C# source' bash "$ci_dir/verify-economy.sh" \
    --validate-whole-solution-evidence "$root" "$log" "$trx"
}

test_whole_solution_scaffold_scan_prunes_build_outputs() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq -- '-type d \( -name bin -o -name obj \) -prune' "$gate" || return 1
  ! grep -Fq -- "! -path '*/bin/*' ! -path '*/obj/*'" "$gate"
}

test_pnpm_invocation_uses_standard_command_on_every_host() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'declare -a pnpm_command=(pnpm)' "$gate" || return 1
  ! grep -Fq 'pnpm_command=(pnpm.cmd)' "$gate"
}

test_whole_solution_isolates_vstest_processes() {
  local gate="$ci_dir/verify-economy.sh"
  local path_separator=$'\\'

  grep -Fq 'run_whole_solution_test_project()' "$gate" || return 1
  grep -Fq 'wait_for_whole_solution_batch()' "$gate" || return 1
  grep -Fq 'dotnet sln apps/api/GameGuild.sln list' "$gate" || return 1
  grep -Fq 'whole_solution_test_project()' "$gate" || return 1
  grep -Fq '<IsTestProject>[[:space:]]*false[[:space:]]*</IsTestProject>' "$gate" || return 1
  grep -Fq 'economy_test_project()' "$gate" || return 1
  grep -Fq 'economy_test_project "$test_project" && continue' "$gate" || return 1
  grep -Fq "tr '${path_separator}134' '/'" "$gate" || return 1
  grep -Fq 'for test_project in "${whole_solution_projects[@]}"; do' "$gate" || return 1
  grep -Fq 'run_whole_solution_test_project "$test_project" "$whole_solution_results" &' "$gate" || return 1
  grep -Fq 'Whole-solution test project produced no TRX:' "$gate" || return 1
  ! grep -Fq 'dotnet test apps/api/GameGuild.sln' "$gate"
}

test_whole_solution_log_aggregation_excludes_aggregate_output() {
  local gate="$ci_dir/verify-economy.sh"

  grep -Fq 'find "$whole_solution_results" -mindepth 2 -type f -name '\''dotnet-test.log'\''' "$gate"
}
test_whole_solution_recovers_scaffold_identity_from_trx() {
  local root="$fixture_root/whole-solution-trx-fallback"
  local project='apps/api/tests/GameGuild.Localization.IntegrationTests/GameGuild.Localization.IntegrationTests.csproj'
  local project_directory="$root/$(dirname "$project")"
  local trx="$root/localization-empty.trx"
  local log="$root/dotnet-test.log"
  mkdir -p "$project_directory"
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' > "$root/$project"
  cat > "$trx" <<'XML'
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <ResultSummary outcome="Completed">
    <Counters total="0" executed="0" passed="0" failed="0" />
    <Output>
      <StdOut>[xUnit.net] Discovering: GameGuild.Localization.IntegrationTests
[xUnit.net] Discovered: GameGuild.Localization.IntegrationTests</StdOut>
    </Output>
  </ResultSummary>
</TestRun>
XML
  printf 'Test run for %s/bin/Release/net10.0/GameGuild.Localization.IntegrationTests.dll (.NETCoreApp,Version=v10.0)\nResults File: %s/unrelated.trx\n' \
    "$project_directory" "$root" > "$log"

  bash "$ci_dir/verify-economy.sh" --validate-whole-solution-evidence "$root" "$log" "$trx" >/dev/null
}

test_cobertura_requires_full_method_coverage() {
  local coverage="$fixture_root/coverage.cobertura.xml"
  cat > "$coverage" <<'XML'
<coverage><packages><package name="GameGuild.Finance.Economy" line-rate="1" branch-rate="1"><classes><class name="A"><methods><method name="Covered"><lines><line number="1" hits="1" /></lines></method><method name="Missed"><lines><line number="2" hits="0" /></lines></method></methods><lines><line number="1" hits="1" /><line number="2" hits="1" /></lines></class></classes></package></packages></coverage>
XML
  assert_throws 'method coverage' assert_cobertura_coverage "$coverage" 'GameGuild.Finance.Economy' || return 1
  sed -i 's/number="2" hits="0"/number="2" hits="1"/' "$coverage"
  assert_cobertura_coverage "$coverage" 'GameGuild.Finance.Economy' >/dev/null
}

test_cobertura_requires_full_branch_coverage() {
  local coverage="$fixture_root/branch-threshold.cobertura.xml"
  cat > "$coverage" <<'XML'
<coverage><packages><package name="GameGuild.Finance.Economy" line-rate="1" branch-rate="1"><classes><class name="A"><methods><method name="Covered"><lines><line number="1" hits="1" branch="true" condition-coverage="50% (1/2)" /></lines></method></methods><lines><line number="1" hits="1" branch="true" condition-coverage="50% (1/2)" /></lines></class></classes></package></packages></coverage>
XML
  assert_throws 'branch coverage' assert_cobertura_coverage "$coverage" 'GameGuild.Finance.Economy' || return 1
  sed -i 's/condition-coverage="50% (1\/2)"/condition-coverage="100% (2\/2)"/g' "$coverage"
  assert_cobertura_coverage "$coverage" 'GameGuild.Finance.Economy' >/dev/null
}

test_cobertura_supports_path_scoped_capabilities() {
  local coverage="$fixture_root/path-coverage.cobertura.xml"
  cat > "$coverage" <<'XML'
<coverage><packages><package name="GameGuild.AI" line-rate="0.5" branch-rate="0.5"><classes><class name="Legacy" filename="Legacy/Old.cs"><methods><method name="Missed"><lines><line number="1" hits="0" /></lines></method></methods><lines><line number="1" hits="0" /></lines></class><class name="Cost" filename="CostAccounting/Cost.cs"><methods><method name="Covered"><lines><line number="2" hits="1" /></lines></method></methods><lines><line number="2" hits="1" branch="true" condition-coverage="100% (2/2)" /></lines></class></classes></package></packages></coverage>
XML
  assert_throws 'line coverage' assert_cobertura_coverage "$coverage" 'GameGuild.AI' || return 1
  assert_cobertura_coverage "$coverage" 'GameGuild.AI' 'CostAccounting/' >/dev/null
  assert_throws 'contains no classes' assert_cobertura_coverage "$coverage" 'GameGuild.AI' 'Missing/'
}

test_json_evidence_rejects_pending_and_skipped() {
  local vitest="$fixture_root/vitest.json" playwright="$fixture_root/playwright.json"
  printf '{"numTotalTests":3,"numPassedTests":2,"numFailedTests":0,"numPendingTests":1,"testResults":[]}\n' > "$vitest"
  printf '{"stats":{"expected":2,"unexpected":0,"skipped":1},"suites":[]}\n' > "$playwright"
  assert_throws 'pending' assert_vitest_evidence "$vitest" || return 1
  assert_throws 'skipped' assert_playwright_evidence "$playwright"
}

test_canonical_json_preserves_arrays() {
  local first="$fixture_root/first.json" second="$fixture_root/second.json"
  local first_out="$fixture_root/first.out.json" second_out="$fixture_root/second.out.json"
  printf '{"tags":["Economy"],"paths":{"/b":{},"/a":{}},"servers":[{"url":"https://example.test"}]}\n' > "$first"
  printf '{"servers":[{"url":"https://example.test"}],"paths":{"/a":{},"/b":{}},"tags":["Economy"]}\n' > "$second"
  canonicalize_json "$first" "$first_out"
  canonicalize_json "$second" "$second_out"
  cmp -s "$first_out" "$second_out" || return 1
  grep -q '"tags":\["Economy"\]' "$first_out"
}

run_test 'CI policy contains only shell scripts' test_shell_only_ci_policy
run_test 'contributors visualization uses native xvfb' test_contributors_visualization_uses_native_xvfb
run_test 'Emception publish opens a release PR to main' test_release_flow_opens_version_pr_to_main
run_test 'repository policy runs in a parallel required gate' test_repository_policy_runs_in_a_parallel_required_gate
run_test 'workflow uses fast PR and full release Economy profiles' test_workflow_uses_fast_pr_and_full_release_economy_profiles
run_test 'workflow caches gate dependencies' test_workflow_caches_gate_dependencies
run_test 'Economy preflight is independent and records its summary' test_economy_preflight_has_no_repository_policy_side_effect
run_test 'Economy preflight avoids the pnpm install lock' test_economy_preflight_avoids_the_pnpm_install_lock
run_test 'Economy gate rejects stage skips' test_economy_gate_rejects_stage_skips
run_test 'Economy gate requires 100 percent web coverage' test_economy_gate_requires_full_web_coverage
run_test 'Economy gate runs the Economy browser surface' test_economy_gate_runs_economy_browser_surface
run_test 'Economy gate bounds hung tests and records timings' test_economy_gate_bounds_hung_tests_and_records_timings
run_test 'Economy gate supports fast PR and full release profiles' test_economy_gate_supports_fast_pr_and_full_release_profiles
run_test 'Economy gate batches whole-solution tests' test_economy_gate_batches_whole_solution_tests
run_test 'Economy unit tests bound parallelism without global serialization' test_economy_unit_tests_bound_parallelism_without_global_serialization
run_test 'Economy gate builds release targets before packaging' test_economy_gate_builds_release_targets_before_packaging
run_test 'OpenAPI gate ignores generator provenance only' test_openapi_gate_uses_semantic_generated_client_diff
run_test 'Economy gate rejects nested PostgreSQL Testcontainers' test_economy_gate_rejects_nested_postgres_testcontainers
run_test 'Economy gate isolates global roles from application databases' test_economy_gate_isolates_global_economy_roles_from_application_databases
run_test 'full gate isolates API migration tests from the Economy template' test_full_gate_isolates_api_migration_tests_from_the_economy_template
run_test 'Economy gate uses memory-backed disposable PostgreSQL' test_economy_gate_uses_memory_backed_disposable_postgres
run_test 'Economy gate clones one migrated PostgreSQL template' test_economy_gate_migrates_one_template_and_clones_isolated_test_databases
run_test 'Emception versioning is scoped to its fixed group' test_auto_changeset_bumps_entire_lockstep_workspace
run_test 'Changesets config isolates the Emception release group' test_changesets_config_matches_lockstep_workspace
run_test 'Emception emits a gate result for every main push' test_emception_emits_a_gate_result_for_every_main_push
run_test 'Emception CI validates Toolchain-consuming changes on develop' test_emception_ci_validates_develop_pull_requests
run_test 'web Vitest uses direct exec for JSON evidence' test_web_vitest_uses_direct_exec_for_json_evidence
run_test 'Economy frontend build reuses its validated client' test_economy_frontend_build_reuses_the_validated_client
run_test 'web server uses a directly managed Node process' test_web_server_uses_direct_node_process_for_cleanup
run_test 'standalone web server uses an origin-safe bind address' test_standalone_web_server_uses_origin_safe_bind_address
run_test 'browser server uses the published API instance' test_browser_server_uses_published_api
run_test 'web Vitest is isolated from the published API instance' test_web_vitest_isolated_from_published_api
run_test 'local API readiness enables payment simulation explicitly' test_local_api_readiness_enables_simulation_explicitly
run_test 'Coolify forwards Stripe gateway identity and mode' test_coolify_compose_forwards_stripe_gateway_identity
run_test 'published API uses its published content root' test_published_api_uses_published_content_root
run_test 'whole-solution tests provision isolated Garage storage' test_whole_solution_provisions_garage
run_test 'Windows Testcontainers cleanup is scoped to the Economy gate' test_windows_testcontainers_cleanup_is_gate_scoped
run_test 'Windows Economy gate disables reusable MSBuild workers' test_windows_gate_disables_reusable_msbuild_workers
run_test 'manifest rejects undeclared Economy projects' test_manifest_rejects_undeclared_project
run_test 'manifest rejects undeclared Economy compliance projects' test_manifest_rejects_undeclared_compliance_project
run_test 'manifest accepts declared Economy projects and tests' test_manifest_accepts_declared_projects
run_test 'manifest requires 100 percent branch coverage' test_manifest_requires_full_branch_coverage
run_test 'Economy coverage excludes compiler-generated members without skipping source properties' test_economy_coverage_ignores_only_compiler_generated_members
run_test 'manifest prunes build outputs before project discovery' test_manifest_prunes_build_outputs
run_test 'manifest records normalize Windows line endings' test_manifest_record_fields_normalize_windows_line_endings
run_test 'coverage records preserve empty prefixes and branch threshold' test_coverage_record_fields_preserve_empty_prefixes
run_test 'warning scope resolves touched Commerce projects' test_warning_scope_finds_commerce_projects
run_test 'readiness requires consecutive successful probes' test_readiness_requires_consecutive_successes
run_test 'process cleanup terminates Bash background processes' test_process_cleanup_stops_background_process
run_test 'TRX evidence rejects skipped and zero-test suites' test_trx_rejects_skips_and_empty_suites
run_test 'whole-solution evidence allows only named source-empty scaffolds' test_whole_solution_allows_only_source_empty_scaffolds
run_test 'whole-solution scaffold scan prunes build outputs' test_whole_solution_scaffold_scan_prunes_build_outputs
run_test 'pnpm invocation uses the standard command on every host' test_pnpm_invocation_uses_standard_command_on_every_host
run_test 'whole-solution tests isolate VSTest processes' test_whole_solution_isolates_vstest_processes
run_test 'whole-solution log aggregation excludes its own output' test_whole_solution_log_aggregation_excludes_aggregate_output
run_test 'whole-solution evidence recovers scaffold identity from TRX metadata' test_whole_solution_recovers_scaffold_identity_from_trx
run_test 'Cobertura enforces line, branch, and method coverage' test_cobertura_requires_full_method_coverage
run_test 'Cobertura requires 100 percent branch coverage' test_cobertura_requires_full_branch_coverage
run_test 'Cobertura supports path-scoped capability coverage' test_cobertura_supports_path_scoped_capabilities
run_test 'Vitest and Playwright reject pending or skipped tests' test_json_evidence_rejects_pending_and_skipped
run_test 'canonical JSON is deterministic and preserves arrays' test_canonical_json_preserves_arrays

printf 'Shell gate tests: %s passed, %s failed\n' "$passed" "$failed"
((failed == 0))
