#!/usr/bin/env sh
set -eu

base_url="${FULFILLMENT_API_URL:-http://localhost:8080}"
attempt=0

until curl --fail --silent "$base_url/health/ready" >/dev/null; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 30 ]; then
    echo "API did not become ready at $base_url." >&2
    exit 1
  fi
  sleep 2
done

echo "PostgreSQL-backed API is ready."
