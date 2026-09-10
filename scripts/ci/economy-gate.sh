#!/usr/bin/env bash

economy_gate_error() {
  printf '%s\n' "$*" >&2
  return 1
}

resolve_python() {
  local candidate
  for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1 && "$candidate" -c 'import json, sys, xml.etree.ElementTree' >/dev/null 2>&1; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  economy_gate_error 'Python 3 is required for structured CI evidence validation'
}

PYTHON_BIN="${PYTHON_BIN:-$(resolve_python)}"

normalize_shell_record_field() {
  printf '%s' "${1%$'\r'}"
}

assert_economy_manifest() {
  local repository_root="$1" manifest_path="$2"
  "$PYTHON_BIN" - "$repository_root" "$manifest_path" <<'PY'
import json
import os
import pathlib
import sys

root = pathlib.Path(sys.argv[1]).resolve()
manifest_path = pathlib.Path(sys.argv[2]).resolve()

with manifest_path.open(encoding="utf-8") as handle:
    manifest = json.load(handle)

if manifest.get("schemaVersion") != 1:
    raise SystemExit("Economy project manifest schemaVersion must be 1")

entries = manifest.get("projects", [])
declared_production = {
    entry["productionProject"].replace("\\", "/")
    for entry in entries
}
declared_tests = {
    path.replace("\\", "/")
    for entry in entries
    for path in entry.get("testProjects", [])
}

def discover_projects(relative_directory):
    projects = set()
    base_directory = root / relative_directory
    scope_prefixes = (
        "GameGuild.Finance.Economy",
        "GameGuild.Compliance.KYC",
        "GameGuild.Compliance.FinancialCrime",
        "GameGuild.TrustSafety",
    )
    for current, directories, filenames in os.walk(base_directory):
        if pathlib.Path(current) == base_directory:
            directories[:] = [directory for directory in directories if directory.startswith(scope_prefixes)]
        else:
            directories[:] = [directory for directory in directories if directory not in {"bin", "obj"}]
        for filename in filenames:
            if filename.endswith(".csproj"):
                projects.add((pathlib.Path(current) / filename).relative_to(root).as_posix())
    return projects

discovered_production = discover_projects("apps/api/Source/Modules")
discovered_tests = discover_projects("apps/api/tests")

for path in sorted(discovered_production - declared_production):
    raise SystemExit(f"Discovered Economy production project is not declared in the manifest: {path}")
for path in sorted(discovered_tests - declared_tests):
    raise SystemExit(f"Discovered Economy test project is not declared in the manifest: {path}")
for path in sorted(declared_production | declared_tests):
    if not (root / path).is_file():
        raise SystemExit(f"Required Economy project is missing: {path}")

for entry in entries:
    if not entry.get("testProjects"):
        raise SystemExit(f"Economy production project has no declared tests: {entry.get('productionProject')}")
    if not entry.get("coverageAssemblies"):
        raise SystemExit(f"Economy production project has no coverage assemblies: {entry.get('productionProject')}")
PY
}

get_touched_commerce_projects() {
  local repository_root="$1"
  shift
  local changed_path directory project relative
  declare -A projects=()

  for changed_path in "$@"; do
    changed_path="${changed_path//\\//}"
    if [[ "$changed_path" =~ ^(apps/api/(Source/Modules|tests)/GameGuild\.Commerce[^/]+)/ ]]; then
      directory="${BASH_REMATCH[1]}"
      project="$(find "$repository_root/$directory" -maxdepth 1 -type f -name '*.csproj' -print -quit 2>/dev/null || true)"
      if [[ -n "$project" ]]; then
        relative="${project#"$repository_root/"}"
        projects["${relative//\\//}"]=1
      fi
    fi
  done

  if ((${#projects[@]} > 0)); then
    printf '%s\n' "${!projects[@]}" | LC_ALL=C sort
  fi
}

wait_for_consecutive_successes() {
  local probe="$1" required_successes="${2:-2}" maximum_attempts="${3:-90}" delay_seconds="${4:-1}"
  local consecutive=0 attempt
  for ((attempt = 1; attempt <= maximum_attempts; attempt++)); do
    if "$probe"; then
      consecutive=$((consecutive + 1))
      if ((consecutive >= required_successes)); then
        return 0
      fi
    else
      consecutive=0
    fi
    if ((attempt < maximum_attempts)) && [[ "$delay_seconds" != '0' ]]; then
      sleep "$delay_seconds"
    fi
  done
  economy_gate_error "Probe did not become stably ready after $maximum_attempts attempts"
}

assert_trx_evidence() {
  local path="$1"
  "$PYTHON_BIN" - "$path" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
counters = next((node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == "Counters"), None)
if counters is None:
    raise SystemExit(f"TRX evidence has no counters: {path}")

def count(name):
    return int(counters.attrib.get(name, "0") or "0")

total = count("total")
failed = sum(count(name) for name in ("failed", "error", "timeout", "aborted"))
counter_skips = sum(count(name) for name in ("notExecuted", "notRunnable", "pending"))
executed_value = counters.attrib.get("executed")
execution_gap = max(0, total - int(executed_value)) if executed_value not in (None, "") else 0
skip_outcomes = {"NotExecuted", "Skipped", "Pending", "NotRunnable"}
result_skips = sum(
    1
    for node in root.iter()
    if node.tag.rsplit("}", 1)[-1] == "UnitTestResult" and node.attrib.get("outcome") in skip_outcomes
)
skipped = max(counter_skips, execution_gap, result_skips)

if total == 0:
    raise SystemExit(f"TRX suite contains zero tests: {path}")
if failed:
    raise SystemExit(f"TRX suite contains {failed} failed tests: {path}")
if skipped:
    raise SystemExit(f"TRX suite contains {skipped} skipped or not executed tests: {path}")

print(json_result := f'{{"total":{total},"failed":{failed},"skipped":{skipped}}}')
PY
}

assert_cobertura_coverage() {
  local path="$1" assembly="$2" path_prefixes="${3:-}" minimum_branch_rate="${4:-1}"
  "$PYTHON_BIN" - "$path" "$assembly" "$path_prefixes" "$minimum_branch_rate" <<'PY'
import json
import re
import sys
import xml.etree.ElementTree as ET

path, assembly, path_prefixes_csv, minimum_branch_rate = sys.argv[1:5]
try:
    minimum_branch_rate = float(minimum_branch_rate)
except ValueError as error:
    raise SystemExit(f"Coverage branch threshold must be numeric: {minimum_branch_rate}") from error
if not 0 <= minimum_branch_rate <= 1:
    raise SystemExit(f"Coverage branch threshold must be between 0 and 1: {minimum_branch_rate}")
prefixes = [value.replace("\\", "/").strip("/") + "/" for value in path_prefixes_csv.split(",") if value]
root = ET.parse(path).getroot()
package = next((node for node in root.iter() if node.tag.rsplit("}", 1)[-1] == "package" and node.attrib.get("name") == assembly), None)
if package is None:
    raise SystemExit(f"Coverage report does not contain required assembly '{assembly}': {path}")

classes = [node for node in package.iter() if node.tag.rsplit("}", 1)[-1] == "class"]
if prefixes:
    classes = [
        node for node in classes
        if any(node.attrib.get("filename", "").replace("\\", "/").startswith(prefix) for prefix in prefixes)
    ]
    if not classes:
        raise SystemExit(
            f"Coverage assembly '{assembly}' contains no classes under path prefixes {path_prefixes_csv}"
        )

methods = [
    node for class_node in classes
    for node in class_node.iter()
    if node.tag.rsplit("}", 1)[-1] == "method"
]
if not methods:
    raise SystemExit(f"Coverage assembly '{assembly}' contains zero methods")

lines = []
for class_node in classes:
    class_lines = next(
        (child for child in class_node if child.tag.rsplit("}", 1)[-1] == "lines"),
        None,
    )
    if class_lines is not None:
        lines.extend(
            line for line in class_lines
            if line.tag.rsplit("}", 1)[-1] == "line"
        )
if not lines:
    scope = f" under {path_prefixes_csv}" if prefixes else ""
    raise SystemExit(f"Coverage assembly '{assembly}' contains zero executable lines{scope}")
line_rate = sum(int(line.attrib.get("hits", "0")) > 0 for line in lines) / len(lines)
covered_branches = total_branches = 0
for line in lines:
    if line.attrib.get("branch", "false").lower() != "true":
        continue
    match = re.search(r"\((\d+)/(\d+)\)", line.attrib.get("condition-coverage", ""))
    if match is None:
        raise SystemExit(f"Coverage branch evidence is malformed for assembly '{assembly}'")
    covered_branches += int(match.group(1))
    total_branches += int(match.group(2))
branch_rate = covered_branches / total_branches if total_branches else 1.0

covered_methods = sum(
    1 for method in methods
    if any(
        line.tag.rsplit("}", 1)[-1] == "line" and int(line.attrib.get("hits", "0")) > 0
        for line in method.iter()
    )
)
method_rate = covered_methods / len(methods)

if line_rate < 1:
    raise SystemExit(f"Assembly '{assembly}' line coverage is {line_rate * 100:g}% (required: 100%)")
if branch_rate < minimum_branch_rate:
    raise SystemExit(
        f"Assembly '{assembly}' branch coverage is {branch_rate * 100:g}% "
        f"(required: {minimum_branch_rate * 100:g}%)")
if method_rate < 1:
    raise SystemExit(f"Assembly '{assembly}' method coverage is {method_rate * 100:g}% (required: 100%)")

print(json.dumps({
    "assembly": assembly,
    "pathPrefixes": prefixes,
    "lineRate": line_rate,
    "branchRate": branch_rate,
    "minimumBranchRate": minimum_branch_rate,
    "methodRate": method_rate,
}, separators=(",", ":")))
PY
}

assert_vitest_evidence() {
  local path="$1"
  "$PYTHON_BIN" - "$path" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as handle:
    result = json.load(handle)
total = int(result.get("numTotalTests", 0))
failed = int(result.get("numFailedTests", 0))
pending = int(result.get("numPendingTests", 0))
if total == 0:
    raise SystemExit(f"Vitest suite contains zero tests: {path}")
if failed:
    raise SystemExit(f"Vitest suite contains {failed} failed tests: {path}")
if pending:
    raise SystemExit(f"Vitest suite contains {pending} pending, skipped, or todo tests: {path}")
print(json.dumps({"total": total, "failed": failed, "pending": pending}, separators=(",", ":")))
PY
}

assert_playwright_evidence() {
  local path="$1"
  "$PYTHON_BIN" - "$path" <<'PY'
import json
import sys

path = sys.argv[1]
with open(path, encoding="utf-8") as handle:
    result = json.load(handle)
stats = result.get("stats", {})
expected = int(stats.get("expected", 0))
failed = int(stats.get("unexpected", 0))
skipped = int(stats.get("skipped", 0))
total = expected + failed + skipped
if total == 0:
    raise SystemExit(f"Playwright suite contains zero tests: {path}")
if failed:
    raise SystemExit(f"Playwright suite contains {failed} failed tests: {path}")
if skipped:
    raise SystemExit(f"Playwright suite contains {skipped} skipped tests: {path}")
print(json.dumps({"total": total, "failed": failed, "skipped": skipped}, separators=(",", ":")))
PY
}

canonicalize_json() {
  local input_path="$1" output_path="$2"
  mkdir -p "$(dirname "$output_path")"
  "$PYTHON_BIN" - "$input_path" "$output_path" <<'PY'
import json
import pathlib
import sys

input_path, output_path = sys.argv[1:3]
with open(input_path, encoding="utf-8") as handle:
    value = json.load(handle)
pathlib.Path(output_path).write_text(
    json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n",
    encoding="utf-8",
)
PY
}

get_ephemeral_port() {
  "$PYTHON_BIN" - <<'PY'
import socket
with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    print(sock.getsockname()[1])
PY
}

wait_http_ready() {
  local url="$1" process_id="$2" maximum_attempts="${3:-90}"
  local attempt status
  for ((attempt = 1; attempt <= maximum_attempts; attempt++)); do
    kill -0 "$process_id" 2>/dev/null || economy_gate_error "Process exited before readiness at $url"
    status="$(curl --silent --show-error --output /dev/null --write-out '%{http_code}' --max-time 5 "$url" 2>/dev/null || true)"
    if [[ "$status" =~ ^[234][0-9][0-9]$ ]]; then
      return 0
    fi
    sleep 2
  done
  economy_gate_error "Timed out waiting for readiness at $url"
}

stop_process_tree() {
  local process_id="${1:-}"
  [[ -n "$process_id" ]] || return 0
  if [[ "$(uname -s)" =~ ^(MINGW|MSYS|CYGWIN) ]]; then
    local windows_process_id
    windows_process_id="$(ps -p "$process_id" -l 2>/dev/null | awk 'NR == 2 { print $4 }')"
    [[ -n "$windows_process_id" ]] || windows_process_id="$process_id"
    taskkill.exe //PID "$windows_process_id" //T //F >/dev/null 2>&1 || true
    wait "$process_id" >/dev/null 2>&1 || true
  else
    kill -TERM "$process_id" >/dev/null 2>&1 || true
    wait "$process_id" >/dev/null 2>&1 || true
  fi
}
