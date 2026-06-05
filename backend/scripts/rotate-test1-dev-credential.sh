#!/usr/bin/env bash
# Rotates the dev-realm credential for `test1@gmail.com`, the account that was
# manually unblocked during the 2026-05-22 "Account is not fully set up"
# diagnosis. Run this once against the local Docker Keycloak (or any shared
# dev instance you pulled this branch on) so the disclosed-in-transcript
# password is no longer accepted.
#
# What it does (in order, idempotent):
#   1. Authenticates kcadm against the local Keycloak as `admin / admin`.
#   2. Resolves the user's UUID by email in the `dineos` realm.
#   3. Disables the account so the disclosed credential cannot grant tokens
#      while you decide whether to keep the user.
#   4. Resets the password to a fresh value with `temporary=true` and stamps
#      the `UPDATE_PASSWORD` required action — forcing whoever uses the
#      account next to set their own password.
#
# Usage:
#   ./backend/scripts/rotate-test1-dev-credential.sh                 # localhost:8080
#   KC_BASE=https://kc.dev.example./bin/sh -c "<script>"             # override host
#
# Prerequisites:
#   - The `dineos-keycloak` container is running, OR a remote dev Keycloak
#     is reachable at $KC_BASE.
#   - `openssl` is on PATH (for generating the fresh password).
#   - `kcadm.sh` lives inside the keycloak container at `/opt/keycloak/bin/`
#     when using the local Docker path; otherwise the script falls back to
#     plain `curl` against the Admin REST API.

set -euo pipefail

KC_BASE="${KC_BASE:-http://localhost:8080}"
KC_REALM="${KC_REALM:-dineos}"
KC_ADMIN_USER="${KC_ADMIN_USER:-admin}"
KC_ADMIN_PASSWORD="${KC_ADMIN_PASSWORD:-admin}"
TARGET_EMAIL="${TARGET_EMAIL:-test1@gmail.com}"

NEW_PASSWORD="$(openssl rand -base64 24 | tr -d '/+=' | cut -c1-20)!9A"

echo "[rotate-test1] Acquiring admin access token from ${KC_BASE} ..."
ADMIN_TOKEN="$(
  curl -fsS -X POST "${KC_BASE}/realms/master/protocol/openid-connect/token" \
    -H 'Content-Type: application/x-www-form-urlencoded' \
    -d "grant_type=password" \
    -d "client_id=admin-cli" \
    -d "username=${KC_ADMIN_USER}" \
    -d "password=${KC_ADMIN_PASSWORD}" \
    | sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'
)"

if [[ -z "${ADMIN_TOKEN}" ]]; then
  echo "[rotate-test1] FAILED to obtain admin token — is Keycloak running at ${KC_BASE}?" >&2
  exit 1
fi

echo "[rotate-test1] Looking up '${TARGET_EMAIL}' in realm '${KC_REALM}' ..."
USER_ID="$(
  curl -fsS -G "${KC_BASE}/admin/realms/${KC_REALM}/users" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}" \
    --data-urlencode "email=${TARGET_EMAIL}" \
    --data-urlencode "exact=true" \
    | sed -n 's/.*"id":"\([^"]*\)".*/\1/p' \
    | head -n1
)"

if [[ -z "${USER_ID}" ]]; then
  echo "[rotate-test1] No user with email '${TARGET_EMAIL}' found in realm '${KC_REALM}'. Nothing to rotate." >&2
  exit 0
fi

echo "[rotate-test1] Disabling user ${USER_ID} ..."
# Read-modify-write to avoid Keycloak's "PUT replaces by omission" trap that
# bit DemoProvisioningJob (see 2026-05-21 dev-log entry).
USER_JSON="$(
  curl -fsS "${KC_BASE}/admin/realms/${KC_REALM}/users/${USER_ID}" \
    -H "Authorization: Bearer ${ADMIN_TOKEN}"
)"
PATCHED_JSON="$(
  printf '%s' "${USER_JSON}" \
    | sed -E 's/"enabled":(true|false)/"enabled":false/' \
    | sed -E 's/"requiredActions":\[[^]]*\]/"requiredActions":["UPDATE_PASSWORD"]/'
)"
# If requiredActions wasn't present at all, inject it.
if ! grep -q '"requiredActions"' <<<"${PATCHED_JSON}"; then
  PATCHED_JSON="$(
    printf '%s' "${PATCHED_JSON}" \
      | sed -E 's/}$/,"requiredActions":["UPDATE_PASSWORD"]}/'
  )"
fi

curl -fsS -X PUT "${KC_BASE}/admin/realms/${KC_REALM}/users/${USER_ID}" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H 'Content-Type: application/json' \
  -d "${PATCHED_JSON}" > /dev/null

echo "[rotate-test1] Resetting password (temporary=true, UPDATE_PASSWORD enforced) ..."
curl -fsS -X PUT "${KC_BASE}/admin/realms/${KC_REALM}/users/${USER_ID}/reset-password" \
  -H "Authorization: Bearer ${ADMIN_TOKEN}" \
  -H 'Content-Type: application/json' \
  -d "$(printf '{"type":"password","value":"%s","temporary":true}' "${NEW_PASSWORD}")" \
  > /dev/null

echo
echo "[rotate-test1] DONE. Account '${TARGET_EMAIL}' (${USER_ID}) is disabled and the password"
echo "[rotate-test1] previously disclosed in a debugging transcript has been replaced."
echo "[rotate-test1] If you intend to keep using this account, re-enable it in the Keycloak admin"
echo "[rotate-test1] UI (Users → ${TARGET_EMAIL} → Details → Enabled = On). The UPDATE_PASSWORD"
echo "[rotate-test1] required action will force the next login to set a fresh password."
