#!/usr/bin/env bash
# Pinned tools for the disposable Debian/Ubuntu-based .NET 10 Linux CI image.
set -euo pipefail

if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'CI tool installer supports Linux x86_64 only.' >&2
  exit 1
fi
if [[ $# -eq 0 ]]; then set -- node; fi
for tool in "$@"; do
  case "$tool" in node|docker|postman) ;; *) echo "Unknown CI tool: $tool" >&2; exit 1 ;; esac
done

if ! command -v curl >/dev/null || ! command -v xz >/dev/null; then
  apt-get update
  apt-get install --yes --no-install-recommends ca-certificates curl gzip tar xz-utils
fi

temporary_directory="$(mktemp -d)"
trap 'rm -rf -- "$temporary_directory"' EXIT
download() {
  local url="$1" destination="$2" checksum="$3"
  curl --fail --silent --show-error --location --retry 3 --connect-timeout 20 --max-time 180 "$url" --output "$destination"
  printf '%s  %s\n' "$checksum" "$destination" | sha256sum --check --status
}

for tool in "$@"; do
  case "$tool" in
    node)
      download 'https://nodejs.org/dist/v22.17.1/node-v22.17.1-linux-x64.tar.xz' \
        "$temporary_directory/node.tar.xz" 'ff04bc7c3ed7699ceb708dbaaf3580d899ff8bf67f17114f979e83aa74fc5a49'
      tar -xJf "$temporary_directory/node.tar.xz" --strip-components=1 -C /usr/local
      [[ "$(node --version)" == v22.17.1 ]]
      node --version
      ;;
    docker)
      download 'https://download.docker.com/linux/static/stable/x86_64/docker-27.2.0.tgz' \
        "$temporary_directory/docker.tgz" '3bd24d57ad188013d3ea3cc9366055452bf7a6e21756fdc0a682e80779d3c3bc'
      tar -xzf "$temporary_directory/docker.tgz" -C "$temporary_directory" docker/docker
      install -m 0755 "$temporary_directory/docker/docker" /usr/local/bin/docker
      docker --version | grep -F 'Docker version 27.2.0,'
      ;;
    postman)
      download 'https://dl-cli.pstmn.io/download/version/1.74.0/linux64' \
        "$temporary_directory/postman.tar.gz" 'b3a35679a210769cf8b5e6e86e16b93af1f3e5648b983d83b3305fe7f6d86a9e'
      mkdir -p /opt/cis-ci/postman-1.74.0
      tar -xzf "$temporary_directory/postman.tar.gz" -C /opt/cis-ci/postman-1.74.0
      chmod 0755 /opt/cis-ci/postman-1.74.0/postman-cli
      ln -sf /opt/cis-ci/postman-1.74.0/postman-cli /usr/local/bin/postman
      [[ "$(postman --version)" == 1.74.0 ]]
      postman --version
      ;;
  esac
done
