# Staff-session PIN authentication

> Status: **Phases 1–4 complete.** Backend staff-session issuance + verification;
> account-vs-operational role split; frontend roster + PIN; demo staff seeded
> with real PINs. The "final tightening" was **considered and declined** — the
> owner keeps full access by design (see the Phase 2 decision note). Remaining
> work is minor (refresh-token rotation — see the bottom). Seamless
> staff-session refresh + server-side revocation are now implemented.

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
- **`Owner` is a composite over `Manager`** in `realm-export.json` — **by
  design and permanently** (see the decision below). The owner token therefore
  carries `Manager`, so the owner login keeps full operational access and the
  FE's `getPrimaryRole` resolves to `Manager` (also avoiding the historical
  "Owner broke FE role gating" bug). **`Demo`** is composite over `Owner` +
  `Manager` + `KitchenStaff` so demo users get the full owner experience.
- A staff-session token carries only its one operational role, so a PIN-selected
  Manager can run the restaurant but **cannot** manage staff or billing
  (verified: Manager staff token → `/menu` 200, `/orders` 200, `/staff` 403,
  `/billing` 403).

**Running-realm reconciliation:** Keycloak only imports a realm if absent, so
apply the composite graph to an existing dev stack with
`./backend/scripts/heal-composite-roles.sh` (idempotent). Existing **owner**
users provisioned before Phase 2 hold `Manager` directly — grant them `Owner`
(or re-provision) to restore staff/billing access.

**Decision (2026-05-31): keep the `Owner → Manager` composite — do NOT tighten
further.** We considered dropping it so that even owners must start a PIN staff
session for operations. We chose not to. Rationale:

- The security goal is already met: PIN staff sessions are role-scoped (a
  Cashier session cannot perform Manager actions — verified), and the
  staff-session expiry escalation is closed (the apiClient 401 path refreshes
  via the staff refresh endpoint, never the Keycloak refresh token).
- In the common restaurant-POS model the owner/manager account legitimately does
  everything; PINs are for fast, scoped *staff* switching on a shared terminal.
  Forcing the owner to PIN-in for every operation degrades UX with no security
  gain.
- Dropping it would require a first-class `Owner` FE role (else `getPrimaryRole`
  throws), reclassifying ~10 controllers, and an owner-onboarding redesign —
  cost without benefit.

So `Owner → Manager` is intentional and permanent. **Do not remove it** as a
"cleanup" — revisit this decision explicitly if the product ever needs a strict
shared-terminal mode where owners cannot operate without a PIN.

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
- **Staff-session token expiry mid-shift — handled by refresh.** The apiClient
  401 interceptor detects a staff session (`isStaffSession`) and exchanges the
  **staff refresh token** at `POST /auth/staff-session/refresh` for a new access
  token, then retries the request — no re-PIN, and never via the Keycloak
  refresh token (which would mint an owner token and escalate). Only if the
  refresh fails (expired/revoked) does it restore owner mode and bounce to
  `/select-staff`.
- A real owner with **no staff yet** must "Continue as owner" → Staff settings to
  create staff before the roster is useful (expected; surfaced via empty state).

## Known limitations / hardening follow-ups
- **PIN brute-force** is bounded only by the IP+tenant rate limiter. Add a
  per-staff failed-attempt lockout (e.g. disable after N misses in a window).
- **Refresh-token rotation (minor).** `POST /auth/staff-session/refresh` is
  non-rotating: it issues a new access token but echoes the same refresh token
  until its own expiry, so a leaked refresh token is usable for its full TTL.
  Rotating it (blacklist old jti, issue new) on each refresh would shrink that
  window. Not done because the refresh token never leaves the cookie jar and the
  shift TTL is short; revisit if staff sessions get longer-lived.
- The business Keycloak account still carries `Manager` today (Phase 2 moves
  operational roles entirely behind the PIN — see below). Until then, the
  Keycloak token alone can still authorize Manager actions.

## Fix (2026-06-02): both schemes on bare `[Authorize]` (incl. SignalR hub)

The dual-scheme model (Keycloak **or** StaffSession accepted everywhere) was
only applied via `FallbackPolicy` — which ASP.NET Core uses **only for endpoints
with no authorization metadata**. Endpoints carrying a bare `[Authorize]` (no
policy) — `OrderUpdatesHub`, `GET /me`, `GET /menu/items`, shift-note reads —
resolve to `DefaultPolicy`, which was never overridden and therefore
authenticated only the default (Keycloak) scheme. Result: a PIN-issued
staff-session token got **401** on the SignalR hub negotiate (so Cashier /
KitchenStaff lost the realtime kitchen + order boards) and on those REST
endpoints.

Fix (`Program.cs`):
- Set `options.DefaultPolicy` to the same `bothSchemes` builder as
  `FallbackPolicy`, so a bare `[Authorize]` accepts the Keycloak **and**
  StaffSession schemes.
- Add the `OnMessageReceived` (`?access_token=` on `/hubs`) handler to the
  StaffSession scheme — the Keycloak scheme already had it. Browsers can't set
  headers on the WebSocket/SSE transports, so without this a staff token
  authenticated at negotiate (header) but not on the persistent connection.

Verified against the live stack with a real KitchenStaff PIN session: hub
negotiate via header = 200, via `?access_token=` = 200, `GET /menu/items` = 200,
`GET /me` = 200 — all were 401 for staff tokens before.

## Phase 4 — cleanup (status)

- **Demo staff have real, loginable PINs — DONE.** `DemoTenantSeeder` now seeds
  the demo staff with BCrypt-hashed PINs (documented in `keycloak-setup.md`) and
  self-heals demo tenants seeded before real PINs existed, so `/select-staff`
  works out-of-the-box on the shared demo tenant.
- **Owner self-setup** is covered by the existing Staff settings screen: a new
  owner logs in → roster (empty) → "Continue as owner" → Staff → adds staff with
  PINs → "Switch user" → operates via PIN. No dedicated onboarding wizard.
- **Retiring the seeded `manager@/cashier@/kitchen@` Keycloak users — DECLINED.**
  They are kept as **RBAC test fixtures**: `LiveRbacTests` / `LiveAuthLoginTests`
  log in as each to validate the role→policy mapping (still meaningful — staff
  sessions carry the same roles). Removing them would force a live-test rewrite
  for no functional gain. Revisit only if the dev realm is reworked.
