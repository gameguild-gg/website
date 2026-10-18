#!/usr/bin/env bash

# GitHub-hosted runners share egress IPs that Docker Hub and public ECR both
# throttle intermittently ("toomanyrequests"). Reuse exact digest references
# already present on this runner and bound registry acquisition to under two
# minutes so an outage cannot consume the required tests' job budget.
pull_ci_image() {
  local image="$1" attempt delay status
  if [[ "$image" =~ @sha256:[a-f0-9]{64}$ ]] &&
    timeout --kill-after=1s 3s docker image inspect "$image" >/dev/null 2>&1; then
    return 0
  fi

  for attempt in 1 2 3; do
    if timeout --kill-after=5s 25s docker pull "$image" >/dev/null; then
      return 0
    else
      status=$?
    fi
    if (( attempt == 3 )); then
      printf 'docker pull %s still failing after 3 attempts (last exit %d)\n' "$image" "$status" >&2
      return 1
    fi
    delay=$((5 * attempt))
    printf 'docker pull %s failed (attempt %d/3, exit %d); retrying in %ds\n' \
      "$image" "$attempt" "$status" "$delay" >&2
    sleep "$delay"
  done
}
