#!/usr/bin/env bash
set -Eeuo pipefail

root="$(git -c core.commitGraph=false rev-parse --show-toplevel)"
contract_root="$root/contracts/certificate-anchoring"
artifact_root="$contract_root/artifacts/native"
temporary="$(mktemp -d "${RUNNER_TEMP:?}/evm-certificate.XXXXXX")"
readonly rpc_url='http://127.0.0.1:8545'
anvil_pid=''
tls_pid=''
cleanup() {
  local status=$?
  trap - EXIT
  if [[ -n "$tls_pid" ]]; then kill "$tls_pid" 2>/dev/null || true; fi
  if [[ -n "$anvil_pid" ]]; then kill "$anvil_pid" 2>/dev/null || true; fi
  rm -f -- "$temporary/server-key.pem"
  exit "$status"
}
trap cleanup EXIT
mkdir -p "$artifact_root"
git -c core.commitGraph=false rev-parse HEAD > "$artifact_root/checkout-commit.txt"
git -c core.commitGraph=false rev-parse 'HEAD^{tree}' > "$artifact_root/checkout-tree.txt"
forge --version > "$artifact_root/foundry-version.txt"
dotnet --version > "$artifact_root/dotnet-version.txt"
export NO_PROXY="127.0.0.1,localhost,${NO_PROXY:-}"

anvil --silent --host 127.0.0.1 --port 8545 --chain-id 12345 > "$temporary/anvil.log" 2>&1 &
anvil_pid=$!
for _attempt in {1..60}; do
  kill -0 "$anvil_pid"
  if curl --noproxy 127.0.0.1 --fail --silent --output /dev/null --header 'Content-Type: application/json' \
      --data '{"jsonrpc":"2.0","id":1,"method":"eth_chainId","params":[]}' "$rpc_url"; then break; fi
  sleep 0.25
done
[[ "$(cast chain-id --rpc-url "$rpc_url")" == '12345' ]]

private_hex="$(openssl rand -hex 32)"
private_key="0x$private_hex"
printf '::add-mask::%s\n' "$private_hex"
printf '::add-mask::%s\n' "$private_key"
signer="$(cast wallet address --private-key "$private_key")"
cast rpc --rpc-url "$rpc_url" anvil_setBalance "$signer" 0x56bc75e2d63100000 > /dev/null
issuer="0x$(printf '22%.0s' {1..32})"
active_hash="0x$(printf '33%.0s' {1..32})"
revoked_hash="0x$(printf '44%.0s' {1..32})"
cd "$contract_root"
forge create src/CertificateAnchorRegistry.sol:CertificateAnchorRegistry --broadcast --json \
  --rpc-url "$rpc_url" --private-key "$private_key" --constructor-args "$issuer" "$signer" "$signer" \
  > "$artifact_root/deployment.json"
contract="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["deployedTo"])' "$artifact_root/deployment.json")"
cast send --rpc-url "$rpc_url" --private-key "$private_key" --json "$contract" 'anchor(bytes32)' "$active_hash" > "$artifact_root/active-anchor-receipt.json"
cast send --rpc-url "$rpc_url" --private-key "$private_key" --json "$contract" 'anchor(bytes32)' "$revoked_hash" > "$artifact_root/revoked-anchor-receipt.json"
cast send --rpc-url "$rpc_url" --private-key "$private_key" --json "$contract" 'revoke(bytes32)' "$revoked_hash" > "$artifact_root/revoke-receipt.json"
unset private_key private_hex
cast code --rpc-url "$rpc_url" "$contract" > "$artifact_root/deployed-runtime.hex"

openssl req -x509 -newkey rsa:2048 -nodes -days 1 -subj '/CN=localhost' \
  -addext 'subjectAltName=DNS:localhost,IP:127.0.0.1' \
  -keyout "$temporary/server-key.pem" -out "$temporary/server-certificate.pem" > /dev/null 2>&1
chmod 600 "$temporary/server-key.pem"
openssl x509 -in "$temporary/server-certificate.pem" -outform DER -out "$temporary/server-certificate.der"
python3 "$contract_root/ci/rpc_tls_proxy.py" --certificate "$temporary/server-certificate.pem" \
  --key "$temporary/server-key.pem" > "$temporary/tls-proxy.log" 2>&1 &
tls_pid=$!
for _attempt in {1..60}; do
  kill -0 "$tls_pid"
  if curl --noproxy 127.0.0.1 --cacert "$temporary/server-certificate.pem" --fail --silent --output /dev/null \
      --header 'Content-Type: application/json' --data '{"jsonrpc":"2.0","id":1,"method":"eth_chainId","params":[]}' \
      'https://127.0.0.1:8546/'; then break; fi
  sleep 0.25
done

python3 - "$artifact_root" "$temporary/server-certificate.der" "$signer" "$issuer" "$active_hash" "$revoked_hash" <<'PY'
import hashlib
import json
import pathlib
import sys

art = pathlib.Path(sys.argv[1])
def receipt(name):
    value = json.loads((art / name).read_text())
    if value.get("status") not in ("0x1", "0x01", 1):
        raise ValueError("Native fixture transaction did not succeed")
    return value

deployment = json.loads((art / "deployment.json").read_text())
runtime = (art / "deployed-runtime.hex").read_text().strip()
if not runtime.startswith("0x") or len(runtime) < 10:
    raise ValueError("Native fixture registry code is absent")
fixture = {
    "chainId": 12345,
    "rpcEndpoint": "https://127.0.0.1:8546/",
    "serverCertificateSha256": hashlib.sha256(pathlib.Path(sys.argv[2]).read_bytes()).hexdigest(),
    "contractAddress": deployment["deployedTo"],
    "issuerId": sys.argv[4],
    "expectedRuntimeCodeSha256": hashlib.sha256(bytes.fromhex(runtime[2:])).hexdigest(),
    "signerAddress": sys.argv[3],
    "activeCertificateHash": sys.argv[5],
    "activeAnchorTransactionHash": receipt("active-anchor-receipt.json")["transactionHash"],
    "revokedCertificateHash": sys.argv[6],
    "revokedAnchorTransactionHash": receipt("revoked-anchor-receipt.json")["transactionHash"],
    "revokeTransactionHash": receipt("revoke-receipt.json")["transactionHash"],
}
(art / "native-fixture.json").write_text(json.dumps(fixture, indent=2) + "\n")
PY
export GAMEGUILD_EVM_NATIVE_FIXTURE="$artifact_root/native-fixture.json"
cd "$root"
dotnet test "$contract_root/dotnet-tests/GameGuild.CertificateAnchoring.NativeTests.csproj" \
  --configuration Release --logger 'trx;LogFileName=evm-native.trx' --results-directory "$artifact_root/test-results" \
  --verbosity minimal /p:TreatWarningsAsErrors=true
