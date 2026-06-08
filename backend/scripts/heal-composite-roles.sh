#!/usr/bin/env bash
# Heals the composite realm-role graph on a *running* dineos Keycloak so it
# matches what `keycloak/realm-export.json` declares.
#
# Why this is needed: Keycloak is started with `start-dev --import-realm`, which
# imports the realm ONLY if it does not already exist. A dev volume created
# before a composite was added to the export keeps a stale role with no
# associated roles, so tokens are missing the expected roles and backend
# policies return 403. This script reconciles a running realm without wiping the
# volume; the destructive alternative is `docker compose down -v && up`.
#
# Default graph (#216 demo access + #staff-pin-auth Phase 2):
#   Owner -> Manager                       (owner keeps operational access while
#                                            the PIN UI is built; FE resolves Manager)
#   Demo  -> Owner, Manager, KitchenStaff  (demo gets the full owner experience
#                                            incl. staff mgmt + the kitchen board)
#
# What it does (idempotent): authenticates as admin/admin, and for each
# "Parent:Child,Child" entry resolves each child realm role and POSTs it as a
# composite of the parent (Keycloak returns 204 whether or not it already exists).
#
# Usage:
#   ./backend/scripts/heal-composite-roles.sh                     # default graph
#   COMPOSITE_GRAPH="Owner:Manager" ./backend/scripts/heal-composite-roles.sh
#   KC_BASE=https://kc.dev.example ./backend/scripts/heal-composite-roles.sh
#
# Prerequisites: the Keycloak container (or remote $KC_BASE) is running; `curl`
# and `python3` on PATH.

set -euo pipefail

KC_BASE="${KC_BASE:-http://localhost:8080}"
KC_REALM="${KC_REALM:-dineos}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASSWORD="${KC_ADMIN_PASSWORD:-admin}"

# Space-separated "Parent:Child,Child" entries. Override to heal a single edge.
COMPOSITE_GRAPH="${COMPOSITE_GRAPH:-Owner:Manager Demo:Owner,Manager,KitchenStaff}"

echo "[heal-roles] Acquiring admin access token from ${KC_BASE} ..."
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
  echo "[heal-roles] FAILED to obtain admin token — is Keycloak running at ${KC_BASE}?" >&2
  exit 1
fi

for ENTRY in ${COMPOSITE_GRAPH}; do
  PARENT_ROLE="${ENTRY%%:*}"
  CHILDREN="${ENTRY#*:}"

  for CHILD_ROLE in ${CHILDREN//,/ }; do
    echo "[heal-roles] Fetching '${CHILD_ROLE}' role representation in realm '${KC_REALM}' ..."
    CHILD_JSON="$(
      curl -fsS "${KC_BASE}/admin/realms/${KC_REALM}/roles/${CHILD_ROLE}" \
        -H "Authorization: Bearer ${ADMIN_TOKEN}"
    )"

    if ! python3 -c "import sys,json;json.loads(sys.argv[1])['id']" "${CHILD_JSON}" >/dev/null 2>&1; then
      echo "[heal-roles] Could not resolve the '${CHILD_ROLE}' role — is the realm imported?" >&2
      exit 1
    fi

    echo "[heal-roles] Adding '${CHILD_ROLE}' as a composite of '${PARENT_ROLE}' ..."
    curl -fsS -X POST "${KC_BASE}/admin/realms/${KC_REALM}/roles/${PARENT_ROLE}/composites" \
      -H "Authorization: Bearer ${ADMIN_TOKEN}" \
      -H 'Content-Type: application/json' \
      -d "[${CHILD_JSON}]" > /dev/null
  done
done

echo
echo "[heal-roles] DONE. Healed composite graph: ${COMPOSITE_GRAPH}."
echo "[heal-roles] Users must log in again to pick up roles in a fresh token."
