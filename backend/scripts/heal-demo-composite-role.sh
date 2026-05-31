#!/usr/bin/env bash
# Heals the `Demo` realm role on a *running* dineos Keycloak so it is composite
# over `Manager`, matching what `keycloak/realm-export.json` already declares.
#
# Why this is needed: Keycloak is started with `start-dev --import-realm`, which
# imports the realm ONLY if it does not already exist. A dev volume created
# before the `Demo -> Manager` composite was added to the export keeps a stale
# `Demo` role with no associated roles. Demo users then receive a token carrying
# only `Demo` (not `Manager`), so every `ManagerAndAbove` / `CashierAndAbove`
# backend policy returns 403 ("you don't have permission") even though the
# frontend renders the Manager UI (it maps Demo -> Manager client-side).
#
# This script brings the running realm in line with the committed export without
# wiping the volume. The alternative is a destructive re-import:
#     docker compose down -v && docker compose up
# which is the right move for a throwaway environment but loses all local data.
#
# What it does (idempotent):
#   1. Authenticates against the local Keycloak as `admin / admin`.
#   2. Resolves the `Manager` realm-role representation in the `dineos` realm.
#   3. POSTs it as a composite of the `Demo` role (Keycloak returns 204 whether
#      or not the association already exists).
#
# Usage:
#   ./backend/scripts/heal-demo-composite-role.sh            # localhost:8080
#   KC_BASE=https://kc.dev.example ./backend/scripts/heal-demo-composite-role.sh
#
# Prerequisites:
#   - The Keycloak container is running, OR a remote dev Keycloak is reachable
#     at $KC_BASE.
#   - `curl` and `python3` on PATH.

set -euo pipefail

KC_BASE="${KC_BASE:-http://localhost:8080}"
KC_REALM="${KC_REALM:-dineos}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASSWORD="${KC_ADMIN_PASSWORD:-admin}"
PARENT_ROLE="${PARENT_ROLE:-Demo}"
CHILD_ROLE="${CHILD_ROLE:-Manager}"

echo "[heal-demo] Acquiring admin access token from ${KC_BASE} ..."
ADMIN_TOKEN="$(
  curl -fsS -X POST "${KC_BASE}/realms/master/protocol/openid-connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=${KC_ADMIN_USER}" \
    -d "password=${KC_ADMIN_PASSWORD}" \
    | python3 -c "import sys,json;print(json.load(sys.stdin).get('access_token',''))"
)"

if [[ -z "${ADMIN_TOKEN}" ]]; then
  echo "[heal-demo] FAILED to obtain admin token — is Keycloak running at ${KC_BASE}?" >&2
  exit 1
fi

echo "[heal-demo] Fetching '${CHILD_ROLE}' role representation in realm '${KC_REALM}' ..."
CHILD_JSON="$(
  curl -fsS "${KC_BASE}/admin/realms/${KC_REALM}/roles/${CHILD_ROLE}" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}"
)"

if ! python3 -c "import sys,json;json.loads(sys.argv[1])['id']" "${CHILD_JSON}" >/dev/null 2>&1; then
  echo "[heal-demo] Could not resolve the '${CHILD_ROLE}' role — is the realm imported?" >&2
  exit 1
fi

echo "[heal-demo] Adding '${CHILD_ROLE}' as a composite of '${PARENT_ROLE}' ..."
curl -fsS -X POST "${KC_BASE}/admin/realms/${KC_REALM}/roles/${PARENT_ROLE}/composites" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H 'Content-Type: application/json' \
  -d "[${CHILD_JSON}]" > /dev/null

echo
echo "[heal-demo] DONE. '${PARENT_ROLE}' is now composite over '${CHILD_ROLE}'."
echo "[heal-demo] New demo logins will carry the '${CHILD_ROLE}' role in realm_access.roles,"
echo "[heal-demo] so ManagerAndAbove / CashierAndAbove backend policies will pass."
echo "[heal-demo] Existing demo sessions must log in again to pick up the new token."
