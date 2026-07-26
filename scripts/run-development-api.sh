#!/usr/bin/env sh
set -eu

repository_root="$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)"
cd "$repository_root"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="${ASPNETCORE_URLS:-http://127.0.0.1:5080}"
export Authentication__SigningKey="$(
  if [ -n "${Authentication__SigningKey:-}" ]; then
    printf '%s' "$Authentication__SigningKey"
  else
    openssl rand -hex 32
  fi
)"

exec dotnet run --project src/Fulfillment.Api/Fulfillment.Api.csproj
