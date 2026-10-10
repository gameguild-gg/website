#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
WEB_DIR="$(cd -- "${SCRIPT_DIR}/.." && pwd)"
REPO_ROOT="$(cd -- "${WEB_DIR}/../.." && pwd)"
ARTIFACTS_DIR="${TESTING_LAB_E2E_ARTIFACTS:-${REPO_ROOT}/artifacts/test-results/testing-lab}"
RUNTIME_DIR="${TESTING_LAB_E2E_RUNTIME_DIR:-${ARTIFACTS_DIR}/runtime}"
# The 42xxx-44xxx bands fall inside Linux's default ephemeral port range
# (32768-60999), so the runner's own outbound sockets can already hold a
# candidate. Probe until one is actually free instead of failing the run.
pick_default_port() {
  local base="$1" port attempt
  for ((attempt = 1; attempt <= 10; attempt += 1)); do
    port=$((base + RANDOM % 1000))
    if node -e 'const net = require("node:net"); const port = Number(process.argv[1]); const server = net.createServer(); server.once("error", () => process.exit(1)); server.listen(port, "0.0.0.0", () => server.close());' "${port}"; then
      printf '%s\n' "${port}"
      return 0
    fi
  done
  echo "testing-lab-browser-e2e: no free port in the ${base} band after 10 attempts" >&2
  return 1
}

POSTGRES_PORT="${TESTING_LAB_E2E_POSTGRES_PORT:-$(pick_default_port 43000)}"
API_PORT="${TESTING_LAB_E2E_API_PORT:-$(pick_default_port 42000)}"
WEB_PORT="${TESTING_LAB_E2E_WEB_PORT:-$(pick_default_port 44000)}"
POSTGRES_USER="gameguild_e2e"
POSTGRES_PASSWORD="gameguild_e2e_password"
POSTGRES_DATABASE="gameguild_e2e"
ADMIN_PASSWORD="${E2E_SYSTEM_ADMIN_PASSWORD:-Admin123!}"
RUN_ID="${TESTING_LAB_E2E_RUN_ID:-$(date +%s)-$$}"
LOCK_DIR="${TESTING_LAB_E2E_LOCK_DIR:-${REPO_ROOT}/.tmp/testing-lab-browser-e2e.lock}"
NEXT_BUILD_DIR="${WEB_DIR}/.next"
NEXT_STANDALONE_SERVER="${NEXT_BUILD_DIR}/standalone/apps/web/server.js"
NEXT_STANDALONE_ROOT="$(dirname "${NEXT_STANDALONE_SERVER}")"
WEB_MODE="${TESTING_LAB_E2E_WEB_MODE:-development}"
API_MODE="${TESTING_LAB_E2E_API_MODE:-development}"
API_READY_TIMEOUT_SECONDS="${TESTING_LAB_E2E_API_READY_TIMEOUT_SECONDS:-600}"
API_RELEASE_DIR="${TESTING_LAB_E2E_API_RELEASE_DIR:-${REPO_ROOT}/.tmp/testing-lab-release-api}"
API_RELEASE_DLL="${API_RELEASE_DIR}/GameGuild.API.dll"
LOCK_ACQUIRED="0"
POSTGRES_CONTAINER="gameguild-testing-lab-e2e-postgres-${RUN_ID}"
API_PID=""
WEB_PID=""

mkdir -p "${RUNTIME_DIR}" "${ARTIFACTS_DIR}" "$(dirname -- "${LOCK_DIR}")"

stop_process() {
  local pid="${1:-}"
  if [[ -n "${pid}" ]] && kill -0 "${pid}" >/dev/null 2>&1; then
    if [[ "$(uname -s)" == MINGW* || "$(uname -s)" == CYGWIN* ]]; then
      local win_pid
      win_pid="$(ps -W -p "${pid}" -l 2>/dev/null | awk 'NR > 1 { print $4; exit }')"
      if [[ -n "${win_pid}" ]]; then
        MSYS_NO_PATHCONV=1 taskkill.exe /PID "${win_pid}" /T /F >/dev/null 2>&1 || true
      else
        kill "${pid}" >/dev/null 2>&1 || true
      fi

      # MSYS background PIDs can remain waitable after taskkill has already
      # terminated their Windows process tree. Do not let cleanup block on
      # that stale shell PID; the port-based fallback below owns the server.
      return
    else
      kill "${pid}" >/dev/null 2>&1 || true
      wait "${pid}" >/dev/null 2>&1 || true
    fi
  fi
}

stop_port_listener() {
  local port="$1"
  if [[ "$(uname -s)" != MINGW* && "$(uname -s)" != CYGWIN* ]]; then
    return
  fi

  # The runner reserves both ports before it starts. Resolving listeners by
  # the exact owned port is a reliable fallback when MSYS detaches a child
  # process and no longer maps its POSIX PID back to the Windows process.
  local win_pid
  while IFS= read -r win_pid; do
    [[ -n "${win_pid}" ]] || continue
    MSYS_NO_PATHCONV=1 taskkill.exe /PID "${win_pid}" /T /F >/dev/null 2>&1 || true
  done < <(netstat.exe -ano -p tcp | tr -d '\r' | awk -v port=":${port}" '$1 == "TCP" && $2 ~ (port "$") && $4 == "LISTENING" { print $5 }')
}

cleanup() {
  local exit_code=$?
  trap - EXIT INT TERM
  # Preserve host diagnostics even when fixture bootstrap fails before the first
  # browser page. The CI uploader collects artifacts/test-results on all exits.
  printf '{"runId":"%s","exitCode":%s}\n' "${RUN_ID}" "${exit_code}" >"${ARTIFACTS_DIR}/runner-result.json"
  stop_port_listener "${WEB_PORT}"
  stop_port_listener "${API_PORT}"
  if [[ "$(uname -s)" != MINGW* && "$(uname -s)" != CYGWIN* ]]; then
    stop_process "${WEB_PID}"
    stop_process "${API_PID}"
  fi
  docker rm -f "${POSTGRES_CONTAINER}" >/dev/null 2>&1 || true
  if [[ "${LOCK_ACQUIRED}" == "1" ]]; then
    rmdir "${LOCK_DIR}" >/dev/null 2>&1 || true
  fi
  exit "${exit_code}"
}
trap cleanup EXIT INT TERM

if ! mkdir "${LOCK_DIR}" >/dev/null 2>&1; then
  echo "Another Testing Lab browser E2E run already owns ${LOCK_DIR}." >&2
  exit 1
fi
LOCK_ACQUIRED="1"

case "${WEB_MODE}" in
  development|production) ;;
  *)
    echo "TESTING_LAB_E2E_WEB_MODE must be development or production, received: ${WEB_MODE}" >&2
    exit 1
    ;;
esac

case "${API_MODE}" in
  development|release) ;;
  *)
    echo "TESTING_LAB_E2E_API_MODE must be development or release, received: ${API_MODE}" >&2
    exit 1
    ;;
esac

case "${NEXT_BUILD_DIR}" in
  "${WEB_DIR}/.next") ;;
  *)
    echo "Refusing to clean unexpected Next build directory: ${NEXT_BUILD_DIR}" >&2
    exit 1
    ;;
esac
if [[ "${WEB_MODE}" == "development" ]]; then
  rm -rf -- "${NEXT_BUILD_DIR}"
elif [[ ! -f "${NEXT_BUILD_DIR}/BUILD_ID" || ! -f "${NEXT_STANDALONE_SERVER}" ]]; then
  echo "A standalone production Next build is required before TESTING_LAB_E2E_WEB_MODE=production." >&2
  exit 1
fi

stage_standalone_assets() {
  # Next intentionally leaves static and public assets outside of output:
  # standalone. A release runner must stage them beside server.js, otherwise
  # the HTML loads but every hydrated route fails with /_next/static 404s.
  local standalone_next_dir="${NEXT_STANDALONE_ROOT}/.next"
  rm -rf -- "${standalone_next_dir}/static" "${NEXT_STANDALONE_ROOT}/public"
  mkdir -p "${standalone_next_dir}" "${NEXT_STANDALONE_ROOT}/public"
  cp -R "${NEXT_BUILD_DIR}/static" "${standalone_next_dir}/static"
  cp -R "${WEB_DIR}/public/." "${NEXT_STANDALONE_ROOT}/public/"
}

if [[ "${WEB_MODE}" == "production" ]]; then
  stage_standalone_assets
fi

if [[ "${API_MODE}" == "release" && ! -f "${API_RELEASE_DLL}" ]]; then
  echo "A published API DLL is required before TESTING_LAB_E2E_API_MODE=release." >&2
  exit 1
fi

wait_for_http() {
  local url="$1"
  local label="$2"
  local log_file="$3"
  local attempts="${4:-120}"
  local process_pid="${5:-}"
  for ((attempt = 1; attempt <= attempts; attempt += 1)); do
    if [[ -n "${process_pid}" ]] && ! kill -0 "${process_pid}" >/dev/null 2>&1; then
      echo "${label} process ${process_pid} exited before readiness." >&2
      if [[ -f "${log_file}" ]]; then
        tail -n 160 "${log_file}" >&2
      fi
      return 1
    fi
    if curl --fail --silent --show-error --connect-timeout 1 --max-time 5 "${url}" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  echo "${label} did not become ready at ${url}." >&2
  if [[ -f "${log_file}" ]]; then
    tail -n 160 "${log_file}" >&2
  fi
  return 1
}

assert_port_available() {
  local port="$1"
  local label="$2"
  node -e 'const net = require("node:net"); const port = Number(process.argv[1]); const label = process.argv[2]; const server = net.createServer(); server.once("error", () => { console.error(label + " port " + port + " is already in use."); process.exit(1); }); server.listen(port, "0.0.0.0", () => server.close());' "${port}" "${label}"
}

assert_port_available "${POSTGRES_PORT}" "PostgreSQL"
assert_port_available "${API_PORT}" "GameGuild API"
assert_port_available "${WEB_PORT}" "GameGuild web"
if [[ "${TESTING_LAB_E2E_SKIP_CLIENT_BUILD:-0}" != "1" ]]; then
  echo "[testing-lab-browser-e2e] building branch-local API client"
  (cd "${REPO_ROOT}" && pnpm --filter @game-guild/client build)
fi

echo "[testing-lab-browser-e2e] starting disposable PostgreSQL"
docker run --detach --rm --name "${POSTGRES_CONTAINER}" --publish "127.0.0.1:${POSTGRES_PORT}:5432" --env "POSTGRES_USER=${POSTGRES_USER}" --env "POSTGRES_PASSWORD=${POSTGRES_PASSWORD}" --env "POSTGRES_DB=${POSTGRES_DATABASE}" public.ecr.aws/docker/library/postgres:16-alpine >/dev/null
for ((attempt = 1; attempt <= 60; attempt += 1)); do
  if docker exec "${POSTGRES_CONTAINER}" pg_isready --username "${POSTGRES_USER}" --dbname "${POSTGRES_DATABASE}" >/dev/null 2>&1; then
    break
  fi
  if [[ "${attempt}" -eq 60 ]]; then
    docker logs "${POSTGRES_CONTAINER}" >&2 || true
    exit 1
  fi
  sleep 1
done

CONNECTION_STRING="Host=127.0.0.1;Port=${POSTGRES_PORT};Database=${POSTGRES_DATABASE};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD};Include Error Detail=true"
API_LOG="${RUNTIME_DIR}/api.log"
WEB_LOG="${RUNTIME_DIR}/web.log"
API_ENV=(
  "ASPNETCORE_ENVIRONMENT=${TESTING_LAB_E2E_ASPNETCORE_ENVIRONMENT:-Development}"
  "ASPNETCORE_URLS=http://127.0.0.1:${API_PORT}"
  "ConnectionStrings__DefaultConnection=${CONNECTION_STRING}"
  "ConnectionStrings__AuthenticationDb=${CONNECTION_STRING}"
  "ConnectionStrings__MigrationConnection=${CONNECTION_STRING}"
  "POSTGRES_HOST=127.0.0.1"
  "POSTGRES_PORT=${POSTGRES_PORT}"
  "POSTGRES_DB=${POSTGRES_DATABASE}"
  "POSTGRES_USER=${POSTGRES_USER}"
  "POSTGRES_PASSWORD=${POSTGRES_PASSWORD}"
  "Database__RunStartupInitialization=true"
  "Database__FailStartupOnMigrationFailure=true"
  "Database__FailStartupOnSeedFailure=true"
  "Redis__Enabled=false"
  "SeedData__ImportSnapshotCourses=false"
  "Seed__AdminPassword=${ADMIN_PASSWORD}"
  "Jwt__SecretKey=testing-lab-e2e-jwt-secret-key-at-least-32-characters"
  "Authentication__JwtSecretKey=testing-lab-e2e-jwt-secret-key-at-least-32-characters"
)

echo "[testing-lab-browser-e2e] starting API on ${API_PORT}"
(
  if [[ "${API_MODE}" == "release" ]]; then
    cd "${API_RELEASE_DIR}"
    env "${API_ENV[@]}" dotnet "${API_RELEASE_DLL}"
  else
    cd "${REPO_ROOT}"
    env "${API_ENV[@]}" dotnet run --no-launch-profile --project apps/api/Source/GameGuild.API/GameGuild.API.csproj --urls "http://127.0.0.1:${API_PORT}"
  fi
) >"${API_LOG}" 2>&1 &
API_PID=$!
wait_for_http "http://127.0.0.1:${API_PORT}/ready" "GameGuild API" "${API_LOG}" "${API_READY_TIMEOUT_SECONDS}" "${API_PID}"

WEB_ENV=(
  "NODE_ENV=${WEB_MODE}"
  "API_URL=http://127.0.0.1:${API_PORT}"
  "NEXT_PUBLIC_API_URL=http://127.0.0.1:${API_PORT}"
  "GAMEGUILD_DISABLE_WEBPACK_CACHE=1"
  "NEXT_PUBLIC_APP_URL=http://127.0.0.1:${WEB_PORT}"
  "NEXTAUTH_URL=http://127.0.0.1:${WEB_PORT}"
  "AUTH_SECRET=testing-lab-e2e-auth-secret-at-least-32-characters"
  "NEXTAUTH_SECRET=testing-lab-e2e-auth-secret-at-least-32-characters"
  # The production artifact is served over loopback HTTP in this local smoke
  # test. Keep cookies secure in real deployments; override only this runner
  # so Playwright can send the session back to the standalone server.
  "AUTH_COOKIE_SECURE=false"
  "AUTH_TRUST_HOST=true"
)

echo "[testing-lab-browser-e2e] starting web on ${WEB_PORT}"
(
  cd "${WEB_DIR}"
  if [[ "${WEB_MODE}" == "production" ]]; then
    env "${WEB_ENV[@]}" "HOSTNAME=127.0.0.1" "PORT=${WEB_PORT}" node "${NEXT_STANDALONE_SERVER}"
  else
    env "${WEB_ENV[@]}" pnpm exec next dev --turbopack --hostname 127.0.0.1 --port "${WEB_PORT}"
  fi
) >"${WEB_LOG}" 2>&1 &
WEB_PID=$!
wait_for_http "http://127.0.0.1:${WEB_PORT}/api/health" "GameGuild web" "${WEB_LOG}" 180 "${WEB_PID}"

BROWSER_ENV=(
  "TESTING_LAB_E2E_DATABASE_MODE=disposable"
  "API_BASE_URL=http://127.0.0.1:${API_PORT}"
  "TESTING_LAB_E2E_BASE_URL=http://127.0.0.1:${WEB_PORT}"
  "TESTING_LAB_E2E_ARTIFACTS=${ARTIFACTS_DIR}"
  "E2E_SYSTEM_ADMIN_EMAIL=admin@game-guild.com"
  "E2E_SYSTEM_ADMIN_PASSWORD=${ADMIN_PASSWORD}"
)

echo "[testing-lab-browser-e2e] running browser journeys"
env "${BROWSER_ENV[@]}" node "${SCRIPT_DIR}/testing-lab-browser-e2e.mjs"
