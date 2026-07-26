#!/usr/bin/env sh
set -eu

api_url="${FULFILLMENT_API_URL:-http://127.0.0.1:5080}"
attempt=1

while ! curl --fail --silent --output /dev/null "$api_url/health/ready"; do
  if [ "$attempt" -ge 30 ]; then
    echo "API did not become ready at $api_url within 30 seconds." >&2
    exit 1
  fi

  attempt=$((attempt + 1))
  sleep 1
done

token_response="$(
  curl --fail --silent --show-error \
    --request POST \
    --header "Content-Type: application/json" \
    --data '{"role":"Operator"}' \
    "$api_url/dev/token"
)"
access_token="$(
  printf '%s' "$token_response" |
    python3 -c 'import json, sys; print(json.load(sys.stdin)["accessToken"])'
)"

curl --fail --silent --show-error \
  --output /dev/null \
  --header "Authorization: Bearer $access_token" \
  "$api_url/api/fulfillment-requests?page=1&pageSize=1"

echo "Quick-start smoke check passed at $api_url."
