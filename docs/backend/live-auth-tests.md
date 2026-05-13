# Live Keycloak Integration Tests

Tests under `Integration/LiveKeycloak/` spin up a real Keycloak 26.1 container via
Testcontainers, import the `dineos` realm, and issue genuine JWT tokens for the four
seeded test users. They verify the full auth and RBAC stack end-to-end — no symmetric
key shortcuts.

## Prerequisites

- **Docker Desktop** (Windows / macOS) or **Docker Engine** (Linux) must be running.
- No Keycloak instance or network connectivity needed — the container is self-contained.

## Running tests

### Fast tests only (default)

```bash
cd backend
dotnet test tests/DineOS.Tests/DineOS.Tests.csproj --configuration Release
```

Live-auth tests are excluded automatically via `default.runsettings`
(`Category!=LiveAuth` filter). This is safe to run without Docker.

### Live Keycloak tests only

```bash
cd backend
dotnet test tests/DineOS.Tests/DineOS.Tests.csproj \
  --configuration Release \
  --settings tests/DineOS.Tests/live.runsettings
```

Keycloak 26.1 starts in ~60–90 s on first run (image pull) and ~20–30 s on subsequent
runs (cached image). Tokens are cached within the test run to avoid hammering the
container.

### All tests (fast + live)

```bash
cd backend
dotnet test tests/DineOS.Tests/DineOS.Tests.csproj \
  --configuration Release \
  --filter "Category=LiveAuth|Category!=LiveAuth"
```

## Seeded test users

| Email | Password | Role | tenant_id claim |
|-------|----------|------|----------------|
| admin@dineos.dev | Test1234! | SuperAdmin | — |
| manager@dineos.dev | Test1234! | Manager | 1 |
| cashier@dineos.dev | Test1234! | Cashier | 1 |
| kitchen@dineos.dev | Test1234! | KitchenStaff | 1 |

Users and roles are defined in `backend/keycloak/realm-export.json` and imported
automatically when the container starts.

## CI

The `Backend CI` workflow (`backend-ci.yml`) runs **fast tests only** on every push and
pull request. Live tests are excluded via `--filter "Category!=LiveAuth"` on the fast
job's `dotnet test` step.

A separate **Live Keycloak Integration Tests** job runs on:
- Manual trigger (`workflow_dispatch` from the GitHub Actions UI)
- Every push to `main` (after the fast-test job passes)

Docker is available on `ubuntu-latest` runners, so no additional runner configuration
is needed.
