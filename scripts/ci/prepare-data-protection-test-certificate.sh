#!/usr/bin/env bash
set -Eeuo pipefail

# Only the CI setup action invokes this. Runtime startup never creates a fallback key.
if [[ $# != 0 || "${CI:-}" != true || -z "${GITHUB_ENV:-}" || -z "${RUNNER_TEMP:-}" ]]; then
  printf 'Disposable CI certificate preparation requires the GitHub runner environment.\n' >&2
  exit 1
fi
if [[ -n "${DATAPROTECTION_CERTIFICATE_BASE64:-}" || -n "${DATAPROTECTION_CERTIFICATE_KEY_BASE64:-}" ]]; then
  if [[ -z "${DATAPROTECTION_CERTIFICATE_BASE64:-}" || -z "${DATAPROTECTION_CERTIFICATE_KEY_BASE64:-}" ]]; then
    printf 'Both explicit CI key-protection certificate variables are required.\n' >&2
    exit 1
  fi
  exit 0
fi

umask 077
certificate_directory="$(mktemp -d "${RUNNER_TEMP%/}/dataprotection-ci.XXXXXX")"
cleanup() {
  rm -f -- "$certificate_directory/key.pem" "$certificate_directory/certificate.pem"
  rmdir -- "$certificate_directory"
}
trap cleanup EXIT
openssl req -x509 -newkey rsa:2048 -noenc -sha256 -days 2 \
  -subj '/CN=disposable-ci-key-protection' \
  -keyout "$certificate_directory/key.pem" \
  -out "$certificate_directory/certificate.pem" >/dev/null 2>&1
certificate_base64="$(base64 -w 0 "$certificate_directory/certificate.pem")"
key_base64="$(base64 -w 0 "$certificate_directory/key.pem")"
printf '::add-mask::%s\n' "$key_base64"
{
  printf 'DATAPROTECTION_CERTIFICATE_BASE64=%s\n' "$certificate_base64"
  printf 'DATAPROTECTION_CERTIFICATE_KEY_BASE64=%s\n' "$key_base64"
} >> "$GITHUB_ENV"
