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

The `dineos-frontend` client includes two access-token mappers:

- audience mapper for `dineos-api`
- user attribute mapper for `tenant_id`

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
