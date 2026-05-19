# recent.md

## Current Focus

### Public Signup + Stripe Billing (active)
- `484e2a3 feat(signup): public signup page + Stripe checkout polling UI (#204)`
- `4554d8d feat(signup): public signup endpoint + Stripe checkout (#204)`
- `b926e4a Configure Stripe dev billing setup` (PR #207)

Backend now exposes `Application/Signup/` + `Application/Billing/` features; frontend adds public signup page with polled Stripe checkout state. Stripe dev config wired in.

### Kitchen Board Realtime Wiring
- `d2fcd7a feat: wire /kitchen board to real backend + queue counters (FE-204)` (PR #201)
- `92cc8e0 feat: SignalR client for /hubs/orders (FE-501)` (PR #199)

SignalR client now connects to `/hubs/orders`; `useKitchenBoard` hook + components hydrate from real backend with live queue counters.

### Menu Feature Polish
- `24a5e41 feat: add Describe with AI button on menu items (FE-202)` — calls `POST /api/v1/ai/menu-items/{id}/describe` (PR #198)
- `fc624aa feat: wire menu page to real backend (items, categories, image upload)` (PR #197)

### Settings / Manager Pages
- `e4bcbba ... build settings/profile, settings/tables pages (Manager)` (PR #195) — backed by `Application/RestaurantProfile/`, `Application/RestaurantTables/`.

### Tooling / Tests
- `300936f chore: add GitNexus code-intelligence config + skill files` (PR #202)
- `c4dc406 test(e2e): remove stale dev-login UI specs after real-auth login shipped` — real Keycloak-backed login is replacing the dev role-picker
- `6133217 chore(frontend): pin Turbopack root to silence workspace-root warning`

### Uncommitted
- `backend/.gitignore` +7 lines — additional ignore entries (no code drift).

### Themes
1. Productionizing onboarding flow: public signup → Stripe checkout → tenant provisioning.
2. Replacing dev role-picker with real Keycloak auth across frontend; cleaning up obsolete e2e specs.
3. Realtime ops (SignalR `/hubs/orders`) feeding kitchen + order boards.
4. AI features surfacing in UI (menu item "Describe with AI").
5. Manager settings surfaces (profile, tables) consuming new backend endpoints.