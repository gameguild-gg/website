#!/usr/bin/env bash

# Credentials exist only for this runner's loopback-bound, disposable databases.
# Node is already a prerequisite of both callers; use its OS-backed CSPRNG.
new_disposable_postgres_password() {
  node -e 'process.stdout.write(require("node:crypto").randomBytes(32).toString("hex"))'
}

declare -a disposable_postgres_secrets=()
register_disposable_postgres_password() {
  local password="$1"
  [[ "$password" =~ ^[a-f0-9]{64}$ ]] || {
    printf 'Invalid generated disposable PostgreSQL credential\n' >&2
    return 1
  }
  disposable_postgres_secrets+=("$password")
  if [[ "${GITHUB_ACTIONS:-}" == true ]]; then
    printf '::add-mask::%s\n' "$password"
  fi
}

print_redacted_ci_command() {
  local argument password
  printf '> '
  for argument in "$@"; do
    for password in "${disposable_postgres_secrets[@]}"; do
      argument="${argument//"$password"/[REDACTED]}"
    done
    printf '%q ' "$argument"
  done
  printf '\n'
}

redact_disposable_postgres_output() {
  # Buffer through line boundaries so a password split across stream chunks is
  # still removed. Give the child process credentials via its environment.
  GAMEGUILD_CI_LOG_SECRETS="$(printf '%s\n' "${disposable_postgres_secrets[@]}")" node -e '
    const secrets = (process.env.GAMEGUILD_CI_LOG_SECRETS || "").split("\n").filter(Boolean);
    let pending = "";
    const redact = (value) => secrets.reduce((text, secret) => text.split(secret).join("[REDACTED]"), value);
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => {
      pending += chunk;
      const boundary = pending.lastIndexOf("\n");
      if (boundary >= 0) {
        process.stdout.write(redact(pending.slice(0, boundary + 1)));
        pending = pending.slice(boundary + 1);
      }
    });
    process.stdin.on("end", () => process.stdout.write(redact(pending)));
    process.stdin.on("error", () => { process.exitCode = 1; });
  '
}
