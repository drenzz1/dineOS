#!/usr/bin/env bash
set -euo pipefail

: "${KEYCLOAK_BASE_URL:?KEYCLOAK_BASE_URL is required}"
: "${KEYCLOAK_ADMIN_USERNAME:?KEYCLOAK_ADMIN_USERNAME is required}"
: "${KEYCLOAK_ADMIN_PASSWORD:?KEYCLOAK_ADMIN_PASSWORD is required}"
: "${GOOGLE_CLIENT_ID:?GOOGLE_CLIENT_ID is required}"
: "${GOOGLE_CLIENT_SECRET:?GOOGLE_CLIENT_SECRET is required}"
: "${GOOGLE_AUTH_CLIENT_SECRET:?GOOGLE_AUTH_CLIENT_SECRET is required}"
: "${GOOGLE_CALLBACK_URL:?GOOGLE_CALLBACK_URL is required}"
: "${FRONTEND_URL:?FRONTEND_URL is required}"

realm="${KEYCLOAK_REALM:-dineos}"
provider_alias="${GOOGLE_PROVIDER_ALIAS:-google}"
google_client_id="${GOOGLE_AUTH_CLIENT_ID:-dineos-google}"
top_flow="first broker login auto link by email"
link_flow="first broker login auto link by email user creation or linking"
base_url="${KEYCLOAK_BASE_URL%/}"

urlencode() {
  jq -rn --arg value "$1" '$value | @uri'
}

admin_url="$base_url/admin/realms/$realm"

for attempt in $(seq 1 30); do
  token_response=$(curl --silent --show-error --fail-with-body \
    --request POST \
    --header "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "client_id=admin-cli" \
    --data-urlencode "grant_type=password" \
    --data-urlencode "username=$KEYCLOAK_ADMIN_USERNAME" \
    --data-urlencode "password=$KEYCLOAK_ADMIN_PASSWORD" \
    "$base_url/realms/master/protocol/openid-connect/token" 2>/dev/null || true)

  admin_token=$(jq -r '.access_token // empty' <<<"$token_response")
  if [[ -n "$admin_token" ]]; then
    break
  fi

  if [[ "$attempt" == "30" ]]; then
    echo "Keycloak did not become ready for Google auth configuration." >&2
    exit 1
  fi
  sleep 5
done

api() {
  local method="$1"
  local path="$2"
  local body="${3:-}"
  local args=(
    --silent
    --show-error
    --fail-with-body
    --request "$method"
    --header "Authorization: Bearer $admin_token"
  )

  if [[ -n "$body" ]]; then
    args+=(--header "Content-Type: application/json" --data "$body")
  fi

  curl "${args[@]}" "$admin_url$path"
}

flow_exists() {
  local alias="$1"
  api GET "/authentication/flows" | jq -e --arg alias "$alias" \
    'any(.[]; .alias == $alias)' >/dev/null
}

ensure_flow() {
  local alias="$1"
  local description="$2"
  local top_level="$3"

  if flow_exists "$alias"; then
    return
  fi

  api POST "/authentication/flows" "$(jq -cn \
    --arg alias "$alias" \
    --arg description "$description" \
    --argjson topLevel "$top_level" \
    '{alias: $alias, description: $description, providerId: "basic-flow", topLevel: $topLevel, builtIn: false}')" \
    >/dev/null
}

execution_exists() {
  local flow="$1"
  local selector="$2"
  api GET "/authentication/flows/$(urlencode "$flow")/executions" \
    | jq -e "$selector" >/dev/null
}

add_execution() {
  local flow="$1"
  local provider="$2"
  api POST "/authentication/flows/$(urlencode "$flow")/executions/execution" \
    "$(jq -cn --arg provider "$provider" '{provider: $provider}')" >/dev/null
}

set_execution_requirement() {
  local flow="$1"
  local selector="$2"
  local requirement="$3"
  local execution

  execution=$(api GET "/authentication/flows/$(urlencode "$flow")/executions" \
    | jq -c "$selector")
  if [[ -z "$execution" ]]; then
    echo "No execution matching '$selector' found in flow '$flow'." >&2
    exit 1
  fi
  api PUT "/authentication/flows/$(urlencode "$flow")/executions" \
    "$(jq -c --arg requirement "$requirement" '.requirement = $requirement' <<<"$execution")" \
    >/dev/null
}

ensure_flow \
  "$top_flow" \
  "Create a brokered user when the email is unique, or auto-link Google to the existing trusted-email account." \
  true

if ! execution_exists "$top_flow" \
  'any(.[]; .providerId == "idp-review-profile")'; then
  add_execution "$top_flow" "idp-review-profile"
fi
set_execution_requirement "$top_flow" \
  '.[] | select(.providerId == "idp-review-profile")' \
  "REQUIRED"

# Sub-flow executions carry no flowAlias in the executions listing — they are
# identified by authenticationFlow=true plus the alias echoed in displayName.
if ! execution_exists "$top_flow" \
  'any(.[]; .authenticationFlow == true and .displayName == "first broker login auto link by email user creation or linking")'; then
  api POST "/authentication/flows/$(urlencode "$top_flow")/executions/flow" \
    "$(jq -cn \
      --arg alias "$link_flow" \
      '{alias: $alias, type: "basic-flow", provider: "registration-page-form", description: "Create a unique brokered user or automatically link the matching existing user."}')" \
    >/dev/null
fi
set_execution_requirement "$top_flow" \
  '.[] | select(.authenticationFlow == true and .displayName == "first broker login auto link by email user creation or linking")' \
  "REQUIRED"

if ! execution_exists "$link_flow" \
  'any(.[]; .providerId == "idp-create-user-if-unique")'; then
  add_execution "$link_flow" "idp-create-user-if-unique"
fi
set_execution_requirement "$link_flow" \
  '.[] | select(.providerId == "idp-create-user-if-unique")' \
  "ALTERNATIVE"

if ! execution_exists "$link_flow" \
  'any(.[]; .providerId == "idp-auto-link")'; then
  add_execution "$link_flow" "idp-auto-link"
fi
set_execution_requirement "$link_flow" \
  '.[] | select(.providerId == "idp-auto-link")' \
  "ALTERNATIVE"

client_payload=$(jq -cn \
  --arg clientId "$google_client_id" \
  --arg secret "$GOOGLE_AUTH_CLIENT_SECRET" \
  --arg redirectUri "$GOOGLE_CALLBACK_URL" \
  --arg webOrigin "$FRONTEND_URL" \
  '{
    clientId: $clientId,
    name: "DineOS Google Login",
    description: "Confidential backend client for Google sign-in through Keycloak.",
    enabled: true,
    publicClient: false,
    bearerOnly: false,
    protocol: "openid-connect",
    standardFlowEnabled: true,
    directAccessGrantsEnabled: false,
    serviceAccountsEnabled: false,
    secret: $secret,
    redirectUris: [$redirectUri],
    webOrigins: [$webOrigin],
    protocolMappers: [
      {
        name: "dineos-api-audience",
        protocol: "openid-connect",
        protocolMapper: "oidc-audience-mapper",
        consentRequired: false,
        config: {
          "included.client.audience": "dineos-api",
          "id.token.claim": "false",
          "access.token.claim": "true"
        }
      },
      {
        name: "tenant-id",
        protocol: "openid-connect",
        protocolMapper: "oidc-usermodel-attribute-mapper",
        consentRequired: false,
        config: {
          "user.attribute": "tenant_id",
          "claim.name": "tenant_id",
          "jsonType.label": "String",
          "id.token.claim": "true",
          "access.token.claim": "true",
          "userinfo.token.claim": "true"
        }
      }
    ]
  }')

existing_client=$(api GET "/clients?clientId=$(urlencode "$google_client_id")")
client_uuid=$(jq -r '.[0].id // empty' <<<"$existing_client")
if [[ -z "$client_uuid" ]]; then
  api POST "/clients" "$client_payload" >/dev/null
else
  api PUT "/clients/$client_uuid" \
    "$(jq -c --arg id "$client_uuid" '. + {id: $id}' <<<"$client_payload")" \
    >/dev/null
fi

provider_payload=$(jq -cn \
  --arg alias "$provider_alias" \
  --arg flow "$top_flow" \
  --arg clientId "$GOOGLE_CLIENT_ID" \
  --arg clientSecret "$GOOGLE_CLIENT_SECRET" \
  '{
    alias: $alias,
    providerId: "google",
    enabled: true,
    updateProfileFirstLoginMode: "missing",
    trustEmail: true,
    storeToken: false,
    addReadTokenRoleOnCreate: false,
    authenticateByDefault: false,
    linkOnly: false,
    firstBrokerLoginFlowAlias: $flow,
    config: {
      clientId: $clientId,
      clientSecret: $clientSecret,
      defaultScope: "openid profile email"
    }
  }')

provider_path="/identity-provider/instances/$(urlencode "$provider_alias")"
if api GET "$provider_path" >/dev/null 2>&1; then
  api PUT "$provider_path" "$provider_payload" >/dev/null
else
  api POST "/identity-provider/instances" "$provider_payload" >/dev/null
fi

echo "Google authentication configured for Keycloak realm '$realm'."
