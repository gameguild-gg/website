#!/usr/bin/env bash

# GitHub-hosted runners share egress IPs that Docker Hub and public ECR both
# throttle intermittently ("toomanyrequests"), which has killed required-gate
# jobs at container start. Pull once with retry-and-backoff before every CI
# container start so `docker run` never has to reach the registry itself.
pull_ci_image() {
  local image="$1" attempt delay
  for attempt in 1 2 3 4 5 6; do
    if docker pull "$image" >/dev/null 2>&1; then
      return 0
    fi
    delay=$((5 * (2 ** (attempt - 1))))
    printf 'docker pull %s failed (attempt %d/6); retrying in %ds\n' \
      "$image" "$attempt" "$delay" >&2
    sleep "$delay"
  done
  printf 'docker pull %s still failing after 6 attempts\n' "$image" >&2
  return 1
}
