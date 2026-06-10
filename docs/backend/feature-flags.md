# Feature Flags (Unleash) — M5.6

dineOS uses **[Unleash](https://www.getunleash.io/)** (self-hosted, open-source) for
runtime feature flags. A flag can be flipped in the Unleash dashboard and the change
takes effect in the running API within seconds — **no redeploy**.

- **Provider:** Unleash, self-hosted (chosen over LaunchDarkly to stay self-contained
  and zero-cost, consistent with the rest of the stack — Keycloak, Postgres, Redis, etc.).
- **First flag:** `ai-menu-generation` — gates AI menu-description generation.
- **Owner:** Backend.

---

## Why Unleash vs. static config

The app already has *static* config toggles (`RabbitMq:Enabled`, `Email:Enabled`, …),
but those are read at **startup** and changing them needs a **redeploy**. A feature flag
is evaluated at **runtime**, flippable live, and can target a subset (per tenant, %
rollout). Unleash runs as a container like the rest of the stack and stores its data in
its own Postgres — no external SaaS dependency.

---

## Architecture

Clean-architecture friendly — the app depends on an abstraction, not on Unleash:

| Piece | Location |
|---|---|
| `IFeatureFlags` (+ `FeatureFlag` key constants) | `DineOS.Application/Interfaces/Services/IFeatureFlags.cs` |
| `UnleashFeatureFlags` (Unleash-backed) + `DefaultFeatureFlags` (no-op) | `DineOS.Infrastructure/Services/UnleashFeatureFlags.cs` |
| `UnleashOptions` (config binding) | `DineOS.Application/Options/UnleashOptions.cs` |
| Conditional DI registration | `DineOS.Infrastructure/DependencyInjection.cs` |

**Registration (mirrors the `RabbitMq:Enabled` pattern):**

- `Unleash:Enabled = true` → a singleton Unleash client (background polling) is built and
  wrapped in `UnleashFeatureFlags`. If construction fails, it degrades to `DefaultFeatureFlags`.
- `Unleash:Enabled = false` (or absent) → `DefaultFeatureFlags`, which returns each call's
  **default** value. So dev/test/CI behave exactly as if no flag system existed.

**Resilience:** every evaluation is wrapped — if Unleash is unreachable or the flag is
unknown, `IsEnabled(flag, defaultValue)` returns `defaultValue`. A flag-provider outage
can never take down a request path.

---

## The `ai-menu-generation` flag

**Kill-switch semantics — defaults ON.**

```csharp
// AiMenuService.SuggestDescriptionAsync — short-circuits before any DB/AI work
if (!featureFlags.IsEnabled(FeatureFlag.AiMenuGeneration, defaultValue: true))
    return ServiceResult<MenuItemDescriptionSuggestionDto>.ServiceUnavailable(
        "AI menu generation is currently disabled.");
```

- **On** (default, and whenever Unleash is absent/unreachable): the AI endpoint works as before.
- **Off** (flip in Unleash): `POST /api/v1/ai/menu-items/{id}/describe` returns **503 Service
  Unavailable** with `"AI menu generation is currently disabled."`, and the AI provider is
  never called.

**Why a kill-switch?** Each AI call costs money per request. A runtime off-switch lets you
cap provider spend or stop abuse instantly without an emergency deploy — defaulting ON means
removing Unleash never silently breaks the feature.

---

## Local setup

Unleash + its own Postgres are in both `docker-compose.yml` (root, full stack) and
`backend/docker-compose.yml` (backend dev stack). Deterministic tokens are seeded so no
manual setup is needed.

```bash
# From the backend dev stack (or repo root for the full stack):
docker compose up -d unleash unleash-db

# Unleash dashboard:        http://localhost:4242   (default admin login: admin / unleash4all)
# Seeded client API token:  default:development.unleash-insecure-api-token   (used by the API)
# Seeded admin API token:   *:*.unleash-insecure-admin-token                 (for scripting)
```

The API is wired via env (`Unleash__Enabled=true`, `Unleash__ApiUrl=http://unleash:4242/api/`,
`Unleash__ApiToken=default:development.unleash-insecure-api-token`). In `appsettings.json`
`Unleash:Enabled` defaults to **false** — it's enabled by the compose env for the dev stack.

### Create + toggle the flag (admin API)

```bash
ADMIN="Authorization: *:*.unleash-insecure-admin-token"
BASE="http://localhost:4242/api/admin/projects/default/features"

# Create
curl -s -X POST $BASE -H "$ADMIN" -H 'Content-Type: application/json' \
  -d '{"name":"ai-menu-generation","type":"kill-switch","description":"AI menu generation kill-switch"}'
# 100% rollout strategy in the development environment
curl -s -X POST "$BASE/ai-menu-generation/environments/development/strategies" -H "$ADMIN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"flexibleRollout","parameters":{"rollout":"100","stickiness":"default","groupId":"ai-menu-generation"}}'
# Turn on / off (this is the live toggle)
curl -s -X POST "$BASE/ai-menu-generation/environments/development/on"  -H "$ADMIN"
curl -s -X POST "$BASE/ai-menu-generation/environments/development/off" -H "$ADMIN"
```

(Or just flip it in the dashboard at http://localhost:4242.)

### Verify the runtime toggle

```bash
# What the .NET SDK polls — the 'enabled' field flips with the toggle, no redeploy:
curl -s http://localhost:4242/api/client/features \
  -H "Authorization: default:development.unleash-insecure-api-token" | jq '.features[] | select(.name=="ai-menu-generation") | .enabled'

# End-to-end: with the API running (Manager token + a menu item):
#   flag ON  → POST /api/v1/ai/menu-items/{id}/describe → 200 (or 422 if no provider key)
#   flag OFF → same call → 503 "AI menu generation is currently disabled."
```

---

## Cluster (Helm)

The chart carries the API's Unleash config (`Unleash__Enabled` / `Unleash__ApiUrl` in the
ConfigMap, `Unleash__ApiToken` in the Secret), **defaulted to disabled** in `values.yaml`
and both overlays — so in-cluster the `ai-menu-generation` flag defaults ON and nothing
changes until Unleash is stood up.

To enable flags in a cluster:
1. Deploy an Unleash server (e.g. the Unleash community Helm chart, or a Deployment + a
   dedicated `unleash` database — the Keycloak DB-provisioning pattern).
2. Set `api.env.Unleash__Enabled: "true"` and `api.env.Unleash__ApiUrl` to the in-cluster
   Unleash URL, and put a client token in `secrets.unleashApiToken`.

---

## See also

- **[Backend auth](auth.md)**, **[Redis caching](redis-caching.md)** — other backend infra.
- **[Security audit](../devops/security-audit.md)** — gitleaks/ZAP/Lighthouse CI (M5.5).
