# Staff-session PIN authentication

> Status: **Phases 1–3 implemented** (backend staff-session issuance +
> verification; account-vs-operational role split; frontend roster + PIN).
> Phase 4 (cleanup) + the final tightening are planned — see the bottom.

## Problem

Originally each operational role for a business was a **separate Keycloak user**
(`manager@`, `cashier@`, `kitchen@`). One restaurant therefore juggled multiple
emails/passwords, and switching role meant logging out and back in. We want the
restaurant-POS model instead: **one business account, many PIN-gated staff
identities** on the same device.

## Model

| Layer | Identity | Carries | Answers |
|-------|----------|---------|---------|
| **Keycloak** | One email per business (the owner) | `tenant_id`, account-level role | "Which business is this?" |
| **Staff session** | A PIN-selected `StaffMember` | `tenant_id`, `staff_member_id`, operational `role` | "What can this person do right now?" |

The operational role lives in the **staff-session token**, not in the Keycloak
account. That is what makes the 4-digit PIN a real authorization boundary rather
than a UI switch: even if the Keycloak business account could authorize an
action, the *staff* token presented for the request only carries that one staff
member's role.

## Phase 1 — what exists today

### Flow
1. The business logs in via Keycloak (`POST /auth/login`) → normal Keycloak JWT
   carrying `tenant_id`.
2. Client calls `POST /api/v1/auth/staff-session` with that Keycloak token and a
   body `{ "staffMemberId": <id>, "pin": "1234" }`.
3. Backend (`StaffSessionService`):
   - resolves the tenant from the Keycloak token (`ITenantService.TenantId`),
   - loads the **active** `StaffMember` with that id **within that tenant**,
   - verifies the PIN against `PinHash` via the existing `IPinHasher` (BCrypt),
   - mints a short-lived HS256 token (see claims below).
4. Client uses the staff-session token for operational API calls. Existing
   `RequireRole(...)` policies authorize it transparently.

### Staff-session token
Backend-signed HS256 (key = `StaffSession:SigningKey`, ≥ 32 bytes). Claims:

| Claim | Value |
|-------|-------|
| `iss` / `aud` | `StaffSession:Issuer` / `StaffSession:Audience` |
| `sub` | `staff:{id}` |
| `tenant_id` | the staff member's tenant (so `ITenantService` resolves identically to a Keycloak token) |
| `role` | the staff member's operational role (`Manager` / `Cashier` / `KitchenStaff`) |
| `staff_member_id`, `name`, `token_use=staff_session` | identity / marker |
| `iat` / `nbf` / `exp` | `StaffSession:TokenLifetimeMinutes` (default 720 = a 12h shift) |

### Wiring (`Program.cs`)
- A second `JwtBearer` scheme **`StaffSession`** validates the token with the
  shared symmetric key. `MapInboundClaims = false` keeps `tenant_id` / `role`
  verbatim; `RoleClaimType = "role"`, `NameClaimType = "name"`.
- Every authorization policy (and the fallback) accepts **both** schemes
  (`Bearer` + `StaffSession`). Whichever token authenticates the request
  supplies the role claim that `RequireRole` checks.
- `POST /auth/staff-session` itself is restricted to the **Keycloak** scheme
  (`[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`)
  so a staff-session token cannot bootstrap another one.
- Rate limiter policy **`staff-pin`**: 10/min partitioned by `tenant_id` + remote
  IP, to bound PIN brute-force.

### Configuration
```jsonc
"StaffSession": {
  "SigningKey": "<≥32 bytes — OVERRIDE per environment>",
  "Issuer": "dineos-staff-session",
  "Audience": "dineos-api",
  "TokenLifetimeMinutes": 720
}
```
The committed value is a **dev-only placeholder**. Leaking the production key
lets an attacker mint any staff role — treat it like a signing secret.

### Files
- `Application/Options/StaffSessionOptions.cs`
- `Application/DTOs/StartStaffSessionRequest.cs` (+ validator), `StaffSessionResponse.cs`
- `Application/Interfaces/Services/IStaffSessionService.cs`
- `Application/Authorization/AuthorizationConstants.cs` (`AuthSchemes`)
- `Infrastructure/Services/StaffSessionService.cs`
- `Api/Program.cs`, `Api/Controllers/AuthController.cs`
- Tests: `tests/DineOS.Tests/Unit/StaffSessionServiceTests.cs`

## Phase 2 — what exists today (account vs operational split)

The business Keycloak account is now the account-level **`Owner`** role, distinct
from the operational roles a staff member acquires per-shift via a PIN.

- **New `Owner` realm role + `OwnerOnly` policy** (`RequireRole(SuperAdmin, Owner)`).
- **Account-level endpoints moved to `OwnerOnly`:** `StaffController` (staff +
  PIN management) and `BillingController`. Operational endpoints
  (orders/payments/menu/shifts/reports/kitchen) keep their `*AndAbove` /
  `KitchenStaffOnly` policies.
- **`OwnerProvisioningJob` now assigns `Owner`** (was `Manager`).
- **`Owner` is a composite over `Manager`** in `realm-export.json`. This is the
  safety mechanism: an owner token still carries `Manager`, so operational
  policies keep passing during the transition and the FE's `getPrimaryRole`
  still resolves to `Manager` — avoiding the historical "Owner broke FE role
  gating" bug. **`Demo`** is now composite over `Owner` + `Manager` +
  `KitchenStaff` so demo users get the full owner experience.
- A staff-session token carries only its one operational role, so a PIN-selected
  Manager can run the restaurant but **cannot** manage staff or billing
  (verified: Manager staff token → `/menu` 200, `/orders` 200, `/staff` 403,
  `/billing` 403).

**Running-realm reconciliation:** Keycloak only imports a realm if absent, so
apply the composite graph to an existing dev stack with
`./backend/scripts/heal-composite-roles.sh` (idempotent). Existing **owner**
users provisioned before Phase 2 hold `Manager` directly — grant them `Owner`
(or re-provision) to restore staff/billing access.

**The final tightening (deferred to after Phase 3):** drop the `Owner → Manager`
composite so an owner token no longer carries operational access — at that point
*everyone*, including the owner, must start a PIN staff session to run
operations. Safe to do only once the frontend roster/PIN screen exists.

## Phase 3 — what exists today (frontend roster + PIN)

The frontend now uses a **two-token** model with an explicit operational-session
step:

- **`business_token` cookie** holds the Keycloak/Owner token (set at login,
  retained for the whole session). **`access_token`** holds the *active*
  operational token: it starts as the business token (owner mode) and is swapped
  to the staff-session token after a PIN is entered.
- **After login → `/select-staff`** (the "Who's working?" roster), not a role
  dashboard. SuperAdmins are bounced to `/admin` by middleware.
- The roster (`app/select-staff/page.tsx`) lists active staff (`GET /staff` via
  the owner token), each card opens a 4-digit PIN entry → `authStore.startStaffSession`
  → `POST /auth/staff-session` (sent with the **business token** via a dedicated
  axios client, since that endpoint only accepts the Keycloak scheme) → the
  returned staff token becomes `access_token`, the role cookie becomes the staff
  role, and the user is routed to that role's destination.
- **"Continue as the owner"** skips the PIN and proceeds in owner mode (full
  access via the `Owner→Manager` composite).
- **"Switch user"** in the sidebar calls `endStaffSession` (restores the
  business token + owner mode) and returns to the roster.
- The sidebar **hides Staff + Billing** in a staff session so an operational
  Manager doesn't hit a raw 403, and uses the stored role (not `getPrimaryRole`
  on the staff token, which has no `realm_access.roles`).
- `StaffSessionService.VerifyPin` swallows malformed-hash exceptions (the demo
  seeder's placeholder `PinHash`) → clean 401 instead of 500.

Key files: `app/select-staff/{page,loading,error}.tsx`, `lib/api/staffSessionApi.ts`,
`stores/authStore.ts` (`startStaffSession`/`endStaffSession`, `business_token`),
`lib/auth/keycloak.ts` (cookie helpers), `app/login/page.tsx`,
`components/shared/ProtectedSidebar.tsx`.

### Phase 3 follow-ups
- **Staff-session token expiry mid-shift — FIXED.** The apiClient 401 interceptor
  now detects a staff session (`isStaffSession`) and, instead of refreshing via
  the Keycloak `refresh_token` (which would mint an *owner* token and silently
  escalate a Cashier to Manager-backed access), it ends the staff session and
  redirects to `/select-staff` to re-PIN. The operational request is rejected,
  never retried with an owner token. Seamless staff-session *refresh* (without a
  re-PIN) and server-side revocation remain follow-ups below.
- A real owner with **no staff yet** must "Continue as owner" → Staff settings to
  create staff before the roster is useful (expected; surfaced via empty state).

## Known limitations / hardening follow-ups
- **PIN brute-force** is bounded only by the IP+tenant rate limiter. Add a
  per-staff failed-attempt lockout (e.g. disable after N misses in a window).
- **No seamless staff-session refresh / server-side revocation** yet. On
  expiry the user is bounced to the roster to re-PIN (safe — see Phase 3 fix
  above), but there is no `…/staff-session/refresh` to extend a shift without a
  re-PIN, and `…/end` only drops the client cookie (the token stays valid until
  `exp`). Add a Redis blacklist (like the Keycloak refresh-token path) for true
  "sign out", and a refresh endpoint for long shifts.
- The business Keycloak account still carries `Manager` today (Phase 2 moves
  operational roles entirely behind the PIN — see below). Until then, the
  Keycloak token alone can still authorize Manager actions.

## Planned phases

- **Phase 3 — frontend roster + PIN.** Post-login "Who's working?" screen
  (roster from `GET /staff`, never PINs) → tap → 4-digit PIN → store the
  staff-session token, set the role cookie from the staff role. "Switch user"
  returns to the roster. `apiClient` sends the staff-session token; middleware
  gates on the staff role.
- **Phase 4 — owner self-setup & cleanup.** Owner onboarding to create staff
  (incl. themselves) with PINs; migrate the seeded `manager@/cashier@/kitchen@`
  users into staff records and retire those Keycloak accounts.
