#!/usr/bin/env sh
set -eu

role="${1:-Operator}"

case "$role" in
  Operator|Supervisor) ;;
  *)
    echo "Role must be Operator or Supervisor." >&2
    exit 2
    ;;
esac

curl --fail --silent --show-error \
  --request POST \
  --header "Content-Type: application/json" \
  --data "{\"role\":\"$role\"}" \
  "${FULFILLMENT_API_URL:-http://127.0.0.1:5080}/dev/token"
