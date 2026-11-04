#!/usr/bin/env bash
set -euo pipefail

for attempt in {1..30}; do
  if docker info >/dev/null 2>&1; then
    docker version --format 'Docker server {{.Server.Version}}'
    exit 0
  fi
  sleep 2
done
echo 'Docker did not become ready; verify the privileged runner and DIND service.' >&2
docker info
exit 1
