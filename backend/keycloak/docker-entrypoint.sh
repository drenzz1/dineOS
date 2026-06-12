#!/bin/sh
set -eu

template="/opt/keycloak/realm-export.template.json"
output="/opt/keycloak/data/import/realm-export.json"

escape_sed() {
  printf '%s' "$1" | sed 's/[\/&]/\\&/g'
}

mkdir -p /opt/keycloak/data/import

google_client_id=$(escape_sed "${GOOGLE_CLIENT_ID:-}")
google_client_secret=$(escape_sed "${GOOGLE_CLIENT_SECRET:-}")
google_auth_client_secret=$(escape_sed "${GOOGLE_AUTH_CLIENT_SECRET:-dev-google-auth-client-secret-change-me}")

sed \
  -e "s/__GOOGLE_CLIENT_ID__/$google_client_id/g" \
  -e "s/__GOOGLE_CLIENT_SECRET__/$google_client_secret/g" \
  -e "s/__GOOGLE_AUTH_CLIENT_SECRET__/$google_auth_client_secret/g" \
  "$template" > "$output"

exec /opt/keycloak/bin/kc.sh "$@"
