#!/bin/sh
# Provision a single-node Garage development cluster via the v2 admin HTTP API.
# Cross-platform: no docker socket needed.
set -eu

GARAGE_HOST="${GARAGE_HOST:-garage}"
GARAGE_ADMIN_PORT="${GARAGE_ADMIN_PORT:-3903}"
ADMIN_TOKEN="${GARAGE_ADMIN_TOKEN:-development-garage-admin-token}"
API_BASE="http://${GARAGE_HOST}:${GARAGE_ADMIN_PORT}/v2"
AUTH="Authorization: Bearer ${ADMIN_TOKEN}"

BUCKET="${GARAGE_S3_BUCKET:-gameguild-assets}"
KEY_NAME="${GARAGE_KEY_NAME:-gameguild-api}"
KEY_ID="${GARAGE_KEY_ID:-GK333333333333333333333333}"
KEY_SECRET="${GARAGE_KEY_SECRET:-2222222222222222222222222222222222222222222222222222222222222222}"
ZONE="${GARAGE_ZONE:-local}"
CAPACITY="${GARAGE_CAPACITY:-1000000000}"

wait_for_admin() {
  i=0
  while [ "$i" -lt 60 ]; do
    i=$((i + 1))
    if curl -fsS -H "$AUTH" "${API_BASE}/GetClusterHealth" >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

echo "[garage-init] waiting for admin API at ${API_BASE}..."
if ! wait_for_admin; then
  echo "[garage-init] garage admin API not reachable in 60s" >&2
  exit 1
fi

NODE_ID=$(curl -fsS -H "$AUTH" "${API_BASE}/GetClusterStatus" \
  | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([0-9a-f]*\)".*/\1/p' \
  | head -n1)

if [ -z "$NODE_ID" ]; then
  echo "[garage-init] could not parse node id from GetClusterStatus" >&2
  curl -sS -H "$AUTH" "${API_BASE}/GetClusterStatus" >&2
  exit 1
fi
echo "[garage-init] node=$NODE_ID zone=$ZONE capacity=$CAPACITY"

curl -sS -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "${API_BASE}/UpdateClusterLayout" \
  -d "{\"roles\":[{\"id\":\"${NODE_ID}\",\"zone\":\"${ZONE}\",\"capacity\":${CAPACITY},\"tags\":[]}]}" \
  >/dev/null 2>&1 || true

LAYOUT_VERSION=$(curl -fsS -H "$AUTH" "${API_BASE}/GetClusterLayout" \
  | sed -n 's/.*"version"[[:space:]]*:[[:space:]]*\([0-9]*\).*/\1/p' \
  | head -n1)
APPLY_VERSION=${LAYOUT_VERSION:-0}
APPLY_VERSION=$((APPLY_VERSION + 1))
echo "[garage-init] applying layout version ${APPLY_VERSION}"
curl -sS -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "${API_BASE}/ApplyClusterLayout" \
  -d "{\"version\":${APPLY_VERSION}}" \
  >/dev/null 2>&1 || true

echo "[garage-init] creating bucket '${BUCKET}'"
RESP=$(curl -fsS -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "${API_BASE}/CreateBucket" \
  -d "{\"globalAlias\":\"${BUCKET}\"}" 2>/dev/null || true)
BUCKET_ID=$(printf '%s' "$RESP" \
  | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([0-9a-f]*\)".*/\1/p' \
  | head -n1)

if [ -z "$BUCKET_ID" ]; then
  echo "[garage-init] bucket may already exist; looking up by globalAlias"
  BUCKET_ID=$(curl -fsS -H "$AUTH" \
    "${API_BASE}/GetBucketInfo?globalAlias=${BUCKET}" 2>/dev/null \
    | sed -n 's/.*"id"[[:space:]]*:[[:space:]]*"\([0-9a-f]*\)".*/\1/p' \
    | head -n1)
fi

if [ -z "$BUCKET_ID" ]; then
  echo "[garage-init] could not resolve bucket id for ${BUCKET}" >&2
  exit 1
fi

echo "[garage-init] importing key '${KEY_NAME}' (id=${KEY_ID})"
curl -sS -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "${API_BASE}/ImportKey" \
  -d "{\"accessKeyId\":\"${KEY_ID}\",\"secretAccessKey\":\"${KEY_SECRET}\",\"name\":\"${KEY_NAME}\"}" \
  >/dev/null 2>&1 || true

echo "[garage-init] granting ${KEY_NAME} RWO on ${BUCKET} (id=${BUCKET_ID})"
curl -sS -X POST -H "$AUTH" -H "Content-Type: application/json" \
  "${API_BASE}/AllowBucketKey" \
  -d "{\"accessKeyId\":\"${KEY_ID}\",\"bucketId\":\"${BUCKET_ID}\",\"permissions\":{\"read\":true,\"write\":true,\"owner\":true}}" \
  >/dev/null 2>&1 || true

echo "[garage-init] bucket=${BUCKET} key=${KEY_NAME} key_id=${KEY_ID} provisioned"
