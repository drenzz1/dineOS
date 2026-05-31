# Keycloak Local Setup

Keycloak runs in Docker and auto-imports the `dineos` realm on first start.

## Start the stack

```bash
cd backend
docker compose up keycloak postgres
```

- **Admin UI**: http://localhost:8080 (admin / admin)
- **Realm**: `dineos`

The API picks up auth config from `appsettings.Development.json` when run with `dotnet run`:

```bash
cd backend/src/DineOS.Api
dotnet run
```

## Login through the backend

The API exposes backend auth endpoints for scriptable local testing and for
clients that intentionally use the backend as the token broker:

```bash
curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@dineos.dev","password":"Test1234!"}' \
  | jq -r '.data.accessToken'
```

This uses the same public Keycloak client as the browser flow, but exchanges the
username/password on the backend via `POST /api/v1/auth/login`.

## Seeded users

| Email | Password | Role | Tenant claim |
|-------|----------|------|--------------|
| admin@dineos.dev | Test1234! | SuperAdmin | none |
| manager@dineos.dev | Test1234! | Manager | `tenant_id=1` |
| cashier@dineos.dev | Test1234! | Cashier | `tenant_id=1` |
| kitchen@dineos.dev | Test1234! | KitchenStaff | `tenant_id=1` |

These per-role Keycloak users are kept deliberately as **RBAC test fixtures**
(the live `LiveRbacTests` / `LiveAuthLoginTests` log in as each to validate the
role→policy mapping). They are not part of the staff-PIN model below.

### Demo-tenant staff PINs (#staff-pin-auth)

The demo tenant is seeded with loginable staff so the post-login
**`/select-staff`** roster works out-of-the-box. Log in as the demo business
(via the demo-access flow), then pick a profile and enter its PIN:

| Staff | Role | PIN |
|-------|------|-----|
| Ada Manager | Manager | `1111` |
| Bram Cashier | Cashier | `2222` |
| Cleo Cashier | Cashier | `3333` |
| Dario Kitchen | KitchenStaff | `4444` |
| Elif Kitchen | KitchenStaff | `5555` |

Non-secret demo credentials only. `DemoTenantSeeder` is idempotent and self-heals
older demo tenants that were seeded before real PINs existed.

The `dineos-frontend` client includes two access-token mappers:

- audience mapper for `dineos-api`
- user attribute mapper for `tenant_id`

## Composite realm roles (`Owner`, `Demo`)

`realm-export.json` declares two composite realm roles. Keycloak expands
composites into the access token, so a correctly-imported realm issues tokens
carrying the parent **and** its children in `realm_access.roles`:

| Role | Composite over | Why |
|------|----------------|-----|
| `Owner` | `Manager` | The business account (#staff-pin-auth Phase 2) is the account-level `Owner` — it gates staff management + billing (`OwnerOnly` policy). The `Manager` child keeps operational access working (and the FE's `getPrimaryRole` resolving to `Manager`) until the PIN roster UI ships; the final tightening drops this composite. |
| `Demo` | `Owner`, `Manager`, `KitchenStaff` | Demo users (#216) get the full owner experience: account screens (`Owner`), all tenant routes (`Manager`), and the kitchen board (`KitchenStaffOnly` = `RequireRole(KitchenStaff)`, which `Manager` alone does not satisfy). |

The frontend maps `Demo → Manager` / `Owner → Manager` for its UI and middleware
gating, so these tokens render the Manager experience. All of this only lines up
when the composites are present.

**Stale-realm gotcha.** Keycloak runs `start-dev --import-realm`, which imports
the realm **only if it does not already exist**. A dev volume created before a
composite was added keeps a role with no associated roles, so tokens are missing
the expected roles and protected endpoints return `403` ("you don't have
permission") even though the UI renders. Diagnose with:

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/realms/dineos/protocol/openid-connect/token \
  -d grant_type=client_credentials -d client_id=dineos-admin \
  -d client_secret=dev-admin-secret-change-me | jq -r .access_token)
for r in Owner Demo; do echo "$r ->"; \
  curl -s http://localhost:8080/admin/realms/dineos/roles/$r/composites \
    -H "Authorization: Bearer $TOKEN" | jq -r '.[].name'; done
```

Fix a running stack without losing data:

```bash
./backend/scripts/heal-composite-roles.sh   # idempotent; heals the full graph
```

…or do a destructive re-import (throwaway environments only):

```bash
docker compose down -v && docker compose up --build
```

After either, existing sessions must log in again to get a fresh token. Note:
existing **owner** users provisioned before Phase 2 carry `Manager` directly
(not `Owner`); grant them `Owner` (or re-provision) to restore staff/billing
access.

## Test a protected endpoint

```bash
TOKEN=$(curl -s -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin@dineos.dev","password":"Test1234!"}' \
  | jq -r '.data.accessToken')

# Who am I?
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/me

# No token → 401
curl -v http://localhost:5000/api/v1/me
```

## Swagger UI with OAuth2 + PKCE

The local Swagger UI is configured as an OAuth2 Authorization Code + PKCE client.

1. Start the Docker API stack:

   ```bash
   cd backend
   docker compose up --build
   ```

2. Open http://localhost:5000/swagger.
3. Click **Authorize**.
4. Choose the **Keycloak** OAuth2 entry.
5. Select `openid`, `profile`, and `email`.
6. Sign in with one of the seeded users.
7. Call a protected endpoint such as `GET /api/v1/me`.

Swagger also keeps the plain **Bearer** option for copy/paste testing with a
token returned from `POST /api/v1/auth/login`.

## Frontend OAuth2 SSO

The Next.js login screen starts the Keycloak Authorization Code + PKCE flow and
handles the redirect at `/auth/callback`.

Local defaults:

```text
NEXT_PUBLIC_KEYCLOAK_AUTHORITY=http://localhost:8080/realms/dineos
NEXT_PUBLIC_KEYCLOAK_CLIENT_ID=dineos-frontend
```

After a successful callback, the frontend stores the access token and role in
browser cookies for route middleware and stores the same session data in Zustand
for client API calls.

## Clients

| Client ID | Type | Purpose |
|-----------|------|---------|
| `dineos-api` | Bearer-only | Backend resource server |
| `dineos-frontend` | Public | SPA, Swagger OAuth2 + PKCE, and local dev token fetching |

The local realm allows these redirect URIs for the public client:

- `http://localhost:3000/*`
- `http://localhost:5000/swagger/oauth2-redirect.html`
- `http://localhost:5138/swagger/oauth2-redirect.html`
- `https://localhost:7202/swagger/oauth2-redirect.html`

For deployed environments, configure the backend with a confidential client and set `Keycloak__ClientSecret` through the deployment secret store.

## Mandatory rotation: `test1@gmail.com`

During the 2026-05-22 "Account is not fully set up" diagnosis, the dev
Keycloak instance running in Docker was manually adjusted: the
`test1@gmail.com` user had its password reset via the Admin REST API to a
known value, and its `requiredActions` list was cleared, so the account
could log in for debugging. That password was disclosed in the debugging
transcript and **must be rotated on every machine that pulled this
branch**. The rotation is not optional — it is a precondition of the
branch being safe to share.

Run the committed rotation script once against your local Docker
Keycloak:

```bash
backend/scripts/rotate-test1-dev-credential.sh
```

The script (idempotent) authenticates as the admin, disables the
account, and resets the password to a fresh random value with
`UPDATE_PASSWORD` enforced — so even if you re-enable the account, the
old credential is rejected and the first interactive login is forced
through a password change. Override `KC_BASE` / `TARGET_EMAIL` to point
at a remote dev realm or a different account.

### Underlying defect (fixed)

`OwnerProvisioningJob` previously produced an empty `lastName` for
single-word owner names. Keycloak's declarative user-profile
(`unmanagedAttributePolicy: ENABLED`) rejects direct-grant logins for
profiles with any declared attribute empty, surfacing as the same
`"Account is not fully set up"` error string that pending
`requiredActions` produce.

The fix lives in
`backend/src/DineOS.Infrastructure/Auth/KeycloakProfileDefaults.cs`:
a shared, testable helper splits any free-form display name into the
`(firstName, lastName)` pair Keycloak expects, always emitting non-empty
values. Both `OwnerProvisioningJob` and `DemoProvisioningJob` route
through this helper / its constants so the same class of bug cannot
re-appear in a sibling provisioning path. Single-word names now produce
`(token, "—")` rather than mirroring the token into the lastName — the
em dash sentinel is honest about "lastName was not provided" instead of
storing fabricated surname data.

Regression coverage:

- `tests/DineOS.Tests/Unit/KeycloakProfileDefaultsTests.cs` (whitespace
  edge cases — null, empty, tabs, double-space, three-plus tokens).
- `tests/DineOS.Tests/Unit/OwnerProvisioningJobTests.cs` (`[Theory]`
  `RunAsync_AlwaysSendsNonEmptyLastNameToKeycloak`).
- `tests/DineOS.Tests/Integration/LiveKeycloak/LiveOwnerProvisioningTests.cs`
  drives the full provision → first-login → standard-login flow against
  a real Keycloak Testcontainer (`live.runsettings`).
